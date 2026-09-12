namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>What a reset stage clears before rebuild.</summary>
    public enum WorldResetMode : byte
    {
        ContentOnly = 0,
        TerrainAndContent = 1
    }
}
