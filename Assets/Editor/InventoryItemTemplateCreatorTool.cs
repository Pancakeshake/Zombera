using UnityEditor;
using UnityEngine;
using Zombera.Combat;
using Zombera.Data;
using Zombera.Inventory;

namespace Zombera.Editor
{
    /// <summary>
    /// Creates reusable inventory item templates and arrow-specific bootstrap assets.
    /// </summary>
    public static class InventoryItemTemplateCreatorTool
    {
        private const string ItemsFolderPath = "Assets/ScriptableObjects/Items";
        private const string WeaponsFolderPath = "Assets/ScriptableObjects/Weapons";
        private const string PickupsFolderPath = "Assets/Prefabs/Inventory/Pickups";

        private const string ArrowItemAssetPath = "Assets/ScriptableObjects/Items/Item_Arrow.asset";
        private const string ArrowPickupPrefabPath = "Assets/Prefabs/Inventory/Pickups/Pickup_Arrow.prefab";

        private enum ItemTemplateKind
        {
            Generic,
            Weapon,
            Ammo,
            Medical,
            Food,
            Vitamin,
            Material,
            Arrow
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Generic Item Template")]
        public static void CreateGenericItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Generic);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Weapon Item Template (With WeaponData)")]
        public static void CreateWeaponItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Weapon);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Ammo Item Template")]
        public static void CreateAmmoItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Ammo);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Medical Item Template")]
        public static void CreateMedicalItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Medical);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Food Item Template")]
        public static void CreateFoodItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Food);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Vitamin Item Template")]
        public static void CreateVitaminItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Vitamin);
        }

        [MenuItem("Tools/Zombera/Inventory/Creators/Tools/Create Material Item Template")]
        public static void CreateMaterialItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Material);
        }

        [MenuItem("Tools/Zombera/Inventory/Arrow/Tools/Create Arrow Item Template")]
        public static void CreateArrowItemTemplate()
        {
            CreateItemTemplate(ItemTemplateKind.Arrow);
        }

        [MenuItem("Tools/Zombera/Inventory/Arrow/Tools/Bootstrap Arrow Item Pipeline (Item + Pickup)")]
        public static void BootstrapArrowItemPipeline()
        {
            EnsureArrowAsset(out ItemDefinition arrowItem, out bool createdArrowItem);
            EnsureArrowPickupPrefab(arrowItem, out bool createdArrowPickup);

            bool assignedIcon = false;
            if (TryGetSelectedSprite(out Sprite selectedSprite))
            {
                arrowItem.inventoryIcon = selectedSprite;
                EditorUtility.SetDirty(arrowItem);
                assignedIcon = true;
            }

            bool assignedPickupPrefab = false;
            if (TryGetSelectedPrefab(out GameObject selectedPrefab) && arrowItem.worldPickupPrefab == null)
            {
                arrowItem.worldPickupPrefab = selectedPrefab;
                EditorUtility.SetDirty(arrowItem);
                assignedPickupPrefab = true;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = arrowItem;
            EditorGUIUtility.PingObject(arrowItem);

            string message = "[Zombera] Arrow pipeline bootstrapped.\n"
                + $"  Arrow item asset: {ArrowItemAssetPath} (created: {createdArrowItem})\n"
                + $"  Arrow pickup prefab: {ArrowPickupPrefabPath} (created: {createdArrowPickup})\n"
                + (assignedIcon ? "  Arrow icon assigned from selected Sprite.\n" : "  Arrow icon unchanged (no Sprite selected).\n")
                + (assignedPickupPrefab ? "  Arrow world pickup visual assigned from selected Prefab." : "  Arrow world pickup visual unchanged.");

            Debug.Log(message, arrowItem);
            EditorUtility.DisplayDialog("Zombera Arrow Setup", message, "OK");
        }

        [MenuItem("Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Sprite To Arrow Item")]
        public static void AssignSelectedSpriteToArrowItem()
        {
            if (!TryGetSelectedSprite(out Sprite selectedSprite))
            {
                EditorUtility.DisplayDialog(
                    "Assign Arrow Icon",
                    "Select a Sprite asset first, then run this command again.",
                    "OK");
                return;
            }

            EnsureArrowAsset(out ItemDefinition arrowItem, out _);
            arrowItem.inventoryIcon = selectedSprite;
            EditorUtility.SetDirty(arrowItem);
            AssetDatabase.SaveAssets();

            Selection.activeObject = arrowItem;
            EditorGUIUtility.PingObject(arrowItem);
            Debug.Log($"[Zombera] Assigned Arrow icon '{selectedSprite.name}' to {ArrowItemAssetPath}", arrowItem);
        }

        [MenuItem("Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Prefab To Arrow World Pickup Visual")]
        public static void AssignSelectedPrefabToArrowWorldPickupVisual()
        {
            if (!TryGetSelectedPrefab(out GameObject selectedPrefab))
            {
                EditorUtility.DisplayDialog(
                    "Assign Arrow World Pickup Visual",
                    "Select a Prefab asset first, then run this command again.",
                    "OK");
                return;
            }

            EnsureArrowAsset(out ItemDefinition arrowItem, out _);
            arrowItem.worldPickupPrefab = selectedPrefab;
            EditorUtility.SetDirty(arrowItem);
            AssetDatabase.SaveAssets();

            EnsureArrowPickupPrefab(arrowItem, out _);
            Debug.Log($"[Zombera] Assigned Arrow world pickup visual '{selectedPrefab.name}' to {ArrowItemAssetPath}", arrowItem);
        }

        private static void CreateItemTemplate(ItemTemplateKind kind)
        {
            EnsureFolderRecursive(ItemsFolderPath);
            EnsureFolderRecursive(WeaponsFolderPath);

            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            ApplyItemTemplateDefaults(item, kind);

            if (TryGetSelectedSprite(out Sprite selectedSprite))
            {
                item.inventoryIcon = selectedSprite;
            }

            if (TryGetSelectedPrefab(out GameObject selectedPrefab))
            {
                if (kind == ItemTemplateKind.Weapon && item.equippedVisualPrefab == null)
                {
                    item.equippedVisualPrefab = selectedPrefab;
                }

                if (item.worldPickupPrefab == null)
                {
                    item.worldPickupPrefab = selectedPrefab;
                }
            }

            string itemNameStem = ResolveTemplateNameStem(kind);
            string itemAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{ItemsFolderPath}/Item_{itemNameStem}.asset");
            AssetDatabase.CreateAsset(item, itemAssetPath);

            string createdWeaponPath = null;
            if (kind == ItemTemplateKind.Weapon)
            {
                WeaponData weaponData = ScriptableObject.CreateInstance<WeaponData>();
                ApplyWeaponTemplateDefaults(weaponData, item);

                createdWeaponPath = AssetDatabase.GenerateUniqueAssetPath($"{WeaponsFolderPath}/Weapon_{itemNameStem}.asset");
                AssetDatabase.CreateAsset(weaponData, createdWeaponPath);

                item.equippedWeaponData = weaponData;
                item.enforceSpecificEquipSlot = true;
                item.forcedEquipSlot = EquipmentSlot.RightHand;
                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);

            string resultMessage = createdWeaponPath == null
                ? $"[Zombera] Created item template: {itemAssetPath}"
                : "[Zombera] Created weapon template assets.\n"
                    + $"  Item: {itemAssetPath}\n"
                    + $"  WeaponData: {createdWeaponPath}\n"
                    + "  WeaponData linked on ItemDefinition.equippedWeaponData.";

            Debug.Log(resultMessage, item);
            EditorUtility.DisplayDialog("Create Inventory Template", resultMessage, "OK");
        }

        private static void ApplyItemTemplateDefaults(ItemDefinition item, ItemTemplateKind kind)
        {
            item.tags = System.Array.Empty<string>();
            item.statBonuses = System.Array.Empty<ItemStatBonus>();
            item.equippedVisualLocalPosition = Vector3.zero;
            item.equippedVisualLocalEulerAngles = Vector3.zero;
            item.equippedVisualLocalScale = Vector3.one;

            switch (kind)
            {
                case ItemTemplateKind.Weapon:
                    item.itemId = "weapon_new";
                    item.displayName = "New Weapon";
                    item.itemType = ItemType.Weapon;
                    item.weight = 2f;
                    item.stackable = false;
                    item.maxStack = 1;
                    item.economyValue = 50;
                    break;

                case ItemTemplateKind.Ammo:
                    item.itemId = "ammo_new";
                    item.displayName = "New Ammo";
                    item.itemType = ItemType.Ammo;
                    item.weight = 0.05f;
                    item.stackable = true;
                    item.maxStack = 120;
                    item.economyValue = 2;
                    break;

                case ItemTemplateKind.Medical:
                    item.itemId = "medical_new";
                    item.displayName = "New Medical";
                    item.itemType = ItemType.Medical;
                    item.weight = 0.3f;
                    item.stackable = true;
                    item.maxStack = 20;
                    item.healAmount = 25f;
                    item.economyValue = 30;
                    break;

                case ItemTemplateKind.Food:
                    item.itemId = "food_new";
                    item.displayName = "New Food";
                    item.itemType = ItemType.Food;
                    item.weight = 0.4f;
                    item.stackable = true;
                    item.maxStack = 25;
                    item.mealQuality = 1f;
                    item.economyValue = 8;
                    break;

                case ItemTemplateKind.Vitamin:
                    item.itemId = "vitamin_new";
                    item.displayName = "New Vitamin";
                    item.itemType = ItemType.Vitamin;
                    item.weight = 0.1f;
                    item.stackable = true;
                    item.maxStack = 30;
                    item.economyValue = 12;
                    break;

                case ItemTemplateKind.Material:
                    item.itemId = "material_new";
                    item.displayName = "New Material";
                    item.itemType = ItemType.Material;
                    item.weight = 0.35f;
                    item.stackable = true;
                    item.maxStack = 99;
                    item.economyValue = 5;
                    break;

                case ItemTemplateKind.Arrow:
                    item.itemId = "ammo_arrow";
                    item.displayName = "Arrow";
                    item.itemType = ItemType.Ammo;
                    item.weight = 0.05f;
                    item.stackable = true;
                    item.maxStack = 50;
                    item.economyValue = 1;
                    break;

                default:
                    item.itemId = "item_new";
                    item.displayName = "New Item";
                    item.itemType = ItemType.Generic;
                    item.weight = 1f;
                    item.stackable = true;
                    item.maxStack = 99;
                    item.economyValue = 10;
                    break;
            }
        }

        private static void ApplyWeaponTemplateDefaults(WeaponData weaponData, ItemDefinition item)
        {
            weaponData.weaponId = "weapon_new_data";
            weaponData.displayName = item != null && !string.IsNullOrWhiteSpace(item.displayName)
                ? item.displayName
                : "New Weapon";
            weaponData.weaponCategory = WeaponCategory.Rifle;
            weaponData.baseDamage = 10f;
            weaponData.effectiveRange = 20f;
            weaponData.fireRate = 2f;
            weaponData.magazineSize = 12;
            weaponData.reloadTimeSeconds = 1.5f;
            weaponData.spreadAngle = 2f;
            weaponData.animationProfileId = "weapon";
        }

        private static string ResolveTemplateNameStem(ItemTemplateKind kind)
        {
            return kind switch
            {
                ItemTemplateKind.Weapon => "Weapon_New",
                ItemTemplateKind.Ammo => "Ammo_New",
                ItemTemplateKind.Medical => "Medical_New",
                ItemTemplateKind.Food => "Food_New",
                ItemTemplateKind.Vitamin => "Vitamin_New",
                ItemTemplateKind.Material => "Material_New",
                ItemTemplateKind.Arrow => "Arrow_New",
                _ => "Generic_New",
            };
        }

        private static void EnsureArrowAsset(out ItemDefinition arrowItem, out bool createdArrowItem)
        {
            EnsureFolderRecursive(ItemsFolderPath);

            arrowItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ArrowItemAssetPath);
            createdArrowItem = false;

            if (arrowItem == null)
            {
                arrowItem = ScriptableObject.CreateInstance<ItemDefinition>();
                ApplyItemTemplateDefaults(arrowItem, ItemTemplateKind.Arrow);
                AssetDatabase.CreateAsset(arrowItem, ArrowItemAssetPath);
                createdArrowItem = true;
            }
            else
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(arrowItem.itemId))
                {
                    arrowItem.itemId = "ammo_arrow";
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(arrowItem.displayName))
                {
                    arrowItem.displayName = "Arrow";
                    changed = true;
                }

                if (arrowItem.itemType != ItemType.Ammo)
                {
                    arrowItem.itemType = ItemType.Ammo;
                    changed = true;
                }

                if (arrowItem.maxStack <= 0)
                {
                    arrowItem.maxStack = 50;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(arrowItem);
                }
            }
        }

        private static void EnsureArrowPickupPrefab(ItemDefinition arrowItem, out bool createdPrefab)
        {
            EnsureFolderRecursive(PickupsFolderPath);

            createdPrefab = false;
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPickupPrefabPath);

            if (pickupPrefab == null)
            {
                GameObject tempRoot = new GameObject("Pickup_Arrow");
                tempRoot.AddComponent<SphereCollider>();
                tempRoot.AddComponent<ItemPickup>();

                PrefabUtility.SaveAsPrefabAsset(tempRoot, ArrowPickupPrefabPath);
                Object.DestroyImmediate(tempRoot);
                createdPrefab = true;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ArrowPickupPrefabPath);
            try
            {
                SphereCollider trigger = GetOrAddComponent<SphereCollider>(prefabRoot);
                trigger.isTrigger = true;
                trigger.radius = 0.75f;

                ItemPickup pickup = GetOrAddComponent<ItemPickup>(prefabRoot);
                SerializedObject pickupSerialized = new SerializedObject(pickup);

                SerializedProperty itemDefProp = pickupSerialized.FindProperty("itemDefinition");
                SerializedProperty quantityProp = pickupSerialized.FindProperty("quantity");
                SerializedProperty spawnVisualProp = pickupSerialized.FindProperty("spawnVisualFromItemDefinition");
                SerializedProperty destroyOnPickupProp = pickupSerialized.FindProperty("destroyOnPickup");

                if (itemDefProp != null)
                {
                    itemDefProp.objectReferenceValue = arrowItem;
                }

                if (quantityProp != null)
                {
                    quantityProp.intValue = 1;
                }

                if (spawnVisualProp != null)
                {
                    spawnVisualProp.boolValue = true;
                }

                if (destroyOnPickupProp != null)
                {
                    destroyOnPickupProp.boolValue = true;
                }

                pickupSerialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ArrowPickupPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = Selection.activeObject as GameObject;
            if (prefab == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab"))
            {
                prefab = null;
                return false;
            }

            GameObject loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (loadedPrefab == null)
            {
                prefab = null;
                return false;
            }

            prefab = loadedPrefab;
            return true;
        }

        private static bool TryGetSelectedSprite(out Sprite sprite)
        {
            sprite = Selection.activeObject as Sprite;
            if (sprite != null)
            {
                return true;
            }

            Texture2D texture = Selection.activeObject as Texture2D;
            if (texture == null)
            {
                return false;
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                return false;
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                Sprite subSprite = subAssets[i] as Sprite;
                if (subSprite != null)
                {
                    sprite = subSprite;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int lastSlashIndex = folderPath.LastIndexOf('/');
            if (lastSlashIndex <= 0)
            {
                return;
            }

            string parentPath = folderPath.Substring(0, lastSlashIndex);
            string folderName = folderPath.Substring(lastSlashIndex + 1);

            EnsureFolderRecursive(parentPath);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
    }
}
