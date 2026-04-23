using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Data;
using Zombera.Inventory;

namespace Zombera.Editor
{
    /// <summary>
    /// Bootstraps Bow item authoring, visuals, pickup prefab wiring, and combat linkage.
    /// </summary>
    public static class InventoryItemIconSetupTool
    {
        private const string ItemsFolderPath = "Assets/ScriptableObjects/Items";
        private const string WeaponsFolderPath = "Assets/ScriptableObjects/Weapons";
        private const string PickupsFolderPath = "Assets/Prefabs/Inventory/Pickups";
        private const string BowItemAssetPath = "Assets/ScriptableObjects/Items/Item_Bow.asset";
        private const string BowWeaponAssetPath = "Assets/ScriptableObjects/Weapons/Weapon_Bow.asset";
        private const string BowPickupPrefabPath = "Assets/Prefabs/Inventory/Pickups/Pickup_Bow.prefab";
        private const string ThirdPartyBowVisualModelPath = "Assets/ThirdParty/Free medieval weapons/Models/Wooden Bow.fbx";
        private const string ThirdPartyBowVisualPrefabPath = "Assets/ThirdParty/Free medieval weapons/Prefabs/Wooden Bow.prefab";
        private const string ThirdPartyArrowVisualPrefabPath = "Assets/ThirdParty/Free medieval weapons/Prefabs/Arrow.prefab";

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Bootstrap Item Icon Pipeline (Bow)")]
        public static void BootstrapItemIconPipelineBow()
        {
            BootstrapBowGameplayPipeline();
        }

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Bootstrap Bow Gameplay Pipeline (Icon + Equip + Pickup)")]
        public static void BootstrapBowGameplayPipeline()
        {
            EnsureBowAssets(out ItemDefinition bowItem, out WeaponData bowWeapon, out bool createdItemAsset, out bool createdWeaponAsset);
            EnsureBowPickupPrefab(bowItem, out bool createdPickupPrefab);

            bool assignedIcon = false;
            if (TryGetSelectedSprite(out Sprite selectedSprite))
            {
                bowItem.inventoryIcon = selectedSprite;
                assignedIcon = true;
            }

            bool hasSelectedPrefab = TryGetSelectedPrefab(out GameObject selectedPrefab);
            bool assignedSelectedPrefabToVisuals = false;
            if (hasSelectedPrefab)
            {
                if (bowItem.equippedVisualPrefab == null)
                {
                    bowItem.equippedVisualPrefab = selectedPrefab;
                    assignedSelectedPrefabToVisuals = true;
                }

                if (bowItem.worldPickupPrefab == null)
                {
                    bowItem.worldPickupPrefab = selectedPrefab;
                    assignedSelectedPrefabToVisuals = true;
                }
            }

            bool assignedDefaultThirdPartyBowToVisuals = TryAssignDefaultThirdPartyBowPrefabIfEmpty(bowItem);

            bowItem.equippedWeaponData = bowWeapon;
            EditorUtility.SetDirty(bowItem);
            EditorUtility.SetDirty(bowWeapon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = bowItem;
            EditorGUIUtility.PingObject(bowItem);

            string iconStatus = assignedIcon
                ? "Bow icon assigned from selected Sprite."
                : "No Sprite selected; icon remains unchanged.";
            string visualStatus;
            if (assignedSelectedPrefabToVisuals)
            {
                visualStatus = "Assigned selected Prefab to any empty bow visual slots.";
            }
            else if (assignedDefaultThirdPartyBowToVisuals)
            {
                visualStatus = "Assigned default Free medieval weapons Wooden Bow model/prefab to any empty bow visual slots.";
            }
            else if (hasSelectedPrefab)
            {
                visualStatus = "No visual Prefab auto-assigned (existing values already set).";
            }
            else
            {
                visualStatus = "No visual Prefab auto-assigned (existing values kept or default Wooden Bow model/prefab not found).";
            }

            string projectileStatus = "Arrow projectile visual default is applied when running 'Wire Bow Systems On Selected Unit'.";

            string message = "[Zombera] Bow gameplay pipeline bootstrapped.\n"
                + $"  Bow item asset: {BowItemAssetPath} (created: {createdItemAsset})\n"
                + $"  Bow weapon asset: {BowWeaponAssetPath} (created: {createdWeaponAsset})\n"
                + $"  Bow pickup prefab: {BowPickupPrefabPath} (created: {createdPickupPrefab})\n"
                + $"  {iconStatus}\n"
                + $"  {visualStatus}\n"
                + $"  {projectileStatus}\n"
                + "  Optional: run 'Wire Bow Systems On Selected Unit' on a player prefab/object.";

            Debug.Log(message, bowItem);
            EditorUtility.DisplayDialog(
                "Zombera Bow Gameplay Setup",
                message,
                "OK");
        }

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Assign Selected Sprite To Bow Item")]
        public static void AssignSelectedSpriteToBowItem()
        {
            if (!TryGetSelectedSprite(out Sprite selectedSprite))
            {
                EditorUtility.DisplayDialog(
                    "Assign Bow Icon",
                    "Select a Sprite asset first, then run this command again.",
                    "OK");
                return;
            }

            EnsureBowAssets(out ItemDefinition bowItem, out _, out _, out _);

            bowItem.inventoryIcon = selectedSprite;
            EditorUtility.SetDirty(bowItem);
            AssetDatabase.SaveAssets();

            Selection.activeObject = bowItem;
            EditorGUIUtility.PingObject(bowItem);

            Debug.Log($"[Zombera] Assigned Bow icon '{selectedSprite.name}' to {BowItemAssetPath}", bowItem);
        }

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow Equipped Visual")]
        public static void AssignSelectedPrefabToBowEquippedVisual()
        {
            if (!TryGetSelectedPrefab(out GameObject selectedPrefab))
            {
                EditorUtility.DisplayDialog(
                    "Assign Bow Equipped Visual",
                    "Select a Prefab or FBX model asset first, then run this command again.",
                    "OK");
                return;
            }

            EnsureBowAssets(out ItemDefinition bowItem, out _, out _, out _);
            bowItem.equippedVisualPrefab = selectedPrefab;
            EditorUtility.SetDirty(bowItem);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Zombera] Assigned equipped visual '{selectedPrefab.name}' to {BowItemAssetPath}", bowItem);
        }

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow World Pickup Visual")]
        public static void AssignSelectedPrefabToBowWorldPickupVisual()
        {
            if (!TryGetSelectedPrefab(out GameObject selectedPrefab))
            {
                EditorUtility.DisplayDialog(
                    "Assign Bow World Pickup Visual",
                    "Select a Prefab or FBX model asset first, then run this command again.",
                    "OK");
                return;
            }

            EnsureBowAssets(out ItemDefinition bowItem, out _, out _, out _);
            bowItem.worldPickupPrefab = selectedPrefab;
            EditorUtility.SetDirty(bowItem);
            AssetDatabase.SaveAssets();

            EnsureBowPickupPrefab(bowItem, out _);

            Debug.Log($"[Zombera] Assigned world pickup visual '{selectedPrefab.name}' to {BowItemAssetPath}", bowItem);
        }

