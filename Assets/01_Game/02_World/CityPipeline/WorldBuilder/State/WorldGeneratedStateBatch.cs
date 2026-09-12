using System;
using System.Collections.Generic;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class WorldGeneratedStateBatch
    {
        public string Reason = string.Empty;
        public List<WorldTileCoord> ScopeTiles = new();

        public List<WorldTileTerrainRecord> TerrainRecords = new();
        public List<RegionState> Regions = new();
        public List<SettlementState> Settlements = new();
        public List<RoadState> Roads = new();
        public List<DistrictState> Districts = new();
        public List<LotState> Lots = new();
        public List<BuildingState> Buildings = new();
        public List<PoiState> Pois = new();
        public List<TerrainModificationState> TerrainModifications = new();
        public List<WorldEventState> Events = new();

        public bool HasScopedTiles => ScopeTiles != null && ScopeTiles.Count > 0;
    }
}
