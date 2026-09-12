namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>No-op progress sink used when callers omit progress reporting.</summary>
    public sealed class NullWorldBuildProgress : IWorldBuildProgress
    {
        public static readonly NullWorldBuildProgress Instance = new();

        public void Report(WorldBuildStageId stage, float normalized01, string message)
        {
        }

        public void ReportWarning(WorldBuildStageId stage, string message)
        {
        }
    }
}
