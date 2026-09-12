using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    ///     Confirms reserved sites after biomes/hydrology exist. Does not re-scatter when
    ///     <see cref="WorldBuildArtifacts.Sites"/> were already set by ReserveCityPads.
    /// </summary>
    public sealed class SelectCitySitesAndLandmarksStage : WorldBuildStageBase
    {
        public SelectCitySitesAndLandmarksStage() : base(WorldBuildStageId.SelectCitySitesAndLandmarks)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldGenerationProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Confirming city sites");

            var query = context.WorldBuilder?.TerrainQuery;
            if (query == null)
                throw new WorldBuildStageException(Descriptor.Id, "IWorldTerrainQuery is required.");

            context.WorldBuilder.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            context.CityBuilder?.ResnapWorldTerrainGridToSession(context.Session);

            var catalog = context.WorldBuilder?.TileCatalog;
            var siteBounds = WorldSiteBoundsUtility.ResolvePlayableSiteBounds(
                context.Session,
                context.Profile,
                catalog);
            var plan = context.Artifacts.Sites;

            if (plan?.CitySites == null || plan.CitySites.Count == 0)
            {
                // Fallback if ReserveCityPads was skipped (legacy hub runs).
                context.Progress?.Report(Descriptor.Id, 0.2f, "Selecting city sites (fallback)");
                var rng = new DeterministicRng(context.Session.Seed ^ unchecked((int)0xC17E51E5));
                var planner = new WorldSitePlanner();
                plan = planner.Plan(new WorldSitePlanArgs
                {
                    Session = context.Session,
                    Profile = context.Profile,
                    TerrainQuery = query,
                    Rng = rng,
                    WorldBoundsOverride = siteBounds,
                    TileCatalog = catalog,
                    Orogen = context.Artifacts.Orogen
                });

                planner.ImproveHighwayConnectivity(new ImproveHighwayConnectivityArgs
                {
                    Cities = plan.CitySites,
                    Session = context.Session,
                    Profile = context.Profile,
                    TerrainQuery = query,
                    Rng = rng,
                    Bounds = siteBounds,
                    TileCatalog = catalog,
                    Orogen = context.Artifacts.Orogen
                });

                context.Artifacts.SetSites(plan);
            }
            else
            {
                var rng = new DeterministicRng(context.Session.Seed ^ unchecked((int)0xC17E51E5));
                var planner = new WorldSitePlanner();
                planner.ImproveHighwayConnectivity(new ImproveHighwayConnectivityArgs
                {
                    Cities = plan.CitySites,
                    Session = context.Session,
                    Profile = context.Profile,
                    TerrainQuery = query,
                    Rng = rng,
                    Bounds = siteBounds,
                    TileCatalog = catalog,
                    Orogen = context.Artifacts.Orogen
                });
            }

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

            PublishSites(context, plan);
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Sites cities={plan.CitySites.Count} landmarks={plan.LandmarkSites?.Count ?? 0}");
            yield break;
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
