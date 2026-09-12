#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    /// <summary>
    ///     Central library ScriptableObject that holds all building archetypes, keyed by district type.
    ///     Templates are organized in subfolders under <see cref="TemplatesRoot"/>
    ///     (Residential, Commercial, Industrial, Hospital, Military, CityCore, Park, Mixed)
    ///     and auto-synced into the matching per-zone array.
    ///     The generator window references this to populate the lot-type / template dropdowns.
    ///     Created via Assets → Create → Zombera → Building → Archetype Library.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Archetype Library",
        fileName = "BuildingArchetypeLibrary",
        order = 99)]
    public sealed class BuildingArchetypeLibrary : ScriptableObject
    {
        /// <summary>Root folder that holds the per-zone template subfolders.</summary>
        public const string TemplatesRoot = "Assets/02_Shared/ScriptableObjects/Buildings/Templates";

        [Header("Per-Zone Templates (auto-synced)")]
        [Tooltip("House templates for Residential lots. Auto-synced from the Templates/Residential subfolder.")]
        public ResidentialHouseTemplate[] ResidentialTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Commercial lots. Auto-synced from the Templates/Commercial subfolder.")]
        public ResidentialHouseTemplate[] CommercialTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Industrial lots. Auto-synced from the Templates/Industrial subfolder.")]
        public ResidentialHouseTemplate[] IndustrialTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Hospital lots. Auto-synced from the Templates/Hospital subfolder.")]
        public ResidentialHouseTemplate[] HospitalTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Military lots. Auto-synced from the Templates/Military subfolder.")]
        public ResidentialHouseTemplate[] MilitaryTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for CityCore lots. Auto-synced from the Templates/CityCore subfolder.")]
        public ResidentialHouseTemplate[] CityCoreTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Park lots. Auto-synced from the Templates/Park subfolder.")]
        public ResidentialHouseTemplate[] ParkTemplates = Array.Empty<ResidentialHouseTemplate>();

        [Tooltip("Templates for Mixed lots. Auto-synced from the Templates/Mixed subfolder.")]
        public ResidentialHouseTemplate[] MixedTemplates = Array.Empty<ResidentialHouseTemplate>();

        /// <summary>
        ///     Returns all templates that match the given lot type.
        /// </summary>
        public ResidentialHouseTemplate[] GetTemplatesForLotType(CityDistrictType lotType)
        {
            return lotType switch
            {
                CityDistrictType.Residential => ResidentialTemplates,
                CityDistrictType.Commercial => CommercialTemplates,
                CityDistrictType.Industrial => IndustrialTemplates,
                CityDistrictType.Hospital => HospitalTemplates,
                CityDistrictType.Military => MilitaryTemplates,
                CityDistrictType.CityCore => CityCoreTemplates,
                CityDistrictType.Park => ParkTemplates,
                CityDistrictType.Mixed => MixedTemplates,
                _ => Array.Empty<ResidentialHouseTemplate>()
            };
        }

        /// <summary>
        ///     Returns true if there is at least one template available for the given lot type.
        /// </summary>
        public bool HasTemplatesFor(CityDistrictType lotType)
        {
            return GetTemplatesForLotType(lotType).Length > 0;
        }

        /// <summary>
        ///     Scans the per-zone subfolders under <see cref="TemplatesRoot"/> and updates the
        ///     matching zone arrays so new SOs appear immediately in the generator dropdown.
        ///     Templates found elsewhere in the project fall back to their authored LotType.
        ///     Returns true if any array changed.
        /// </summary>
        public bool SyncFromProject()
        {
            var found = ScanProjectTemplates();
            var changed = false;

            foreach (var zone in GetAllZoneTypes())
            {
                var templates = found.TryGetValue(zone, out var list)
                    ? list.ToArray()
                    : Array.Empty<ResidentialHouseTemplate>();

                var current = GetTemplatesForLotType(zone);
                if (!ArraysEqual(current, templates))
                {
                    SetTemplatesForLotType(zone, templates);
                    changed = true;
                }
            }

            if (changed)
                EditorUtility.SetDirty(this);

            return changed;
        }

        /// <summary>
        ///     Scans the whole project and buckets templates by zone.
        ///     Templates inside <see cref="TemplatesRoot"/>&lt;Zone&gt; folders are bucketed by
        ///     folder name; everything else uses the template's authored LotType.
        /// </summary>
        private static Dictionary<CityDistrictType, List<ResidentialHouseTemplate>> ScanProjectTemplates()
        {
            var buckets = new Dictionary<CityDistrictType, List<ResidentialHouseTemplate>>();
            var folderZoneLookup = BuildFolderZoneLookup();

            var guids = AssetDatabase.FindAssets("t:ResidentialHouseTemplate");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<ResidentialHouseTemplate>(path);
                if (template == null)
                    continue;

                // Primary key: the parent folder of the asset when it lives under TemplatesRoot.
                var zone = ResolveZoneForPath(path, folderZoneLookup);
                if (!zone.HasValue)
                    zone = template.LotType; // Fallback for assets outside the Templates folder tree.

                if (!buckets.TryGetValue(zone.Value, out var list))
                {
                    list = new List<ResidentialHouseTemplate>();
                    buckets[zone.Value] = list;
                }

                list.Add(template);
            }

            // Sort each bucket by asset name for a predictable dropdown order.
            foreach (var bucket in buckets.Values)
                bucket.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            return buckets;
        }

        /// <summary>
        ///     Builds a case-insensitive lookup from zone name → zone type for every
        ///     <see cref="CityDistrictType"/> value.
        /// </summary>
        private static Dictionary<string, CityDistrictType> BuildFolderZoneLookup()
        {
            var lookup = new Dictionary<string, CityDistrictType>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in GetAllZoneTypes())
                lookup[zone.ToString()] = zone;
            return lookup;
        }

        /// <summary>
        ///     Resolves a zone for an asset path by checking its immediate parent folder
        ///     name against the zone-name lookup when the asset lives under TemplatesRoot.
        /// </summary>
        private static CityDistrictType? ResolveZoneForPath(
            string path, Dictionary<string, CityDistrictType> folderZoneLookup)
        {
            var normalized = path.Replace('\\', '/');
            var templatesRoot = TemplatesRoot.TrimEnd('/') + "/";
            if (!normalized.StartsWith(templatesRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = normalized.Substring(templatesRoot.Length);
            var firstSlash = relative.IndexOf('/');
            var folderName = firstSlash >= 0
                ? relative.Substring(0, firstSlash)
                : string.Empty;

            if (string.IsNullOrEmpty(folderName))
                return null;

            return folderZoneLookup.TryGetValue(folderName, out var zone)
                ? zone
                : (CityDistrictType?)null;
        }

        private void SetTemplatesForLotType(CityDistrictType lotType, ResidentialHouseTemplate[] templates)
        {
            switch (lotType)
            {
                case CityDistrictType.Residential: ResidentialTemplates = templates; break;
                case CityDistrictType.Commercial: CommercialTemplates = templates; break;
                case CityDistrictType.Industrial: IndustrialTemplates = templates; break;
                case CityDistrictType.Hospital: HospitalTemplates = templates; break;
                case CityDistrictType.Military: MilitaryTemplates = templates; break;
                case CityDistrictType.CityCore: CityCoreTemplates = templates; break;
                case CityDistrictType.Park: ParkTemplates = templates; break;
                case CityDistrictType.Mixed: MixedTemplates = templates; break;
            }
        }

        private static IEnumerable<CityDistrictType> GetAllZoneTypes()
        {
            return (CityDistrictType[])Enum.GetValues(typeof(CityDistrictType));
        }

        private static bool ArraysEqual<T>(T[] a, T[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;
            return true;
        }
    }
}
#endif
