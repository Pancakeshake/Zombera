using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Deterministic thermal erosion on the planning landform field.</summary>
    public sealed class ErodeLandformsStage : WorldBuildStageBase
    {
        public ErodeLandformsStage() : base(WorldBuildStageId.ErodeLandforms)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformField is required.");
            if (context.Profile?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Thermal erosion");

            var pads = context.Artifacts.CityPads;
            if (pads != null && pads.Count > 0)
                ThermalErosionSolver.Apply(context.Artifacts.Landforms, context.Profile.Landforms, pads);
            else
                ThermalErosionSolver.Apply(context.Artifacts.Landforms, context.Profile.Landforms);

            // Reassert arterial flat cores; surroundings keep eroded/noisy landforms.
            if (pads != null && pads.Count > 0)
            {
                var seaLevel = context.Profile.Hydrology != null
                    ? context.Profile.Hydrology.SeaLevelWorldY
                    : 0f;
                CityPadCoreUtility.StampCoresWithBlend(
                    context.Artifacts.Landforms,
                    pads,
                    context.Profile.Landforms,
                    context.Profile.RoadNetworkSettings,
                    context.Artifacts.Hydrology,
                    seaLevel);
            }

            context.Artifacts.SetLandforms(context.Artifacts.Landforms);
            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            context.Progress?.Report(Descriptor.Id, 1f, "Erosion complete");
            yield break;
        }
    }
}
