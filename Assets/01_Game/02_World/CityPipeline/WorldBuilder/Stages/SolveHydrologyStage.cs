using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Ocean flood-fill, priority-flood, rivers, lakes → HydrologyPlan.</summary>
    public sealed class SolveHydrologyStage : WorldBuildStageBase
    {
        public SolveHydrologyStage() : base(WorldBuildStageId.SolveHydrology)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformField is required.");
            if (context.Profile?.Hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Solving hydrology");

            var plan = HydrologySolver.Solve(
                context.Artifacts.Landforms,
                context.Profile.Hydrology,
                context.Profile.Landforms,
                context.Session,
                context.Artifacts.CityPads);
            context.Artifacts.SetHydrology(plan);
            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                plan);

            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Hydrology rivers={plan.Rivers?.Length ?? 0} lakes={plan.Lakes?.Length ?? 0}");
            yield break;
        }
    }
}
