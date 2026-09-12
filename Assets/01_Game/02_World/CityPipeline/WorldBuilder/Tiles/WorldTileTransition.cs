namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Recorded state transition for a world tile.</summary>
    public readonly struct WorldTileTransition
    {
        public WorldTileTransition(WorldTileInfo tile, WorldTileState previous, WorldTileState current)
        {
            Tile = tile;
            Previous = previous;
            Current = current;
        }

        public WorldTileInfo Tile { get; }
        public WorldTileState Previous { get; }
        public WorldTileState Current { get; }
    }
}
