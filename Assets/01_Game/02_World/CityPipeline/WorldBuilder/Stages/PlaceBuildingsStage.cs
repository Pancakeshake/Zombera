using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.PlaceBuildingsInAreas"/>.</summary>
    public sealed class PlaceBuildingsStage : WorldBuildStageBase
    {
        public PlaceBuildingsStage() : base(WorldBuildStageId.PlaceBuildings)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Starting");
            var builder = CityBuilderStageUtility.RequireBuilder(context, Descriptor.Id);
            var hasStateSink = WorldStateStageCaptureUtility.TryGetActiveStage(
                context, Descriptor.Id, out var stage);

            try
            {
                if (hasStateSink)
                    builder.BindGeneratedBuildingStateSink(stage);
                builder.LotTerrainQuery = context.WorldBuilder?.TerrainQuery;
                builder.LotDeepWaterDepthMeters =
                    WorldWaterPlacementGate.ResolveDeepWaterDepth(context.Profile);
                builder.LotMinDistanceToWaterMeters =
                    WorldWaterPlacementGate.DefaultMinDistanceToWaterMeters;
                builder.LotMaxReclaimDepthMeters =
                    CityPadReclaimPolicy.MaxReclaimDepthMeters(context.Profile?.Landforms);
                builder.LotRequireWaterGate = context.WorldBuilder != null;
                builder.PlaceBuildingsInAreas();
            }
            finally
            {
                builder.LotTerrainQuery = null;
                builder.LotRequireWaterGate = false;
                builder.LotMaxReclaimDepthMeters = 0f;
                if (hasStateSink)
                    builder.ClearGeneratedBuildingStateSink(stage);
            }

            context.Progress?.Report(Descriptor.Id, 1f, "Complete");
            yield break;
        }
    }
}
