using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Data
{
    /// <summary>
    /// Weighted item table consumed by location-based loot generation.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Loot Table Data", fileName = "LootTableData")]
    public sealed class LootTableData : ScriptableObject
    {
        public string tableId;
        public List<LootTableEntryData> entries = new List<LootTableEntryData>();

        // TODO: Add conditional entries by game phase, region, and difficulty.
    }

    [Serializable]
    public sealed class LootTableEntryData
    {
        public ItemDefinition item;
        public float weight = 1f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }
}