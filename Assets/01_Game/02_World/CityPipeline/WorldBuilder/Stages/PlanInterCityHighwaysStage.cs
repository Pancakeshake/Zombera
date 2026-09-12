using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Pathfinds inter-city highways on landforms and carves coarse corridors before city pads.</summary>
    public sealed class PlanInterCityHighwaysStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);
        private readonly List<Rect> _corridorBounds = new(8);

        public PlanInterCityHighwaysStage() : base(WorldBuildStageId.PlanInterCityHighways)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Sites == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldSitePlan is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Planning inter-city highways");

            var settings = context.Profile?.RoadNetworkSettings;
            if (settings == null ||
                !settings.planInterCityHighwaysBeforePads ||
                !settings.connectCitiesWithHighways ||
                context.Artifacts.Sites.CitySites == null ||
                context.Artifacts.Sites.CitySites.Count < 2)
            {
                context.Progress?.Report(Descriptor.Id, 1f, "Inter-city highways skipped");
                yield break;
            }

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            var query = context.WorldBuilder?.TerrainQuery;
            if (query == null)
                throw new WorldBuildStageException(Descriptor.Id, "IWorldTerrainQuery is required.");

            var cities = context.Artifacts.Sites.CitySites;
            var planner = new InterCityHighwayPlanner();
            var planResult = planner.Plan(
                context.Session,
                cities,
                context.Profile,
                query,
                assignSiteEntries: true,
                orogen: context.Artifacts.Orogen,
                landformField: context.Artifacts.Landforms,
                hydrologyQuery: context.WorldBuilder?.HydrologyQuery);

            if (cities.Count >= 2 && planResult.RoutedMstEdgeCount == 0)
            {
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    BuildMstFailureMessage(planResult));
            }

            if (planResult.FailedMstEdgeCount > 0)
                Debug.LogWarning("[PlanInterCityHighways] " + BuildMstFailureMessage(planResult));

            context.Artifacts.SetRoads(planResult.Network);
            PublishRoads(context, planResult.Network);

            var roadSystem = Object.FindFirstObjectByType<ProceduralRoadSystem>();
            roadSystem?.SetGlobalNetwork(planResult.Network);

            // When tunnels are enabled, defer corridor carve/bake to ResolveMountainTunnels
            // so abandoned switchback lobes are never written into landforms.
            // Chord rewrite runs only at ResolveMountainTunnels.
            var deferCorridorCarve = settings.enableMountainTunnels;
            if (!deferCorridorCarve &&
                context.Artifacts.Landforms != null &&
                planResult.Network.Roads.Count > 0)
            {
                CarveAndBakeCoarseCorridors(context, settings, planResult.Network);
            }

            Debug.Log(
                $"[PlanInterCityHighways] highways={planResult.RoutedEdgeCount} failed={planResult.FailedEdgeCount} " +
                $"failedMst={planResult.FailedMstEdgeCount} " +
                $"costField={planResult.CostFieldMs}ms astar={planResult.AstarMs}ms " +
                $"deferCarve={deferCorridorCarve}");
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Planned {planResult.Network.Roads.Count} highways ({planResult.FailedEdgeCount} failed edges)");
            yield break;
        }

        private static string BuildMstFailureMessage(InterCityHighwayPlanResult planResult)
        {
            var sb = new StringBuilder(256);
            sb.Append("MST highway routing failed for ")
                .Append(planResult.FailedMstEdgeCount)
                .Append(" edge(s); routedMst=")
                .Append(planResult.RoutedMstEdgeCount)
                .Append('/')
                .Append(planResult.MstEdgeCount)
                .Append('.');

            for (var i = 0; i < planResult.Edges.Count; i++)
            {
                var edge = planResult.Edges[i];
                if (edge == null || edge.PathfindingSucceeded || !edge.IsMstEdge)
                    continue;

                sb.Append(" [")
                    .Append(edge.SiteIndexA)
                    .Append("->")
                    .Append(edge.SiteIndexB)
                    .Append(" reason=")
                    .Append(edge.FailReason ?? "unknown")
                    .Append(']');
            }

            return sb.ToString();
        }

        private void CarveAndBakeCoarseCorridors(
            WorldBuildContext context,
            RoadNetworkSettings settings,
            RoadNetworkRuntime network)
        {
            HighwayCorridorCarver.Carve(
                context.Artifacts.Landforms,
                network.Roads,
                settings,
                new HighwayCorridorCarver.HighwayCarveOptions
                {
                    Hydrology = context.Artifacts.Hydrology,
                    MaxReclaimDepthMeters = CityPadReclaimPolicy.MaxReclaimDepthMeters(
                        context.Profile?.Landforms)
                });

            var catalog = context.WorldBuilder?.TileCatalog;
            if (catalog == null || context.Profile?.TerrainGrid == null)
                return;

            HighwayCorridorTerrainBake.CollectCorridorBakeBounds(network.Roads, settings, _corridorBounds);
            if (_corridorBounds.Count > 0)
            {
                WorldBuildScopeUtility.CollectTilesOverlappingRects(
                    _corridorBounds,
                    context.Session,
                    _tiles);
            }
            else
                WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);

            var detailSeed = context.Session.Seed ^ unchecked((int)0xB1C7A401);
            LandformHeightmapBaker.BakeScopedTiles(
                context.Artifacts.Landforms,
                context.Profile.TerrainGrid,
                context.Profile.Hydrology,
                catalog,
                _tiles,
                detailSeed,
                applyDetailNoise: false);
            HighwayCorridorTerrainBake.SyncScopedHeightmaps(catalog, _tiles);
            context.Artifacts.SetHighwayCorridorsBakedPostRefine(true);
        }

        private void PublishRoads(WorldBuildContext context, RoadNetworkRuntime network)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            stage.ReplaceRoads(
                RoadSourceKind.WorldPlanned,
                RoadStateGenerationProjection.CreateWorldPlannedRoads(context.Session, network));
        }
    }
}
