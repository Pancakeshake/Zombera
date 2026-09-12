#region

using System;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.Roads;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Streamed city catalog rebuild tooling. MapMagic builder bootstrap menus are retired.
    /// </summary>
    public static class StreamedCityBuilderQuickTool
    {
        private const string DefaultPrefabFolder = "Assets/02_Shared/Prefabs/Building/Buildings_Modular_Complete";
        private const string CatalogFolder = "Assets/Resources/World";
        private const string CatalogAssetPath = CatalogFolder + "/StreamedCityCatalog.asset";

        [MenuItem("Tools/World/City/Rebuild Streamed City Catalog", priority = -500)]
        public static void RebuildStreamedCityCatalogMenu()
        {
            var catalog = LoadOrCreateCatalog();
            if (catalog == null)
            {
                Debug.LogError("[StreamedCityBuilderQuickTool] Could not create or load StreamedCityCatalog asset.");
                return;
            }

            RebuildCatalogFromDefaultFolder(catalog);
            Selection.activeObject = catalog;
        }

        [MenuItem("Tools/World/City/Build Streamed City Builder", priority = -500)]
        private static void BuildStreamedCityBuilder()
        {
            EditorUtility.DisplayDialog(
                "MapMagic City Builder Removed",
                "StreamedMapMagicCityBuilder / MapMagicTileStreamBridge are gone. " +
                "Use World Builder + WorldStreamedCityBuilder on the World scene instead. " +
                "Catalog rebuild menus still work.",
                "OK");
        }

        [MenuItem("Tools/World/City/Apply Dense Validation Tuning", priority = -500)]
        private static void ApplyDenseValidationTuningMenu()
        {
            EditorUtility.DisplayDialog(
                "Dense Tuning Removed",
                "Apply Dense Validation Tuning belonged to StreamedMapMagicCityBuilder and is no longer available.",
                "OK");
        }

        [MenuItem("Tools/World/City/Rebuild Streamed City Catalog (Buildings Modular Complete)", priority = -500)]
        private static void RebuildCatalogMenu()
        {
            var catalog = LoadOrCreateCatalog();
            if (catalog == null)
            {
                Debug.LogError("[StreamedCityBuilderQuickTool] Could not create or load StreamedCityCatalog asset.");
                return;
            }

            RebuildCatalogFromDefaultFolder(catalog);
            Selection.activeObject = catalog;
        }

        private static StreamedCityCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StreamedCityCatalog>(CatalogAssetPath);
            if (catalog != null) return catalog;

            EnsureFolderHierarchy(CatalogFolder);

            catalog = ScriptableObject.CreateInstance<StreamedCityCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void RebuildCatalogFromDefaultFolder(StreamedCityCatalog catalog)
        {
            if (catalog == null) return;

            if (!AssetDatabase.IsValidFolder(DefaultPrefabFolder))
            {
                Debug.LogWarning(
                    $"[StreamedCityBuilderQuickTool] Folder '{DefaultPrefabFolder}' was not found. Catalog left unchanged.");
                return;
            }

            var proxyBySourceName = LoadProxyMap(DefaultPrefabFolder + "/Proxies");
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { DefaultPrefabFolder });
            var entries = catalog.Entries;
            entries.Clear();

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsUnderProxiesFolder(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var entry = new StreamedCityBuildingEntry
                {
                    id = prefab.name,
                    prefab = prefab,
                    weight = 1f
                };

                if (ModularBuildingCategoryResolver.TryParseCategoryFromPrefabName(prefab.name, out var category))
                {
                    entry.allowedRoadCityZones = new System.Collections.Generic.List<RoadCityZone>
                    {
                        CityNamedAreaMarker.ToRoadCityZone(category)
                    };
                }

                // Same measured footprint the hub building placer uses — hardcoded sizes break block packing.
                if (CityBuildingPrefabFootprintUtility.TryMeasureFootprint(prefab, out var footprint))
                {
                    entry.footprintWidthMeters = Mathf.Max(4f, footprint.WidthMeters);
                    entry.footprintDepthMeters = Mathf.Max(4f, footprint.DepthMeters);
                }
                else if (TryEstimateFootprint(prefab, out var width, out var depth))
                {
                    entry.footprintWidthMeters = Mathf.Max(4f, width);
                    entry.footprintDepthMeters = Mathf.Max(4f, depth);
                }

                proxyBySourceName.TryGetValue(prefab.name, out entry.proxyPrefab);

                entries.Add(entry);
            }

            entries.Sort((a, b) => string.Compare(a?.id, b?.id, StringComparison.OrdinalIgnoreCase));

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[StreamedCityBuilderQuickTool] Rebuilt catalog with {entries.Count} prefab entries from '{DefaultPrefabFolder}'.",
                catalog);
        }

        private static System.Collections.Generic.Dictionary<string, GameObject> LoadProxyMap(string proxyFolder)
        {
            var map = new System.Collections.Generic.Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(proxyFolder) || !AssetDatabase.IsValidFolder(proxyFolder))
                return map;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { proxyFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                if (!CityBuildingPrefabNaming.TryParseSourceNameFromProxyPrefabName(prefab.name, out var sourceName))
                    continue;

                map[sourceName] = prefab;
            }

            return map;
        }

        private static bool IsUnderProxiesFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return true;

            var normalized = assetPath.Replace('\\', '/');
            return normalized.StartsWith(
                DefaultPrefabFolder.TrimEnd('/') + "/Proxies/",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var segments = folderPath.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal)) return;

            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static bool TryEstimateFootprint(GameObject prefab, out float width, out float depth)
        {
            width = 0f;
            depth = 0f;
            if (prefab == null) return false;

            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);

            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            if (!hasBounds)
            {
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;

                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds) return false;

            width = bounds.size.x;
            depth = bounds.size.z;
            return width > 0.01f && depth > 0.01f;
        }
    }
}
