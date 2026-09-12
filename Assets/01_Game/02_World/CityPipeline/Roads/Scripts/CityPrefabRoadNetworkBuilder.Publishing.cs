using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ──────────────────────────────────────────────
        //  Publishing: terrain flatten + road mesh build
        // ──────────────────────────────────────────────

        private void RefineHighwaysAgainstTerrain(RoadNetworkRuntime network, RoadNetworkSettings settings)
        {
            if (network == null || settings == null || !settings.rerouteRoadsWithTerrainPathfinding)
                return;

            var terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Highway terrain refinement skipped (no active terrains).",
                    this);
                return;
            }

            WorldMapRoadNetworkGenerator.RefineHighwaysForTerrain(
                network,
                settings,
                UseFastRoadBuildQuality);
            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Refined inter-city highways against terrain before flatten.",
                this);
        }

        private string TryFlattenHubTerrain(CityRoadNetworkBuildContext buildContext)
        {
            LastPadFlattenMs = 0;
            LastHighwayProfileMs = 0;
            LastFastHighwayRoadBedMs = 0;
            LastFastHighwayRoadBedCount = 0;
            LastFastHighwayRoadBedTerrainCount = 0;
            LastFastHighwayRoadBedSamplesChanged = 0;
            LastFastHighwayRoadBedMaxDelta = 0f;
            LastFastHighwayRoadBedReadMs = 0;
            LastFastHighwayRoadBedApplyMs = 0;
            LastFastHighwayRoadBedWriteMs = 0;
            LastSkippedRoadTerrainWrites = !WriteRoadTerrainHeightmaps;

            if (!FlattenTerrainOnGenerate)
            {
                // Still register highway profiles for mesh Y when flatten is disabled.
                RegisterHighwayProfilesOnly(buildContext);
                return string.Empty;
            }

            if (RegionModeActive)
                return TryFlattenRegionHubTerrain(buildContext);

            if (!buildContext.HasInnerFlattenRect)
            {
                RegisterHighwayProfilesOnly(buildContext);
                return string.Empty;
            }

            return TryFlattenSingleSiteHubTerrain(buildContext);
        }

        private string TryFlattenSingleSiteHubTerrain(CityRoadNetworkBuildContext buildContext)
        {
            var writeHeights = WriteRoadTerrainHeightmaps;
            var outerMargin = TerrainFlattenOuterMarginMeters;
            var inner = buildContext.InnerFlattenRect;
            var outer = CityRegionSiteLayoutUtility.ExpandRect(inner, outerMargin);
            var terrainFlattenSummary = string.Empty;

            if (writeHeights)
            {
                var padSw = Stopwatch.StartNew();
                if (IsWaterProtected(outer))
                {
                    terrainFlattenSummary = "Terrain flatten skipped (city footprint intersects protected inland water).";
                }
                else if (CityHubTerrainFlattener.FlattenInnerFootprint(
                        new CityHubFlattenSettings(
                            inner,
                            outer,
                            ResolveFlattenCenterXZ(buildContext),
                            ResolveGroundHeight,
                            TerrainFlattenPaddingMeters,
                            TerrainFlattenBlendMeters),
                        out _,
                        out terrainFlattenSummary,
                        RecordTerrainUndo))
                {
                    // keep summary
                }
                else
                {
                    terrainFlattenSummary = string.Empty;
                }

                LastPadFlattenMs = padSw.ElapsedMilliseconds;
            }

            var highwayOutcome = FlattenHighways(buildContext, writeHeights, applyRoadBed: true);
            AppendHighwaySummary(ref terrainFlattenSummary, highwayOutcome, writeHeights);

            if (!string.IsNullOrEmpty(terrainFlattenSummary))
                Debug.Log("[CityPrefabRoadNetworkBuilder] " + terrainFlattenSummary, this);

            return terrainFlattenSummary;
        }

        private string TryFlattenRegionHubTerrain(CityRoadNetworkBuildContext buildContext)
        {
            var region = ActiveRegionAsset;
            if (region == null || Layout == null)
            {
                RegisterHighwayProfilesOnly(buildContext);
                return string.Empty;
            }

            var writeHeights = WriteRoadTerrainHeightmaps;
            var flattenedSites = writeHeights
                ? FlattenRegionSitePads(region)
                : 0;

            var highwayOutcome = FlattenHighways(buildContext, writeHeights, applyRoadBed: true);
            if (flattenedSites == 0 && highwayOutcome.RegisteredHighwayCount == 0)
                return string.Empty;

            var summary = writeHeights
                ? "Flattened " + flattenedSites + " city arterial pad(s)"
                : "Skipped pad heightmap writes (profiles-only hub path)";
            AppendHighwaySummary(ref summary, highwayOutcome, writeHeights);

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + summary, this);
            return summary;
        }

        private int FlattenRegionSitePads(CityRegionAsset region)
        {
            var regionSeed = ResolveRegionSeed();
            var outerMargin = TerrainFlattenOuterMarginMeters;
            var flattenedSites = 0;
            var padSw = Stopwatch.StartNew();

            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                if (site == null)
                    continue;

                var siteSeed = CityRegionSiteLayoutUtility.ResolveSiteSeed(site, regionSeed, i);
                if (!CityRegionSiteLayoutUtility.TryResolveArterialPadBounds(
                        Layout,
                        site,
                        siteSeed,
                        outerMargin,
                        out var inner,
                        out var outer))
                    continue;

                if (IsWaterProtected(outer))
                    continue;

                if (!CityHubTerrainFlattener.FlattenInnerFootprint(
                        new CityHubFlattenSettings(
                            inner,
                            outer,
                            site.centerXZ,
                            ResolveGroundHeight,
                            TerrainFlattenPaddingMeters,
                            TerrainFlattenBlendMeters),
                        out _,
                        out _,
                        RecordTerrainUndo))
                    continue;

                flattenedSites++;
            }

            LastPadFlattenMs = padSw.ElapsedMilliseconds;
            return flattenedSites;
        }

        private HighwayFlattenOutcome FlattenHighways(
            CityRoadNetworkBuildContext buildContext,
            bool writeHeights,
            bool applyRoadBed)
        {
            RefreshFinalRoadInfrastructure(buildContext.Network);
            var inserted = HighwayInfrastructurePolylineUtility.InsertBoundaryPoints(buildContext.Network);

            // Visible profiles are always terrain-following; never slope-clamped.
            var outcome = CityHubTerrainFlattener.FlattenHighwayRoads(
                buildContext.Network?.Roads,
                roadNetworkSettings,
                ResolveGroundHeight);
            LastHighwayProfileMs = outcome.ProfileMs;

            if (applyRoadBed)
                ApplyHighwayRoadBeds(buildContext, writeHeights);

            LogHighwayInfrastructureSummary(outcome.RegisteredHighwayCount, inserted);
            return outcome;
        }

        private void LogHighwayInfrastructureSummary(int registeredHighways, int insertedBoundaryPoints)
        {
            if (registeredHighways <= 0 && insertedBoundaryPoints <= 0)
                return;

            var tunnels = MountainTunnelBuildCache.Active;
            var crossings = WaterCrossingBuildCache.Active;
            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Highway infrastructure: highways=" + registeredHighways +
                " boundaryPointsInserted=" + insertedBoundaryPoints +
                " tunnels=" + (tunnels?.Count ?? 0) +
                " crossings=" + (crossings?.Count ?? 0) +
                " roadBedSamples=" + LastFastHighwayRoadBedSamplesChanged +
                " maxDelta=" + LastFastHighwayRoadBedMaxDelta.ToString("F3") +
                "m terrains=" + LastFastHighwayRoadBedTerrainCount +
                " bedMs=" + LastFastHighwayRoadBedMs +
                " (read=" + LastFastHighwayRoadBedReadMs +
                "ms apply=" + LastFastHighwayRoadBedApplyMs +
                "ms write=" + LastFastHighwayRoadBedWriteMs + "ms).",
                this);

            if (tunnels == null)
                return;

            for (var i = 0; i < tunnels.Count; i++)
            {
                var tunnel = tunnels[i];
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Tunnel " + tunnel.StableId +
                    " road=" + tunnel.RoadId +
                    " entry=" + tunnel.EntryXZ +
                    " exit=" + tunnel.ExitXZ +
                    " length=" + tunnel.LengthMeters.ToString("F1") + "m" +
                    " cover=" + tunnel.PeakCoverMeters.ToString("F1") + "m.",
                    this);
            }
        }

        private void ApplyHighwayRoadBeds(
            CityRoadNetworkBuildContext buildContext,
            bool writeHeights)
        {
            // Minimal road-bed bake is the only highway height write path.
            if (!writeHeights && !UseFastRoadBuildQuality)
                return;

            // FastIteration + post-refine corridor bake already shaped highway beds.
            if (ShouldSkipRedundantHighwayRoadBedBake())
            {
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Skipped road-bed bake (corridors already baked).",
                    this);
                return;
            }

            var outcome = CityHubTerrainFlattener.BakeHighwayRoadBeds(
                buildContext.Network?.Roads,
                roadNetworkSettings,
                RecordTerrainUndo,
                syncGpu: false);
            LastFastHighwayRoadBedMs = outcome.WriteMs;
            LastFastHighwayRoadBedCount = outcome.HighwayCount;
            LastFastHighwayRoadBedTerrainCount = outcome.TerrainCount;
            LastFastHighwayRoadBedSamplesChanged = outcome.SamplesChanged;
            LastFastHighwayRoadBedMaxDelta = outcome.MaxAbsDeltaMeters;
            LastFastHighwayRoadBedReadMs = outcome.HeightmapReadMs;
            LastFastHighwayRoadBedApplyMs = outcome.HeightmapApplyMs;
            LastFastHighwayRoadBedWriteMs = outcome.HeightmapWriteMs;
            if (outcome.HighwayCount > 0)
                LastSkippedRoadTerrainWrites = false;

            StitchAndSyncHighwayBedTerrains(buildContext.Network?.Roads);

            if (outcome.SamplesChanged > 0)
            {
                CityHubTerrainFlattener.ResampleHighwayProfilesFromHeightmap(
                    buildContext.Network?.Roads,
                    roadNetworkSettings,
                    ResolveGroundHeight);
            }
        }

        private void StitchAndSyncHighwayBedTerrains(IReadOnlyList<RoadPolyline> roads)
        {
            if (TryResolveWorldTileCatalog(out var catalog))
                WorldTerrainNeighborUtility.StitchAndLink(catalog);

            if (roads == null || roads.Count == 0)
                return;

            // Sync only terrains that highway beds may have touched (union of corridor AABBs).
            var synced = new HashSet<Terrain>();
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;
                if (!WorldTileInfoUtility.TryResolveAllTerrainsOverlapping(road.BoundsXZ, out var terrains))
                    continue;
                for (var t = 0; t < terrains.Count; t++)
                {
                    if (terrains[t] != null)
                        synced.Add(terrains[t]);
                }
            }

            CityHubTerrainFlattener.SyncHighwayBedTerrains(synced);
        }

        private bool TryResolveWorldTileCatalog(out WorldTileCatalog catalog)
        {
            catalog = null;
            var stack = transform.Find("WorldBuilderStack");
            if (stack == null)
                return false;

            var service = stack.GetComponent<WorldBuilderService>();
            catalog = service != null ? service.TileCatalog : stack.GetComponent<WorldTileCatalog>();
            return catalog != null;
        }

        private bool ShouldSkipRedundantHighwayRoadBedBake() =>
            UseFastRoadBuildQuality &&
            _infrastructureArtifacts != null &&
            _infrastructureArtifacts.HighwayCorridorsBakedPostRefine &&
            (roadNetworkSettings == null || !roadNetworkSettings.applyFinalHighwayRoadBedAlignment);

        private void RegisterHighwayProfilesOnly(CityRoadNetworkBuildContext buildContext)
        {
            var applyRoadBed = roadNetworkSettings != null &&
                               roadNetworkSettings.applyFinalHighwayRoadBedAlignment;
            var outcome = FlattenHighways(buildContext, writeHeights: false, applyRoadBed: applyRoadBed);
            LastSkippedRoadTerrainWrites = !applyRoadBed;
            if (outcome.RegisteredHighwayCount > 0)
            {
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Registered " + outcome.RegisteredHighwayCount +
                    " highway height profile(s)" +
                    (applyRoadBed ? " with final road-bed alignment." : " without terrain writes."),
                    this);
            }
        }

        private static void AppendHighwaySummary(
            ref string summary,
            HighwayFlattenOutcome outcome,
            bool writeHeights)
        {
            if (outcome.RegisteredHighwayCount <= 0)
                return;

            var piece = writeHeights
                ? " Applied minimal road-bed blend for " + outcome.RegisteredHighwayCount + " highway(s)."
                : " Registered " + outcome.RegisteredHighwayCount +
                  " terrain-following highway profile(s).";

            if (string.IsNullOrEmpty(summary))
                summary = piece.TrimStart();
            else
                summary += piece;
        }

        private void EnsureHighwayHeightProfilesForCachedRoads()
        {
            EnsureRoadCache();
            var roads = _lastGeneratedRoadNetwork?.Roads;
            if (roads == null || roads.Count == 0)
                return;

            if (!NeedsHighwayProfileRebuild(roads))
                return;

            var outcome = CityHubTerrainFlattener.FlattenHighwayRoads(
                roads,
                roadNetworkSettings,
                ResolveGroundHeight);
            LastHighwayProfileMs = outcome.ProfileMs;
            LastSkippedRoadTerrainWrites = true;

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Rebuilt " + outcome.RegisteredHighwayCount +
                " highway height profile(s) for cached roads (no heightmap writes).",
                this);
        }

        private static bool NeedsHighwayProfileRebuild(IReadOnlyList<RoadPolyline> roads)
        {
            var highwayCount = 0;
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                highwayCount++;
                if (!HighwayRoadHeightProfiles.HasProfile(road.id))
                    return true;
            }

            return highwayCount > 0 && HighwayRoadHeightProfiles.Count == 0;
        }

        private void PublishGeneratedRoadNetwork(
            CityRoadNetworkBuildContext buildContext,
            string terrainFlattenSummary,
            int roadCount)
        {
            var junctionRegistry = JunctionRegistry.Detect(buildContext.Network.Roads);
            var junctionsT = 0;
            var junctionsX = 0;
            for (var i = 0; i < junctionRegistry.Count; i++)
            {
                if (junctionRegistry[i].Kind == JunctionKind.T)
                    junctionsT++;
                else if (junctionRegistry[i].Kind == JunctionKind.X)
                    junctionsX++;
            }

            var junctionCount = junctionsT + junctionsX + CountCornerJunctions(junctionRegistry);

            var summary = BuildGenerationSummary(
                buildContext.Network,
                roadCount,
                junctionCount,
                junctionsT,
                junctionsX,
                terrainFlattenSummary);

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + summary, this);

            // Cache roads for downstream steps (street lamps, etc.)
            _lastGeneratedRoadNetwork = buildContext.Network;
            _cachedRoadPolylines = new List<RoadPolyline>(buildContext.Network.Roads);
            _lastJunctionRegistry = junctionRegistry;
            _lastGeneratedBounds = buildContext.Bounds;
        }

        private static string BuildGenerationSummary(
            RoadNetworkRuntime network,
            int roadCount,
            int junctionCount,
            int junctionsT,
            int junctionsX,
            string terrainFlattenSummary)
        {
            var summary = "Generated city roads=" + roadCount +
                          ", junctions=" + junctionCount +
                          " (T=" + junctionsT + ", X=" + junctionsX + ")" +
                          ", polylines=" + network.Roads.Count +
                          DescribeJunctionMode() + ".";

            if (string.IsNullOrWhiteSpace(terrainFlattenSummary))
                return summary;

            return terrainFlattenSummary + " " + summary;
        }

        private static string DescribeJunctionMode() =>
            ", T+X hybrid";

        private static int CountCornerJunctions(IReadOnlyList<JunctionRecord> junctions)
        {
            if (junctions == null)
                return 0;

            var corners = 0;
            for (var i = 0; i < junctions.Count; i++)
            {
                if (junctions[i].Kind == JunctionKind.Corner)
                    corners++;
            }

            return corners;
        }

    }
}
