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

        private static void RenameLegacyWrapperRootIfPossible()
        {
            if (!AssetDatabase.IsValidFolder(LegacyWrapperRoot)) return;
            if (AssetDatabase.IsValidFolder(LegacySystemsWrapperRoot)) return;

            var targetParent = Path.GetDirectoryName(LegacySystemsWrapperRoot)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(targetParent) || !AssetDatabase.IsValidFolder(targetParent)) return;

            var moveError = AssetDatabase.MoveAsset(LegacyWrapperRoot, LegacySystemsWrapperRoot);
            if (!string.IsNullOrWhiteSpace(moveError))
                Debug.LogWarning("[ClothingPrefabAndWrapperBatchTool] Could not migrate legacy wrapper root: " +
                                 moveError);
        }


        private static bool TryResolveCanonicalItemPath(
            string relativePathWithoutExtension,
            out string outputRelativePath,
            out string itemFolder,
            out string itemName)
        {
            outputRelativePath = string.Empty;
            itemFolder = string.Empty;
            itemName = string.Empty;

            var sanitized = SanitizeRelativePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(sanitized))
                return false;

            var segments = sanitized.Split('/');
            if (segments.Length < 2)
                return false;

            var slotFolder = segments[0];
            var rawItemName = segments[1];
            if (string.IsNullOrWhiteSpace(slotFolder) || string.IsNullOrWhiteSpace(rawItemName))
                return false;

            if (slotFolder.StartsWith("_", StringComparison.Ordinal) || IsGeneratedSubfolderName(rawItemName))
                return false;

            itemName = rawItemName;
            itemFolder = slotFolder.TrimEnd('/') + "/" + itemName;
            outputRelativePath = itemFolder + "/" + itemName;
            return true;
        }


        private static bool IsGeneratedSubfolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return true;

            return folderName.Equals(DataFolderName, StringComparison.OrdinalIgnoreCase)
                   || folderName.Equals(MaterialFolderName, StringComparison.OrdinalIgnoreCase)
                   || folderName.Equals(ModelFolderName, StringComparison.OrdinalIgnoreCase)
                   || folderName.Equals(WrappersFolderName, StringComparison.OrdinalIgnoreCase)
                   || folderName.StartsWith("UMA_", StringComparison.OrdinalIgnoreCase);
        }


        private static bool TryEnsureCanonicalPrefabForOutputPath(
            string outputRelativePath,
            out GameObject sourcePrefab,
            out string failureReason)
        {
            sourcePrefab = null;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(outputRelativePath))
            {
                failureReason = "Output path was empty.";
                return false;
            }

            var canonicalPrefabPath = BuildPrefabPath(PrefabRoot, outputRelativePath);
            sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalPrefabPath);
            if (sourcePrefab != null)
                return true;

            var itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
            var itemName = Path.GetFileName(outputRelativePath);
            if (string.IsNullOrWhiteSpace(itemFolder) || string.IsNullOrWhiteSpace(itemName))
            {
                failureReason = "Could not resolve canonical item folder from output path: " + outputRelativePath;
                return false;
            }

            var itemFolderPath = ModelRoot + "/" + itemFolder;
            if (!TryEnsureCanonicalModelLocation(itemFolderPath, itemName, out var canonicalModelPath,
                    out var modelFailure))
            {
                failureReason = "Canonical prefab missing and model recovery failed: " + modelFailure;
                return false;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath);
            if (modelAsset == null)
            {
                failureReason = "Canonical model could not be loaded at: " + canonicalModelPath;
                return false;
            }

            EnsureFolderHierarchy(Path.GetDirectoryName(canonicalPrefabPath)?.Replace('\\', '/'));
            if (!CreateOrUpdatePrefabFromAsset(modelAsset, canonicalPrefabPath, out var prefabFailure))
            {
                failureReason = "Could not rebuild canonical prefab: " + prefabFailure;
                return false;
            }

            sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalPrefabPath);
            if (sourcePrefab == null)
            {
                failureReason = "Canonical prefab was rebuilt but could not be loaded: " + canonicalPrefabPath;
                return false;
            }

            return true;
        }


        private static bool TryEnsureCanonicalModelLocation(
            string itemFolderPath,
            string itemName,
            out string canonicalModelPath,
            out string failureReason)
        {
            canonicalModelPath = BuildCanonicalModelPath(itemFolderPath, itemName);
            failureReason = string.Empty;

            if (!AssetDatabase.IsValidFolder(itemFolderPath))
            {
                failureReason = "Item folder does not exist: " + itemFolderPath;
                return false;
            }

            var canonicalModel = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath);
            if (canonicalModel != null)
                return true;

            if (!TryFindBestModelPathForItem(itemFolderPath, itemName, canonicalModelPath, out var sourceModelPath))
            {
                failureReason = "No model source found for item: " + itemFolderPath;
                return false;
            }

            if (!TryMoveAssetIfNeeded(sourceModelPath, canonicalModelPath, out var moveFailure))
            {
                failureReason = "Could not move model to canonical path: " + moveFailure;
                return false;
            }

            canonicalModel = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath);
            if (canonicalModel == null)
            {
                failureReason = "Canonical model path is still empty after move: " + canonicalModelPath;
                return false;
            }

            return true;
        }


        private static bool TryFindBestModelPathForItem(
            string itemFolderPath,
            string itemName,
            string canonicalModelPath,
            out string sourceModelPath)
        {
            sourceModelPath = string.Empty;
            var bestScore = int.MinValue;

            var modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { itemFolderPath });
            if (modelGuids == null || modelGuids.Length == 0)
                return false;

            var canonicalModelPathNormalized = NormalizePath(canonicalModelPath);
            var modelFolderPathNormalized = NormalizePath(itemFolderPath + "/" + ModelFolderName);
            var itemFolderPathNormalized = NormalizePath(itemFolderPath);

            for (var i = 0; i < modelGuids.Length; i++)
            {
                var candidatePath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                if (!IsSupportedModelPath(candidatePath))
                    continue;

                var candidateNormalized = NormalizePath(candidatePath);
                var candidateDirectory = NormalizePath(Path.GetDirectoryName(candidatePath)?.Replace('\\', '/'));
                var candidateName = Path.GetFileNameWithoutExtension(candidatePath) ?? string.Empty;

                var score = 0;
                if (string.Equals(candidateNormalized, canonicalModelPathNormalized, StringComparison.OrdinalIgnoreCase))
                    score += 1000;
                else if (string.Equals(candidateDirectory, modelFolderPathNormalized, StringComparison.OrdinalIgnoreCase))
                    score += 800;
                else if (string.Equals(candidateDirectory, itemFolderPathNormalized, StringComparison.OrdinalIgnoreCase))
                    score += 700;
                else if (candidateNormalized.Contains("/" + ModelFolderName + "/", StringComparison.OrdinalIgnoreCase))
                    score += 500;
                else
                    score += 100;

                if (string.Equals(candidateName, itemName, StringComparison.OrdinalIgnoreCase))
                    score += 50;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                sourceModelPath = candidatePath;
            }

            return !string.IsNullOrWhiteSpace(sourceModelPath);
        }


        private static string BuildCanonicalModelPath(string itemFolderPath, string itemName)
        {
            var normalizedItemFolder = NormalizePath(itemFolderPath);
            var modelFolder = normalizedItemFolder + "/" + ModelFolderName;
            EnsureFolderHierarchy(modelFolder);
            return modelFolder + "/" + itemName + ".fbx";
        }


        private static void CleanupGeneratedClothingArtifacts()
        {
            if (!AssetDatabase.IsValidFolder(ModelRoot))
                return;

            var slotFolders = AssetDatabase.GetSubFolders(ModelRoot);
            if (slotFolders == null || slotFolders.Length == 0)
                return;

            for (var i = 0; i < slotFolders.Length; i++)
                CleanupGeneratedArtifactsInSlot(slotFolders[i]?.Replace('\\', '/'));
        }


        private static void CleanupGeneratedArtifactsInSlot(string slotFolderPath)
        {
            if (string.IsNullOrWhiteSpace(slotFolderPath))
                return;

            var itemFolders = AssetDatabase.GetSubFolders(slotFolderPath);
            if (itemFolders == null || itemFolders.Length == 0)
                return;

            for (var itemIndex = 0; itemIndex < itemFolders.Length; itemIndex++)
                CleanupGeneratedArtifactsForItemFolder(itemFolders[itemIndex]?.Replace('\\', '/'));
        }


        private static void CleanupGeneratedArtifactsForItemFolder(string itemFolderPath)
        {
            if (!TryGetCleanupItemName(itemFolderPath, out var itemName)) return;

            if (!TryEnsureCanonicalModelLocation(itemFolderPath, itemName, out var canonicalModelPath, out _))
                canonicalModelPath = BuildCanonicalModelPath(itemFolderPath, itemName);

            CleanupModelFolderRecursion(itemFolderPath, itemName, canonicalModelPath);
            CleanupUmaGeneratedBranches(itemFolderPath, itemName);
        }


        private static bool TryGetCleanupItemName(string itemFolderPath, out string itemName)
        {
            itemName = string.Empty;
            if (string.IsNullOrWhiteSpace(itemFolderPath)) return false;

            itemName = Path.GetFileName(itemFolderPath);
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            if (itemName.StartsWith("_", StringComparison.Ordinal)) return false;
            return !IsGeneratedSubfolderName(itemName);
        }


        private static void CleanupModelFolderRecursion(string itemFolderPath, string itemName, string canonicalModelPath)
        {
            if (string.IsNullOrWhiteSpace(itemFolderPath) || string.IsNullOrWhiteSpace(itemName))
                return;

            var modelFolderPath = NormalizePath(itemFolderPath) + "/" + ModelFolderName;
            if (!AssetDatabase.IsValidFolder(modelFolderPath))
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath) == null)
                return;

            var modelSubFolders = AssetDatabase.GetSubFolders(modelFolderPath);
            for (var i = 0; i < modelSubFolders.Length; i++)
            {
                var subFolder = modelSubFolders[i]?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(subFolder))
                    continue;

                AssetDatabase.DeleteAsset(subFolder);
            }

            var modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { modelFolderPath });
            for (var i = 0; i < modelGuids.Length; i++)
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                if (!IsSupportedModelPath(modelPath))
                    continue;

                var modelDirectory = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
                if (!string.Equals(modelDirectory, modelFolderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (NormalizePath(modelPath).Equals(NormalizePath(canonicalModelPath), StringComparison.OrdinalIgnoreCase))
                    continue;

                AssetDatabase.DeleteAsset(modelPath);
            }
        }


        private static void CleanupUmaGeneratedBranches(string itemFolderPath, string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemFolderPath) || string.IsNullOrWhiteSpace(itemName))
                return;

            var umaRootPath = NormalizePath(itemFolderPath) + "/UMA_" + itemName;
            if (!AssetDatabase.IsValidFolder(umaRootPath))
                return;

            var umaSubFolders = AssetDatabase.GetSubFolders(umaRootPath);
            for (var i = 0; i < umaSubFolders.Length; i++)
            {
                var subFolder = umaSubFolders[i]?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(subFolder))
                    continue;

                AssetDatabase.DeleteAsset(subFolder);
            }

            var rootAssetGuids = AssetDatabase.FindAssets(string.Empty, new[] { umaRootPath });
            for (var i = 0; i < rootAssetGuids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(rootAssetGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                var assetDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.Equals(assetDirectory, umaRootPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(assetPath);
                if (fileName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    fileName.IndexOf("Temp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("TempMesh", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }
    }
}
#endif
