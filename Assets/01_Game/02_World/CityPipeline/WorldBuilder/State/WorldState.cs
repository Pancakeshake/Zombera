using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class WorldState
    {
        public WorldStateHeader header = new();
        public long revision;
        public WorldSimulationClockState clock = new();
        public List<WorldTilePartitionState> tiles = new();
        public List<WorldEventState> pendingEvents = new();
        public List<WorldEventState> eventHistory = new();
    }

    [Serializable]
    public sealed class WorldStateHeader
    {
        public int schemaVersion = WorldStateSchema.CurrentVersion;
        public int canonicalFormatVersion = WorldStateSchema.CanonicalFormatVersion;
        public int idAlgorithmVersion = WorldStateSchema.IdAlgorithmVersion;
        public string generatorId = "WorldBuilder";
        public int generatorVersion = 1;
        public int worldSeed;
        public WorldMapSizeTier mapSizeTier;
        public int profileVersion;
        public string profileFingerprint = string.Empty;
        public string planFingerprint = string.Empty;
        public Vector2 worldOriginXZ;
        public int tilesPerSide;
        public float tileSizeMeters;
        public Rect worldBoundsXZ;
    }

    [Serializable]
    public sealed class WorldSimulationClockState
    {
        public long currentHour;
        public long nextEventSequence = 1;
    }

    [Serializable]
    public sealed class WorldTilePartitionState
    {
        public WorldTileKey key;
        public TerrainChunkState terrain = new();
        public List<RegionState> regions = new();
        public List<SettlementState> settlements = new();
        public List<RoadState> roads = new();
        public List<DistrictState> districts = new();
        public List<LotState> lots = new();
        public List<BuildingState> buildings = new();
        public List<PoiState> pois = new();
    }
}
