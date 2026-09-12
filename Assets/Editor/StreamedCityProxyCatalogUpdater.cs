#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    internal sealed class StreamedCityProxyCatalogUpdater
    {
        private readonly string proxyFolderPrefix;
        private readonly float defaultSwapDistanceMeters;

        internal StreamedCityProxyCatalogUpdater(string proxyPrefabFolder, float defaultSwapDistanceMeters)
        {
            proxyFolderPrefix = string.IsNullOrWhiteSpace(proxyPrefabFolder)
                ? string.Empty
                : proxyPrefabFolder.TrimEnd('/') + "/";
            this.defaultSwapDistanceMeters = defaultSwapDistanceMeters;
        }

        internal int WireCatalogToGeneratedProxies(
            StreamedCityCatalog catalog,
            IReadOnlyDictionary<string, GameObject> proxyBySourcePath)
        {
            if (catalog?.Entries == null || proxyBySourcePath == null)
                return 0;

            var wired = 0;
            var dirty = false;

            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry?.prefab == null)
                    continue;

                var sourcePath = AssetDatabase.GetAssetPath(entry.prefab);
                if (string.IsNullOrWhiteSpace(sourcePath))
                    continue;

                if (proxyBySourcePath.TryGetValue(sourcePath, out var proxyPrefab) && proxyPrefab != null)
                {
                    wired++;
                    if (ApplyProxyRules(entry, proxyPrefab))
                        dirty = true;

                    continue;
                }

                if (entry.proxyPrefab == null)
                    continue;

                var proxyPath = AssetDatabase.GetAssetPath(entry.proxyPrefab);
                if (string.IsNullOrWhiteSpace(proxyPath) ||
                    string.IsNullOrWhiteSpace(proxyFolderPrefix) ||
                    !proxyPath.StartsWith(proxyFolderPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                entry.proxyPrefab = null;
                entry.useProxySwap = false;
                dirty = true;
            }

            if (!dirty)
                return wired;

            EditorUtility.SetDirty(catalog);
            return wired;
        }

        private bool ApplyProxyRules(StreamedCityBuildingEntry entry, GameObject proxyPrefab)
        {
            var dirty = false;

            if (entry.proxyPrefab != proxyPrefab)
            {
                entry.proxyPrefab = proxyPrefab;
                dirty = true;
            }

            if (!entry.useProxySwap)
            {
                entry.useProxySwap = true;
                dirty = true;
            }

            if (!Mathf.Approximately(entry.proxySwapDistanceMeters, defaultSwapDistanceMeters))
            {
                entry.proxySwapDistanceMeters = defaultSwapDistanceMeters;
                dirty = true;
            }

            if (!entry.proxySwapOnFirstDamage)
            {
                entry.proxySwapOnFirstDamage = true;
                dirty = true;
            }

            return dirty;
        }
    }
}
#endif
