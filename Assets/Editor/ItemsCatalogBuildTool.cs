#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    ///     Idempotent inventory-content builder:
    ///     - scans weapon prefabs
    ///     - creates/updates ItemDefinition + WeaponData assets
    ///     - overwrites inventory icons from Generated icon folder
    ///     - syncs PlayerSpawner dev spawn inventory so player starts with all created items in debug mode
    /// </summary>
    public static class ItemsCatalogBuildTool
    {
        private const string MenuRoot = "Tools/Items/";
        private const string BuildMenuPath = MenuRoot + "Build Items + Weapons From Prefabs And Wire Spawn Inventory";

        private static readonly string[] PrefabRoots = 
        {
            "Assets/02_Shared/Prefabs/Weapons",
            "Assets/01_Game/05_Combat/Weapons"
        };

        private const string WrapperFolderName = "1.EquipWrappers";
        private const string LegacyWrapperFolderName = "EquipWrappers";
        private const string ItemsAssetRoot = "Assets/ScriptableObjects/Items";
        private const string WeaponsAssetRoot = "Assets/ScriptableObjects/Weapons";
        private const string GeneratedIconsRoot = "Assets/Art/InventoryIcons/Generated";
        private const string ItemSaveRegistryPath = "Assets/ScriptableObjects/Items/ItemSaveRegistry.asset";

        [MenuItem(BuildMenuPath, priority = -500)]
        public static void BuildItemsAndWireSpawnInventory()
        {
            var existingRoots = new List<string>();
            foreach (var root in PrefabRoots)
            {
                if (AssetDatabase.IsValidFolder(root)) existingRoots.Add(root);
            }

            if (existingRoots.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Items Build",
                    "No weapon prefab root folders were found.",
                    "OK");
                return;
            }

            EnsureFolderHierarchy(ItemsAssetRoot);
            EnsureFolderHierarchy(WeaponsAssetRoot);

            var iconLookup = BuildIconLookup();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", existingRoots.ToArray());
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Items Build",
                    "No prefabs found under root folders.",
                    "OK");
                return;
            }

            var createdItems = 0;
            var updatedItems = 0;
            var createdWeapons = 0;
            var updatedWeapons = 0;
            var iconAssignments = 0;
            var skippedPrefabs = 0;
            var skippedWrapperPrefabs = 0;

            foreach (var prefabGuid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (IsWrapperPrefabPath(prefabPath))
                {
                    skippedWrapperPrefabs++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    skippedPrefabs++;
                    continue;
                }

                if (!TryGetRelativePrefabPath(prefabPath, existingRoots, out var relativePathWithoutExtension))
                {
                    skippedPrefabs++;
                    continue;
                }

                var profile = ResolveProfile(prefabPath);
                var displayName = BuildDisplayName(prefab.name);

                var itemAssetPath = BuildAssetPath(ItemsAssetRoot, relativePathWithoutExtension);
                var item = LoadOrCreateAsset(itemAssetPath, out var createdItem, ScriptableObject.CreateInstance<ItemDefinition>);

                if (item == null)
                {
                    skippedPrefabs++;
                    continue;
                }

                if (createdItem) createdItems++;
                else updatedItems++;

                WeaponData weaponData = null;
                if (profile.generateWeaponData)
                {
                    var weaponAssetPath = BuildAssetPath(WeaponsAssetRoot, relativePathWithoutExtension);
                    weaponData = LoadOrCreateAsset(weaponAssetPath, out var createdWeapon,
                        ScriptableObject.CreateInstance<WeaponData>);

                    if (weaponData != null)
                    {
                        if (createdWeapon) createdWeapons++;
                        else updatedWeapons++;

                        ApplyWeaponDataDefaults(weaponData, prefab, displayName, profile, createdWeapon);
                        EditorUtility.SetDirty(weaponData);
                    }
                }

                if (ApplyItemDefinitionDefaults(item, prefab, displayName, profile, weaponData, iconLookup))
                    iconAssignments++;

                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var allItems = LoadAllItemDefinitions();
            var syncedSpawners = SyncPlayerSpawnerInventory(allItems);
            var syncedDebugSettings = EnableDevSpawnInventoryInDebugSettings();
            var syncedDebugManagers = EnableDebugModeOnLoadedDebugManagers();
            var registryUpdated = SyncItemSaveRegistry(allItems);

            var summary =
                "Items/Weapons build complete.\n\n" +
                "Prefabs scanned: " + prefabGuids.Length + "\n" +
                "ItemDefinition created: " + createdItems + "\n" +
                "ItemDefinition updated: " + updatedItems + "\n" +
                "WeaponData created: " + createdWeapons + "\n" +
                "WeaponData updated: " + updatedWeapons + "\n" +
                "Icons assigned/overwritten from Generated: " + iconAssignments + "\n" +
                "Skipped wrapper prefabs: " + skippedWrapperPrefabs + "\n" +
                "Skipped prefabs: " + skippedPrefabs + "\n" +
                "\n" +
                "Total ItemDefinition assets loaded: " + allItems.Count + "\n" +
                "PlayerSpawner components synced: " + syncedSpawners + "\n" +
                "DebugSettings assets synced: " + syncedDebugSettings + "\n" +
                "Loaded DebugManager components enabled: " + syncedDebugManagers + "\n" +
                "ItemSaveRegistry synced: " + (registryUpdated ? "SUCCESS" : "FAILED") + "\n" +
                "\n" +
                "Output folders:\n" +
                "- " + ItemsAssetRoot + "\n" +
                "- " + WeaponsAssetRoot + "\n" +
                "Icons source:\n" +
                "- " + GeneratedIconsRoot;

            Debug.Log("[ItemsCatalogBuildTool] " + summary);
            EditorUtility.DisplayDialog("Items Build", summary, "OK");
            }

            private static bool SyncItemSaveRegistry(List<ItemDefinition> allItems)
            {
            var registry = LoadOrCreateAsset(ItemSaveRegistryPath, out _, ScriptableObject.CreateInstance<ItemSaveRegistry>);
            if (registry == null) return false;

            registry.Clear();
            registry.items.AddRange(allItems);
            registry.Initialize();
            
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return true;
            }

            private static bool TryGetRelativePrefabPath(string prefabPath, List<string> roots, out string relativePathWithoutExtension)
            {
                relativePathWithoutExtension = string.Empty;
                if (string.IsNullOrWhiteSpace(prefabPath)) return false;

                string matchedRoot = null;
                foreach (var root in roots)
                {
                    if (prefabPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedRoot = root;
                        break;
                    }
                }

                if (matchedRoot == null) return false;

                var relativePath = prefabPath[matchedRoot.Length..].TrimStart('/', '\\');
                if (string.IsNullOrWhiteSpace(relativePath)) return false;

                relativePathWithoutExtension = Path.ChangeExtension(relativePath, null)?.Replace('\\', '/');
                return !string.IsNullOrWhiteSpace(relativePathWithoutExtension);
            }

            private static bool IsWrapperPrefabPath(string prefabPath)
            {
                if (string.IsNullOrWhiteSpace(prefabPath)) return false;

                var normalizedPath = NormalizePath(prefabPath);
                return normalizedPath.Contains("/" + WrapperFolderName + "/", StringComparison.OrdinalIgnoreCase)
                       || normalizedPath.EndsWith("/" + WrapperFolderName, StringComparison.OrdinalIgnoreCase)
                       || normalizedPath.Contains("/" + LegacyWrapperFolderName + "/", StringComparison.OrdinalIgnoreCase)
                       || normalizedPath.EndsWith("/" + LegacyWrapperFolderName, StringComparison.OrdinalIgnoreCase);
            }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string BuildAssetPath(string assetRoot, string relativePathWithoutExtension)
        {
            return assetRoot.TrimEnd('/') + "/" + relativePathWithoutExtension.TrimStart('/') + ".asset";
        }

        private static T LoadOrCreateAsset<T>(string assetPath, out bool created, Func<T> create) where T : ScriptableObject
        {
            created = false;

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolderHierarchy(directory);

            var instance = create();
            if (instance == null) return null;

            AssetDatabase.CreateAsset(instance, assetPath);
            created = true;
            return instance;
        }

        private static bool ApplyItemDefinitionDefaults(
            ItemDefinition item,
            GameObject prefab,
            string displayName,
            GenerationProfile profile,
            WeaponData weaponData,
            IReadOnlyDictionary<string, Sprite> iconLookup)
        {
            if (item == null || prefab == null) return false;

            item.itemId = BuildItemId(prefab.name, profile.itemType);
            item.displayName = displayName;
            item.itemType = profile.itemType;
            item.stackable = profile.stackable;
            item.maxStack = profile.maxStack;
            item.weight = item.weight <= 0f ? profile.defaultWeight : item.weight;

            item.worldPickupPrefab = prefab;

            if (profile.forceRightHandEquip)
            {
                item.enforceSpecificEquipSlot = true;
                item.forcedEquipSlot = EquipmentSlot.PrimaryWeapon;
                item.equippedVisualPrefab = prefab;
            }
            else
            {
                item.enforceSpecificEquipSlot = false;

                if (profile.itemType == ItemType.Ammo)
                    item.equippedVisualPrefab = null;
            }

            item.equippedWeaponData = weaponData;

            var iconApplied = false;
            var iconKey = NormalizeKey(prefab.name);
            if (!string.IsNullOrWhiteSpace(iconKey) && iconLookup.TryGetValue(iconKey, out var icon) && icon != null)
            {
                // Explicitly overwrite existing icon assignment with Generated icon output.
                item.inventoryIcon = icon;
                iconApplied = true;
            }

            return iconApplied;
        }

        private static void ApplyWeaponDataDefaults(
            WeaponData weaponData,
            GameObject prefab,
            string displayName,
            GenerationProfile profile,
            bool newlyCreated)
        {
            if (weaponData == null || prefab == null) return;

            weaponData.weaponId = BuildWeaponId(prefab.name);
            weaponData.displayName = displayName;
            weaponData.weaponCategory = profile.weaponCategory;

            if (!newlyCreated) return;

            var defaults = ResolveWeaponDefaults(profile.weaponCategory);
            weaponData.baseDamage = defaults.baseDamage;
            weaponData.effectiveRange = defaults.range;
            weaponData.fireRate = defaults.fireRate;
            weaponData.magazineSize = defaults.magazineSize;
            weaponData.recoilForce = defaults.recoil;
            weaponData.spreadAngle = defaults.spread;
            weaponData.reloadTimeSeconds = defaults.reloadTime;
            weaponData.animationProfileId = defaults.animationProfileId;
        }

        private static IReadOnlyDictionary<string, Sprite> BuildIconLookup()
        {
            var map = new Dictionary<string, Sprite>();
            if (!AssetDatabase.IsValidFolder(GeneratedIconsRoot)) return map;

            var iconGuids = AssetDatabase.FindAssets("t:Sprite", new[] { GeneratedIconsRoot });
            foreach (var iconGuid in iconGuids)
            {
                var iconPath = AssetDatabase.GUIDToAssetPath(iconGuid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null) continue;

                var filenameKey = NormalizeKey(Path.GetFileNameWithoutExtension(iconPath));
                if (!string.IsNullOrWhiteSpace(filenameKey)) map[filenameKey] = sprite;

                var spriteNameKey = NormalizeKey(sprite.name);
                if (!string.IsNullOrWhiteSpace(spriteNameKey)) map[spriteNameKey] = sprite;
            }

            return map;
        }

        private static List<ItemDefinition> LoadAllItemDefinitions()
        {
            var items = new List<ItemDefinition>();
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var itemGuid in itemGuids)
            {
                var itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null || items.Contains(item)) continue;

                items.Add(item);
            }

            return items;
        }

        private static int SyncPlayerSpawnerInventory(IReadOnlyList<ItemDefinition> allItems)
        {
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var syncedCount = 0;

            for (var i = 0; i < spawners.Length; i++)
            {
                var spawner = spawners[i];
                if (spawner == null) continue;

                var so = new SerializedObject(spawner);
                var itemsProp = so.FindProperty("devModeSpawnInventoryItems");
                if (itemsProp != null)
                {
                    itemsProp.arraySize = allItems != null ? allItems.Count : 0;
                    for (var index = 0; index < itemsProp.arraySize; index++)
                        itemsProp.GetArrayElementAtIndex(index).objectReferenceValue = allItems[index];
                }

                SetBoolIfPresent(so, "autoPopulateDevModeSpawnItemsInEditor", true);
                SetBoolIfPresent(so, "logDevModeSpawnInventory", true);
                SetIntIfPresent(so, "devModeSpawnQuantityPerItem", 1);
                SetIntIfPresent(so, "devModeSpawnAmmoQuantityPerItem", 999);

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawner);

                if (spawner.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

                syncedCount++;
            }

            return syncedCount;
        }

        private static int EnableDevSpawnInventoryInDebugSettings()
        {
            var synced = 0;
            var debugSettingsGuids = AssetDatabase.FindAssets("t:DebugSettings");

            foreach (var debugSettingsGuid in debugSettingsGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(debugSettingsGuid);
                var settings = AssetDatabase.LoadAssetAtPath<DebugSettings>(path);
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

        private static int EnableDebugModeOnLoadedDebugManagers()
        {
            var managers = Object.FindObjectsByType<DebugManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var synced = 0;

            foreach (var manager in managers)
            {
                if (manager == null) continue;

                var so = new SerializedObject(manager);
                var prop = so.FindProperty("debugEnabled");
                if (prop == null) continue;

                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);

                if (manager.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

                synced++;
            }

            return synced;
        }

        private static string BuildItemId(string prefabName, ItemType itemType)
        {
            var slug = BuildSlug(prefabName);
            return itemType switch
            {
                ItemType.Weapon => "weapon_" + slug,
                ItemType.Ammo => "ammo_" + slug,
                ItemType.Material => "material_" + slug,
                _ => "item_" + slug
            };
        }

        private static string BuildWeaponId(string prefabName)
        {
            return "weapon_" + BuildSlug(prefabName) + "_default";
        }

        private static string BuildDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "Item";
            var normalized = rawName.Replace('_', ' ').Trim();
            return string.IsNullOrWhiteSpace(normalized) ? rawName.Trim() : normalized;
        }

        private static string BuildSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item";

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
                else if (ch is ' ' or '-' or '_') sb.Append('_');

            var slug = sb.ToString().Trim('_');
            while (slug.Contains("__", StringComparison.Ordinal)) slug = slug.Replace("__", "_");
            return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));

            return sb.ToString();
        }

        private static GenerationProfile ResolveProfile(string prefabPath)
        {
            var lowerPath = (prefabPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

            if (lowerPath.Contains("/ammo/"))
                return new GenerationProfile(ItemType.Ammo, false, WeaponCategory.Bow, true, 200, 0.05f, false);

            if (lowerPath.Contains("/scopes/"))
                return new GenerationProfile(ItemType.Material, false, WeaponCategory.Rifle, false, 1, 0.35f, false);

            if (lowerPath.Contains("/throwables/"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Throwable, false, 1, 1f, true);

            if (lowerPath.Contains("/bows/"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Bow, false, 1, 2f, true);

            if (lowerPath.Contains("/melee weapons/") || lowerPath.Contains("/crafted weapons/") ||
                lowerPath.Contains("/tools/"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Melee, false, 1, 2.5f, true);

            if (lowerPath.Contains("/guns/shotguns/"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Shotgun, false, 1, 3f, true);

            if (lowerPath.Contains("/guns/pistols/"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Pistol, false, 1, 1.4f, true);

            if (lowerPath.Contains("/guns/") || lowerPath.Contains("/legendary weapons"))
                return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Rifle, false, 1, 3.5f, true);

            return new GenerationProfile(ItemType.Weapon, true, WeaponCategory.Melee, false, 1, 2f, true);
        }

        private static WeaponDefaults ResolveWeaponDefaults(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Pistol => new WeaponDefaults(16f, 32f, 5.5f, 15, 0.35f, 2f, 1.4f, "pistol"),
                WeaponCategory.Rifle => new WeaponDefaults(24f, 65f, 8.2f, 30, 0.55f, 2.5f, 2.2f, "rifle"),
                WeaponCategory.Shotgun => new WeaponDefaults(38f, 18f, 1.3f, 8, 0.75f, 5f, 2.8f, "shotgun"),
                WeaponCategory.Melee => new WeaponDefaults(18f, 2f, 1.4f, 1, 0f, 0f, 0f, "melee"),
                WeaponCategory.Throwable => new WeaponDefaults(55f, 22f, 0.7f, 1, 0f, 0f, 1f, "throwable"),
                WeaponCategory.Bow => new WeaponDefaults(12f, 25f, 1f, 1, 0.3f, 1.5f, 0.9f, "bow"),
                _ => new WeaponDefaults(12f, 20f, 1f, 1, 0.2f, 1f, 1f, string.Empty)
            };
        }

        private static void SetBoolIfPresent(SerializedObject so, string propertyName, bool value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetIntIfPresent(SerializedObject so, string propertyName, int value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null) prop.intValue = value;
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal)) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct GenerationProfile
        {
            public readonly ItemType itemType;
            public readonly bool generateWeaponData;
            public readonly WeaponCategory weaponCategory;
            public readonly bool stackable;
            public readonly int maxStack;
            public readonly float defaultWeight;
            public readonly bool forceRightHandEquip;

            public GenerationProfile(
                ItemType itemType,
                bool generateWeaponData,
                WeaponCategory weaponCategory,
                bool stackable,
                int maxStack,
                float defaultWeight,
                bool forceRightHandEquip)
            {
                this.itemType = itemType;
                this.generateWeaponData = generateWeaponData;
                this.weaponCategory = weaponCategory;
                this.stackable = stackable;
                this.maxStack = Mathf.Max(1, maxStack);
                this.defaultWeight = Mathf.Max(0.01f, defaultWeight);
                this.forceRightHandEquip = forceRightHandEquip;
            }
        }

        private readonly struct WeaponDefaults
        {
            public readonly float baseDamage;
            public readonly float range;
            public readonly float fireRate;
            public readonly int magazineSize;
            public readonly float recoil;
            public readonly float spread;
            public readonly float reloadTime;
            public readonly string animationProfileId;

            public WeaponDefaults(
                float baseDamage,
                float range,
                float fireRate,
                int magazineSize,
                float recoil,
                float spread,
                float reloadTime,
                string animationProfileId)
            {
                this.baseDamage = baseDamage;
                this.range = range;
                this.fireRate = fireRate;
                this.magazineSize = Mathf.Max(1, magazineSize);
                this.recoil = Mathf.Max(0f, recoil);
                this.spread = Mathf.Max(0f, spread);
                this.reloadTime = Mathf.Max(0f, reloadTime);
                this.animationProfileId = animationProfileId ?? string.Empty;
            }
        }
    }
}
#endif
