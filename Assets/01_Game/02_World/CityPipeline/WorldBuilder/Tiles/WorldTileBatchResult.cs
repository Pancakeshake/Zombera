namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Result of a scoped tile batch reaching a completed state.</summary>
    public readonly struct WorldTileBatchResult
    {
        public WorldTileBatchResult(
            WorldBuildScope scope,
            WorldTileState completedState,
            int succeeded,
            int failed)
        {
            Scope = scope;
            CompletedState = completedState;
            Succeeded = succeeded;
            Failed = failed;
        }

        public WorldBuildScope Scope { get; }
        public WorldTileState CompletedState { get; }
        public int Succeeded { get; }
        public int Failed { get; }
    }
}
