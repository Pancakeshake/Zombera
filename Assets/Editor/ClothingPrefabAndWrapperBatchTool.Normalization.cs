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
        private static SourceNormalizationResult NormalizeModelSourceAssets()
        {
            var result = new SourceNormalizationResult();

            EnsureModelImportScaleFactor1(result);

            var fbxGuids = AssetDatabase.FindAssets("t:GameObject", new[] { ModelRoot });
            if (fbxGuids == null || fbxGuids.Length == 0)
                return result;

            try
            {
                for (var i = 0; i < fbxGuids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                    if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.ScannedFbx++;

                    EditorUtility.DisplayProgressBar(
                        "Normalize Clothing Source Assets",
                        "Processing " + assetPath + " (" + result.ScannedFbx + ")",
                        (i + 1f) / fbxGuids.Length);

                    if (!TryNormalizeSingleModelAsset(assetPath, result, out var failure)
                        && !string.IsNullOrWhiteSpace(failure))
                        result.Failures.Add(assetPath + " -> " + failure);
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
                        "Normalize Clothing Import Settings",
                        "Checking " + modelPath + " (" + result.ScannedModelImporters + ")",
                        (i + 1f) / modelGuids.Length);

                    var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if (modelImporter == null)
                        continue;

                    if (!ApplyClothingModelImporterDefaults(modelImporter)) continue;

                    result.UpdatedModelImporters++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }


        private static bool ApplyClothingModelImporterDefaults(ModelImporter modelImporter)
        {
            var changed = ItemPipelineImporterUtility.ApplyModelImporterDefaults(modelImporter, true);
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

            if (!TryRenameItemRootFolder(fbxPath, strippedName, out var fbxPathAfterFolderRename,
                    out var folderRenamed, out var folderRenameFailure))
            {
                failureReason = folderRenameFailure;
                return false;
            }

            if (folderRenamed) result.RenamedItemFolders++;

            fbxPath = fbxPathAfterFolderRename;
            directory = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');

            NormalizeTexturesInFolder(directory, result);
            RenameMaterialAssetsForModel(fbxPath, strippedName, result);

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

            if (!TryGetRelativePathWithoutExtension(fbxPath, ModelRoot, out var relativePathWithoutExtension))
                return true;

            var relativeSegments = relativePathWithoutExtension.Split('/');
            if (relativeSegments.Length == 0) return true;

            var slotFolder = relativeSegments[0];
            if (string.IsNullOrWhiteSpace(slotFolder)) return true;

            var slotRootPath = NormalizePath(ModelRoot) + "/" + slotFolder;
            EnsureFolderHierarchy(slotRootPath);

            var destinationFolderPath = slotRootPath.TrimEnd('/') + "/" + effectiveName;

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


        private static void NormalizeTexturesInFolder(string itemFolderPath, SourceNormalizationResult result)
        {
            if (string.IsNullOrWhiteSpace(itemFolderPath) || !AssetDatabase.IsValidFolder(itemFolderPath))
                return;

            var materialFolderPath = itemFolderPath + "/Material";

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { itemFolderPath });
            for (var i = 0; i < textureGuids.Length; i++)
            {
                var texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                var targetPath = ResolveMaterialFolderAssetPath(texturePath, materialFolderPath);
                TryMoveAssetIfNeeded(texturePath, targetPath, out _);

                var nameNoExt = Path.GetFileNameWithoutExtension(targetPath);
                if (string.IsNullOrWhiteSpace(nameNoExt)) continue;

                if (TryNormalizeTextureAssetName(targetPath, nameNoExt)) result.RenamedTextures++;
            }
        }


        private static string ResolveMaterialFolderAssetPath(string assetPath, string materialFolderPath)
        {
            var textureDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var isAlreadyInMaterialFolder = textureDirectory != null
                                            && textureDirectory.StartsWith(materialFolderPath,
                                                StringComparison.OrdinalIgnoreCase);

            if (isAlreadyInMaterialFolder) return assetPath;

            EnsureFolderHierarchy(materialFolderPath);
            return materialFolderPath + "/" + Path.GetFileName(assetPath);
        }


        private static bool TryNormalizeTextureAssetName(string targetPath, string nameNoExt)
        {
            if (string.Equals(nameNoExt, "Image_3", StringComparison.OrdinalIgnoreCase))
                return TryRenameAssetIfNeeded(targetPath, "Emission_Map", out _);

            if (string.Equals(nameNoExt, "Image_0", StringComparison.OrdinalIgnoreCase))
                return TryRenameAssetIfNeeded(targetPath, "Base_Map", out _);

            return nameNoExt.IndexOf("roughness", StringComparison.OrdinalIgnoreCase) >= 0
                   && TryRenameAssetIfNeeded(targetPath, "Metallic_Map", out _);
        }


        private static void RenameMaterialAssetsForModel(string fbxPath, string strippedName, SourceNormalizationResult result)
        {
            if (string.IsNullOrWhiteSpace(fbxPath)) return;

            var targetMaterialName = ResolveTargetMaterialName(fbxPath, strippedName);
            var itemFolderPath = ResolveItemFolderForMaterialNormalization(fbxPath);

            if (!string.IsNullOrWhiteSpace(itemFolderPath) && AssetDatabase.IsValidFolder(itemFolderPath))
                NormalizeMaterialAssetsInItemFolder(itemFolderPath, targetMaterialName, result);

            RenameEmbeddedMaterialSubAssets(fbxPath, targetMaterialName, result);
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


        private static string ResolveItemFolderForMaterialNormalization(string fbxPath)
        {
            var itemFolderPath = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');
            if (itemFolderPath != null && itemFolderPath.EndsWith("/Model", StringComparison.OrdinalIgnoreCase))
                itemFolderPath = Path.GetDirectoryName(itemFolderPath)?.Replace('\\', '/');

            return itemFolderPath;
        }


        private static void NormalizeMaterialAssetsInItemFolder(
            string itemFolderPath,
            string targetMaterialName,
            SourceNormalizationResult result)
        {
            var materialFolderPath = itemFolderPath + "/Material";
            var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { itemFolderPath });
            var renameIndex = 0;

            for (var i = 0; i < materialGuids.Length; i++)
            {
                var materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                var targetPath = ResolveMaterialFolderAssetPath(materialPath, materialFolderPath);
                TryMoveAssetIfNeeded(materialPath, targetPath, out _);

                var materialAsset = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                if (materialAsset == null)
                    materialAsset = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                ApplyDefaultMaterialSettings(materialAsset);

                var expectedName = renameIndex == 0 ? targetMaterialName : targetMaterialName + "_" + renameIndex;
                if (TryRenameAssetIfNeeded(targetPath, expectedName, out _)) result.RenamedMaterials++;

                renameIndex++;
            }
        }


        private static void RenameEmbeddedMaterialSubAssets(
            string fbxPath,
            string targetMaterialName,
            SourceNormalizationResult result)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is not Material material) continue;

                ApplyDefaultMaterialSettings(material);
                if (material.name == targetMaterialName) continue;

                material.name = targetMaterialName;
                EditorUtility.SetDirty(material);
                result.RenamedMaterials++;
            }
        }


        private static void ApplyDefaultMaterialSettings(Material material)
        {
            if (material == null) return;

            var changed = false;

            changed |= TrySetMaterialFloatIfPresent(material, "_Metallic", 0f);
            changed |= TrySetMaterialFloatIfPresent(material, "_MetallicScale", 0f);
            changed |= TrySetMaterialFloatIfPresent(material, "_Smoothness", 0.1f);
            changed |= TrySetMaterialFloatIfPresent(material, "_Glossiness", 0.1f);
            changed |= TrySetMaterialFloatIfPresent(material, "_GlossMapScale", 0.1f);
            changed |= TrySetMaterialFloatIfPresent(material, "_SpecularHighlights", 0f);
            changed |= TrySetMaterialFloatIfPresent(material, "_SpecularHighlightsToggle", 0f);

            if (!material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"))
            {
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                changed = true;
            }

            if (material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_ON"))
            {
                material.DisableKeyword("_SPECULARHIGHLIGHTS_ON");
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(material);
        }


        private static bool TrySetMaterialFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
                return false;

            if (Mathf.Approximately(material.GetFloat(propertyName), value))
                return false;

            material.SetFloat(propertyName, value);
            return true;
        }
    }
}
#endif
