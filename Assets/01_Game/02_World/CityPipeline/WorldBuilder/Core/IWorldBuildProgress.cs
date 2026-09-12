namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Progress sink for world-build stages.</summary>
    public interface IWorldBuildProgress
    {
        void Report(WorldBuildStageId stage, float normalized01, string message);
        void ReportWarning(WorldBuildStageId stage, string message);
    }
}
