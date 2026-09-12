#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.World.City
{
    internal static class CityAssembledBuildingCatalogLoader
    {
        public static List<CityAssembledBuildingCatalogEntry> Load(string assembledFolder, string proxyFolder)
        {
            var results = new List<CityAssembledBuildingCatalogEntry>();
            if (string.IsNullOrWhiteSpace(assembledFolder) || !AssetDatabase.IsValidFolder(assembledFolder))
                return results;

            var proxyBySourceName = LoadProxyMap(proxyFolder);
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { assembledFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) || IsUnderProxiesFolder(path, assembledFolder))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var entry = new CityAssembledBuildingCatalogEntry
                {
                    id = prefab.name,
                    assetPath = path,
                    prefab = prefab
                };

                entry.districtType = ResolveDistrictType(path, assembledFolder, prefab.name);

                BuildingFootprintInfo footprint;
                if (CityBuildingPrefabFootprintUtility.TryMeasureFootprint(prefab, out footprint))
                    entry.ApplyFootprint(footprint);

                entry.doorYawOffsetDegrees = CityBuildingRoadFacingUtility.GetDoorYawOffset(prefab, entry.yawOffsetDegrees, footprint);
                entry.doorYawResolved = true;

                proxyBySourceName.TryGetValue(prefab.name, out entry.proxyPrefab);
                if (entry.proxyPrefab != null &&
                    CityBuildingPrefabFootprintUtility.TryMeasureFootprint(entry.proxyPrefab, out var proxyFootprint))
                {
                    entry.ApplyProxyFootprint(proxyFootprint);
                }
                results.Add(entry);
            }

            results.Sort((a, b) => string.Compare(a?.id, b?.id, System.StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static Dictionary<string, GameObject> LoadProxyMap(string proxyFolder)
        {
            var map = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
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

        private static CityDistrictType ResolveDistrictType(string assetPath, string assembledFolder, string prefabName)
        {
            if (TryParseDistrictFromSubFolder(assetPath, assembledFolder, out var fromFolder) ||
                CityBuildingPrefabNaming.TryParseDistrictFromPrefabName(prefabName, out fromFolder))
                return fromFolder;

            return CityDistrictType.Mixed;
        }

        private static bool IsUnderProxiesFolder(string assetPath, string assembledFolder)
        {
            var normalized = assetPath.Replace('\\', '/');
            var proxiesRoot = assembledFolder.TrimEnd('/') + "/Proxies/";
            return normalized.StartsWith(proxiesRoot, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Resolves the district type from the first subfolder under the assembled folder.
        ///     Subfolders mirror <see cref="CityDistrictType"/> names, e.g.
        ///     "…/Buildings_Modular_Complete/Residential/Residential_123.prefab" → Residential.
        ///     Returns false for prefabs at the assembled root or under unknown folders,
        ///     so the caller can fall back to name-based parsing.
        /// </summary>
        private static bool TryParseDistrictFromSubFolder(string assetPath, string assembledFolder, out CityDistrictType district)
        {
            district = CityDistrictType.Mixed;

            var root = assembledFolder.TrimEnd('/').Replace('\\', '/') + "/";
            var normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                return false;

            var relative = normalized[root.Length..];
            var separator = relative.IndexOf('/');
            if (separator <= 0)
                return false; // Prefab sits directly in the assembled root — no lot-type subfolder.

            var folderName = relative[..separator];
            if (folderName.Equals("Proxies", System.StringComparison.OrdinalIgnoreCase))
                return false;

            return System.Enum.TryParse(folderName, true, out district) &&
                   System.Enum.IsDefined(typeof(CityDistrictType), district);
        }

    }
}
#endif