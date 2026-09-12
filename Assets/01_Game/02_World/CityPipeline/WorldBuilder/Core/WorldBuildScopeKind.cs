namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>How a world-build run is spatially scoped.</summary>
    public enum WorldBuildScopeKind : byte
    {
        FullMap = 0,
        InitialPlayArea = 1,
        Bounds = 2,
        TileSet = 3
    }
}
