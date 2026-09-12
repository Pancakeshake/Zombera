#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Characters;
using Zombera.Debugging;
using Zombera.Inventory;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class WeaponCleanPrefabAndWrapperBatchTool
    {
        private static int AssignWrappersToWeaponItems()
        {
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot });
            var assigned = 0;

            for (var i = 0; i < itemGuids.Length; i++)
            {
                var itemPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null || item.itemType != ItemType.Weapon) continue;

                var sourceVisual = item.equippedVisualPrefab;
                if (sourceVisual == null) continue;

                var sourcePath = AssetDatabase.GetAssetPath(sourceVisual)?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(sourcePath)) continue;

                if (Path.GetFileNameWithoutExtension(sourcePath)
                    .StartsWith("Wrapper_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryResolveWrapperPathForItem(
                        sourcePath,
                        out var wrapperPath,
                        out var legacyWrapperPath,
                        out var legacyWrapperCanonicalPath,
                        out var legacySystemsWrapperPath))
                    continue;

                var wrapperPrefab = TryLoadWrapperPrefabWithLegacyFallback(
                    wrapperPath,
                    legacyWrapperPath,
                    legacyWrapperCanonicalPath,
                    legacySystemsWrapperPath);

                if (wrapperPrefab == null) continue;
                if (item.equippedVisualPrefab == wrapperPrefab) continue;

                Undo.RecordObject(item, "Assign Weapon Wrapper");
                ApplyWrapperAssignmentDefaults(item, wrapperPrefab);

                EditorUtility.SetDirty(item);
                assigned++;
            }

            return assigned;
        }

        private static bool TryResolveWrapperPathForItem(
            string sourcePath,
            out string wrapperPath,
            out string legacyWrapperPath,
            out string legacyWrapperCanonicalPath,
            out string legacySystemsWrapperPath)
        {
            wrapperPath = string.Empty;
            legacyWrapperPath = string.Empty;
            legacyWrapperCanonicalPath = string.Empty;
            legacySystemsWrapperPath = string.Empty;

            var sourcePathNormalized = NormalizePath(sourcePath);
            if (sourcePathNormalized.StartsWith(NormalizePath(LegacySystemsWrapperRoot) + "/", StringComparison.OrdinalIgnoreCase)
                || sourcePathNormalized.StartsWith(NormalizePath(LegacyWrapperRoot) + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetRelativePathWithoutExtension(sourcePath, PrefabRoot, out var relativePathWithoutExtension))
                return false;

            var outputRelativePath = BuildOutputRelativePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(outputRelativePath)) return false;

            var itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
            var itemName = Path.GetFileName(outputRelativePath);
            if (string.IsNullOrWhiteSpace(itemName)) return false;

            if (string.IsNullOrWhiteSpace(itemFolder))
                itemFolder = itemName;

            var wrapperSubfolder = itemFolder + "/Wrappers";
            wrapperPath = BuildPrefabPath(PrefabRoot, wrapperSubfolder + "/" + "Wrapper_" + itemName);
            legacyWrapperPath = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(relativePathWithoutExtension));
            legacyWrapperCanonicalPath = BuildPrefabPath(LegacyWrapperRoot, PrependWrapperPrefix(outputRelativePath));
            legacySystemsWrapperPath = BuildPrefabPath(LegacySystemsWrapperRoot, PrependWrapperPrefix(outputRelativePath));
            return true;
        }

        private static GameObject TryLoadWrapperPrefabWithLegacyFallback(
            string wrapperPath,
            string legacyWrapperPath,
            string legacyWrapperCanonicalPath,
            string legacySystemsWrapperPath)
        {
            var wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(legacyWrapperPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(legacyWrapperCanonicalPath);
            if (wrapperPrefab != null) return wrapperPrefab;

            return AssetDatabase.LoadAssetAtPath<GameObject>(legacySystemsWrapperPath);
        }

        private static void ApplyWrapperAssignmentDefaults(ItemDefinition item, GameObject wrapperPrefab)
        {
            item.equippedVisualPrefab = wrapperPrefab;
            item.equippedVisualLocalPosition = Vector3.zero;
            item.equippedVisualLocalEulerAngles = Vector3.zero;
            if (IsNearZeroScale(item.equippedVisualLocalScale))
                item.equippedVisualLocalScale = Vector3.one;
        }

        private static List<ItemDefinition> LoadWeaponItemDefinitions()
        {
            var items = new List<ItemDefinition>();
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot });

            for (var i = 0; i < itemGuids.Length; i++)
            {
                var itemPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null || items.Contains(item)) continue;

                if (item.worldPickupPrefab == null) continue;

                var prefabPath = AssetDatabase.GetAssetPath(item.worldPickupPrefab)?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(prefabPath)) continue;

                var normalizedPrefabPath = NormalizePath(prefabPath);
                if (!normalizedPrefabPath.StartsWith(NormalizePath(PrefabRoot) + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Path.GetFileNameWithoutExtension(prefabPath)
                    .StartsWith("Wrapper_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (normalizedPrefabPath.StartsWith(NormalizePath(LegacySystemsWrapperRoot) + "/",
                        StringComparison.OrdinalIgnoreCase)
                    || normalizedPrefabPath.StartsWith(NormalizePath(LegacyWrapperRoot) + "/",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                items.Add(item);
            }

            return items;
        }

        private static int SyncWeaponItemsToPlayerSpawnerInventory(IReadOnlyList<ItemDefinition> weaponItems)
        {
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var syncedCount = 0;

            for (var i = 0; i < spawners.Length; i++)
            {
                var spawner = spawners[i];
                if (!TryGetSpawnerInventoryProperty(spawner, out var so, out var itemsProp)) continue;

                var mergedItems = CollectMergedSpawnerInventoryItems(itemsProp, weaponItems);
                ApplyMergedItemsToInventoryProperty(itemsProp, mergedItems);

                so.ApplyModifiedPropertiesWithoutUndo();
                MarkSpawnerDirty(spawner);

                syncedCount++;
            }

            return syncedCount;
        }

        private static bool TryGetSpawnerInventoryProperty(
            PlayerSpawner spawner,
            out SerializedObject serializedSpawner,
            out SerializedProperty itemsProp)
        {
            serializedSpawner = null;
            itemsProp = null;
            if (spawner == null) return false;

            serializedSpawner = new SerializedObject(spawner);
            itemsProp = serializedSpawner.FindProperty("devModeSpawnInventoryItems");
            return itemsProp != null;
        }

        private static List<ItemDefinition> CollectMergedSpawnerInventoryItems(
            SerializedProperty itemsProp,
            IReadOnlyList<ItemDefinition> weaponItems)
        {
            var mergedItems = new List<ItemDefinition>();
            AppendExistingSpawnerItems(itemsProp, mergedItems);
            AppendDistinctItems(weaponItems, mergedItems);
            return mergedItems;
        }

        private static void AppendExistingSpawnerItems(SerializedProperty itemsProp, List<ItemDefinition> mergedItems)
        {
            for (var index = 0; index < itemsProp.arraySize; index++)
            {
                var existing = itemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemDefinition;
                if (existing == null || mergedItems.Contains(existing)) continue;

                mergedItems.Add(existing);
            }
        }

        private static void AppendDistinctItems(IReadOnlyList<ItemDefinition> sourceItems, List<ItemDefinition> destination)
        {
            if (sourceItems == null) return;

            for (var index = 0; index < sourceItems.Count; index++)
            {
                var item = sourceItems[index];
                if (item == null || destination.Contains(item)) continue;

                destination.Add(item);
            }
        }

        private static void ApplyMergedItemsToInventoryProperty(
            SerializedProperty itemsProp,
            List<ItemDefinition> mergedItems)
        {
            itemsProp.arraySize = mergedItems.Count;
            for (var index = 0; index < mergedItems.Count; index++)
                itemsProp.GetArrayElementAtIndex(index).objectReferenceValue = mergedItems[index];
        }

        private static void MarkSpawnerDirty(PlayerSpawner spawner)
        {
            EditorUtility.SetDirty(spawner);
            if (spawner.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }

        private static int EnableDevSpawnInventoryInDebugSettings()
        {
            var synced = 0;
            var debugSettingsGuids = AssetDatabase.FindAssets("t:DebugSettings");

            for (var i = 0; i < debugSettingsGuids.Length; i++)
            {
                var debugSettingsPath = AssetDatabase.GUIDToAssetPath(debugSettingsGuids[i]);
                var settings = AssetDatabase.LoadAssetAtPath<DebugSettings>(debugSettingsPath);
                if (settings == null) continue;

                var so = new SerializedObject(settings);
                var prop = so.FindProperty("enableDevSpawnFullInventory");
                if (prop == null) continue;

                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                synced++;
            }

            return synced;
        }
    }
}
#endif
