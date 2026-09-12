namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    /// <summary>Non-serialized handoff record for replacing one tile's terrain descriptor.</summary>
    public sealed class WorldTileTerrainRecord
    {
        public WorldTileTerrainRecord(WorldTileKey tile, TerrainChunkState terrain)
        {
            Tile = tile;
            Terrain = WorldStateCloner.Clone(terrain) ?? new TerrainChunkState();
        }

        public WorldTileKey Tile { get; }
        public TerrainChunkState Terrain { get; }

        public TerrainChunkState CreateTerrainCopy() => WorldStateCloner.Clone(Terrain);
    }
}
