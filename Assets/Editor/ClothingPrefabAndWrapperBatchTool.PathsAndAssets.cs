#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class ClothingPrefabAndWrapperBatchTool
    {

        private static bool CreateOrUpdatePrefabFromAsset(GameObject sourceAsset, string outputPath, out string failureReason)
        {
            return ItemPipelineAssetUtility.CreateOrUpdatePrefabFromAsset(sourceAsset, outputPath, out failureReason);
        }


        private static bool CreateOrUpdateWrapperPrefab(GameObject sourcePrefab, string outputPath, out string failureReason)
        {
            return ItemPipelineAssetUtility.CreateOrUpdateWrapperPrefab(
                sourcePrefab,
                outputPath,
                StripMeshyPrefix,
                out failureReason);
        }


        private static bool TryGetRelativePathWithoutExtension(string fullPath, string rootFolder, out string relativePath)
        {
            return ItemPipelineAssetUtility.TryGetRelativePathWithoutExtension(fullPath, rootFolder, out relativePath);
        }


        private static string BuildPrefabPath(string rootFolder, string relativePathWithoutExtension)
        {
            return ItemPipelineAssetUtility.BuildPrefabPath(rootFolder, relativePathWithoutExtension);
        }


        private static string BuildAssetPath(string rootFolder, string relativePathWithoutExtension)
        {
            return ItemPipelineAssetUtility.BuildAssetPath(rootFolder, relativePathWithoutExtension);
        }


        private static bool TryMoveAssetIfNeeded(string sourcePath, string destinationPath, out string failureReason)
        {
            return ItemPipelineAssetUtility.TryMoveAssetIfNeeded(sourcePath, destinationPath, out failureReason);
        }


        private static bool TryRenameAssetIfNeeded(string assetPath, string newNameWithoutExtension, out string failureReason)
        {
            return ItemPipelineAssetUtility.TryRenameAssetIfNeeded(assetPath, newNameWithoutExtension, out failureReason);
        }


        private static T LoadOrCreateAsset<T>(string assetPath, out bool created, Func<T> create)
            where T : ScriptableObject
        {
            created = false;

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            EnsureFolderHierarchy(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));

            var instance = create();
            if (instance == null) return null;

            AssetDatabase.CreateAsset(instance, assetPath);
            created = true;
            return instance;
        }


        private static string PrependWrapperPrefix(string relativePathWithoutExtension)
        {
            var directory = Path.GetDirectoryName(relativePathWithoutExtension)?.Replace('\\', '/');
            var filename = Path.GetFileName(relativePathWithoutExtension);

            if (string.IsNullOrWhiteSpace(directory))
                return "Wrapper_" + filename;

            return directory.TrimEnd('/') + "/" + "Wrapper_" + filename;
        }


        private static string SanitizeRelativePath(string relativePathWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(relativePathWithoutExtension)) return string.Empty;

            var normalized = NormalizePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

            var segments = normalized.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var sanitized = StripMeshyPrefix(segments[i]);
                if (!string.IsNullOrWhiteSpace(sanitized)) segments[i] = sanitized;
            }

            return string.Join("/", segments).Trim('/');
        }


        private static string StripMeshyPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var trimmed = value.Trim();

            const string aiPrefix = "Meshy_AI_";
            while (trimmed.StartsWith(aiPrefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[aiPrefix.Length..];

            const string prefix = "Meshy_";
            while (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[prefix.Length..];

            trimmed = Regex.Replace(trimmed, @"__?\d+_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, @"_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = trimmed.Trim('_', '-', ' ');

            return string.IsNullOrWhiteSpace(trimmed) ? value.Trim() : trimmed;
        }


        private static bool IsSupportedModelPath(string path)
        {
            return ItemPipelineAssetUtility.IsSupportedModelPath(path);
        }


        private static string NormalizePath(string path)
        {
            return ItemPipelineAssetUtility.NormalizePath(path);
        }


        private static void EnsureFolderHierarchy(string folderPath)
        {
            ItemPipelineAssetUtility.EnsureFolderHierarchy(folderPath);
        }
    }
}
#endif
