using System;
using UnityEngine;
using Zombera.Characters;
using Zombera.Data;

namespace Zombera.Inventory
{
    public enum ItemRarityTier
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    public enum ItemType
    {
        Generic,
        Food,
        Vitamin,
        Medical,
        Weapon,
        Ammo,
        Material,
    }

    /// <summary>
    /// Scriptable item definition used by inventory, loot, equipment, and save systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [Header("UI")]
        [Tooltip("Inventory icon shown in squad and HUD inventory views.")]
        public Sprite inventoryIcon;

        [Header("Visuals")]
        [Tooltip("Prefab attached to the unit socket when this item is equipped.")]
        public GameObject equippedVisualPrefab;
        [Tooltip("Local position offset applied to the equipped visual relative to the resolved socket.")]
        public Vector3 equippedVisualLocalPosition = Vector3.zero;
        [Tooltip("Local Euler rotation (degrees) applied to the equipped visual relative to the resolved socket.")]
        public Vector3 equippedVisualLocalEulerAngles = Vector3.zero;
        [Tooltip("Local scale multiplier applied to the equipped visual.")]
        public Vector3 equippedVisualLocalScale = Vector3.one;
        [Tooltip("Prefab shown on ground/world pickups for this item.")]
        public GameObject worldPickupPrefab;

        [Header("Combat")]
        [Tooltip("Optional weapon data applied to WeaponSystem when equipped in a hand slot.")]
        public WeaponData equippedWeaponData;

        [Header("Equipment")]
        [Tooltip("When enabled, this item can only be equipped into the selected slot.")]
        public bool enforceSpecificEquipSlot;
        [Tooltip("Slot used when enforceSpecificEquipSlot is enabled.")]
        public EquipmentSlot forcedEquipSlot = EquipmentSlot.RightHand;

        [Header("Inventory")]
        public ItemType itemType;
        public float weight = 1f;
        public bool stackable = true;
        public int maxStack = 99;

        [Header("Consumable")]
        [Min(0f)] public float healAmount;
        public float mealQuality = 1f;

        [Header("Economy & Classification")]
        [Tooltip("Base barter value in currency units.")]
        [Min(0)] public int economyValue = 10;
        public ItemRarityTier rarity = ItemRarityTier.Common;
        [Tooltip("Freeform tags used for crafting recipes, loot conditions, and UI filtering.")]
        public string[] tags = System.Array.Empty<string>();

        [Header("Equipment Stat Bonuses")]
        [Tooltip("Flat bonuses applied to UnitStats.GetSkillValue() while this item is equipped.")]
        public ItemStatBonus[] statBonuses;
    }

    /// <summary>Flat skill bonus applied while an item is equipped.</summary>
    [Serializable]
    public struct ItemStatBonus
    {
        public UnitSkillType skill;
        public int flatBonus;
    }

    /// <summary>
    /// Runtime stack record for list-based inventories.
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
            if (item == null)
            {
                return 0f;
            }

            return item.weight * quantity;
        }
    }
}