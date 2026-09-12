namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-stage status tracked by the pipeline runner.</summary>
    public enum WorldBuildStageStatus : byte
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Warning = 3,
        Failed = 4,
        Cancelled = 5,
        Stale = 6,
        SkippedCached = 7
    }
}
