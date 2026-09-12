using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Resolves Ford/Causeway/Bridge policies for ocean road crossings.</summary>
    public sealed class ResolveWaterCrossingsStage : WorldBuildStageBase
    {
        public ResolveWaterCrossingsStage() : base(WorldBuildStageId.ResolveWaterCrossings)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Roads == null)
                throw new WorldBuildStageException(Descriptor.Id, "RoadNetworkRuntime is required.");
            if (context.Profile?.Hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Resolving ocean water crossings");

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            var terrainQuery = context.WorldBuilder?.TerrainQuery;
            var hydrologyQuery = context.WorldBuilder?.HydrologyQuery;
            var crossings = WaterCrossingScanner.Scan(
                context.Artifacts.Roads,
                terrainQuery,
                hydrologyQuery,
                context.Profile.Hydrology);

            context.Artifacts.SetCrossings(crossings);
            if (context.Artifacts.Hydrology != null)
                context.Artifacts.Hydrology.SetCrossings(crossings);

            var oceanRenderer = context.WorldBuilder?.OceanWaterRenderer;
            if (oceanRenderer != null && context.Profile.Hydrology != null)
            {
                oceanRenderer.ResyncOceanToBounds(
                    context.Session.WorldBoundsXZ,
                    context.Profile.Hydrology.SeaLevelWorldY);
            }

            Debug.Log($"[ResolveWaterCrossings] Crossings={crossings.Count}");
            context.Progress?.Report(Descriptor.Id, 1f, $"Crossings={crossings.Count}");
            yield break;
        }
    }
}
