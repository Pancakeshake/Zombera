#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    internal static class ItemPipelineAssetUtility
    {
        public static bool CreateOrUpdatePrefabFromAsset(GameObject sourceAsset, string outputPath, out string failureReason)
        {
            failureReason = string.Empty;
            if (sourceAsset == null)
            {
                failureReason = "Source model is null.";
                return false;
            }

            var instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(sourceAsset);

            if (instance == null)
            {
                failureReason = "Could not instantiate source model.";
                return false;
            }

            instance.name = Path.GetFileNameWithoutExtension(outputPath);

            try
            {
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, outputPath, out var success);
                if (!success || saved == null)
                {
                    failureReason = "SaveAsPrefabAsset failed.";
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static bool CreateOrUpdateWrapperPrefab(
            GameObject sourcePrefab,
            string outputPath,
            Func<string, string> sanitizeName,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (sourcePrefab == null)
            {
                failureReason = "Source prefab is null.";
                return false;
            }

            var wrapperRoot = new GameObject(Path.GetFileNameWithoutExtension(outputPath));
            try
            {
                var sourceInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
                if (sourceInstance == null)
                    sourceInstance = Object.Instantiate(sourcePrefab);

                if (sourceInstance == null)
                {
                    failureReason = "Could not instantiate source prefab.";
                    return false;
                }

                var sanitizedChildName = sanitizeName != null ? sanitizeName(sourcePrefab.name) : sourcePrefab.name;
                sourceInstance.name = string.IsNullOrWhiteSpace(sanitizedChildName)
                    ? sourcePrefab.name
                    : sanitizedChildName;
                sourceInstance.transform.SetParent(wrapperRoot.transform, false);

                var saved = PrefabUtility.SaveAsPrefabAsset(wrapperRoot, outputPath, out var success);
                if (!success || saved == null)
                {
                    failureReason = "SaveAsPrefabAsset failed.";
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(wrapperRoot);
            }
        }

        public static bool TryGetRelativePathWithoutExtension(string fullPath, string rootFolder, out string relativePath)
        {
            relativePath = string.Empty;

            var normalizedPath = NormalizePath(fullPath);
            var normalizedRoot = NormalizePath(rootFolder);

            if (!normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            var relativeWithExtension = normalizedPath[(normalizedRoot.Length + 1)..];
            if (string.IsNullOrWhiteSpace(relativeWithExtension))
                return false;

            relativePath = Path.ChangeExtension(relativeWithExtension, null)?.Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        public static string BuildPrefabPath(string rootFolder, string relativePathWithoutExtension)
        {
            return rootFolder.TrimEnd('/') + "/" + relativePathWithoutExtension.TrimStart('/') + ".prefab";
        }

        public static string BuildAssetPath(string rootFolder, string relativePathWithoutExtension)
        {
            return rootFolder.TrimEnd('/') + "/" + relativePathWithoutExtension.TrimStart('/') + ".asset";
        }

        public static bool TryMoveAssetIfNeeded(string sourcePath, string destinationPath, out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                failureReason = "Source or destination path was empty.";
                return false;
            }

            if (NormalizePath(sourcePath).Equals(NormalizePath(destinationPath), StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                AssetDatabase.DeleteAsset(destinationPath);

            EnsureFolderHierarchy(Path.GetDirectoryName(destinationPath)?.Replace('\\', '/'));
            var moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (string.IsNullOrWhiteSpace(moveError)) return true;

            failureReason = "MoveAsset failed: " + moveError;
            return false;
        }

        public static bool TryRenameAssetIfNeeded(string assetPath, string newNameWithoutExtension, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(newNameWithoutExtension))
                return false;

            var currentName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.Equals(currentName, newNameWithoutExtension, StringComparison.Ordinal))
                return false;

            var extension = Path.GetExtension(assetPath);
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var destinationPath = (directory?.TrimEnd('/') ?? string.Empty) + "/" + newNameWithoutExtension + extension;

            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                return false;

            var renameError = AssetDatabase.RenameAsset(assetPath, newNameWithoutExtension);
            if (string.IsNullOrWhiteSpace(renameError)) return true;

            failureReason = renameError;
            return false;
        }

        public static bool IsSupportedModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension is ".fbx" or ".obj" or ".dae" or ".blend";
        }

        public static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        public static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        public static IReadOnlyDictionary<string, Sprite> BuildIconLookup(
            string generatedIconsRoot,
            Func<string, string> normalizeNameKey)
        {
            var map = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(generatedIconsRoot)) return map;

            var iconGuids = AssetDatabase.FindAssets("t:Sprite", new[] { generatedIconsRoot });
            for (var i = 0; i < iconGuids.Length; i++)
            {
                var iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null) continue;

                var filenameKey = normalizeNameKey(Path.GetFileNameWithoutExtension(iconPath));
                if (!string.IsNullOrWhiteSpace(filenameKey)) map[filenameKey] = sprite;

                var spriteNameKey = normalizeNameKey(sprite.name);
                if (!string.IsNullOrWhiteSpace(spriteNameKey)) map[spriteNameKey] = sprite;
            }

            return map;
        }
    }

    internal static class ItemPipelineImporterUtility
    {
        public static bool ApplyModelImporterDefaults(ModelImporter modelImporter, bool disableAnimationAndAvatar)
        {
            var changed = false;

            if (Mathf.Abs(modelImporter.globalScale - 1f) >= 0.0001f)
            {
                modelImporter.globalScale = 1f;
                changed = true;
            }

            if (!disableAnimationAndAvatar) return changed;

            if (modelImporter.importAnimation)
            {
                modelImporter.importAnimation = false;
                changed = true;
            }

            if (modelImporter.animationType != ModelImporterAnimationType.None)
            {
                modelImporter.animationType = ModelImporterAnimationType.None;
                changed = true;
            }

            if (modelImporter.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
            {
                modelImporter.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                changed = true;
            }

            return changed;
        }
    }
}
#endif