        [MenuItem("Tools/Zombera/Inventory/Bow/Tools/Wire Bow Systems On Selected Unit")]
        public static void WireBowSystemsOnSelectedUnit()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog(
                    "Wire Bow Systems",
                    "Select a unit GameObject (or child of one) first, then run this command again.",
                    "OK");
                return;
            }

            Unit selectedUnit = Selection.activeGameObject.GetComponentInParent<Unit>();
            GameObject unitRoot = selectedUnit != null ? selectedUnit.gameObject : Selection.activeGameObject;

            WeaponSystem weaponSystem = GetOrAddComponent<WeaponSystem>(unitRoot);
            EquipmentSystem equipmentSystem = GetOrAddComponent<EquipmentSystem>(unitRoot);
            GetOrAddComponentByTypeName(unitRoot, "Zombera.Inventory.ItemPickupInteractor");

            EnsureRightHandSocket(unitRoot);
            EnsureBowAssets(out ItemDefinition bowItem, out WeaponData bowWeapon, out _, out _);
            TryAssignDefaultThirdPartyBowPrefabIfEmpty(bowItem);
            bool wiredArrowProjectileDefaults = TryAssignBowProjectileDefaults(weaponSystem);

            PlayerAnimationController playerAnimationController = unitRoot.GetComponent<PlayerAnimationController>();
            if (playerAnimationController != null)
            {
                SerializedObject animationSerialized = new SerializedObject(playerAnimationController);
                SerializedProperty clipAssetPath = animationSerialized.FindProperty("bowVisualClipAssetPath");
                if (clipAssetPath != null)
                {
                    clipAssetPath.stringValue = ThirdPartyBowVisualModelPath;
                    animationSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(playerAnimationController);
                }
            }

