using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Builds Crest ocean surfaces immediately after hydrology carving.</summary>
    public sealed class BuildOceanSurfacesStage : WorldBuildStageBase
    {
        public BuildOceanSurfacesStage() : base(WorldBuildStageId.BuildOceanSurfaces)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Building ocean surfaces");

            var hydrology = context.Artifacts?.Hydrology;
            if (hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyPlan is required.");

            var renderer = context.WorldBuilder?.OceanWaterRenderer;
            if (renderer == null)
            {
                Debug.LogWarning(
                    "[BuildOceanSurfacesStage] IOceanWaterRenderer not bound; skipping ocean.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no ocean renderer)");
                yield break;
            }

            BindOceanProfile(renderer, context.Profile);

            var layout = WorldMapBoundaryLayout.Resolve(
                context.Session,
                context.Profile?.Landforms);
            var stripDepth = context.Profile?.Landforms != null
                ? context.Profile.Landforms.EdgeBarrierDepthMeters
                : 1100f;
            // Prefer live allocated terrain footprint so Crest WaterBody matches terrain exactly.
            var surfaceBounds = WorldTerrainBoundsResolver.Resolve(
                context.WorldBuilder?.TileCatalog,
                context.Session);
            if (surfaceBounds.width <= 0f || surfaceBounds.height <= 0f)
                surfaceBounds = context.Session.WorldBoundsXZ;
            var coreBounds = context.Session.CoreWorldBoundsXZ;
            var request = new OceanSurfaceBuildRequest(
                hydrology,
                context.Scope,
                layout,
                stripDepth,
                surfaceBounds,
                context.Session.OceanRingTiles,
                coreBounds);

            var build = renderer.BuildOceanSurfaces(request);
            while (build.MoveNext())
            {
                context.Cancellation.ThrowIfRequested();
                yield return build.Current;
            }

            // Environment may have bound consumers before Crest existed; push again now.
            ApplyWeatherToConsumers(context);

            context.Progress?.Report(Descriptor.Id, 1f, "Ocean surfaces ready");
        }

        private static void ApplyWeatherToConsumers(WorldBuildContext context)
        {
            var source = context.WorldBuilder?.WeatherSource;
            if (source == null)
                return;

            var snapshot = source.Current;
            var consumers = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < consumers.Length; i++)
            {
                if (consumers[i] is not IWorldWeatherConsumer consumer)
                    continue;
                consumer.ApplyWeather(snapshot);
            }
        }

        private static void BindOceanProfile(IOceanWaterRenderer renderer, WorldGenerationProfile profile)
        {
            if (profile == null || renderer is not IWorldWaterProfileBinder binder)
                return;

            binder.BindProfile(profile.Water, profile.Hydrology);
        }
    }
}
