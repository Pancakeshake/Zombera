using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;

namespace Zombera.World
{
    /// <summary>
    /// Resolves biome/region information and base difficulty by world position/chunk.
    /// </summary>
    public sealed class RegionSystem : MonoBehaviour
    {
        [SerializeField] private List<RegionDefinition> regions = new List<RegionDefinition>();
        [SerializeField] private RegionDefinition fallbackRegion;
        [SerializeField] private int chunkSize = 32;

        public RegionDefinition GetRegionAtWorldPosition(Vector3 worldPosition)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                RegionDefinition region = regions[i];

                if (region.Bounds.Contains(worldPosition))
                {
                    return region;
                }
            }

            return fallbackRegion;
        }

        public RegionDefinition GetRegionAtChunk(Vector2Int chunkCoordinates)
        {
            Vector3 samplePosition = new Vector3(chunkCoordinates.x * chunkSize, 0f, chunkCoordinates.y * chunkSize);
            return GetRegionAtWorldPosition(samplePosition);
        }

        public float GetDifficultyAtWorldPosition(Vector3 worldPosition)
        {
            RegionDefinition region = GetRegionAtWorldPosition(worldPosition);
            return region != null ? region.GetDifficulty() : 1f;
        }
    }

    /// <summary>
    /// Region descriptor combining spatial bounds and data-driven configuration.
    /// </summary>
    [Serializable]
    public sealed class RegionDefinition
    {
        [field: SerializeField] public string RegionId { get; private set; }
        [field: SerializeField] public Bounds Bounds { get; private set; }
        [field: SerializeField] public float BaseDifficulty { get; private set; } = 1f;
        [field: SerializeField] public RegionData RegionData { get; private set; }

        public float GetDifficulty()
        {
            if (RegionData != null)
            {
                return RegionData.Difficulty;
            }

            return BaseDifficulty;
        }
    }
}