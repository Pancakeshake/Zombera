using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Builds full-map Crest inland water after the ocean surface exists.</summary>
    public sealed class BuildWaterSurfacesStage : WorldBuildStageBase
    {
        public BuildWaterSurfacesStage() : base(WorldBuildStageId.BuildWaterSurfaces)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Building water surfaces");

            var hydrology = context.Artifacts?.Hydrology;
            if (hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyPlan is required.");

            var renderer = context.WorldBuilder?.WaterRenderer;
            if (renderer == null)
            {
                Debug.LogWarning(
                    "[BuildWaterSurfacesStage] IWorldWaterRenderer not bound; skipping inland Crest water.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no water renderer)");
                yield break;
            }

            BindWaterProfile(renderer, context.Profile);
            // River crossing cutouts are intentionally deferred; this stage only presents
            // the hydrology plan and must not consume the ocean-crossing artifact yet.
            var crossings = System.Array.Empty<WaterCrossing>();
            var build = renderer.BuildSurfaces(hydrology, crossings, context.Scope);
            while (build.MoveNext())
            {
                context.Cancellation.ThrowIfRequested();
                yield return build.Current;
            }

            context.Progress?.Report(Descriptor.Id, 1f, "Water surfaces ready");
        }

        private static void BindWaterProfile(IWorldWaterRenderer renderer, WorldGenerationProfile profile)
        {
            if (profile == null || renderer is not IWorldWaterProfileBinder binder)
                return;
            binder.BindProfile(profile.Water, profile.Hydrology);
        }
    }
}
