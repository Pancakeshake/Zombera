#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Editor
{
    public static class WeaponEquipWrapperBatchTool
    {
        private const string LegacySystemsWrapperRoot = "Assets/Systems/Weapons/_Wrappers";
        private const string LegacyWrapperRoot = "Assets/Prefabs/Weapons/1.EquipWrappers";
        private const string PrefabRoot = "Assets/Systems/Weapons";
        private const string ItemRoot = "Assets/Systems/Weapons";

        [MenuItem("Tools/Items/Weapons/Create/Rename Equip Wrappers from Items", priority = -500)]
        public static void CreateWrappersFromItems()
        {
            RenameLegacyWrapperRootIfPossible();

            string[] itemGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot });
            int created = 0;
            int updated = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var guid in itemGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

                if (item == null || item.itemType != ItemType.Weapon) continue;

                // We only care about items that HAVE a visual prefab (even if broken) or should have one
                GameObject sourceVisual = item.equippedVisualPrefab;
                if (sourceVisual == null)
                {
                    skipped++;
                    continue;
                }

                var sourcePath = AssetDatabase.GetAssetPath(sourceVisual)?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    skipped++;
                    continue;
                }

                var normalizedSourcePath = NormalizePath(sourcePath);
                if (normalizedSourcePath.StartsWith(NormalizePath(LegacySystemsWrapperRoot) + "/", StringComparison.OrdinalIgnoreCase) ||
                    normalizedSourcePath.StartsWith(NormalizePath(LegacyWrapperRoot) + "/", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(sourcePath).StartsWith("Wrapper_", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                string relativePathWithoutExtension;
                if (!TryGetRelativePathWithoutExtension(sourcePath, PrefabRoot, out relativePathWithoutExtension))
                    relativePathWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);

                if (string.IsNullOrWhiteSpace(relativePathWithoutExtension))
                {
                    skipped++;
                    continue;
                }

                var outputRelativePath = BuildOutputRelativePath(relativePathWithoutExtension);
                
                var itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
                var itemName = Path.GetFileName(outputRelativePath);
                var wrapperSubfolder = itemFolder + "/Wrappers";
                var wrapperPath = BuildPrefabPath(PrefabRoot, wrapperSubfolder + "/" + "Wrapper_" + itemName);

                var legacyWrapperPath = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(relativePathWithoutExtension));
                var legacySystemsWrapperPath = BuildPrefabPath(LegacySystemsWrapperRoot, PrependWrapperPrefix(outputRelativePath));

                if (!TryMoveAssetIfNeeded(legacyWrapperPath, wrapperPath, out _) ||
                    !TryMoveAssetIfNeeded(legacySystemsWrapperPath, wrapperPath, out _))
                {
                    failed++;
                    continue;
                }

                EnsureFolderHierarchy(Path.GetDirectoryName(wrapperPath)?.Replace('\\', '/'));
                var alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath) != null;

                GameObject wrapperPrefab = CreateOrUpdateWrapper(wrapperPath, sourceVisual);
                if (wrapperPrefab != null)
                {
                    Undo.RecordObject(item, "Assign Weapon Wrapper");
                    item.equippedVisualPrefab = wrapperPrefab;
                    item.equippedVisualLocalPosition = Vector3.zero;
                    item.equippedVisualLocalEulerAngles = Vector3.zero;
                    item.equippedVisualLocalScale = Vector3.one;
                    EditorUtility.SetDirty(item);

                    if (alreadyExists) updated++;
                    else created++;
                }
                else
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WeaponEquipWrapperBatchTool] Finished. Created: {created}, Updated: {updated}, Skipped: {skipped}, Failed: {failed}.");
        }

        public static GameObject CreateOrUpdateWrapper(string path, GameObject meshSource)
        {
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));
            GameObject meshChild = UnityEngine.Object.Instantiate(meshSource);
            meshChild.name = meshSource.name;
            meshChild.transform.SetParent(root.transform, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static bool TryGetRelativePathWithoutExtension(string assetPath, string rootFolder,
            out string relativePathWithoutExtension)
        {
            relativePathWithoutExtension = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(rootFolder)) return false;

            var normalizedAssetPath = assetPath.Replace('\\', '/');
            var normalizedRoot = rootFolder.Replace('\\', '/').TrimEnd('/');

            if (!normalizedAssetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return false;

            var relativePath = normalizedAssetPath[normalizedRoot.Length..].TrimStart('/');
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            relativePathWithoutExtension = Path.ChangeExtension(relativePath, null)?.Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(relativePathWithoutExtension);
        }

        private static string BuildOutputRelativePath(string relativePathWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(relativePathWithoutExtension)) return string.Empty;

            return relativePathWithoutExtension.Replace('\\', '/').Trim('/');
        }

        private static string PrependWrapperPrefix(string relativePathWithoutExtension)
        {
            var directory = Path.GetDirectoryName(relativePathWithoutExtension)?.Replace('\\', '/');
            var fileName = Path.GetFileName(relativePathWithoutExtension);

            if (string.IsNullOrWhiteSpace(directory))
                return "Wrapper_" + fileName;

            return directory.TrimEnd('/') + "/" + "Wrapper_" + fileName;
        }

        private static string BuildPrefabPath(string rootFolder, string relativePathWithoutExtension)
        {
            return rootFolder.TrimEnd('/') + "/" + relativePathWithoutExtension.TrimStart('/') + ".prefab";
        }

        private static bool TryMoveAssetIfNeeded(string sourcePath, string destinationPath, out string failureReason)
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

            // If destination exists, we overwrite it with the source.
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                AssetDatabase.DeleteAsset(destinationPath);
            }

            EnsureFolderHierarchy(Path.GetDirectoryName(destinationPath)?.Replace('\\', '/'));
            var moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "MoveAsset failed: " + moveError;
                return false;
            }

            return true;
        }

        private static void RenameLegacyWrapperRootIfPossible()
        {
            if (!AssetDatabase.IsValidFolder(LegacyWrapperRoot)) return;
            if (AssetDatabase.IsValidFolder(LegacySystemsWrapperRoot)) return;

            var parentFolder = Path.GetDirectoryName(LegacySystemsWrapperRoot)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder)) return;

            var moveError = AssetDatabase.MoveAsset(LegacyWrapperRoot, LegacySystemsWrapperRoot);
            if (!string.IsNullOrWhiteSpace(moveError))
                Debug.LogWarning("[WeaponEquipWrapperBatchTool] Could not migrate legacy wrapper root: " + moveError);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
