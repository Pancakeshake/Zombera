#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.World.City
{
    internal readonly struct CityLotDecorationCatalog
    {
        public readonly List<GameObject> Trees;
        public readonly List<GameObject> Foliage;
        public readonly List<GameObject> Props;

        public CityLotDecorationCatalog(
            List<GameObject> trees,
            List<GameObject> foliage,
            List<GameObject> props)
        {
            Trees = trees ?? new List<GameObject>();
            Foliage = foliage ?? new List<GameObject>();
            Props = props ?? new List<GameObject>();
        }

        public bool HasAnyContent => Trees.Count > 0 || Foliage.Count > 0 || Props.Count > 0;
    }

    internal static class CityLotDecorationCatalogLoader
    {
        public const string DefaultTreeFolder =
            "Assets/02_Shared/Prefabs/Props/Nature";

        public static CityLotDecorationCatalog LoadTrees(string treeFolder)
        {
            var trees = LoadFilteredPrefabs(treeFolder, IsTreePrefab, rootLevelOnly: true);
            return new CityLotDecorationCatalog(trees, null, null);
        }

        public static CityLotDecorationCatalog Load(
            string treePrefabFolder,
            string foliagePrefabFolder,
            string propsPrefabFolder)
        {
            var trees = LoadFilteredPrefabs(treePrefabFolder, IsTreePrefab, rootLevelOnly: true);
            var foliage = LoadFilteredPrefabs(foliagePrefabFolder, IsFoliagePrefab);
            var props = LoadFilteredPrefabs(propsPrefabFolder, _ => true);
            return new CityLotDecorationCatalog(trees, foliage, props);
        }

        private static List<GameObject> LoadFilteredPrefabs(
            string folder, Func<string, bool> nameFilter, bool rootLevelOnly = false)
        {
            var results = new List<GameObject>();
            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
                return results;

            var normalizedFolder = folder.Replace('\\', '/').TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (rootLevelOnly)
                {
                    var lastSlash = path.LastIndexOf('/');
                    var parentFolder = lastSlash > 0 ? path.Substring(0, lastSlash) : string.Empty;
                    if (!string.Equals(parentFolder, normalizedFolder, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !nameFilter(prefab.name))
                    continue;

                results.Add(prefab);
            }

            results.Sort((a, b) => string.Compare(a?.name, b?.name, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static bool IsTreePrefab(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            return prefabName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0
                   || prefabName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0
                   || prefabName.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFoliagePrefab(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            return prefabName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
