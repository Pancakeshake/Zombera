using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    ///     After base landforms: provisional site pick, city-footprint+margin core + blend
    ///     apron so erosion/hydrology sculpt around frozen flats.
    /// </summary>
    public sealed class ReserveCityPadsStage : WorldBuildStageBase
    {
        private readonly List<CityFlattenPad> _pads = new(16);

        public ReserveCityPadsStage() : base(WorldBuildStageId.ReserveCityPads)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldGenerationProfile is required.");
            if (context.Artifacts?.Landforms == null)
                throw new WorldBuildStageException(Descriptor.Id, "LandformField is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Reserving city pad cores");

            var query = context.WorldBuilder?.TerrainQuery;
            if (query == null)
                throw new WorldBuildStageException(Descriptor.Id, "IWorldTerrainQuery is required.");

            // Hydrology owns the water corridors. Sites can be waterfront-adjacent but
            // their cores/aprons must not reclaim the selected channels or lake basins.
            context.WorldBuilder.BindArtifactFields(
                context.Artifacts.Landforms,
                biomes: context.Artifacts.Biomes,
                hydrology: context.Artifacts.Hydrology);

            context.CityBuilder?.ResnapWorldTerrainGridToSession(context.Session);

            var rng = new DeterministicRng(context.Session.Seed ^ unchecked((int)0xC17E51E5));
            var planner = new WorldSitePlanner();
            var catalog = context.WorldBuilder?.TileCatalog;
            var siteBounds = WorldSiteBoundsUtility.ResolvePlayableSiteBounds(
                context.Session,
                context.Profile,
                catalog);
            var plan = planner.Plan(new WorldSitePlanArgs
            {
                Session = context.Session,
                Profile = context.Profile,
                TerrainQuery = query,
                Rng = rng,
                WorldBoundsOverride = siteBounds,
                TileCatalog = catalog,
                Orogen = context.Artifacts.Orogen,
                ProvisionalPlacement = true
            });

            LogSiteDiagnostics(planner, plan, context.Session.Tier.ToString());
            context.Artifacts.SetSites(plan);

            if (context.CityBuilder == null)
            {
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    "CityPrefabRoadNetworkBuilder is required to apply the session city plan.");
            }

            try
            {
                context.WorldBuilder.ApplyCitySitePlan(
                    context.CityBuilder,
                    plan,
                    siteBounds);
                context.CityBuilder.FinalizeRegionSitesForWorldBuild(
                    context.Session,
                    context.WorldBuilder?.TileCatalog);
            }
            catch (System.Exception ex)
            {
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    "Failed to apply WorldSitePlan to session CityRegion: " + ex.Message);
            }

            var pruneMargin = context.Profile.Landforms != null
                ? context.Profile.Landforms.CityPadHydrologyPruneMarginMeters
                : 16f;
            CityPadCoreUtility.BuildCityCorePads(
                plan,
                context.CityBuilder,
                context.Artifacts.Landforms,
                context.Profile.Landforms,
                pruneMargin,
                context.Artifacts.Hydrology,
                context.Profile.Hydrology != null
                    ? context.Profile.Hydrology.CityWaterSetbackMeters
                    : 64f,
                _pads);

            var seaLevel = context.Profile.Hydrology != null
                ? context.Profile.Hydrology.SeaLevelWorldY
                : 0f;
            CityPadCoreUtility.StampCoresWithBlend(
                context.Artifacts.Landforms,
                _pads,
                context.Profile.Landforms,
                context.Profile.RoadNetworkSettings,
                hydrology: context.Artifacts.Hydrology,
                seaLevel);

            SyncSitePadHeights(plan, _pads);
            context.Artifacts.SetCityPads(new List<CityFlattenPad>(_pads));
            context.Artifacts.SetLandforms(context.Artifacts.Landforms);
            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            PublishSites(context, plan);
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Reserved {_pads.Count} pad cores (cities={plan.CitySites.Count})");
            yield break;
        }

        private static void LogSiteDiagnostics(
            WorldSitePlanner planner,
            WorldSitePlan plan,
            string tier)
        {
            var cityCount = plan?.CitySites != null ? plan.CitySites.Count : 0;
            Debug.Log($"[ReserveCityPads] tier={tier} cities={cityCount}");

            if (planner?.Diagnostics == null)
                return;

            for (var i = 0; i < planner.Diagnostics.Count; i++)
            {
                var d = planner.Diagnostics[i];
                if (d == null || string.IsNullOrEmpty(d.Code))
                    continue;
                if (d.Code != "under_target" &&
                    d.Code != "hierarchy_place_fail" &&
                    d.Code != "missing_inputs")
                    continue;

                if (d.IsError)
                    Debug.LogWarning($"[ReserveCityPads] {d.Code}: {d.Message}");
                else
                    Debug.Log($"[ReserveCityPads] {d.Code}: {d.Message}");
            }
        }

        private static void SyncSitePadHeights(WorldSitePlan sites, IReadOnlyList<CityFlattenPad> pads)
        {
            if (sites?.CitySites == null || pads == null)
                return;

            for (var i = 0; i < sites.CitySites.Count && i < pads.Count; i++)
            {
                var site = sites.CitySites[i];
                var pad = pads[i];
                if (site == null || pad == null)
                    continue;
                site.PadHeightWorldY = pad.TargetHeightWorldY;
            }
        }

        private void PublishSites(WorldBuildContext context, WorldSitePlan plan)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            stage.ReplaceRegions(new List<RegionState>
            {
                WorldStateSiteGenerationProjection.CreateWorldRegion(context.Session)
            });
            stage.ReplaceSettlements(
                WorldStateSiteGenerationProjection.CreateSettlements(context.Session, plan));
        }
    }
}
