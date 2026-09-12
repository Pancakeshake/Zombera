#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Model-first weapon pipeline mirroring the clothing workflow:
    ///     normalize names/assets, build prefabs, build equip wrappers, wire item/weapon data, and sync inventory.
    /// </summary>
    public static partial class WeaponCleanPrefabAndWrapperBatchTool
    {
        private const string MenuRoot = "Tools/Items/Weapons/";
        private const string BuildAllMenuPath =
            MenuRoot + "Clean Names + Create Prefabs + Wrappers + Batch Wire Equipment + Wire to Inventory";
        private const string BuildPrefabsMenuPath = MenuRoot + "Create Prefabs From Models";
        private const string BuildWrappersMenuPath = MenuRoot + "Create/Rename Equip Wrappers (1.EquipWrappers)";
        private const string BatchCaptureIconsMenuPath = MenuRoot + "Batch Capture Icons (Weapon Prefabs)";

        private const string ModelRoot = "Assets/Systems/Weapons";
        private const string PrefabRoot = "Assets/Systems/Weapons";
        private const string LegacySystemsWrapperRoot = "Assets/Systems/Weapons/_Wrappers";
        private const string LegacyWrapperRoot = "Assets/Prefabs/Weapons/1.EquipWrappers";
        private const string ItemRoot = "Assets/Systems/Weapons";

        [MenuItem(BuildAllMenuPath, priority = -500)]
        private static void BuildAll()
        {
            TouchSplitMembersForAnalysis();
            if (!ValidateModelRoot()) return;

            var normalization = NormalizeModelSourceAssets();

            RenameLegacyWrapperRootIfPossible();
            EnsureFolderHierarchy(PrefabRoot);

            var prefabResult = CreatePrefabsFromModelsInternal();
            var wrapperResult = CreateWrappersFromPrefabsInternal();
            var wireSummary = WeaponEquipmentBatchTool.RunBatchWireWeaponEquipment(false);
            var wrapperAssignments = AssignWrappersToWeaponItems();
            var weaponItems = LoadWeaponItemDefinitions();
            var syncedSpawners = SyncWeaponItemsToPlayerSpawnerInventory(weaponItems);
            var syncedDebugSettings = EnableDevSpawnInventoryInDebugSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildAllSummary(new BuildAllSummaryData
            {
                Normalization = normalization,
                PrefabResult = prefabResult,
                WrapperResult = wrapperResult,
                WireSummary = wireSummary,
                WrapperAssignments = wrapperAssignments,
                WeaponItemsLoaded = weaponItems.Count,
                SyncedSpawners = syncedSpawners,
                SyncedDebugSettings = syncedDebugSettings
            });

            Debug.Log("[WeaponCleanPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Weapon Pipeline", summary, "OK");
        }

        [MenuItem(BuildPrefabsMenuPath, priority = -500)]
        private static void CreatePrefabsFromModels()
        {
            if (!ValidateModelRoot()) return;

            var normalization = NormalizeModelSourceAssets();
            EnsureFolderHierarchy(PrefabRoot);

            var prefabResult = CreatePrefabsFromModelsInternal();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildPrefabsSummary(normalization, prefabResult);

            Debug.Log("[WeaponCleanPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Weapon Prefabs", summary, "OK");
        }

        [MenuItem(BuildWrappersMenuPath, priority = -500)]
        private static void CreateOrRenameWrappers()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                EditorUtility.DisplayDialog(
                    "Weapon Wrappers",
                    "Prefab root folder was not found:\n" + PrefabRoot,
                    "OK");
                return;
            }

            RenameLegacyWrapperRootIfPossible();

            var wrapperResult = CreateWrappersFromPrefabsInternal();
            var wrapperAssignments = AssignWrappersToWeaponItems();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildWrappersSummary(wrapperResult, wrapperAssignments);

            Debug.Log("[WeaponCleanPrefabAndWrapperBatchTool] " + summary);
            EditorUtility.DisplayDialog("Weapon Wrappers", summary, "OK");
        }

        [MenuItem(BatchCaptureIconsMenuPath, priority = -500)]
        private static void BatchCaptureIconsFromWeaponPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                EditorUtility.DisplayDialog(
                    "Weapon Icon Capture",
                    "Prefab root folder was not found:\n" + PrefabRoot,
                    "OK");
                return;
            }

            RenameLegacyWrapperRootIfPossible();
            InventoryIconRenderPipelineTool.BatchCaptureIconsFromRootFolder(
                PrefabRoot,
                "Batch Capturing Inventory Icons (Weapons Root)",
                new[] { LegacySystemsWrapperRoot, LegacyWrapperRoot },
                2f);
        }

        private static bool ValidateModelRoot()
        {
            if (AssetDatabase.IsValidFolder(ModelRoot)) return true;

            EditorUtility.DisplayDialog(
                "Weapon Models",
                "Model root folder was not found:\n" + ModelRoot,
                "OK");
            return false;
        }

        private static void RenameLegacyWrapperRootIfPossible()
        {
            if (!AssetDatabase.IsValidFolder(LegacyWrapperRoot)) return;
            if (AssetDatabase.IsValidFolder(LegacySystemsWrapperRoot)) return;

            var parentFolder = Path.GetDirectoryName(LegacySystemsWrapperRoot)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder)) return;

            var moveError = AssetDatabase.MoveAsset(LegacyWrapperRoot, LegacySystemsWrapperRoot);
            if (!string.IsNullOrWhiteSpace(moveError))
                Debug.LogWarning("[WeaponCleanPrefabAndWrapperBatchTool] Could not migrate legacy wrapper root: " +
                                 moveError);
        }

        private static void TouchSplitMembersForAnalysis()
        {
            _ = ItemRoot;
        }

        private static string BuildAllSummary(BuildAllSummaryData data)
        {
            return
                "Weapon model pipeline complete.\n\n" +
                "Model importers scanned for scale: " + data.Normalization.ScannedModelImporters + "\n" +
                "Model importers set to scale 1: " + data.Normalization.UpdatedModelImporters + "\n\n" +
                "FBX scanned for normalization: " + data.Normalization.ScannedFbx + "\n" +
                "FBX renamed: " + data.Normalization.RenamedFbx + "\n" +
                "Item folders renamed: " + data.Normalization.RenamedItemFolders + "\n" +
                "Texture renamed: " + data.Normalization.RenamedTextures + "\n" +
                "Materials renamed: " + data.Normalization.RenamedMaterials + "\n\n" +
                "Models scanned: " + data.PrefabResult.Scanned + "\n" +
                "Prefabs created: " + data.PrefabResult.Created + "\n" +
                "Prefabs updated: " + data.PrefabResult.Updated + "\n" +
                "Models skipped: " + data.PrefabResult.Skipped + "\n" +
                "Prefab build failures: " + data.PrefabResult.Failed + "\n\n" +
                "Prefabs scanned for wrappers: " + data.WrapperResult.Scanned + "\n" +
                "Wrappers created: " + data.WrapperResult.Created + "\n" +
                "Wrappers updated: " + data.WrapperResult.Updated + "\n" +
                "Prefabs skipped for wrappers: " + data.WrapperResult.Skipped + "\n" +
                "Wrapper build failures: " + data.WrapperResult.Failed + "\n" +
                "Items assigned wrapper visuals: " + data.WrapperAssignments + "\n\n" +
                "Weapon items loaded: " + data.WeaponItemsLoaded + "\n" +
                "PlayerSpawner inventory bindings synced: " + data.SyncedSpawners + "\n" +
                "DebugSettings inventory toggle synced: " + data.SyncedDebugSettings + "\n\n" +
                "Weapon equipment batch summary:\n" + data.WireSummary + "\n\n" +
                "Model source: " + ModelRoot + "\n" +
                "Prefab output: " + PrefabRoot + "\n" +
                "Wrapper output: (Decentralized in item folders)";
        }

        private static string BuildPrefabsSummary(SourceNormalizationResult normalization, BatchResult prefabResult)
        {
            return
                "Weapon prefab build complete.\n\n" +
                "Model importers scanned for scale: " + normalization.ScannedModelImporters + "\n" +
                "Model importers set to scale 1: " + normalization.UpdatedModelImporters + "\n\n" +
                "FBX scanned for normalization: " + normalization.ScannedFbx + "\n" +
                "FBX renamed: " + normalization.RenamedFbx + "\n" +
                "Item folders renamed: " + normalization.RenamedItemFolders + "\n" +
                "Texture renamed: " + normalization.RenamedTextures + "\n" +
                "Materials renamed: " + normalization.RenamedMaterials + "\n\n" +
                "Models scanned: " + prefabResult.Scanned + "\n" +
                "Prefabs created: " + prefabResult.Created + "\n" +
                "Prefabs updated: " + prefabResult.Updated + "\n" +
                "Models skipped: " + prefabResult.Skipped + "\n" +
                "Failures: " + prefabResult.Failed + "\n\n" +
                "Model source: " + ModelRoot + "\n" +
                "Prefab output: " + PrefabRoot;
        }

        private static string BuildWrappersSummary(BatchResult wrapperResult, int wrapperAssignments)
        {
            return
                "Weapon wrapper build complete.\n\n" +
                "Prefabs scanned: " + wrapperResult.Scanned + "\n" +
                "Wrappers created: " + wrapperResult.Created + "\n" +
                "Wrappers updated: " + wrapperResult.Updated + "\n" +
                "Prefabs skipped: " + wrapperResult.Skipped + "\n" +
                "Failures: " + wrapperResult.Failed + "\n" +
                "Items assigned wrapper visuals: " + wrapperAssignments + "\n\n" +
                "Wrapper output: (Decentralized in item folders)";
        }
    }
}
#endif