            SerializedObject equipmentSerialized = new SerializedObject(equipmentSystem);
            SerializedProperty weaponSystemProperty = equipmentSerialized.FindProperty("weaponSystem");
            if (weaponSystemProperty != null)
            {
                weaponSystemProperty.objectReferenceValue = weaponSystem;
                equipmentSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (bowItem.equippedWeaponData != bowWeapon)
            {
                bowItem.equippedWeaponData = bowWeapon;
                EditorUtility.SetDirty(bowItem);
            }

            equipmentSystem.Equip(EquipmentSlot.RightHand, bowItem);

            if (Application.isPlaying)
            {
                UnitInventory inventory = GetOrAddComponent<UnitInventory>(unitRoot);
                if (!inventory.HasItem(bowItem))
                {
                    inventory.AddItem(bowItem, 1);
                }
            }

            EditorUtility.SetDirty(unitRoot);
            EditorUtility.SetDirty(weaponSystem);
            EditorUtility.SetDirty(equipmentSystem);
            EditorSceneManager.MarkSceneDirty(unitRoot.scene);
            AssetDatabase.SaveAssets();

            Selection.activeObject = unitRoot;
            EditorGUIUtility.PingObject(unitRoot);

            string runtimeInventoryNote = Application.isPlaying
                ? "Bow item added to UnitInventory if absent."
                : "Inventory add skipped (UnitInventory items are runtime-only in edit mode).";

            string message = "[Zombera] Bow systems wired on selected unit.\n"
                + $"  Unit: {unitRoot.name}\n"
                + "  Ensured components: WeaponSystem, EquipmentSystem, ItemPickupInteractor\n"
                + "  Ensured socket: Socket_RightHand\n"
                + $"  Bow projectile visual: {(wiredArrowProjectileDefaults ? "Arrow.prefab wired" : "unchanged")}\n"
                + "  Equipped Bow into RightHand slot\n"
                + $"  {runtimeInventoryNote}";

            Debug.Log(message, unitRoot);
            EditorUtility.DisplayDialog("Wire Bow Systems", message, "OK");
        }

        private static void EnsureBowAssets(
            out ItemDefinition bowItem,
            out WeaponData bowWeapon,
            out bool createdItemAsset,
            out bool createdWeaponAsset)
        {
            EnsureFolderRecursive(ItemsFolderPath);
            EnsureFolderRecursive(WeaponsFolderPath);

            bowWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>(BowWeaponAssetPath);
            createdWeaponAsset = false;

            if (bowWeapon == null)
            {
                bowWeapon = ScriptableObject.CreateInstance<WeaponData>();
                bowWeapon.weaponId = "weapon_bow_default";
                bowWeapon.displayName = "Bow";
                bowWeapon.weaponCategory = WeaponCategory.Bow;
                bowWeapon.baseDamage = 12f;
                bowWeapon.effectiveRange = 25f;
                bowWeapon.fireRate = 1f;
                bowWeapon.magazineSize = 1;
                bowWeapon.reloadTimeSeconds = 0.9f;
                bowWeapon.spreadAngle = 1.5f;
                bowWeapon.animationProfileId = "bow";

                AssetDatabase.CreateAsset(bowWeapon, BowWeaponAssetPath);
                createdWeaponAsset = true;
            }

            bool bowWeaponChanged = false;
            if (bowWeapon.weaponCategory != WeaponCategory.Bow)
            {
                bowWeapon.weaponCategory = WeaponCategory.Bow;
                bowWeaponChanged = true;
            }

            if (string.IsNullOrWhiteSpace(bowWeapon.animationProfileId))
            {
                bowWeapon.animationProfileId = "bow";
                bowWeaponChanged = true;
            }

            if (bowWeaponChanged)
            {
                EditorUtility.SetDirty(bowWeapon);
            }

            bowItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(BowItemAssetPath);
            createdItemAsset = false;

            if (bowItem == null)
            {
                bowItem = ScriptableObject.CreateInstance<ItemDefinition>();
                bowItem.itemId = "weapon_bow";
                bowItem.displayName = "Bow";
                bowItem.itemType = ItemType.Weapon;
                bowItem.weight = 2f;
                bowItem.stackable = false;
                bowItem.maxStack = 1;

                AssetDatabase.CreateAsset(bowItem, BowItemAssetPath);
                createdItemAsset = true;
            }

            if (bowItem.equippedWeaponData == null)
            {
                bowItem.equippedWeaponData = bowWeapon;
                EditorUtility.SetDirty(bowItem);
            }
        }

        private static void EnsureBowPickupPrefab(ItemDefinition bowItem, out bool createdPrefab)
        {
            EnsureFolderRecursive(PickupsFolderPath);

            createdPrefab = false;
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BowPickupPrefabPath);

