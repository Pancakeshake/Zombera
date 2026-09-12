using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Shared execution context passed to every world-build stage.</summary>
    public sealed class WorldBuildContext
    {
        public WorldMapSession Session { get; }
        public WorldBuildScope Scope { get; }
        public WorldGenerationProfile Profile { get; }
        public WorldBuilderService WorldBuilder { get; }
        public CityPrefabRoadNetworkBuilder CityBuilder { get; }
        public WorldBuildArtifacts Artifacts { get; }
        public WorldStateManager StateManager { get; }
        public WorldStateGenerationRecorder StateRecorder { get; }
        public WorldBuildRunOptions Options { get; }
        public IWorldBuildProgress Progress { get; }
        public WorldBuildCancellation Cancellation { get; }

        public WorldBuildContext(
            WorldMapSession session,
            WorldBuildScope scope,
            WorldGenerationProfile profile,
            WorldBuilderService worldBuilder,
            CityPrefabRoadNetworkBuilder cityBuilder,
            WorldBuildArtifacts artifacts,
            WorldStateManager stateManager,
            WorldStateGenerationRecorder stateRecorder,
            WorldBuildRunOptions options,
            IWorldBuildProgress progress,
            WorldBuildCancellation cancellation)
        {
            Session = session;
            Scope = scope;
            Profile = profile;
            WorldBuilder = worldBuilder;
            CityBuilder = cityBuilder;
            Artifacts = artifacts ?? throw new System.ArgumentNullException(nameof(artifacts));
            StateManager = stateManager;
            StateRecorder = stateRecorder;
            Options = options ?? new WorldBuildRunOptions();
            Progress = progress ?? NullWorldBuildProgress.Instance;
            Cancellation = cancellation ?? new WorldBuildCancellation();
        }
    }
}
