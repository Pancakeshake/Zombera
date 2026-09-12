using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Zombera.Data
{
    /// <summary>
    /// A weighted catalogue of building material skins organised by zone/condition.
    /// Create via right-click > Create > Zombera/Data/Building Skin Table.
    ///
    /// The building generator and any procedural city tools use this to pick a
    /// <see cref="SkinSet"/> at generation time — the set is then fed into
    /// <c>BuildingSkinReskinTool</c> for actual material assignment.
    ///
    /// Zone types and condition names are free-form strings so the city generator
    /// can define its own vocabulary (e.g. "Residential", "Commercial", "Industrial").
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Building Skin Table", fileName = "BuildingSkinTable")]
    public sealed class BuildingSkinTable : ScriptableObject
    {
        [Header("Scriptable Objects")]
        [Tooltip("Optional per-room-type floor material config. When set, the generator applies room-specific floor materials after the main reskin pass.")]
        public RoomFloorSkinConfig roomFloorSkinConfig;

        [Header("Zones")]
        [Tooltip("Fallback skin used when no zone or condition match is found.")]
        public SkinSet fallback;

        public List<ZoneSkinSet> zones = new();

        /// <summary>
        /// Picks a weighted-random <see cref="SkinSet"/> for the given zone and condition.
        /// Falls back to <see cref="fallback"/> when nothing matches.
        /// Returns null if no entry has materials configured.
        /// </summary>
        public SkinSet PickSkin(string zoneName, string condition = "")
        {
            ZoneSkinSet match = null;
            foreach (var z in zones)
            {
                if (string.Equals(z.zoneName, zoneName, StringComparison.OrdinalIgnoreCase))
                {
                    match = z;
                    break;
                }
            }

            if (match == null)
                return fallback;

            // Try condition-specific skins first.
            if (!string.IsNullOrEmpty(condition))
            {
                foreach (var cs in match.conditionOverrides)
                {
                    if (string.Equals(cs.condition, condition, StringComparison.OrdinalIgnoreCase))
                    {
                        var picked = PickWeighted(cs.skins);
                        if (picked != null) return picked;
                    }
                }
            }

            // Fall back to zone default skins.
            return PickWeighted(match.defaultSkins) ?? fallback;
        }

        private static SkinSet PickWeighted(List<SkinSet> skins)
        {
            if (skins == null || skins.Count == 0) return null;

            var total = 0f;
            foreach (var s in skins) total += Mathf.Max(0f, s.weight);
            if (total <= 0f) return skins[Random.Range(0, skins.Count)];

            var roll = Random.value * total;
            var acc = 0f;
            foreach (var s in skins)
            {
                acc += Mathf.Max(0f, s.weight);
                if (roll <= acc) return s;
            }

            return skins[skins.Count - 1];
        }
    }

    /// <summary>
    /// A complete set of materials for every building-piece category.
    /// Each category contains a weighted list — one material is picked per category
    /// when the skin is resolved. Supports gable-to-exterior-wall matching and a
    /// chance to pick a different exterior material for variety.
    /// </summary>
    [Serializable]
    public sealed class SkinSet
    {
        [Tooltip("Display name for this skin set in the inspector.")]
        public string name;

        [Tooltip("Relative weight when this set is part of a randomised list. Higher = more likely.")]
        [Min(0f)] public float weight = 1f;

        [Header("Floor & Foundation")]
        public List<WeightedMaterial> floorMaterials = new();
        public List<WeightedMaterial> foundationMaterials = new();

        [Header("Walls")]
        public List<WeightedMaterial> wallExteriorMaterials = new();
        public List<WeightedMaterial> wallInteriorMaterials = new();

        [Header("Roof & Gable")]
        public List<WeightedMaterial> roofMaterials = new();
        public List<WeightedMaterial> gableMaterials = new();

        [Header("Stairs & Windows")]
        public List<WeightedMaterial> stairMaterials = new();
        public List<WeightedMaterial> windowMaterials = new();

        [Header("Variation")]
        [Tooltip("When enabled, gables use the same material as exterior walls instead of their own list.")]
        public bool gableMatchesExteriorWall = true;

        [Tooltip("Chance (0–1) that a DIFFERENT exterior material is picked for variety. " +
                 "If this fires, a second roll from the exterior list replaces the first pick.")]
        [Range(0f, 1f)] public float differentExteriorChance;

        /// <summary>
        /// Builds a runtime <c>BuildingSkin</c> from this set, picking one material
        /// from each category's weighted list. Uses <c>UnityEngine.Random</c>.
        /// </summary>
        public BuildingSkin BuildBuildingSkin()
        {
            return BuildBuildingSkin(null);
        }

        /// <summary>
        /// Builds a runtime <c>BuildingSkin</c> using the given <c>System.Random</c> for material picks.
        /// Falls back to <c>UnityEngine.Random</c> when null.
        /// </summary>
        public BuildingSkin BuildBuildingSkin(System.Random rng)
        {
            var skin = new BuildingSkin
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Skin" : name
            };

            // Floor materials are now controlled by RoomFloorSkinConfig.
            // floorMaterials serves as the catalog that RoomFloorSkinConfig draws from.
            skin.Floor        = null;
            skin.Foundation   = PickFromList(foundationMaterials, rng);
            skin.Roof         = PickFromList(roofMaterials, rng);
            skin.Stairs       = PickFromList(stairMaterials, rng);
            skin.Windows      = PickFromList(windowMaterials, rng);

            var exteriorPick = PickFromList(wallExteriorMaterials, rng);
            if (exteriorPick != null && differentExteriorChance > 0f)
            {
                var roll = rng?.NextDouble() ?? (double)Random.value;
                if (roll < differentExteriorChance)
                {
                    var alt = PickFromList(wallExteriorMaterials, rng);
                    if (alt != null && alt != exteriorPick)
                        exteriorPick = alt;
                }
            }
            skin.WallExterior = exteriorPick;
            skin.WallInterior = PickFromList(wallInteriorMaterials, rng) ?? skin.WallExterior;

            if (gableMatchesExteriorWall)
                skin.Gable = skin.WallExterior;
            else
                skin.Gable = PickFromList(gableMaterials, rng) ?? skin.Roof ?? skin.WallExterior;

            skin.Default = skin.WallExterior ?? skin.Foundation;
            return skin;
        }

        private static Material PickFromList(List<WeightedMaterial> list, System.Random rng = null)
        {
            if (list == null || list.Count == 0) return null;

            var total = 0f;
            foreach (var wm in list)
                if (wm.material != null)
                    total += Mathf.Max(0f, wm.weight);

            if (total <= 0f)
                return PickUniform(list, rng);

            return PickWeighted(list, total, rng);
        }

        private static Material PickUniform(List<WeightedMaterial> list, System.Random rng)
        {
            var valid = new List<Material>();
            foreach (var wm in list)
                if (wm.material != null) valid.Add(wm.material);
            if (valid.Count == 0) return null;
            var idx = rng != null ? rng.Next(valid.Count) : Random.Range(0, valid.Count);
            return valid[idx];
        }

        private static Material PickWeighted(List<WeightedMaterial> list, float total, System.Random rng)
        {
            var roll = (rng != null ? (float)rng.NextDouble() : Random.value) * total;
            var acc = 0f;
            foreach (var wm in list)
            {
                if (wm.material == null) continue;
                acc += Mathf.Max(0f, wm.weight);
                if (roll <= acc) return wm.material;
            }

            for (var i = list.Count - 1; i >= 0; i--)
                if (list[i].material != null) return list[i].material;
            return null;
        }
    }

    /// <summary>A set of skins (with weights) for a named city zone, e.g. "Residential", "Commercial".</summary>
    [Serializable]
    public sealed class ZoneSkinSet
    {
        [Tooltip("Zone identifier, e.g. 'Residential', 'Commercial', 'Industrial'.")]
        public string zoneName;

        [Tooltip("Skins chosen from when no condition override matches.")]
        public List<SkinSet> defaultSkins = new();

        [Tooltip("Condition-specific overrides, e.g. 'Abandoned', 'FireDamaged'.")]
        public List<ConditionSkinSet> conditionOverrides = new();
    }

    /// <summary>Skins tied to a specific building condition, e.g. 'Abandoned' or 'Wealthy'.</summary>
    [Serializable]
    public sealed class ConditionSkinSet
    {
        [Tooltip("Condition tag, e.g. 'Abandoned', 'FireDamaged', 'New'.")]
        public string condition;

        public List<SkinSet> skins = new();
    }

    /// <summary>A material reference with a spawn weight for randomised picking.</summary>
    [Serializable]
    public sealed class WeightedMaterial
    {
        [Tooltip("The material asset to apply.")]
        public Material material;

        [Tooltip("Relative weight. Higher = more likely to be picked.")]
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>Categories of building pieces that can receive distinct materials.</summary>
    public enum PieceCategory
    {
        Unknown,
        Floors,
        Walls,
        InteriorWalls,
        Foundation,
        Roof,
        Gable,
        Stairs,
        Windows
    }

    /// <summary>
    /// A runtime material palette for a building skin. Built from a <see cref="SkinSet"/>
    /// via <see cref="SkinSet.BuildBuildingSkin"/>. Consumed by the reskin tool at material-application time.
    /// </summary>
    public sealed class BuildingSkin
    {
        public string Name;
        public Material Default;

        public Material Floor;
        public Material Foundation;
        public Material Roof;
        public Material Stairs;

        public Material WallExterior;
        public Material WallInterior;
        public Material Windows;

        public Material Gable;

        public Material[] ResolveMaterials(PieceCategory category, int slotCount)
        {
            slotCount = Mathf.Max(1, slotCount);

            switch (category)
            {
                case PieceCategory.Floors:
                    // Floors are controlled by RoomFloorSkinConfig — return null here.
                    return null;
                case PieceCategory.Foundation:
                    return BuildSingle(slotCount, Foundation ?? Default);
                case PieceCategory.Roof:
                    return BuildSingle(slotCount, Roof ?? Default);
                case PieceCategory.Gable:
                    return BuildSingle(slotCount, Gable ?? Roof ?? WallExterior ?? Default);
                case PieceCategory.Stairs:
                    return BuildSingle(slotCount, Stairs ?? Default);
                case PieceCategory.Windows:
                {
                    if (slotCount <= 1)
                        return BuildSingle(slotCount, Windows ?? Default);

                    var mats = new Material[slotCount];
                    mats[0] = WallInterior ?? WallExterior ?? Default;
                    for (var i = 1; i < slotCount; i++)
                        mats[i] = Windows ?? WallExterior ?? mats[0];
                    return mats;
                }
                case PieceCategory.Walls:
                {
                    if (slotCount <= 1)
                        return BuildSingle(slotCount, WallExterior ?? Default);

                    var mats = new Material[slotCount];
                    mats[0] = WallInterior ?? WallExterior ?? Default;
                    mats[1] = WallExterior ?? Default;
                    for (var i = 2; i < slotCount; i++)
                        mats[i] = mats[i - 1] ?? Default;
                    return mats;
                }
                case PieceCategory.InteriorWalls:
                    return BuildSingle(slotCount, WallInterior ?? WallExterior ?? Default);
                default:
                    return null;
            }
        }

        private static Material[] BuildSingle(int slotCount, Material mat)
        {
            if (mat == null)
                return null;
            var mats = new Material[slotCount];
            for (var i = 0; i < slotCount; i++)
                mats[i] = mat;
            return mats;
        }
    }
}
