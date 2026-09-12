using System;
using UnityEngine;

namespace Zombera.Inventory.Crafting
{
    [Serializable]
    public class CraftingIngredient
    {
        public ItemDefinition item;
        public int amount;
        public bool useTag; // optional tag filter mode
        public string requiredTag;
    }
}
