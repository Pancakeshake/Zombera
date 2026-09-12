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
        private sealed class PrefabPassContext
        {
            public readonly BatchResult Result = new();
            public readonly string[] ModelGuids;
            public readonly HashSet<string> ProcessedRelativePaths = new(StringComparer.OrdinalIgnoreCase);

            public PrefabPassContext(string[] modelGuids)
            {
                ModelGuids = modelGuids ?? Array.Empty<string>();
            }
        }

        private sealed class WrapperPassContext
        {
            public readonly BatchResult Result = new();
            public readonly string[] PrefabGuids;
            public readonly HashSet<string> ProcessedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
            public readonly string LegacySystemsWrapperRootNormalized = NormalizePath(LegacySystemsWrapperRoot);
            public readonly string LegacyWrapperRootNormalized = NormalizePath(LegacyWrapperRoot);

            public WrapperPassContext(string[] prefabGuids)
            {
                PrefabGuids = prefabGuids ?? Array.Empty<string>();
            }
        }

        private sealed class ItemPassContext
        {
            public readonly BatchResult Result = new();
            public readonly string[] PrefabGuids;
            public readonly HashSet<string> ProcessedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
            public readonly string LegacySystemsWrapperRootNormalized = NormalizePath(LegacySystemsWrapperRoot);
            public readonly string LegacyWrapperRootNormalized = NormalizePath(LegacyWrapperRoot);
            public readonly IReadOnlyDictionary<string, Sprite> IconLookup = BuildIconLookup();

            public ItemPassContext(string[] prefabGuids)
            {
                PrefabGuids = prefabGuids ?? Array.Empty<string>();
            }
        }

        private sealed class UmaPassContext
        {
            public readonly BatchResult Result = new();
            public readonly string[] PrefabGuids;
            public readonly HashSet<string> ProcessedRelativePaths = new(StringComparer.OrdinalIgnoreCase);

            public UmaPassContext(string[] prefabGuids)
            {
                PrefabGuids = prefabGuids ?? Array.Empty<string>();
            }
        }

        private readonly struct CanonicalItemPathInfo
        {
            public readonly string PrefabPath;
            public readonly string RelativePathWithoutExtension;
            public readonly string OutputRelativePath;
            public readonly string ItemFolder;
            public readonly string ItemName;

            public CanonicalItemPathInfo(
                string prefabPath,
                string relativePathWithoutExtension,
                string outputRelativePath,
                string itemFolder,
                string itemName)
            {
                PrefabPath = prefabPath;
                RelativePathWithoutExtension = relativePathWithoutExtension;
                OutputRelativePath = outputRelativePath;
                ItemFolder = itemFolder;
                ItemName = itemName;
            }
        }

        private readonly struct ItemPassAssetPaths
        {
            public readonly string DataSubfolder;
            public readonly string WrapperSubfolder;
            public readonly string UmaSubfolder;
            public readonly string ItemAssetPath;
            public readonly string LegacyItemAssetPath;
            public readonly string LegacyItemAssetPathInClothesRoot;
            public readonly string ArmorDataPath;
            public readonly string LegacyArmorDataPath;

            public ItemPassAssetPaths(CanonicalItemPathInfo pathInfo)
            {
                DataSubfolder = pathInfo.ItemFolder + "/" + DataFolderName;
                WrapperSubfolder = pathInfo.ItemFolder + "/" + WrappersFolderName;
                UmaSubfolder = pathInfo.ItemFolder + "/UMA_" + pathInfo.ItemName;

                ItemAssetPath = BuildAssetPath(ItemRoot, DataSubfolder + "/" + pathInfo.ItemName);
                LegacyItemAssetPath = BuildAssetPath(ItemRoot, pathInfo.OutputRelativePath);
                LegacyItemAssetPathInClothesRoot = BuildAssetPath(ClothesAssetRoot, pathInfo.OutputRelativePath);

                ArmorDataPath = BuildAssetPath(ClothesAssetRoot, DataSubfolder + "/" + pathInfo.ItemName + "_Armor");
                LegacyArmorDataPath = BuildAssetPath(ClothesAssetRoot, AppendArmorSuffix(pathInfo.OutputRelativePath));
            }
        }

        private readonly struct UmaAssetPaths
        {
            public readonly string ItemFolderPath;
            public readonly string UmaSubfolder;
            public readonly string UmaRootPath;
            public readonly string MaterialFolderPath;
            public readonly string SlotPath;
            public readonly string OverlayPath;
            public readonly string RecipePath;

            public UmaAssetPaths(CanonicalItemPathInfo pathInfo)
            {
                ItemFolderPath = PrefabRoot + "/" + pathInfo.ItemFolder;
                UmaSubfolder = pathInfo.ItemFolder + "/UMA_" + pathInfo.ItemName;
                UmaRootPath = PrefabRoot + "/" + UmaSubfolder;
                MaterialFolderPath = PrefabRoot + "/" + pathInfo.ItemFolder + "/" + MaterialFolderName;

                SlotPath = UmaRootPath + "/UMA_" + pathInfo.ItemName + "_Slot.asset";
                OverlayPath = UmaRootPath + "/UMA_" + pathInfo.ItemName + "_Overlay.asset";
                RecipePath = UmaRootPath + "/UMA_" + pathInfo.ItemName + "_Recipe.asset";
            }
        }

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
                "Clothing Prefab Build",
                "Processing model " + modelPath + " (" + (index + 1) + "/" + context.ModelGuids.Length + ")",
                (index + 1f) / context.ModelGuids.Length);

            if (!TryResolveCanonicalItemFromModelPath(modelPath, context.ProcessedRelativePaths, context.Result,
                    out var outputRelativePath, out var itemFolder, out var itemName))
                return;

            if (!TryLoadCanonicalModelAssetForItem(modelPath, itemFolder, itemName, context.Result,
                    out var modelAsset,
                    out var canonicalModelPath))
                return;

            var prefabPath = BuildPrefabPath(PrefabRoot, outputRelativePath);
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
            out string outputRelativePath,
            out string itemFolder,
            out string itemName)
        {
            outputRelativePath = string.Empty;
            itemFolder = string.Empty;
            itemName = string.Empty;

            if (!IsSupportedModelPath(modelPath))
            {
                result.Skipped++;
                return false;
            }

            if (!TryGetRelativePathWithoutExtension(modelPath, ModelRoot, out var relativePathWithoutExtension))
            {
                result.Skipped++;
                return false;
            }

            if (!TryResolveCanonicalItemPath(
                    relativePathWithoutExtension,
                    out outputRelativePath,
                    out itemFolder,
                    out itemName))
            {
                result.Skipped++;
                return false;
            }

            if (processedRelativePaths.Add(outputRelativePath)) return true;

            result.Skipped++;
            return false;
        }


        private static bool TryLoadCanonicalModelAssetForItem(
            string modelPath,
            string itemFolder,
            string itemName,
            BatchResult result,
            out GameObject modelAsset,
            out string canonicalModelPath)
        {
            modelAsset = null;
            canonicalModelPath = string.Empty;

            var itemFolderPath = ModelRoot + "/" + itemFolder;
            if (!TryEnsureCanonicalModelLocation(itemFolderPath, itemName, out canonicalModelPath, out var modelFailure))
            {
                result.Failed++;
                result.Failures.Add(modelPath + " -> " + modelFailure);
                return false;
            }

            CleanupModelFolderRecursion(itemFolderPath, itemName, canonicalModelPath);

            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalModelPath);
            if (modelAsset != null) return true;

            result.Failed++;
            result.Failures.Add(modelPath + " -> Could not load canonical model at " + canonicalModelPath);
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
                "Clothing Wrapper Build",
                "Processing prefab " + prefabPath + " (" + (index + 1) + "/" + context.PrefabGuids.Length + ")",
                (index + 1f) / context.PrefabGuids.Length);

            if (ShouldSkipWrapperSourcePrefab(prefabPath, context)) return;

            if (!TryResolveCanonicalItemFromPrefabPath(
                    prefabPath,
                    context.ProcessedRelativePaths,
                    context.Result,
                    out var pathInfo))
                return;

            if (!TryEnsureCanonicalPrefabForOutputPath(pathInfo.OutputRelativePath, out var sourcePrefab,
                    out var canonicalPrefabFailure))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> " + canonicalPrefabFailure);
                return;
            }

            var wrapperSubfolder = pathInfo.ItemFolder + "/Wrappers";
            var wrapperPath = BuildPrefabPath(PrefabRoot, wrapperSubfolder + "/" + "Wrapper_" + pathInfo.ItemName);

            if (!TryMoveLegacyWrapperPaths(pathInfo, wrapperPath, out var moveError))
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
                normalizedPath.StartsWith(context.LegacySystemsWrapperRootNormalized + "/",
                    StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.LegacySystemsWrapperRootNormalized, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(context.LegacyWrapperRootNormalized + "/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.LegacyWrapperRootNormalized, StringComparison.OrdinalIgnoreCase);

            if (!isLegacyPath) return false;

            context.Result.Skipped++;
            return true;
        }


        private static bool TryMoveLegacyWrapperPaths(
            CanonicalItemPathInfo pathInfo,
            string wrapperPath,
            out string moveError)
        {
            var legacyWrapperPath = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(pathInfo.RelativePathWithoutExtension));
            var legacyWrapperPathForCanonical = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(pathInfo.OutputRelativePath));
            var legacySystemsWrapperPath = BuildPrefabPath(LegacySystemsWrapperRoot, PrependWrapperPrefix(pathInfo.OutputRelativePath));

            return TryMoveAssetIfNeeded(legacyWrapperPath, wrapperPath, out moveError)
                   && TryMoveAssetIfNeeded(legacyWrapperPathForCanonical, wrapperPath, out moveError)
                   && TryMoveAssetIfNeeded(legacySystemsWrapperPath, wrapperPath, out moveError);
        }


        private static BatchResult CreateItemDefinitionsFromPrefabsInternal()
        {
            var context = new ItemPassContext(AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }));
            if (context.PrefabGuids.Length == 0)
                return context.Result;

            try
            {
                for (var i = 0; i < context.PrefabGuids.Length; i++) ProcessItemDefinitionPrefabGuid(context, i);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Result;
        }


        private static void ProcessItemDefinitionPrefabGuid(ItemPassContext context, int index)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(context.PrefabGuids[index]);
            context.Result.Scanned++;

            EditorUtility.DisplayProgressBar(
                "Clothing ItemDefinition Build",
                "Processing prefab " + prefabPath + " (" + (index + 1) + "/" + context.PrefabGuids.Length + ")",
                (index + 1f) / context.PrefabGuids.Length);

            if (ShouldSkipItemSourcePrefab(prefabPath, context)) return;

            if (!TryResolveCanonicalItemFromPrefabPath(
                    prefabPath,
                    context.ProcessedRelativePaths,
                    context.Result,
                    out var pathInfo))
                return;

            if (!TryEnsureCanonicalPrefabForOutputPath(pathInfo.OutputRelativePath, out var sourcePrefab,
                    out var canonicalPrefabFailure))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> " + canonicalPrefabFailure);
                return;
            }

            var paths = new ItemPassAssetPaths(pathInfo);

            if (!TryMoveLegacyItemAssets(paths, prefabPath, context.Result)) return;

            var item = LoadOrCreateAsset(paths.ItemAssetPath, out var createdItem,
                ScriptableObject.CreateInstance<ItemDefinition>);
            if (item == null)
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> Could not create ItemDefinition asset.");
                return;
            }

            var wrapperPrefab = ResolveWrapperPrefabForItem(pathInfo, paths);
            var hasResolvedSlot = TryResolveEquipmentSlotFromRootFolder(pathInfo.ItemFolder, out var slot);
            if (!hasResolvedSlot)
                hasResolvedSlot = TryResolveEquipmentSlot(pathInfo.OutputRelativePath, out slot);

            var resolvedSlotForArmor = hasResolvedSlot ? slot : EquipmentSlot.Chest;

            if (!TryMoveLegacyArmorAsset(paths, prefabPath, context.Result)) return;

            var armorData = LoadOrCreateAsset(paths.ArmorDataPath, out var createdArmorData,
                ScriptableObject.CreateInstance<ArmorData>);
            if (armorData == null)
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> Could not create ArmorData asset.");
                return;
            }

            var umaRecipe = AssetDatabase.LoadAssetAtPath<UMA.CharacterSystem.UMAWardrobeRecipe>(
                BuildAssetPath(ClothesAssetRoot, paths.UmaSubfolder + "/UMA_" + pathInfo.ItemName + "_Recipe.asset"));

            ApplyArmorDataDefaults(
                armorData,
                pathInfo.OutputRelativePath,
                resolvedSlotForArmor,
                createdArmorData);

            var itemDefaults = new ItemDefinitionDefaultsContext(
                sourcePrefab,
                wrapperPrefab,
                umaRecipe,
                pathInfo.OutputRelativePath,
                hasResolvedSlot,
                slot,
                armorData,
                createdItem,
                context.IconLookup);

            if (ApplyItemDefinitionDefaults(item, itemDefaults)) context.Result.IconAssigned++;

            EditorUtility.SetDirty(item);
            EditorUtility.SetDirty(armorData);

            if (createdItem)
                context.Result.Created++;
            else
                context.Result.Updated++;

            if (createdArmorData)
                context.Result.ArmorCreated++;
            else
                context.Result.ArmorUpdated++;
        }


        private static bool ShouldSkipItemSourcePrefab(string prefabPath, ItemPassContext context)
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
                normalizedPath.StartsWith(context.LegacySystemsWrapperRootNormalized + "/",
                    StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.LegacySystemsWrapperRootNormalized, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(context.LegacyWrapperRootNormalized + "/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(context.LegacyWrapperRootNormalized, StringComparison.OrdinalIgnoreCase);

            if (!isLegacyPath) return false;

            context.Result.Skipped++;
            return true;
        }


        private static bool TryMoveLegacyItemAssets(ItemPassAssetPaths paths, string prefabPath, BatchResult result)
        {
            if (TryMoveAssetIfNeeded(paths.LegacyItemAssetPath, paths.ItemAssetPath, out var moveError)
                && TryMoveAssetIfNeeded(paths.LegacyItemAssetPathInClothesRoot, paths.ItemAssetPath, out moveError))
                return true;

            result.Failed++;
            result.Failures.Add(prefabPath + " -> " + moveError);
            return false;
        }


        private static GameObject ResolveWrapperPrefabForItem(CanonicalItemPathInfo pathInfo, ItemPassAssetPaths paths)
        {
            var wrapperPath = BuildPrefabPath(PrefabRoot, paths.WrapperSubfolder + "/Wrapper_" + pathInfo.ItemName);
            var wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            var legacyWrapperPath = BuildPrefabPath(LegacyWrapperRoot,
                PrependWrapperPrefix(pathInfo.RelativePathWithoutExtension));
            wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(legacyWrapperPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            var legacyWrapperCanonicalPath = BuildPrefabPath(LegacyWrapperRoot,
                PrependWrapperPrefix(pathInfo.OutputRelativePath));
            wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(legacyWrapperCanonicalPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            var legacySystemsWrapperPath = BuildPrefabPath(LegacySystemsWrapperRoot,
                PrependWrapperPrefix(pathInfo.OutputRelativePath));
            return AssetDatabase.LoadAssetAtPath<GameObject>(legacySystemsWrapperPath);
        }


        private static bool TryMoveLegacyArmorAsset(ItemPassAssetPaths paths, string prefabPath, BatchResult result)
        {
            if (TryMoveAssetIfNeeded(paths.LegacyArmorDataPath, paths.ArmorDataPath, out var moveError)) return true;

            result.Failed++;
            result.Failures.Add(prefabPath + " -> " + moveError);
            return false;
        }


        private static BatchResult CreateUmaAssetsFromPrefabsInternal()
        {
            var context = new UmaPassContext(AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }));
            if (context.PrefabGuids.Length == 0)
                return context.Result;

            try
            {
                for (var i = 0; i < context.PrefabGuids.Length; i++) ProcessUmaPrefabGuid(context, i);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Result;
        }


        private static void ProcessUmaPrefabGuid(UmaPassContext context, int index)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(context.PrefabGuids[index]);
            context.Result.Scanned++;

            EditorUtility.DisplayProgressBar(
                "Clothing UMA Build",
                "Processing UMA for " + prefabPath + " (" + (index + 1) + "/" + context.PrefabGuids.Length + ")",
                (index + 1f) / context.PrefabGuids.Length);

            if (!TryResolveUmaCanonicalPathInfo(prefabPath, context, out var pathInfo)) return;

            if (!TryEnsureCanonicalPrefabForOutputPath(pathInfo.OutputRelativePath, out _, out var canonicalPrefabFailure))
            {
                context.Result.Failed++;
                context.Result.Failures.Add(prefabPath + " -> " + canonicalPrefabFailure);
                return;
            }

            var paths = new UmaAssetPaths(pathInfo);
            CleanupUmaGeneratedBranches(paths.ItemFolderPath, pathInfo.ItemName);
            EnsureFolderHierarchy(paths.UmaRootPath);

            var hasResolvedSlot = TryResolveEquipmentSlotFromRootFolder(pathInfo.ItemFolder, out var slot);
            if (!hasResolvedSlot)
                hasResolvedSlot = TryResolveEquipmentSlot(pathInfo.OutputRelativePath, out slot);

            var wardrobeSlot = MapEquipmentSlotToUmaSlot(hasResolvedSlot ? slot : EquipmentSlot.Chest);

            var slotAsset = LoadOrCreateAsset(paths.SlotPath, out var createdSlot,
                ScriptableObject.CreateInstance<UMA.SlotDataAsset>);
            var overlayAsset = LoadOrCreateAsset(paths.OverlayPath, out var createdOverlay,
                ScriptableObject.CreateInstance<UMA.OverlayDataAsset>);
            var recipeAsset = LoadOrCreateAsset(paths.RecipePath, out var createdRecipe,
                ScriptableObject.CreateInstance<UMA.CharacterSystem.UMAWardrobeRecipe>);

            ApplyUmaAssetDefaults(pathInfo.ItemName, paths.MaterialFolderPath, wardrobeSlot, createdSlot, slotAsset,
                createdOverlay, overlayAsset, createdRecipe, recipeAsset);

            if (createdSlot || createdOverlay || createdRecipe)
                context.Result.Created++;
            else
                context.Result.Updated++;
        }


        private static bool TryResolveUmaCanonicalPathInfo(string prefabPath, UmaPassContext context,
            out CanonicalItemPathInfo pathInfo)
        {
            pathInfo = default;

            if (string.IsNullOrWhiteSpace(prefabPath)
                || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                context.Result.Skipped++;
                return false;
            }

            if (Path.GetFileName(prefabPath).StartsWith("Wrapper_", StringComparison.OrdinalIgnoreCase))
            {
                context.Result.Skipped++;
                return false;
            }

            if (!TryResolveCanonicalItemFromPrefabPath(
                    prefabPath,
                    context.ProcessedRelativePaths,
                    context.Result,
                    out pathInfo))
                return false;

            return true;
        }


        private static void ApplyUmaAssetDefaults(
            string itemName,
            string materialFolder,
            string wardrobeSlot,
            bool createdSlot,
            UMA.SlotDataAsset slotAsset,
            bool createdOverlay,
            UMA.OverlayDataAsset overlayAsset,
            bool createdRecipe,
            UMA.CharacterSystem.UMAWardrobeRecipe recipeAsset)
        {
            if (createdSlot)
            {
                slotAsset.slotName = "UMA_" + itemName + "_Slot";
                EditorUtility.SetDirty(slotAsset);
            }

            if (createdOverlay)
            {
                overlayAsset.overlayName = "UMA_" + itemName + "_Overlay";
                overlayAsset.material = AssetDatabase.LoadAssetAtPath<UMA.UMAMaterial>(DefaultUmaMaterialPath);
                overlayAsset.textureList = BuildUmaTextureList(materialFolder);
                EditorUtility.SetDirty(overlayAsset);
            }

            if (!createdRecipe) return;

            recipeAsset.wardrobeSlot = wardrobeSlot;
            EditorUtility.SetDirty(recipeAsset);
        }


        private static Texture[] BuildUmaTextureList(string materialFolder)
        {
            var textures = new List<Texture>();
            AddTextureIfPresent(textures, materialFolder + "/Base_Map.png", materialFolder + "/Base_Map.jpg");
            AddTextureIfPresent(textures, materialFolder + "/Normal_Map.png", materialFolder + "/Normal_Map.jpg");
            AddTextureIfPresent(textures, materialFolder + "/Metallic_Map.png", materialFolder + "/Metallic_Map.jpg");
            return textures.ToArray();
        }


        private static void AddTextureIfPresent(List<Texture> textures, string primaryPath, string fallbackPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(primaryPath);
            if (texture == null)
                texture = AssetDatabase.LoadAssetAtPath<Texture>(fallbackPath);

            if (texture != null) textures.Add(texture);
        }


        private static bool TryResolveCanonicalItemFromPrefabPath(
            string prefabPath,
            HashSet<string> processedRelativePaths,
            BatchResult result,
            out CanonicalItemPathInfo pathInfo)
        {
            pathInfo = default;

            if (!TryGetRelativePathWithoutExtension(prefabPath, PrefabRoot, out var relativePathWithoutExtension))
            {
                result.Skipped++;
                return false;
            }

            if (!TryResolveCanonicalItemPath(relativePathWithoutExtension, out var outputRelativePath,
                    out var itemFolder, out var itemName))
            {
                result.Skipped++;
                return false;
            }

            if (!processedRelativePaths.Add(outputRelativePath))
            {
                result.Skipped++;
                return false;
            }

            pathInfo = new CanonicalItemPathInfo(
                prefabPath,
                relativePathWithoutExtension,
                outputRelativePath,
                itemFolder,
                itemName);

            return true;
        }
    }
}
#endif
