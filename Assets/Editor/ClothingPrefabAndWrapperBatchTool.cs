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
    /// <summary>
    ///     Builds clothing prefabs from model assets, wrapper prefabs for equipment offsets,
    ///     and ItemDefinition assets used by inventory/equipment systems.
    /// </summary>
    public static partial class ClothingPrefabAndWrapperBatchTool
    {
        private const string MenuRoot = "Tools/Items/Clothing/";
        private const string BuildAllMenuPath =
            MenuRoot + "Clean Names + Create Prefabs + Wrappers + Item + Armor ScriptableObjects + Wire to Inventory";
        private const string BuildPrefabsMenuPath = MenuRoot + "Create Prefabs From Models";
        private const string BuildWrappersMenuPath = MenuRoot + "Create/Rename Equip Wrappers (1.EquipWrappers)";
        private const string BatchCaptureIconsMenuPath = MenuRoot + "Batch Capture Icons (Clothing Prefabs)";
        private const string BuildItemsMenuPath = MenuRoot + "Create Item + Armor ScriptableObjects From Clothing Prefabs";

        private const string ModelRoot = "Assets/Systems/Clothing";
        private const string PrefabRoot = "Assets/Systems/Clothing";
        private const string LegacyWrapperRoot = "Assets/Prefabs/Clothing/1.EquipWrappers";
        private const string LegacySystemsWrapperRoot = "Assets/Systems/Clothing/_Wrappers";
        private const string ItemRoot = "Assets/Systems/Clothing";
        private const string ClothesAssetRoot = "Assets/Systems/Clothing";
        private const string GeneratedIconsRoot = "Assets/Art/InventoryIcons/Generated";
        private const string DefaultUmaMaterialPath = "Assets/UMA/Content/Core/HumanShared/Materials/Standard/UMA_Diffuse_Normal_Metallic.asset";
        private const string DataFolderName = "Data";
        private const string MaterialFolderName = "Material";
        private const string ModelFolderName = "Model";
        private const string WrappersFolderName = "Wrappers";

        [MenuItem(BuildAllMenuPath, priority = -500)]
        private static void BuildPrefabsAndWrappers()
        {
            TouchSplitMembersForAnalysis();
            if (!ValidateModelRoot()) return;

            var normalization = NormalizeModelSourceAssets();

            RenameLegacyWrapperRootIfPossible();
            EnsureFolderHierarchy(PrefabRoot);
            EnsureFolderHierarchy(ItemRoot);
            EnsureFolderHierarchy(ClothesAssetRoot);
            CleanupGeneratedClothingArtifacts();

            var prefabResult = CreatePrefabsFromModelsInternal();
            var wrapperResult = CreateWrappersFromPrefabsInternal();
            var umaResult = CreateUmaAssetsFromPrefabsInternal();
            var itemResult = CreateItemDefinitionsFromPrefabsInternal();
            var clothingItems = LoadClothingItemDefinitions();
            var syncedSpawners = SyncClothingToPlayerSpawnerInventory(clothingItems);
            var syncedDebugSettings = EnableDevSpawnInventoryInDebugSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                "Clothing batch complete.\n\n" +
                "Model importers scanned for scale: " + normalization.ScannedModelImporters + "\n" +
                "Model importers normalized (scale/rig/animation): " + normalization.UpdatedModelImporters + "\n\n" +
                "FBX scanned for normalization: " + normalization.ScannedFbx + "\n" +
                "FBX renamed: " + normalization.RenamedFbx + "\n" +
                "Item folders renamed: " + normalization.RenamedItemFolders + "\n" +
                "Texture renamed: " + normalization.RenamedTextures + "\n" +
                "Texture deleted: " + normalization.DeletedTextures + "\n" +
                "Materials renamed: " + normalization.RenamedMaterials + "\n\n" +
                "Models scanned: " + prefabResult.Scanned + "\n" +
                "Prefabs created: " + prefabResult.Created + "\n" +
                "Prefabs updated: " + prefabResult.Updated + "\n" +
                "Models skipped: " + prefabResult.Skipped + "\n" +
                "Prefab build failures: " + prefabResult.Failed + "\n\n" +
                "Prefabs scanned for wrappers: " + wrapperResult.Scanned + "\n" +
                "Wrappers created: " + wrapperResult.Created + "\n" +
                "Wrappers updated: " + wrapperResult.Updated + "\n" +
                "Prefabs skipped for wrappers: " + wrapperResult.Skipped + "\n" +
                "Wrapper build failures: " + wrapperResult.Failed + "\n\n" +
                "Prefabs scanned for UMA assets: " + umaResult.Scanned + "\n" +
                "UMA assets created: " + umaResult.Created + "\n" +
                "UMA assets updated: " + umaResult.Updated + "\n" +
                "UMA assets skipped: " + umaResult.Skipped + "\n" +
                "UMA asset build failures: " + umaResult.Failed + "\n\n" +
                "Prefabs scanned for item assets: " + itemResult.Scanned + "\n" +
                "ItemDefinition created: " + itemResult.Created + "\n" +
                "ItemDefinition updated: " + itemResult.Updated + "\n" +
                "Inventory icons assigned/overwritten: " + itemResult.IconAssigned + "\n" +
                "ArmorData created: " + itemResult.ArmorCreated + "\n" +
                "ArmorData updated: " + itemResult.ArmorUpdated + "\n" +
                "Prefabs skipped for item assets: " + itemResult.Skipped + "\n" +
                "Item asset build failures: " + itemResult.Failed + "\n\n" +
                "Clothing ItemDefinitions loaded: " + clothingItems.Count + "\n" +
                "PlayerSpawner inventory bindings synced: " + syncedSpawners + "\n" +
                "DebugSettings inventory toggle synced: " + syncedDebugSettings + "\n\n" +
                "Model source: " + ModelRoot + "\n" +
                "Prefab output: " + PrefabRoot + "\n" +
                "ItemDefinition output: " + ItemRoot + "\n" +
                "ArmorData output: " + ClothesAssetRoot;

            Debug.Log("[ClothingPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Clothing Build", summary, "OK");
        }

        [MenuItem(BuildPrefabsMenuPath, priority = -500)]
        private static void CreatePrefabsFromModels()
        {
            if (!ValidateModelRoot()) return;

            var normalization = NormalizeModelSourceAssets();
            CleanupGeneratedClothingArtifacts();

            EnsureFolderHierarchy(PrefabRoot);

            var result = CreatePrefabsFromModelsInternal();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                "Clothing prefab build complete.\n\n" +
                "Model importers scanned for scale: " + normalization.ScannedModelImporters + "\n" +
                "Model importers normalized (scale/rig/animation): " + normalization.UpdatedModelImporters + "\n\n" +
                "FBX scanned for normalization: " + normalization.ScannedFbx + "\n" +
                "FBX renamed: " + normalization.RenamedFbx + "\n" +
                "Item folders renamed: " + normalization.RenamedItemFolders + "\n" +
                "Texture renamed: " + normalization.RenamedTextures + "\n" +
                "Texture deleted: " + normalization.DeletedTextures + "\n" +
                "Materials renamed: " + normalization.RenamedMaterials + "\n\n" +
                "Models scanned: " + result.Scanned + "\n" +
                "Prefabs created: " + result.Created + "\n" +
                "Prefabs updated: " + result.Updated + "\n" +
                "Models skipped: " + result.Skipped + "\n" +
                "Failures: " + result.Failed + "\n\n" +
                "Model source: " + ModelRoot + "\n" +
                "Prefab output: " + PrefabRoot;

            Debug.Log("[ClothingPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Clothing Prefabs", summary, "OK");
        }

        [MenuItem(BuildWrappersMenuPath, priority = -500)]
        private static void CreateWrappersFromPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                EditorUtility.DisplayDialog(
                    "Clothing Wrappers",
                    "Prefab root folder was not found:\n" + PrefabRoot,
                    "OK");
                return;
            }

            RenameLegacyWrapperRootIfPossible();
            CleanupGeneratedClothingArtifacts();

            var result = CreateWrappersFromPrefabsInternal();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                "Clothing wrapper build complete.\n\n" +
                "Prefabs scanned: " + result.Scanned + "\n" +
                "Wrappers created: " + result.Created + "\n" +
                "Wrappers updated: " + result.Updated + "\n" +
                "Prefabs skipped: " + result.Skipped + "\n" +
                "Failures: " + result.Failed + "\n\n" +
                "Prefab source: " + PrefabRoot + "\n" +
                "Wrapper output: (Decentralized in item folders)";

            Debug.Log("[ClothingPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Clothing Wrappers", summary, "OK");
        }

        [MenuItem(BuildItemsMenuPath, priority = -500)]
        private static void CreateItemDefinitionsFromPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                EditorUtility.DisplayDialog(
                    "Clothing Item ScriptableObjects",
                    "Prefab root folder was not found:\n" + PrefabRoot,
                    "OK");
                return;
            }

            EnsureFolderHierarchy(ItemRoot);
            EnsureFolderHierarchy(ClothesAssetRoot);
            CleanupGeneratedClothingArtifacts();

            var result = CreateItemDefinitionsFromPrefabsInternal();
            var clothingItems = LoadClothingItemDefinitions();
            var syncedSpawners = SyncClothingToPlayerSpawnerInventory(clothingItems);
            var syncedDebugSettings = EnableDevSpawnInventoryInDebugSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                "Clothing item asset build complete.\n\n" +
                "Prefabs scanned: " + result.Scanned + "\n" +
                "ItemDefinition created: " + result.Created + "\n" +
                "ItemDefinition updated: " + result.Updated + "\n" +
                "Inventory icons assigned/overwritten: " + result.IconAssigned + "\n" +
                "ArmorData created: " + result.ArmorCreated + "\n" +
                "ArmorData updated: " + result.ArmorUpdated + "\n" +
                "Prefabs skipped: " + result.Skipped + "\n" +
                "Failures: " + result.Failed + "\n\n" +
                "Clothing ItemDefinitions loaded: " + clothingItems.Count + "\n" +
                "PlayerSpawner inventory bindings synced: " + syncedSpawners + "\n" +
                "DebugSettings inventory toggle synced: " + syncedDebugSettings + "\n\n" +
                "Prefab source: " + PrefabRoot + "\n" +
                "ItemDefinition output: " + ItemRoot + "\n" +
                "ArmorData output: " + ClothesAssetRoot;

            Debug.Log("[ClothingPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Clothing Item ScriptableObjects", summary, "OK");
        }

        [MenuItem(BatchCaptureIconsMenuPath, priority = -500)]
        private static void BatchCaptureIconsFromClothingPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                EditorUtility.DisplayDialog(
                    "Clothing Icon Capture",
                    "Prefab root folder was not found:\n" + PrefabRoot,
                    "OK");
                return;
            }

            RenameLegacyWrapperRootIfPossible();
            InventoryIconRenderPipelineTool.BatchCaptureIconsFromRootFolder(
                PrefabRoot,
                "Batch Capturing Inventory Icons (Clothing Root)",
                new[] { LegacySystemsWrapperRoot, LegacyWrapperRoot },
                3f);
        }

        private static void TouchSplitMembersForAnalysis()
        {
            _ = GeneratedIconsRoot;
            _ = DefaultUmaMaterialPath;
            _ = DataFolderName;
            _ = MaterialFolderName;
            _ = ModelFolderName;
            _ = WrappersFolderName;

            var batch = new BatchResult();
            _ = batch.Scanned;
            _ = batch.Created;
            _ = batch.Updated;
            _ = batch.IconAssigned;
            _ = batch.ArmorCreated;
            _ = batch.ArmorUpdated;
            _ = batch.Skipped;
            _ = batch.Failed;
            _ = batch.Failures;

            var normalization = new SourceNormalizationResult();
            _ = normalization.ScannedModelImporters;
            _ = normalization.UpdatedModelImporters;
            _ = normalization.ScannedFbx;
            _ = normalization.RenamedFbx;
            _ = normalization.RenamedItemFolders;
            _ = normalization.RenamedTextures;
            _ = normalization.DeletedTextures;
            _ = normalization.RenamedMaterials;
            _ = normalization.Failures;
        }

        private static bool ValidateModelRoot()
        {
            if (AssetDatabase.IsValidFolder(ModelRoot)) return true;

            EditorUtility.DisplayDialog(
                "Clothing Prefabs",
                "Model root folder was not found:\n" + ModelRoot,
                "OK");
            return false;
        }

        private sealed class BatchResult
        {
            public int Scanned;
            public int Created;
            public int Updated;
            public int IconAssigned;
            public int ArmorCreated;
            public int ArmorUpdated;
            public int Skipped;
            public int Failed;
            public readonly List<string> Failures = new();

            public BatchResult()
            {
                Scanned = 0;
                Created = 0;
                Updated = 0;
                IconAssigned = 0;
                ArmorCreated = 0;
                ArmorUpdated = 0;
                Skipped = 0;
                Failed = 0;
            }
        }

        private sealed class SourceNormalizationResult
        {
            public int ScannedModelImporters;
            public int UpdatedModelImporters;
            public int ScannedFbx;
            public int RenamedFbx;
            public int RenamedItemFolders;
            public int RenamedTextures;
            public int DeletedTextures;
            public int RenamedMaterials;
            public readonly List<string> Failures = new();

            public SourceNormalizationResult()
            {
                ScannedModelImporters = 0;
                UpdatedModelImporters = 0;
                ScannedFbx = 0;
                RenamedFbx = 0;
                RenamedItemFolders = 0;
                RenamedTextures = 0;
                DeletedTextures = 0;
                RenamedMaterials = 0;
            }
        }
    }
}
#endif
