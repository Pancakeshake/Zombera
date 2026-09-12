#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private static StreamedCityCatalog ResolveTargetCatalog()
        {
            if (Selection.activeObject is StreamedCityCatalog selectedCatalog)
                return selectedCatalog;

            var builder = Object.FindFirstObjectByType<WorldStreamedCityBuilder>();
            if (builder != null)
            {
                if (StreamedCityProxySceneBuilderAdapter.TryResolveCatalog(builder, out var builderCatalog, out var warning))
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                        Debug.LogWarning("[StreamedCityProxySwapTool] " + warning, builder);

                    if (builderCatalog != null)
                        return builderCatalog;
                }
                else if (!string.IsNullOrWhiteSpace(warning))
                {
                    Debug.LogWarning("[StreamedCityProxySwapTool] " + warning, builder);
                }
            }

            return AssetDatabase.LoadAssetAtPath<StreamedCityCatalog>(DefaultCatalogAssetPath);
        }

        private static List<GameObject> CollectSourcePrefabs()
        {
            var prefabs = new List<(string path, GameObject prefab)>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { DefaultSourcePrefabFolder });
            if (guids == null || guids.Length == 0)
                return new List<GameObject>();

            var proxyPrefix = DefaultProxyPrefabFolder + "/";
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (path.StartsWith(proxyPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                prefabs.Add((path, prefab));
            }

            prefabs.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));

            var ordered = new List<GameObject>(prefabs.Count);
            for (var i = 0; i < prefabs.Count; i++)
                ordered.Add(prefabs[i].prefab);

            return ordered;
        }

        private static StreamedCityBuildingEntry FindFirstCatalogEntryForPrefab(
            StreamedCityCatalog catalog,
            GameObject sourcePrefab)
        {
            if (catalog?.Entries == null || sourcePrefab == null)
                return null;

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry?.prefab == sourcePrefab)
                    return entry;
            }

            return null;
        }

        private static string BuildProxyAssetPath(GameObject sourcePrefab, string sourcePath)
        {
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var guidSuffix = !string.IsNullOrWhiteSpace(sourceGuid) && sourceGuid.Length >= 8
                ? sourceGuid[..8]
                : "noguid";

            var sourceName = sourcePrefab != null ? sourcePrefab.name : "Proxy";
            var fileName = SanitizeAssetFileName(sourceName + "_" + guidSuffix + "_Proxy.prefab");

            var relativeFolder = ResolveSourceRelativeFolder(sourcePath);
            var outputFolder = string.IsNullOrEmpty(relativeFolder)
                ? DefaultProxyPrefabFolder
                : DefaultProxyPrefabFolder + "/" + relativeFolder;
            EnsureFolderHierarchy(outputFolder);
            return outputFolder + "/" + fileName;
        }

        private static string ResolveSourceRelativeFolder(string sourcePath)
        {
            var sourceRoot = DefaultSourcePrefabFolder.TrimEnd('/') + "/";
            var normalized = sourcePath.Replace('\\', '/');
            if (!normalized.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var relative = normalized[sourceRoot.Length..];
            var separator = relative.LastIndexOf('/');
            if (separator <= 0)
                return string.Empty;

            return relative[..separator];
        }

        private static string BuildProxyMeshAssetPath(string proxyAssetPath)
        {
            if (string.IsNullOrWhiteSpace(proxyAssetPath))
                return string.Empty;

            var directory = Path.GetDirectoryName(proxyAssetPath)?.Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(proxyAssetPath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            return directory + "/" + fileName + ProxyMeshAssetSuffix;
        }

        private static List<string> CollectOrphanProxyPrefabPaths(ISet<string> expectedProxyPaths)
        {
            var orphans = new List<string>();
            if (!AssetDatabase.IsValidFolder(DefaultProxyPrefabFolder))
                return orphans;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { DefaultProxyPrefabFolder });
            if (guids == null || guids.Length == 0)
                return orphans;

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (expectedProxyPaths != null && expectedProxyPaths.Contains(path))
                    continue;

                orphans.Add(path);
            }

            return orphans;
        }

        private static List<string> CollectOrphanProxyMeshAssetPaths(ISet<string> expectedProxyMeshAssetPaths)
        {
            var orphans = new List<string>();
            if (!AssetDatabase.IsValidFolder(DefaultProxyPrefabFolder))
                return orphans;

            var guids = AssetDatabase.FindAssets("t:Mesh", new[] { DefaultProxyPrefabFolder });
            if (guids == null || guids.Length == 0)
                return orphans;

            var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!path.EndsWith(ProxyMeshAssetSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidatePaths.Add(path);
            }

            foreach (var path in candidatePaths)
            {
                if (expectedProxyMeshAssetPaths != null && expectedProxyMeshAssetPaths.Contains(path))
                    continue;

                orphans.Add(path);
            }

            return orphans;
        }

        private static int DeleteAssets(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null || assetPaths.Count == 0)
                return 0;

            var removed = 0;
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i];
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (AssetDatabase.DeleteAsset(path))
                    removed++;
            }

            return removed;
        }

        private static bool IsAssetPathInsideFolder(string assetPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(folderPath))
                return false;

            var normalizedAssetPath = assetPath.Replace('\\', '/').Trim();
            var normalizedFolderPath = folderPath.Replace('\\', '/').Trim().TrimEnd('/');
            return normalizedAssetPath.StartsWith(normalizedFolderPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryConvertAssetPathToFileSystemPath(string assetPath, out string fileSystemPath)
        {
            fileSystemPath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            var normalized = assetPath.Replace('\\', '/').Trim();
            var assetsRoot = Application.dataPath.Replace('\\', '/');

            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                fileSystemPath = assetsRoot;
                return true;
            }

            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;

            fileSystemPath = Path.Combine(assetsRoot, normalized["Assets/".Length..]).Replace('\\', '/');
            return true;
        }

        private static bool TryGetNearestExistingDirectory(string directoryPath, out string existingDirectoryPath)
        {
            existingDirectoryPath = string.Empty;
            if (string.IsNullOrWhiteSpace(directoryPath))
                return false;

            var candidate = directoryPath;
            while (!string.IsNullOrWhiteSpace(candidate))
            {
                if (Directory.Exists(candidate))
                {
                    existingDirectoryPath = candidate;
                    return true;
                }

                candidate = Path.GetDirectoryName(candidate)?.Replace('\\', '/');
            }

            return false;
        }

        private static string SanitizeAssetFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Proxy.prefab";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = fileName.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                    continue;
                }

                if (chars[i] == ' ')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var segments = folderPath.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                return;

            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }
    }
}
#endif
