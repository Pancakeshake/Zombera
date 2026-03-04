using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;

namespace Zombera.Inventory
{
    /// <summary>
    /// Resolves location-based loot table definitions and rolls weighted loot results.
    /// </summary>
    public sealed class LootTable : MonoBehaviour
    {
        [SerializeField] private List<LocationLootTable> locationLootTables = new List<LocationLootTable>();

        public LootTableData GetTableForLocation(LootLocationType locationType)
        {
            for (int i = 0; i < locationLootTables.Count; i++)
            {
                if (locationLootTables[i].locationType == locationType)
                {
                    return locationLootTables[i].lootTable;
                }
            }

            return null;
        }

        public List<ItemStack> RollLoot(LootLocationType locationType, int rollCount, int seed = 0)
        {
            List<ItemStack> result = new List<ItemStack>();
            LootTableData table = GetTableForLocation(locationType);

            if (table == null || table.entries == null || table.entries.Count == 0 || rollCount <= 0)
            {
                return result;
            }

            System.Random rng = seed == 0 ? new System.Random() : new System.Random(seed);

            for (int i = 0; i < rollCount; i++)
            {
                LootTableEntryData entry = RollEntry(table.entries, rng);

                if (entry == null || entry.item == null)
                {
                    continue;
                }

                int quantity = rng.Next(entry.minQuantity, entry.maxQuantity + 1);
                result.Add(new ItemStack(entry.item, quantity));
            }

            return result;
        }

        private LootTableEntryData RollEntry(IReadOnlyList<LootTableEntryData> entries, System.Random rng)
        {
            float totalWeight = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                totalWeight += Mathf.Max(0f, entries[i].weight);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            double roll = rng.NextDouble() * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += Mathf.Max(0f, entries[i].weight);

                if (roll <= cumulative)
                {
                    return entries[i];
                }
            }

            return entries[entries.Count - 1];
        }
    }

    [Serializable]
    public sealed class LocationLootTable
    {
        public LootLocationType locationType;
        public LootTableData lootTable;
    }

    public enum LootLocationType
    {
        House,
        Hospital,
        PoliceStation,
        MilitaryBase
    }
}