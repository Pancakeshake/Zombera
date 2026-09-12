namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Stage-owned readiness lifecycle for a finite world tile.</summary>
    public enum WorldTileState : byte
    {
        None = 0,
        Allocated = 1,
        TerrainReady = 2,
        InfrastructureReady = 3,
        ContentReady = 4,
        NavigationReady = 5,
        GameplayReady = 6
    }
}
