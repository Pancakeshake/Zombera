using UnityEngine;

namespace Zombera.Data
{
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

        // TODO: Add economy values, rarity tiers, and crafting tags.
    }
}