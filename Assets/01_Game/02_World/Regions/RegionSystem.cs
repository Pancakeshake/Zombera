#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Data;

#endregion

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.World
{
    /// <summary>
    ///     Resolves biome/region information and base difficulty by world position/chunk.
    /// </summary>
    public sealed class RegionSystem : MonoBehaviour
    {
        [SerializeField] private List<RegionDefinition> regions = new();
        [SerializeField] private RegionDefinition fallbackRegion;
        [SerializeField] private int chunkSize = 32;

        private Func<Vector3, (string regionId, string biomeName)> _biomeResolver;

        private void OnEnable()
        {
            _biomeResolver = ResolveBiomeTokens;
            WorldBiomeQuery.Resolve = _biomeResolver;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(WorldBiomeQuery.Resolve, _biomeResolver))
                WorldBiomeQuery.Resolve = null;
            _biomeResolver = null;
        }

        private (string regionId, string biomeName) ResolveBiomeTokens(Vector3 worldPosition)
        {
            var region = GetRegionAtWorldPosition(worldPosition);
            if (region == null)
                return (null, null);

            var biomeName = region.RegionData != null ? region.RegionData.biomeName : string.Empty;
            return (region.RegionId, biomeName);
        }

        public RegionDefinition GetRegionAtWorldPosition(Vector3 worldPosition)
        {
            foreach (var region in regions)
                if (region.Bounds.Contains(worldPosition))
                    return region;

            return fallbackRegion;
        }

        public RegionDefinition GetRegionAtChunk(Vector2Int chunkCoordinates)
        {
            var samplePosition = new Vector3(chunkCoordinates.x * chunkSize, 0f, chunkCoordinates.y * chunkSize);
            return GetRegionAtWorldPosition(samplePosition);
        }

        // ReSharper disable once UnusedMember.Global
        public float GetDifficultyAtWorldPosition(Vector3 worldPosition)
        {
            var region = GetRegionAtWorldPosition(worldPosition);
            return region?.GetDifficulty() ?? 1f;
        }
    }

    /// <summary>
    ///     Region descriptor combining spatial bounds and data-driven configuration.
    /// </summary>
    [Serializable]
    public sealed class RegionDefinition
    {
        [field: SerializeField] public float BaseDifficulty { get; private set; } = 1f;
        [field: SerializeField] public string RegionId { get; private set; }
        [field: SerializeField] public Bounds Bounds { get; private set; }
        [field: SerializeField] public RegionData RegionData { get; private set; }

        public float GetDifficulty()
        {
            return RegionData != null ? RegionData.difficulty : BaseDifficulty;
        }
    }
}