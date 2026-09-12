#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class WeaponCleanPrefabAndWrapperBatchTool
    {
        private static BatchResult CreatePrefabsFromModelsInternal()
        {
            var context = new PrefabPassContext(AssetDatabase.FindAssets("t:GameObject", new[] { ModelRoot }));
            if (context.ModelGuids.Length == 0)
                return context.Result;

            try
            {
                for (var i = 0; i < context.ModelGuids.Length; i++) ProcessPrefabModelGuid(context, i);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Result;
        }

        private static void ProcessPrefabModelGuid(PrefabPassContext context, int index)
        {
            var modelPath = AssetDatabase.GUIDToAssetPath(context.ModelGuids[index]);
            context.Result.Scanned++;

            EditorUtility.DisplayProgressBar(
                "Weapon Prefab Build",
                "Processing model " + modelPath + " (" + (index + 1) + "/" + context.ModelGuids.Length + ")",
                (index + 1f) / context.ModelGuids.Length);

            if (!TryResolveCanonicalItemFromModelPath(
                    modelPath,
                    context.ProcessedRelativePaths,
                    context.Result,
                    out var relativePathWithoutExtension,
                    out var outputRelativePath,
                    out var itemFolder,
                    out var itemName))
                return;

            var canonicalModelFolder = ModelRoot + "/" + itemFolder;
            var canonicalModelPath = canonicalModelFolder.TrimEnd('/') + "/" + itemName + Path.GetExtension(modelPath);

            if (!TryMoveAssetIfNeeded(modelPath, canonicalModelPath, out var moveError))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(modelPath + " -> " + moveError);
                return;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath);
            if (modelAsset == null)
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (modelAsset == null)
            {
                context.Result.Skipped++;
                return;
            }

            var legacyPrefabPath = BuildPrefabPath(PrefabRoot, SanitizeRelativePath(relativePathWithoutExtension));
            var prefabPath = BuildPrefabPath(PrefabRoot, outputRelativePath);
            if (!TryMoveAssetIfNeeded(legacyPrefabPath, prefabPath, out var legacyMoveError))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(modelPath + " -> " + legacyMoveError);
                return;
            }

            EnsureFolderHierarchy(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

            var alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            if (!CreateOrUpdatePrefabFromAsset(modelAsset, prefabPath, out var failureReason))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(modelPath + " -> " + failureReason);
                return;
            }

            if (alreadyExists)
                context.Result.Updated++;
            else
                context.Result.Created++;
        }

        private static bool TryResolveCanonicalItemFromModelPath(
            string modelPath,
            HashSet<string> processedRelativePaths,
            BatchResult result,
            out string relativePathWithoutExtension,
            out string outputRelativePath,
            out string itemFolder,
            out string itemName)
        {
            relativePathWithoutExtension = string.Empty;
            outputRelativePath = string.Empty;
            itemFolder = string.Empty;
            itemName = string.Empty;

            if (!IsSupportedModelPath(modelPath))
            {
                result.Skipped++;
                return false;
            }

            if (!TryGetRelativePathWithoutExtension(modelPath, ModelRoot, out relativePathWithoutExtension))
            {
                result.Skipped++;
                return false;
            }

            outputRelativePath = BuildOutputRelativePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(outputRelativePath))
            {
                result.Skipped++;
                return false;
            }

            itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
            itemName = Path.GetFileName(outputRelativePath);
            if (string.IsNullOrWhiteSpace(itemName))
            {
                result.Skipped++;
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemFolder))
                itemFolder = itemName;

            if (processedRelativePaths.Add(outputRelativePath)) return true;

            result.Skipped++;
            return false;
        }

        private static BatchResult CreateWrappersFromPrefabsInternal()
        {
            var context = new WrapperPassContext(AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }));
            if (context.PrefabGuids.Length == 0)
                return context.Result;

            try
            {
                for (var i = 0; i < context.PrefabGuids.Length; i++) ProcessWrapperPrefabGuid(context, i);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Result;
        }

        private static void ProcessWrapperPrefabGuid(WrapperPassContext context, int index)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(context.PrefabGuids[index]);
            context.Result.Scanned++;

            EditorUtility.DisplayProgressBar(
                "Weapon Wrapper Build",
                "Processing prefab " + prefabPath + " (" + (index + 1) + "/" + context.PrefabGuids.Length + ")",
                (index + 1f) / context.PrefabGuids.Length);

            if (ShouldSkipWrapperSourcePrefab(prefabPath, context)) return;

            if (!TryResolveCanonicalItemFromPrefabPath(
                    prefabPath,
                    context.ProcessedRelativePaths,
                    context.Result,
                    out var relativePathWithoutExtension,
                    out var outputRelativePath,
                    out var itemFolder,
                    out var itemName))
                return;

            var canonicalPrefabPath = BuildPrefabPath(PrefabRoot, outputRelativePath);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalPrefabPath);
            if (sourcePrefab == null)
                sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (sourcePrefab == null)
            {
                context.Result.Skipped++;
                return;
            }

            var wrapperSubfolder = itemFolder + "/Wrappers";
            var wrapperPath = BuildPrefabPath(PrefabRoot, wrapperSubfolder + "/" + "Wrapper_" + itemName);

            if (!TryMoveLegacyWrapperPaths(relativePathWithoutExtension, outputRelativePath, wrapperPath, out var moveError))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> " + moveError);
                return;
            }

            EnsureFolderHierarchy(Path.GetDirectoryName(wrapperPath)?.Replace('\\', '/'));

            var alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath) != null;
            if (!CreateOrUpdateWrapperPrefab(sourcePrefab, wrapperPath, out var failureReason))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> " + failureReason);
                return;
            }

            if (alreadyExists)
                context.Result.Updated++;
            else
                context.Result.Created++;
        }

        private static bool TryResolveCanonicalItemFromPrefabPath(
            string prefabPath,
            HashSet<string> processedRelativePaths,
            BatchResult result,
            out string relativePathWithoutExtension,
            out string outputRelativePath,
            out string itemFolder,
            out string itemName)
        {
            relativePathWithoutExtension = string.Empty;
            outputRelativePath = string.Empty;
            itemFolder = string.Empty;
            itemName = string.Empty;

            if (!TryGetRelativePathWithoutExtension(prefabPath, PrefabRoot, out relativePathWithoutExtension))
            {
                result.Skipped++;
                return false;
            }

            outputRelativePath = BuildOutputRelativePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(outputRelativePath))
            {
                result.Skipped++;
                return false;
            }

            itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
            itemName = Path.GetFileName(outputRelativePath);
            if (string.IsNullOrWhiteSpace(itemName))
            {
                result.Skipped++;
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemFolder))
                itemFolder = itemName;

            if (processedRelativePaths.Add(outputRelativePath)) return true;

            result.Skipped++;
            return false;
        }

        private static bool ShouldSkipWrapperSourcePrefab(string prefabPath, WrapperPassContext context)
        {
            if (string.IsNullOrWhiteSpace(prefabPath)
                || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                context.Result.Skipped++;
                return true;
            }

            var filename = Path.GetFileName(prefabPath);
            if (filename.StartsWith("Wrapper_", StringComparison.OrdinalIgnoreCase))
            {
                context.Result.Skipped++;
                return true;
            }

            var normalizedPath = NormalizePath(prefabPath);
            var isLegacyPath =
                normalizedPath.StartsWith(context.NormalizedLegacySystemsWrapperRoot + "/",
                    StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.NormalizedLegacySystemsWrapperRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(context.NormalizedLegacyWrapperRoot + "/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.NormalizedLegacyWrapperRoot, StringComparison.OrdinalIgnoreCase);

            if (!isLegacyPath) return false;

            context.Result.Skipped++;
            return true;
        }

        private static bool TryMoveLegacyWrapperPaths(
            string relativePathWithoutExtension,
            string outputRelativePath,
            string wrapperPath,
            out string moveError)
        {
            var legacyWrapperPath = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(relativePathWithoutExtension));
            var legacyWrapperPathForCanonical = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(outputRelativePath));
            var legacySystemsWrapperPath = BuildPrefabPath(LegacySystemsWrapperRoot, PrependWrapperPrefix(outputRelativePath));

            return TryMoveAssetIfNeeded(legacyWrapperPath, wrapperPath, out moveError)
                   && TryMoveAssetIfNeeded(legacyWrapperPathForCanonical, wrapperPath, out moveError)
                   && TryMoveAssetIfNeeded(legacySystemsWrapperPath, wrapperPath, out moveError);
        }
    }
}
#endif
