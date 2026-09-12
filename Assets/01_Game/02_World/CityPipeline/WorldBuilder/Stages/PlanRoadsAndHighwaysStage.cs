using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Plans highways and local arms from selected city sites via WorldCostField A*.</summary>
    public sealed class PlanRoadsAndHighwaysStage : WorldBuildStageBase
    {
        public PlanRoadsAndHighwaysStage() : base(WorldBuildStageId.PlanRoadsAndHighways)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Sites == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldSitePlan is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Planning roads");

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            var planner = new WorldRoadPlanner();
            var network = planner.Plan(
                context.Session,
                context.Artifacts.Sites,
                context.Profile,
                context.WorldBuilder?.TerrainQuery,
                context.Artifacts.Roads);

            context.Artifacts.SetRoads(network);
            PublishRoads(context, network);

            var roadSystem = Object.FindFirstObjectByType<Zombera.World.Roads.ProceduralRoadSystem>();
            roadSystem?.SetGlobalNetwork(network);

            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Planned {(network?.Roads != null ? network.Roads.Count : 0)} roads");
            yield break;
        }

        private void PublishRoads(WorldBuildContext context, Zombera.World.Roads.RoadNetworkRuntime network)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            stage.ReplaceRoads(
                RoadSourceKind.WorldPlanned,
                RoadStateGenerationProjection.CreateWorldPlannedRoads(context.Session, network));
        }
    }
}
