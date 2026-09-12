#region

using System;
using UnityEngine;
using UnityEngine.Serialization;
using Zombera.Characters;
using Zombera.Data;

#endregion

namespace Zombera.Inventory
{
    public enum ItemRarityTier
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,

        // ReSharper disable once UnusedMember.Global
        Epic = 3,
        Legendary = 4
    }

    public enum ItemType
    {
        Generic,
        Food,
        Vitamin,
        Medical,
        Weapon,
        Ammo,
        Material
    }

    /// <summary>
    ///     Specialized slots for weapon placement on the body.
    /// </summary>
    public enum WeaponSlot
    {
        Hand_R,
        Hand_L,
        Back,
        Hip_R,
        Hip_L,
        Chest
    }

    /// <summary>
    ///     Per-slot visual transformation for weapons.
    /// </summary>
    [Serializable]
    public struct WeaponVisualOffset
    {
        public WeaponSlot slot;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public static WeaponVisualOffset Default(WeaponSlot slot) => new()
        {
            slot = slot,
            position = Vector3.zero,
            rotation = Vector3.zero,
            scale = Vector3.one
        };
    }

    /// <summary>
    ///     Scriptable item definition used by inventory, loot, equipment, and save systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(3, 10)] public string description;

        [Header("UI")] [Tooltip("Inventory icon shown in squad and HUD inventory views.")]
        public Sprite inventoryIcon;

        [Header("Visuals")] [Tooltip("Prefab attached to the unit socket when this item is equipped.")]
        public GameObject equippedVisualPrefab;

        [Tooltip("Local position offset applied to the equipped visual relative to the resolved socket.")]
        public Vector3 equippedVisualLocalPosition = Vector3.zero;

        [Tooltip("Local Euler rotation (degrees) applied to the equipped visual relative to the resolved socket.")]
        public Vector3 equippedVisualLocalEulerAngles = Vector3.zero;

        [Tooltip("Local scale multiplier applied to the equipped visual.")]
        public Vector3 equippedVisualLocalScale = Vector3.one;

        [Tooltip("Names of body parts (GameObjects) to hide when this item is equipped.")]
        public string[] hiddenBodyParts = Array.Empty<string>();

        [Tooltip("Prefab shown on ground/world pickups for this item.")]
        public GameObject worldPickupPrefab;

        [Header("Combat")] [Tooltip("Optional weapon data applied to WeaponSystem when equipped in a hand slot.")]
        public WeaponData equippedWeaponData;

        [Header("Armor")] [Tooltip("Optional armor data applied while this item is equipped.")]
        public ArmorData equippedArmorData;

        [Header("Equipment")] [Tooltip("When enabled, this item can only be equipped into the selected slot.")]
        public bool enforceSpecificEquipSlot;

        [Tooltip("Slot used when enforceSpecificEquipSlot is enabled.")]
        public EquipmentSlot forcedEquipSlot = EquipmentSlot.RightHand;

        [Header("Wardrobe")]
        [Tooltip("Optional wardrobe asset reference kept for migration compatibility.")]
        [FormerlySerializedAs("umaWardrobeRecipe")]
        public UnityEngine.Object appearanceWardrobeRecipe;

        [Tooltip("Optional wardrobe recipe name fallback used when no recipe asset reference is assigned.")]
        [FormerlySerializedAs("umaWardrobeRecipeName")]
        public string appearanceWardrobeRecipeName = string.Empty;

        [Tooltip("Optional wardrobe slot override. Leave empty to use the default mapping from EquipmentSlot.")]
        [FormerlySerializedAs("umaWardrobeSlotOverride")]
        public string appearanceWardrobeSlotOverride = string.Empty;

        [Header("Inventory")] public ItemType itemType;

        public float weight = 1f;
        public bool stackable = true;
        public int maxStack = 99;

        [Header("Consumable")] [Min(0f)] public float healAmount;

        public float mealQuality = 1f;

        [Header("Economy & Classification")] [Tooltip("Base barter value in currency units.")] [Min(0)]
        public int economyValue = 10;

        public ItemRarityTier rarity = ItemRarityTier.Common;

        [Tooltip("Freeform tags used for crafting recipes, loot conditions, and UI filtering.")]
        public string[] tags = Array.Empty<string>();

        [Header("Equipment Stat Bonuses")]
        [Tooltip("Flat bonuses applied to UnitStats.GetSkillValue() while this item is equipped.")]
        public ItemStatBonus[] statBonuses;

        [Header("Weapon Placement")]
        [Tooltip("Per-slot visual offsets. Used by WeaponSystem to position the weapon in holsters or hands.")]
        public WeaponVisualOffset[] slotOffsets;
        }

    /// <summary>Flat skill bonus applied while an item is equipped.</summary>
    [Serializable]
    public struct ItemStatBonus
    {
        public UnitSkillType skill;
        public int flatBonus;
    }

    /// <summary>
    ///     Runtime stack record for list-based inventories.
    /// </summary>
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition item;
        public int quantity;

        public ItemStack(ItemDefinition itemDefinition, int stackQuantity)
        {
            item = itemDefinition;
            quantity = stackQuantity;
        }

        public float GetTotalWeight()
        {
            if (item == null) return 0f;

            return item.weight * quantity;
        }
    }
}