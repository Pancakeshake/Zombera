#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class WeaponCleanPrefabAndWrapperBatchTool
    {
        private static SourceNormalizationResult NormalizeModelSourceAssets()
        {
            var result = new SourceNormalizationResult();

            EnsureModelImportScaleFactor1(result);

            var modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { ModelRoot });
            if (modelGuids == null || modelGuids.Length == 0)
                return result;

            try
            {
                for (var i = 0; i < modelGuids.Length; i++)
                {
                    var modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                    if (!modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.ScannedFbx++;

                    EditorUtility.DisplayProgressBar(
                        "Normalize Weapon Source Assets",
                        "Processing " + modelPath + " (" + result.ScannedFbx + ")",
                        (i + 1f) / modelGuids.Length);

                    if (!TryNormalizeSingleModelAsset(modelPath, result, out var failure) &&
                        !string.IsNullOrWhiteSpace(failure))
                        result.Failures.Add(modelPath + " -> " + failure);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static void EnsureModelImportScaleFactor1(SourceNormalizationResult result)
        {
            var modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { ModelRoot });
            if (modelGuids == null || modelGuids.Length == 0)
                return;

            try
            {
                for (var i = 0; i < modelGuids.Length; i++)
                {
                    var modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                    if (!IsSupportedModelPath(modelPath))
                        continue;

                    result.ScannedModelImporters++;

                    EditorUtility.DisplayProgressBar(
                        "Normalize Weapon Import Scale",
                        "Checking " + modelPath + " (" + result.ScannedModelImporters + ")",
                        (i + 1f) / modelGuids.Length);

                    var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if (modelImporter == null)
                        continue;

                    if (!ApplyWeaponModelImporterDefaults(modelImporter)) continue;

                    result.UpdatedModelImporters++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool ApplyWeaponModelImporterDefaults(ModelImporter modelImporter)
        {
            var changed = ItemPipelineImporterUtility.ApplyModelImporterDefaults(modelImporter, false);
            if (!changed) return false;

            modelImporter.SaveAndReimport();
            return true;
        }

        private static bool TryNormalizeSingleModelAsset(
            string originalFbxPath,
            SourceNormalizationResult result,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(originalFbxPath)) return true;

            var directory = Path.GetDirectoryName(originalFbxPath)?.Replace('\\', '/');
            var sourceFileName = Path.GetFileNameWithoutExtension(originalFbxPath);
            var strippedName = StripMeshyPrefix(sourceFileName);
            var fbxPath = originalFbxPath;

            if (!string.IsNullOrWhiteSpace(strippedName) &&
                !string.Equals(strippedName, sourceFileName, StringComparison.Ordinal))
            {
                var renameError = AssetDatabase.RenameAsset(originalFbxPath, strippedName);
                if (!string.IsNullOrWhiteSpace(renameError))
                {
                    failureReason = "FBX rename failed: " + renameError;
                    return false;
                }

                result.RenamedFbx++;
                fbxPath = (directory?.TrimEnd('/') ?? string.Empty) + "/" + strippedName + ".fbx";
            }

            if (!TryRenameItemRootFolder(
                    fbxPath,
                    strippedName,
                    out var renamedFbxPath,
                    out var folderRenamed,
                    out var folderRenameFailure))
            {
                failureReason = folderRenameFailure;
                return false;
            }

            if (folderRenamed) result.RenamedItemFolders++;

            fbxPath = renamedFbxPath;
            directory = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');

            var targetMaterialName = ResolveTargetMaterialName(fbxPath, strippedName);
            NormalizeMaterialAssetsInItemFolder(directory, fbxPath, targetMaterialName, result);
            return true;
        }

        private static bool TryRenameItemRootFolder(
            string fbxPath,
            string cleanedName,
            out string updatedFbxPath,
            out bool folderRenamed,
            out string failureReason)
        {
            updatedFbxPath = fbxPath;
            folderRenamed = false;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(fbxPath)) return true;

            var effectiveName = string.IsNullOrWhiteSpace(cleanedName)
                ? Path.GetFileNameWithoutExtension(fbxPath)
                : cleanedName;

            if (string.IsNullOrWhiteSpace(effectiveName)) return true;

            var folderPath = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return true;

            if (NormalizePath(folderPath).Equals(NormalizePath(ModelRoot), StringComparison.OrdinalIgnoreCase))
                return true;

            var parentFolder = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parentFolder)) return true;
            if (!NormalizePath(parentFolder).StartsWith(NormalizePath(ModelRoot), StringComparison.OrdinalIgnoreCase))
                return true;

            EnsureFolderHierarchy(parentFolder);

            var destinationFolderPath = parentFolder.TrimEnd('/') + "/" + effectiveName;
            if (NormalizePath(folderPath).Equals(NormalizePath(destinationFolderPath), StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.IsValidFolder(destinationFolderPath))
            {
                failureReason = "Target item folder already exists: " + destinationFolderPath;
                return false;
            }

            var moveError = AssetDatabase.MoveAsset(folderPath, destinationFolderPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "Item folder move/rename failed: " + moveError;
                return false;
            }

            folderRenamed = true;
            updatedFbxPath = destinationFolderPath + "/" + Path.GetFileName(fbxPath);
            return true;
        }

        private static string ResolveMaterialFolderPath(string itemFolderPath)
        {
            return itemFolderPath.TrimEnd('/') + "/Material";
        }

        private static bool EnsureAssetInMaterialFolder(string assetPath, string materialFolderPath, out string targetPath)
        {
            targetPath = assetPath;
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(materialFolderPath))
                return false;

            var assetDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var isAlreadyInMaterialFolder = assetDirectory != null
                                            && assetDirectory.StartsWith(
                                                materialFolderPath,
                                                StringComparison.OrdinalIgnoreCase);

            if (isAlreadyInMaterialFolder) return true;

            EnsureFolderHierarchy(materialFolderPath);
            targetPath = materialFolderPath + "/" + Path.GetFileName(assetPath);
            return TryMoveAssetIfNeeded(assetPath, targetPath, out _);
        }

        private static bool TryNormalizeTextureName(string targetPath, string nameNoExt)
        {
            if (string.Equals(nameNoExt, "Image_3", StringComparison.OrdinalIgnoreCase))
                return TryRenameAssetIfNeeded(targetPath, "Emission_Map", out _);

            if (string.Equals(nameNoExt, "Image_0", StringComparison.OrdinalIgnoreCase))
                return TryRenameAssetIfNeeded(targetPath, "Base_Map", out _);

            return nameNoExt.IndexOf("roughness", StringComparison.OrdinalIgnoreCase) >= 0
                   && TryRenameAssetIfNeeded(targetPath, "Metallic_Map", out _);
        }

        private static string ResolveTargetMaterialName(string fbxPath, string strippedName)
        {
            var baseName = string.IsNullOrWhiteSpace(strippedName)
                ? StripMeshyPrefix(Path.GetFileNameWithoutExtension(fbxPath))
                : strippedName;

            if (string.IsNullOrWhiteSpace(baseName))
                baseName = Path.GetFileNameWithoutExtension(fbxPath);

            return baseName + "_material";
        }

        private static void NormalizeMaterialAssetsInItemFolder(
            string itemFolderPath,
            string fbxPath,
            string targetMaterialName,
            SourceNormalizationResult result)
        {
            if (string.IsNullOrWhiteSpace(itemFolderPath) || !AssetDatabase.IsValidFolder(itemFolderPath))
            {
                RenameEmbeddedMaterialSubAssets(fbxPath, targetMaterialName, result);
                return;
            }

            var materialFolderPath = ResolveMaterialFolderPath(itemFolderPath);

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { itemFolderPath });
            for (var i = 0; i < textureGuids.Length; i++)
            {
                var texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                EnsureAssetInMaterialFolder(texturePath, materialFolderPath, out var targetPath);

                var nameNoExt = Path.GetFileNameWithoutExtension(targetPath);
                if (string.IsNullOrWhiteSpace(nameNoExt)) continue;

                if (TryNormalizeTextureName(targetPath, nameNoExt)) result.RenamedTextures++;
            }

            var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { itemFolderPath });
            var renameIndex = 0;
            for (var i = 0; i < materialGuids.Length; i++)
            {
                var materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                EnsureAssetInMaterialFolder(materialPath, materialFolderPath, out var targetPath);

                var expectedName = renameIndex == 0 ? targetMaterialName : targetMaterialName + "_" + renameIndex;
                if (TryRenameAssetIfNeeded(targetPath, expectedName, out _))
                    result.RenamedMaterials++;

                renameIndex++;
            }

            RenameEmbeddedMaterialSubAssets(fbxPath, targetMaterialName, result);
        }

        private static void RenameEmbeddedMaterialSubAssets(
            string fbxPath,
            string targetMaterialName,
            SourceNormalizationResult result)
        {
            if (string.IsNullOrWhiteSpace(fbxPath) || string.IsNullOrWhiteSpace(targetMaterialName))
                return;

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is not Material material) continue;
                if (string.Equals(material.name, targetMaterialName, StringComparison.Ordinal)) continue;

                material.name = targetMaterialName;
                EditorUtility.SetDirty(material);
                result.RenamedMaterials++;
            }
        }
    }
}
#endif
