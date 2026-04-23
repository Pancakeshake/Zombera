using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.World
{
    public enum TownPrefabCategory
    {
        Building,
        Prop,
        Utility
    }

    [Serializable]
    public sealed class TownPrefabEntry
    {
        public string id = "TownPrefab";
        public GameObject prefab;

        [Min(0f)]
        public float weight = 1f;

        public TownPrefabCategory category = TownPrefabCategory.Building;

        [Header("Destructible")]
        [Min(1f)]
        public float structureMaxHealth = 100f;
        public bool destroyOnDeath = true;
        public bool addBuildPiece = true;
        public BuildPieceCategory buildPieceCategory = BuildPieceCategory.Other;
    }

    [CreateAssetMenu(menuName = "Zombera/World/Town Prefab Catalog", fileName = "TownPrefabCatalog")]
    public sealed class TownPrefabCatalog : ScriptableObject
    {
        [Header("Determinism")]
        public int generationSeed = 12345;

        [Header("Area Grid")]
        [Min(2f)]
        public float lotSize = 8f;

        [Min(0f)]
        public float edgePadding = 1f;

        [Min(0f)]
        public float randomPositionOffset = 0.75f;

        [Min(0f)]
        public float randomYawJitterDegrees = 0f;

        [Header("Editor Generation Safety")]
        [Min(100)]
        public int confirmLargeLotThreshold = 2500;

        [Min(500)]
        public int maxLotsPerRun = 12000;

        public bool autoClampLargeAreas = true;

        [Header("Lot Distribution")]
        [Range(0f, 1f)]
        public float buildingLotChance = 0.6f;

        [Range(0f, 1f)]
        public float propLotChance = 0.25f;

        [Range(0f, 1f)]
        public float utilityLotChance = 0.1f;

        [Range(0f, 1f)]
        public float emptyLotChance = 0.05f;

        [Header("Entries")]
        public List<TownPrefabEntry> entries = new List<TownPrefabEntry>();

        public bool TryGetWeightedEntry(TownPrefabCategory category, System.Random random, out TownPrefabEntry entry)
        {
            entry = null;

            if (random == null || entries == null || entries.Count == 0)
            {
                return false;
            }

            float totalWeight = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                TownPrefabEntry candidate = entries[i];

                if (!IsUsableEntry(candidate, category))
                {
                    continue;
                }

                totalWeight += candidate.weight;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            double roll = random.NextDouble() * totalWeight;
            float cumulative = 0f;
            TownPrefabEntry fallback = null;

            for (int i = 0; i < entries.Count; i++)
            {
                TownPrefabEntry candidate = entries[i];

                if (!IsUsableEntry(candidate, category))
                {
                    continue;
                }

                fallback = candidate;
                cumulative += candidate.weight;

                if (roll <= cumulative)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = fallback;
            return entry != null;
        }

        private static bool IsUsableEntry(TownPrefabEntry entry, TownPrefabCategory category)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.prefab == null)
            {
                return false;
            }

            if (entry.category != category)
            {
                return false;
            }

            return entry.weight > 0f;
        }
    }
}
