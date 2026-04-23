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

        [Tooltip("Entries that activate only when the specified phase, region, and difficulty conditions are met.")]
        public List<ConditionalLootEntry> conditionalEntries = new List<ConditionalLootEntry>();
    }

    [Serializable]
    public sealed class LootTableEntryData
    {
        public ItemDefinition item;
        public float weight = 1f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }

    [Serializable]
    public sealed class ConditionalLootEntry
    {
        [Tooltip("Minimum game-phase index required for this entry to be active (0 = always).")]
        [Min(0)] public int minGamePhase;
        [Tooltip("Region ID filter. Leave empty to match any region.")]
        public string regionId;
        [Tooltip("Maximum difficulty that allows this entry. 0 = no difficulty cap.")]
        [Min(0f)] public float maxDifficulty;
        public List<LootTableEntryData> entries = new List<LootTableEntryData>();
    }
}