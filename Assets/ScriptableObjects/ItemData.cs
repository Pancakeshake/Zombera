using System;
using UnityEngine;

namespace Zombera.Data
{
    public enum RarityTier
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    /// <summary>
    /// Data-only item record used for content authoring and balancing.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Item Data", fileName = "ItemData")]
    public sealed class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(1, 4)] public string description;
        public float weight = 1f;
        public int maxStack = 99;

        [Header("Economy")]
        [Tooltip("Base barter value in currency units.")]
        [Min(0)] public int economyValue = 10;
        public RarityTier rarity = RarityTier.Common;
        [Tooltip("Freeform tags used to filter this item in crafting recipes and loot conditions.")]
        public string[] craftingTags = Array.Empty<string>();
    }
}