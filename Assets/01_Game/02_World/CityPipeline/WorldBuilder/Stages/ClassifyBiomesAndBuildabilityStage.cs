using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Classifies biomes, buildability, and no-build masks from landforms + hydrology.</summary>
    public sealed class ClassifyBiomesAndBuildabilityStage : WorldBuildStageBase
    {
        public ClassifyBiomesAndBuildabilityStage() : base(WorldBuildStageId.ClassifyBiomesAndBuildability)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformField is required.");
            if (context.Profile?.Biomes == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldBiomePalette is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Classifying biomes");

            EnsureBiomePaletteDefaults(context.Profile.Biomes);

            var biomeSeed = 0;
            if (context.Artifacts.Plan?.SubsystemSeeds != null &&
                context.Artifacts.Plan.SubsystemSeeds.TryGetValue(WorldSubsystemSeeds.Biomes, out var seed))
                biomeSeed = seed;
            else if (context.Artifacts.Plan != null)
                biomeSeed = WorldSubsystemSeeds.Derive(
                    context.Artifacts.Plan.Session.Seed,
                    context.Artifacts.Plan.Session.ProfileVersion,
                    WorldSubsystemSeeds.Biomes);

            var fastClassify = context.Options != null && context.Options.FastBiomeClassify;
            var biomes = BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(
                context.Artifacts.Landforms,
                context.Artifacts.Hydrology,
                new BiomeClassifier.ClassifyProfiles(
                    context.Profile.Biomes,
                    context.Profile.Hydrology,
                    context.Profile.Landforms,
                    context.Profile.MapSizeSettings),
                context.Session,
                biomeSeed,
                fastClassify));

            context.Artifacts.SetBiomes(biomes);
            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                biomes,
                context.Artifacts.Hydrology);

            context.Progress?.Report(Descriptor.Id, 1f, $"Biomes {biomes.Width}x{biomes.Height}");
            yield break;
        }

        private static void EnsureBiomePaletteDefaults(WorldBiomePalette palette)
        {
            if (palette == null) return;
            if (palette.TryGetBiome("Plains", out _) &&
                palette.TryGetBiome("Ocean", out _) &&
                palette.TryGetBiome("Badlands", out _))
                return;
            palette.ApplyProgrammaticDefaults();
        }
    }
}
