using System;
using UnityEngine;

namespace Zombera.Inventory
{
    /// <summary>
    /// Scriptable item definition used by inventory, loot, equipment, and save systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public float weight = 1f;
        public bool stackable = true;
        public int maxStack = 99;

        // TODO: Add rarity, tags, and item behavior hooks.
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