            if (pickupPrefab == null)
            {
                GameObject tempRoot = new GameObject("Pickup_Bow");
                tempRoot.AddComponent<SphereCollider>();
                tempRoot.AddComponent<ItemPickup>();

                PrefabUtility.SaveAsPrefabAsset(tempRoot, BowPickupPrefabPath);
                Object.DestroyImmediate(tempRoot);
                createdPrefab = true;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BowPickupPrefabPath);
            try
            {
                SphereCollider trigger = GetOrAddComponent<SphereCollider>(prefabRoot);
                trigger.isTrigger = true;
                trigger.radius = 0.9f;

                ItemPickup pickup = GetOrAddComponent<ItemPickup>(prefabRoot);
                SerializedObject pickupSerialized = new SerializedObject(pickup);

                SerializedProperty itemDefProp = pickupSerialized.FindProperty("itemDefinition");
                SerializedProperty quantityProp = pickupSerialized.FindProperty("quantity");
                SerializedProperty spawnVisualProp = pickupSerialized.FindProperty("spawnVisualFromItemDefinition");
                SerializedProperty destroyOnPickupProp = pickupSerialized.FindProperty("destroyOnPickup");

                if (itemDefProp != null)
                {
                    itemDefProp.objectReferenceValue = bowItem;
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
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, BowPickupPrefabPath);
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

        private static Component GetOrAddComponentByTypeName(GameObject target, string fullyQualifiedTypeName)
        {
            if (target == null || string.IsNullOrWhiteSpace(fullyQualifiedTypeName))
            {
                return null;
            }

            System.Type componentType = System.Type.GetType($"{fullyQualifiedTypeName}, Assembly-CSharp")
                ?? System.Type.GetType(fullyQualifiedTypeName);

            if (componentType == null)
            {
                Debug.LogWarning($"[Zombera] Could not resolve component type '{fullyQualifiedTypeName}'.", target);
                return null;
            }

            Component component = target.GetComponent(componentType);
            if (component == null)
            {
                component = target.AddComponent(componentType);
            }

            return component;
        }

        private static void EnsureRightHandSocket(GameObject unitRoot)
        {
            Transform existingSocket = FindChildRecursive(unitRoot.transform, "Socket_RightHand");
            if (existingSocket != null)
            {
                return;
            }

            Transform parent = unitRoot.transform;
            Animator animator = unitRoot.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null)
                {
                    parent = rightHand;
                }
            }

            GameObject socket = new GameObject("Socket_RightHand");
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = Vector3.zero;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = Selection.activeObject as GameObject;
            if (prefab == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path))
            {
                prefab = null;
                return false;
            }

            string lowerPath = path.ToLowerInvariant();
            if (!lowerPath.EndsWith(".prefab") && !lowerPath.EndsWith(".fbx"))
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

        private static bool TryAssignDefaultThirdPartyBowPrefabIfEmpty(ItemDefinition bowItem)
        {
            if (bowItem == null)
            {
                return false;
            }

            if (bowItem.equippedVisualPrefab != null && bowItem.worldPickupPrefab != null)
            {
                return false;
            }

            GameObject defaultBowVisual = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyBowVisualModelPath);
            if (defaultBowVisual == null)
            {
                defaultBowVisual = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyBowVisualPrefabPath);
            }

            if (defaultBowVisual == null)
            {
                return false;
            }

            bool changed = false;

            if (bowItem.equippedVisualPrefab == null)
            {
                bowItem.equippedVisualPrefab = defaultBowVisual;
                changed = true;
            }

            if (bowItem.worldPickupPrefab == null)
            {
                bowItem.worldPickupPrefab = defaultBowVisual;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(bowItem);
            }

            return changed;
        }

        private static bool TryAssignBowProjectileDefaults(WeaponSystem weaponSystem)
        {
            if (weaponSystem == null)
            {
                return false;
            }

            GameObject arrowVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyArrowVisualPrefabPath);
            if (arrowVisualPrefab == null)
            {
                return false;
            }

            SerializedObject weaponSerialized = new SerializedObject(weaponSystem);
            SerializedProperty bowArrowVisualPrefab = weaponSerialized.FindProperty("bowArrowVisualPrefab");
            SerializedProperty preferBowArrowVisualPrefab = weaponSerialized.FindProperty("preferBowArrowVisualPrefab");
            SerializedProperty enableRuntimeFallback = weaponSerialized.FindProperty("enableRuntimeBowProjectileFallback");

            bool changed = false;

            if (bowArrowVisualPrefab != null && bowArrowVisualPrefab.objectReferenceValue != arrowVisualPrefab)
            {
                bowArrowVisualPrefab.objectReferenceValue = arrowVisualPrefab;
                changed = true;
            }

            if (preferBowArrowVisualPrefab != null && !preferBowArrowVisualPrefab.boolValue)
            {
                preferBowArrowVisualPrefab.boolValue = true;
                changed = true;
            }

            if (enableRuntimeFallback != null && !enableRuntimeFallback.boolValue)
            {
                enableRuntimeFallback.boolValue = true;
                changed = true;
            }

            if (changed)
            {
                weaponSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weaponSystem);
            }

            return changed;
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