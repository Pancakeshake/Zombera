using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Carve channels/basins on LandformField and bake scoped Terrain heightmaps.</summary>
    public sealed class CarveWaterFeaturesStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);

        public CarveWaterFeaturesStage() : base(WorldBuildStageId.CarveWaterFeatures)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformField is required.");
            if (context.Artifacts.Hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyPlan is required.");
            if (context.Profile?.Hydrology == null)
                throw new WorldBuildStageException(Descriptor.Id, "HydrologyProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Carving water features");

            // Reuse Solve's pre-carve ocean from HydrologyPlan.WaterClass — do not rebuild BuildOceanMask.
            var oceanMask = BuildOceanMaskFromPlan(context.Artifacts.Hydrology);
            // Hydrology is authoritative after SolveHydrology. Do not reapply the
            // placement/obstacle mask here: it can sever a valid mountain runoff
            // stem after the outlet-first network has already selected it. The
            // carver still protects ocean and lake cells from its own plan.
            CarveWithRepairLoop(context, oceanMask);

            // Water carve must not nibble reserved flat cores; restore soft blend after.
            if (context.Artifacts.CityPads != null && context.Artifacts.CityPads.Count > 0)
            {
                CityPadCoreUtility.StampCoresWithBlend(
                    context.Artifacts.Landforms,
                    context.Artifacts.CityPads,
                    context.Profile.Landforms,
                    context.Profile.RoadNetworkSettings,
                    context.Artifacts.Hydrology,
                    context.Profile.Hydrology.SeaLevelWorldY);
            }

            context.Artifacts.SetLandforms(context.Artifacts.Landforms);
            context.Artifacts.SetHydrology(context.Artifacts.Hydrology);

            var catalog = context.WorldBuilder?.TileCatalog;
            if (catalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");

            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);
            var detailSeed = 0;
            if (context.Artifacts.Plan?.SubsystemSeeds != null &&
                context.Artifacts.Plan.SubsystemSeeds.TryGetValue(WorldSubsystemSeeds.Landforms, out var seed))
                detailSeed = seed ^ unchecked((int)0xC4A7E001);

            LandformHeightmapBaker.BakeScopedTiles(
                context.Artifacts.Landforms,
                context.Profile.TerrainGrid,
                context.Profile.Hydrology,
                catalog,
                _tiles,
                detailSeed,
                applyDetailNoise: false);

            SyncScopedHeightmaps(catalog, _tiles);

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            context.Progress?.Report(Descriptor.Id, 1f, $"Carved {_tiles.Count} tiles");
            yield break;
        }

        /// <summary>
        /// Runs the bounded carve → fit → validate repair loop. Each pass re-carves from the
        /// (possibly widened) plan and re-validates the carved field plus the real Crest ribbon.
        /// The loop fails the stage with feature/location diagnostics rather than shipping water
        /// that is still buried or floating.
        /// </summary>
        private InlandWaterFootprintReport CarveWithRepairLoop(WorldBuildContext context, bool[] oceanMask)
        {
            var field = context.Artifacts.Landforms;
            var hydrology = context.Artifacts.Hydrology;
            var profile = context.Profile.Hydrology;
            var options = InlandWaterFootprintOptions.FromWater(context.Profile.Water);
            var crest = CrestRibbonValidationSettings.FromWater(context.Profile.Water);

            var footprint = hydrology.EnsureFootprintPlan(profile, options);
            footprint.CarveCeilingWorldY = profile.SeaLevelWorldY + profile.MaxRiverLandElevationAboveSeaMeters;

            // At least two passes so there is always one re-carve after a repair, even when the
            // profile's repair-pass limit is zero.
            var passes = Mathf.Max(2, options.RepairPassLimit + 1);
            InlandWaterFootprintReport report = null;
            for (var pass = 1; pass <= passes; pass++)
            {
                context.Cancellation.ThrowIfRequested();
                HydrologyCarver.Carve(
                    field,
                    hydrology,
                    profile,
                    exclusionMask: null,
                    oceanMask: oceanMask,
                    options: options);

                report = footprint.ValidateCarvedField(field, options, report);
                report.Pass = pass;
                if (report.IsClean)
                {
                    report = footprint.ValidateCrestRibbon(field, options, crest, report);
                    report.Pass = pass;
                }

                if (report.IsClean)
                    break;

                var maxHalfBefore = footprint.MaxRiverHalfWidthMeters();
                footprint.RepairCrestRibbon(field, options, crest, report);
                var maxHalfAfter = footprint.MaxRiverHalfWidthMeters();
                context.Progress?.Report(
                    Descriptor.Id, 0.05f + 0.15f * (pass / (float)passes),
                    $"Water repair pass {pass}/{passes}: {report.Describe(2)} " +
                    $"(halfWidth {maxHalfBefore:F1}→{maxHalfAfter:F1}m)");
            }

            if (!report.IsClean)
            {
                // The final repair rewrote the plan (inserted controls / flattened runs) without
                // re-carving, so re-validate against the repaired plan before failing the build:
                // ribbon-side drift can be resolved by the repair alone, bed clearance cannot.
                report = footprint.ValidateCarvedField(field, options, report);
                report.Pass = passes;
                if (report.IsClean)
                    report = footprint.ValidateCrestRibbon(field, options, crest, report);
            }

            if (!report.IsClean)
            {
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    $"Inland water footprint failed after {passes} bounded repair pass(es): {report.Describe(6)}");
            }

            return report;
        }

        private static bool[] BuildOceanMaskFromPlan(HydrologyPlan plan)
        {
            if (plan?.WaterClass == null)
                return System.Array.Empty<bool>();

            var mask = new bool[plan.WaterClass.Length];
            for (var i = 0; i < mask.Length; i++)
                mask[i] = plan.WaterClass[i] == WorldWaterClass.Ocean;
            return mask;
        }

        private static void SyncScopedHeightmaps(WorldTileCatalog catalog, List<WorldTileCoord> tiles)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                terrain.terrainData.SyncHeightmap();
            }
        }
    }
}
