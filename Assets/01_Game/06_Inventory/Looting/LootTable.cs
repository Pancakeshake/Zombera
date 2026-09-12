#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Data;
using Random = System.Random;

#endregion

namespace Zombera.Inventory
{
    /// <summary>
    ///     Resolves location-based loot table definitions and rolls weighted loot results.
    /// </summary>
    public sealed class LootTable : MonoBehaviour
    {
        [SerializeField] private List<LocationLootTable> locationLootTables = new();

        public LootTableData GetTableForLocation(LootLocationType locationType)
        {
            return locationLootTables
                .FirstOrDefault(locationLootTable => locationLootTable.locationType == locationType)
                ?.lootTable;
        }

        public List<ItemStack> RollLoot(LootLocationType locationType, int rollCount, int seed = 0)
        {
            var result = new List<ItemStack>();
            var table = GetTableForLocation(locationType);

            if (table == null || table.entries == null || table.entries.Count == 0 || rollCount <= 0) return result;

            var rng = seed == 0 ? new Random() : new Random(seed);

            for (var i = 0; i < rollCount; i++)
            {
                var entry = RollEntry(table.entries, rng);

                if (entry == null || entry.item == null) continue;

                var quantity = rng.Next(entry.minQuantity, entry.maxQuantity + 1);
                result.Add(new ItemStack(entry.item, quantity));
            }

            return result;
        }

        private static LootTableEntryData RollEntry(IReadOnlyList<LootTableEntryData> entries, Random rng)
        {
            var totalWeight = entries.Sum(entry => Mathf.Max(0f, entry.weight));

            if (totalWeight <= 0f) return null;

            var roll = rng.NextDouble() * totalWeight;
            var cumulative = 0f;

            var rolledEntry = entries.FirstOrDefault(entry =>
            {
                cumulative += Mathf.Max(0f, entry.weight);
                return roll <= cumulative;
            });

            return rolledEntry ?? entries[^1];
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

        // ReSharper disable once UnusedMember.Global
        Hospital,

        // ReSharper disable once UnusedMember.Global
        PoliceStation,

        // ReSharper disable once UnusedMember.Global
        MilitaryBase
    }
}