#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.Organizing
{
    /// <summary>
    /// Stateless scanner that searches the asset database for assets of a
    /// given type, filters by excluded folders, and classifies each result.
    /// </summary>
    public static class AssetAuditorScanner
    {
        /// <summary>
        /// Run a full scan and return classified results.
        /// </summary>
        public static List<ScanResult> Scan(
            AssetType assetType,
            string searchFolder,
            string destinationFolder,
            string[] excludedFolders,
            string categoryFilter = null)
        {
            var results = new List<ScanResult>();
            var filter = assetType.ToSearchFilter();

            // Normalize once
            var normalizedDest  = (destinationFolder ?? "").Replace('\\', '/').TrimEnd('/');
            var normalizedExcl  = NormalizeExcluded(excludedFolders);
            var isAllTypes      = assetType == AssetType.AllTypes;

            // Validate destination (skip for AllTypes — destinations are per-asset)
            if (!isAllTypes && !normalizedDest.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[Asset Auditor] Invalid destination '{destinationFolder}'.");
                return results;
            }

            var allGuids = AssetDatabase.FindAssets(filter, new[] { searchFolder });
            var total    = allGuids.Length;

            for (var i = 0; i < total; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allGuids[i]);
                if (!IsValidAssetFile(path, assetType))
                    continue;
                if (IsExcluded(path, normalizedExcl))
                    continue;
                if (!MatchesCategory(path, categoryFilter))
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Asset Auditor — Scanning",
                    Path.GetFileName(path),
                    (float)i / total))
                {
                    break;
                }

                // For AllTypes, compute per-asset destination based on actual type
                var dest = isAllTypes
                    ? BuildDestinationForAsset(path, categoryFilter)
                    : normalizedDest;

                var status       = DetermineStatus(path, dest);
                var currentDir   = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Unknown";

                results.Add(new ScanResult(
                    assetPath:         path,
                    assetName:         Path.GetFileNameWithoutExtension(path),
                    currentFolder:     currentDir,
                    destinationFolder: dest,
                    status:            status));
            }

            EditorUtility.ClearProgressBar();

            Debug.Log($"[Asset Auditor] Scan done — {results.Count} total, " +
                      $"{results.Count(r => r.Status == ScanStatus.NeedsMoving)} need moving, " +
                      $"{results.Count(r => r.Status == ScanStatus.AlreadyCorrect)} already correct.");

            return results;
        }

        // ── helpers ─────────────────────────────────────────────────────

        private static string[] NormalizeExcluded(string[] folders)
        {
            if (folders == null)
                return Array.Empty<string>();

            return folders
                .Select(f => f.Replace('\\', '/').TrimEnd('/'))
                .Where(f => f.Length > 0)
                .ToArray();
        }

        private static bool IsValidAssetFile(string path, AssetType assetType)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return assetType switch
            {
                AssetType.Prefabs   => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase),
                AssetType.Materials => path.EndsWith(".mat",    StringComparison.OrdinalIgnoreCase),
                AssetType.Meshes    => IsMeshFile(path),
                AssetType.Proxies   => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                                       && Path.GetFileNameWithoutExtension(path)
                                              .Contains("_proxy", StringComparison.OrdinalIgnoreCase),
                AssetType.AllTypes  => IsAnyValidAssetFile(path),
                _ => false
            };
        }

        private static bool IsAnyValidAssetFile(string path)
        {
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mat",    StringComparison.OrdinalIgnoreCase)
                || IsMeshFile(path);
        }

        /// <summary>Builds the destination path for a single asset based on its actual type + category.</summary>
        private static string BuildDestinationForAsset(string path, string categoryName)
        {
            var subfolder = GetActualType(path).ToSubfolder();
            var folderName = CategoryRegistry.GetFolderName(categoryName ?? "Misc");
            return $"Assets/02_Shared/{subfolder}/{folderName}";
        }

        /// <summary>Determines the actual AssetType from a file extension.</summary>
        private static AssetType GetActualType(string path)
        {
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return AssetType.Prefabs;
            if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                return AssetType.Materials;
            return AssetType.Meshes;
        }

        private static bool IsMeshFile(string path)
        {
            var ext = Path.GetExtension(path);

            // .mesh and .asset are always mesh-related data
            if (ext.Equals(".mesh",  StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".asset", StringComparison.OrdinalIgnoreCase))
                return true;

            // Model files (.fbx, .obj, .blend) — must not be animation-only
            if (ext.Equals(".fbx",   StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".obj",   StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".blend", StringComparison.OrdinalIgnoreCase))
            {
                return !IsAnimationOnlyModel(path);
            }

            return false;
        }

        private static bool IsAnimationOnlyModel(string path)
        {
            // Fast path: skip files in Animation directories
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Animations/", StringComparison.OrdinalIgnoreCase))
                return true;

            // Secondary check: verify the model actually contains mesh sub-assets
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            return !subAssets.Any(a => a is Mesh);
        }

        private static bool IsExcluded(string path, string[] normalizedExcluded)
        {
            var p = path.Replace('\\', '/');

            foreach (var excl in normalizedExcluded)
            {
                if (p.Equals(excl, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (p.StartsWith(excl + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool MatchesCategory(string assetPath, string categoryFilter)
        {
            if (string.IsNullOrEmpty(categoryFilter) || categoryFilter == "Misc")
                return true;

            var name = Path.GetFileNameWithoutExtension(assetPath);
            var singular = categoryFilter.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? categoryFilter[..^1]
                : categoryFilter;

            // Match on underscore-delimited or camelCase word boundaries
            // — "FenceGrab" must not match "Fences", but "Wooden_Fence" or "Fence_Wood" should
            var segments = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (segment.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase)
                    || segment.Equals(singular, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Also match camelCase sub-words within a segment (e.g. "ChainLinkFence" → "Fence")
                var camelWords = SplitCamelCase(segment);
                if (camelWords.Any(w => w.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase)
                                     || w.Equals(singular, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static readonly System.Text.RegularExpressions.Regex CamelSplitter =
            new System.Text.RegularExpressions.Regex(
                @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[0-9])(?=[A-Za-z])|(?<=[A-Za-z])(?=[0-9])",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string[] SplitCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Array.Empty<string>();
            return CamelSplitter.Split(input);
        }

        private static ScanStatus DetermineStatus(string assetPath, string destFolder)
        {
            var normalized = assetPath.Replace('\\', '/');
            var currentDir = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? "";

            if (currentDir.Equals(destFolder, StringComparison.OrdinalIgnoreCase))
                return ScanStatus.AlreadyCorrect;

            // Check for duplicate name at destination
            var assetName = Path.GetFileNameWithoutExtension(normalized);
            var ext       = Path.GetExtension(normalized);
            var destFull  = $"{destFolder}/{assetName}{ext}";

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destFull)))
                return ScanStatus.DuplicateName;

            return ScanStatus.NeedsMoving;
        }
    }
}
#endif
