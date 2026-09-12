using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Continental fBm, domain-warped hills/mountains, coast falloff → LandformField.</summary>
    public sealed class GenerateBaseLandformsStage : WorldBuildStageBase
    {
        public GenerateBaseLandformsStage() : base(WorldBuildStageId.GenerateBaseLandforms)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Plan == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldPlan is required.");
            if (context.Profile?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Generating base landforms");

            var plan = context.Artifacts.Plan;
            var landformSeed = 0;
            if (plan.SubsystemSeeds != null &&
                plan.SubsystemSeeds.TryGetValue(WorldSubsystemSeeds.Landforms, out var seed))
                landformSeed = seed;
            else
                landformSeed = WorldSubsystemSeeds.Derive(plan.Session.Seed, plan.Session.ProfileVersion, WorldSubsystemSeeds.Landforms);

            context.Cancellation.ThrowIfRequested();

            var fastLandforms = context.Options != null && context.Options.FastLandforms;
            var field = LandformGenerator.GenerateWithOrogen(
                plan,
                context.Profile.Landforms,
                context.Profile.Hydrology,
                context.Profile.TerrainGrid,
                landformSeed,
                fastLandforms,
                out var orogenPlan);

            context.Artifacts.SetLandforms(field);
            context.Artifacts.SetOrogen(orogenPlan);
            context.WorldBuilder?.BindArtifactFields(field, context.Artifacts.Biomes, context.Artifacts.Hydrology);

            var timing = LandformGenerator.LastGenerateTiming;
            Debug.Log(
                "[GenerateBaseLandforms] " + timing.Width + "x" + timing.Height +
                " fast=" + timing.FastLandforms +
                " ranges=" + timing.BuildRangesMs + "ms" +
                " fill=" + timing.FillHeightsMs + "ms" +
                " total=" + timing.TotalMs + "ms");
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Landforms {field.Width}x{field.Height} ({timing.TotalMs}ms)");
            yield break;
        }
    }
}
