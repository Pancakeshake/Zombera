using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Systems;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    /// Builds city road meshes via <see cref="CityPrefabRoadNetworkBuilder"/> procedural mesh path.
    /// EasyRoads authoring is not used; the stage fails if no city builder is present.
    /// </summary>
    public sealed class BuildEasyRoadsMeshesStage : WorldBuildStageBase
    {
        private readonly List<RoadPolyline> _cityPolylines = new(256);

        public BuildEasyRoadsMeshesStage() : base(WorldBuildStageId.BuildEasyRoadsMeshes)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context == null)
                throw new WorldBuildStageException(Descriptor.Id, "Context is null.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Building procedural city road meshes");

            var cityBuilder = context.CityBuilder;
            if (cityBuilder == null)
            {
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    "CityPrefabRoadNetworkBuilder is required for procedural road meshes (EasyRoads fallback removed).");
            }

            var build = RunCityRoadBuild(context, cityBuilder);
            while (build.MoveNext())
                yield return build.Current;
        }

        private IEnumerator RunCityRoadBuild(WorldBuildContext context, CityPrefabRoadNetworkBuilder cityBuilder)
        {
            var previousRecordUndo = cityBuilder.RecordTerrainUndo;
            var previousWriteHeights = cityBuilder.WriteRoadTerrainHeightmaps;
            var previousFastQuality = cityBuilder.UseFastRoadBuildQuality;
            cityBuilder.RecordTerrainUndo = context.Options != null && context.Options.RecordTerrainUndo;
            cityBuilder.WriteRoadTerrainHeightmaps = ResolveWriteRoadTerrainHeightmaps(context);
            cityBuilder.UseFastRoadBuildQuality = context.Options != null &&
                                                context.Options.RoadBuildQuality == RoadBuildQualityMode.FastIteration;
            cityBuilder.ConfigureFinalRoadInfrastructure(
                context.Artifacts,
                context.WorldBuilder?.TerrainQuery,
                context.WorldBuilder?.HydrologyQuery,
                context.Profile?.Hydrology);
            cityBuilder.SetWaterFootprintPlan(
                context.Artifacts?.Hydrology?.FootprintPlan,
                context.Profile?.Water != null
                    ? context.Profile.Water.MinimumBedClearance
                    : 0.5f);

            // World highway chords / tunnels must reach Combined_* asphalt — never reuse stale meshes.
            InvalidateStaleRoadCache(context, cityBuilder);

            ProceduralRoadSystem roadSystem = null;
            var previousDefer = false;

            try
            {
                cityBuilder.AlignLayoutToWorldSession(
                    context.Session,
                    context.Artifacts?.Sites,
                    context.WorldBuilder?.TileCatalog);
                cityBuilder.FinalizeRegionSitesForWorldBuild(
                    context.Session,
                    context.WorldBuilder?.TileCatalog);

                // Legacy EasyRoads purge on clear may still run; defer it during generation.
                roadSystem = cityBuilder.GetComponentInChildren<ProceduralRoadSystem>();
                previousDefer = roadSystem != null && roadSystem.DeferEasyRoadsPurgeOnClear;
                if (roadSystem != null)
                    roadSystem.DeferEasyRoadsPurgeOnClear = true;

                var fast = context.Options != null && context.Options.FastRoads;
                if (fast)
                {
                    RunFastRoadGeneration(cityBuilder);
                }
                else
                {
                    var routine = cityBuilder.GenerateCityRoadNetworkRoutine();
                    while (routine.MoveNext())
                    {
                        context.Cancellation.ThrowIfRequested();
                        yield return routine.Current;
                    }
                }
            }
            finally
            {
                if (roadSystem != null)
                    roadSystem.DeferEasyRoadsPurgeOnClear = previousDefer;
                cityBuilder.RecordTerrainUndo = previousRecordUndo;
                cityBuilder.WriteRoadTerrainHeightmaps = previousWriteHeights;
                cityBuilder.UseFastRoadBuildQuality = previousFastQuality;
                cityBuilder.ClearFinalRoadInfrastructureConfiguration();
                cityBuilder.ClearPipelineWorldBounds();
            }

            if (!cityBuilder.HasGeneratedRoadNetwork)
            {
                cityBuilder.ClearGeneratedRoads();
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    "City road generation finished without a published road network.");
            }

            cityBuilder.CopyGeneratedRoadPolylines(_cityPolylines);
            var placeSw = System.Diagnostics.Stopwatch.StartNew();
            PlaceMountainTunnels(context, cityBuilder, _cityPolylines);
            cityBuilder.LastTunnelPlaceMs = placeSw.ElapsedMilliseconds;

            placeSw.Restart();
            PlaceWaterCrossingBridges(context, cityBuilder, _cityPolylines);
            cityBuilder.LastBridgePlaceMs = placeSw.ElapsedMilliseconds;

            context.Artifacts.SetCityGeneratedRoads(new List<RoadPolyline>(_cityPolylines));
            PublishCityRoads(context);
            SyncPolylineRoadGraph(cityBuilder);
            Debug.Log(
                "[BuildEasyRoadsMeshesStage] Infrastructure place: tunnels=" +
                cityBuilder.LastTunnelPlaceMs + "ms bridges=" + cityBuilder.LastBridgePlaceMs + "ms.",
                cityBuilder);

            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Procedural city roads built (polylines={_cityPolylines.Count}, regionSeed={cityBuilder.LastBuiltRegionSeed})");
        }

        /// <summary>Drops cached road meshes when world highways/tunnels would be overlaid on stale asphalt.</summary>
        private static void InvalidateStaleRoadCache(WorldBuildContext context, CityPrefabRoadNetworkBuilder cityBuilder)
        {
            var artifacts = context.Artifacts;
            if (artifacts?.Roads == null && (artifacts?.Tunnels == null || artifacts.Tunnels.Count == 0))
                return;
            cityBuilder.InvalidateRoadCache("world highways / tunnels present");
        }

        private static void RunFastRoadGeneration(CityPrefabRoadNetworkBuilder cityBuilder)
        {
            if (!cityBuilder.CanReuseCachedRoadsForCurrentBuild())
                cityBuilder.ClearGeneratedRoads();
            cityBuilder.GenerateCityRoadNetwork();
        }

        private static void PlaceMountainTunnels(
            WorldBuildContext context,
            CityPrefabRoadNetworkBuilder cityBuilder,
            IReadOnlyList<RoadPolyline> completeRoads)
        {
            var settings = context.Profile?.RoadNetworkSettings
                           ?? cityBuilder.HubRoadNetworkSettings;
            var tunnels = context.Artifacts?.Tunnels;
            if (settings == null || tunnels == null || tunnels.Count == 0)
                return;

            if (settings.tunnelMidPrefab == null)
            {
                throw new WorldBuildStageException(
                    WorldBuildStageId.BuildEasyRoadsMeshes,
                    "missing_tunnel_mid: Mountain tunnels require tunnelMidPrefab " +
                    $"({tunnels.Count} tunnel span(s) present).");
            }

            var snapped = new System.Collections.Generic.List<MountainTunnel>(tunnels.Count);
            for (var i = 0; i < tunnels.Count; i++)
                snapped.Add(tunnels[i]);
            TunnelMouthMarkerUtility.ApplySceneMarkersInPlace(snapped);
            context.Artifacts.SetTunnels(snapped);
            MountainTunnelBuildCache.Set(snapped);

            var artifactRoads = context.Artifacts.Roads?.Roads;
            var artifactLinks = artifactRoads != null
                ? HighwayTunnelApproachLinker.SnapHighwaysToTunnelMouths(artifactRoads, snapped)
                : 0;
            var meshLinks = completeRoads != null
                ? HighwayTunnelApproachLinker.SnapHighwaysToTunnelMouths(completeRoads, snapped)
                : 0;

            if (completeRoads != null && completeRoads.Count > 0)
            {
                var strips = ProceduralCityRoadBuilder.RebuildAsphalt(
                    cityBuilder.transform,
                    completeRoads,
                    settings,
                    SampleTerrainHeight);
                Debug.Log(
                    $"[BuildEasyRoadsMeshesStage] Tunnel approach link: tunnels={snapped.Count} " +
                    $"artifactHighwaysLinked={artifactLinks} meshHighwaysLinked={meshLinks} " +
                    $"asphaltStrips={strips}.");
            }

            try
            {
                var placer = new TunnelMeshPlacer();
                placer.Place(snapped, cityBuilder.transform, settings);
            }
            catch (System.Exception ex) when (ex is not WorldBuildStageException)
            {
                throw new WorldBuildStageException(
                    WorldBuildStageId.BuildEasyRoadsMeshes,
                    "Failed placing mountain tunnel kits.",
                    ex);
            }

            Debug.Log(
                $"[BuildEasyRoadsMeshesStage] Placed {snapped.Count} mountain tunnel bore(s) " +
                $"(enterable={settings.tunnelEnterable}; stretched bore entry→exit).");

            if (settings.tunnelEnterable)
            {
                TunnelTerrainHoleApplicator.ApplyMouthHoles(snapped, settings);
                TunnelNavMeshBakeHooks.EnqueueOverlappingFloorsIfPresent();
            }
        }

        private static float SampleTerrainHeight(Vector2 xz)
        {
            return CityTerrainFootprintFlattener.TrySampleTerrainHeightmap(xz, out var height)
                ? height
                : 0f;
        }

        private static void PlaceWaterCrossingBridges(
            WorldBuildContext context,
            CityPrefabRoadNetworkBuilder cityBuilder,
            IReadOnlyList<RoadPolyline> completeRoads)
        {
            var settings = context.Profile?.RoadNetworkSettings
                           ?? cityBuilder.HubRoadNetworkSettings;
            var crossings = context.Artifacts?.Crossings;
            if (settings == null || crossings == null || crossings.Count == 0)
                return;

            if (HasBridgeSpans(crossings) && settings.bridgeMidPrefab == null)
            {
                throw new WorldBuildStageException(
                    WorldBuildStageId.BuildEasyRoadsMeshes,
                    "missing_bridge_mid: Bridge/Causeway crossings require bridgeMidPrefab.");
            }

            IReadOnlyList<ResolvedBridgeApproach> approaches;
            try
            {
                var placer = new BridgeMeshPlacer();
                approaches = placer.Place(crossings, cityBuilder.transform, settings);
            }
            catch (System.Exception ex) when (ex is not WorldBuildStageException)
            {
                throw new WorldBuildStageException(
                    WorldBuildStageId.BuildEasyRoadsMeshes,
                    "Failed placing water-crossing bridge kits: " + ex.Message,
                    ex);
            }

            var resolved = new List<ResolvedBridgeApproach>(approaches);
            WaterCrossingBuildCache.SetResolvedApproaches(resolved);
            var artifactRoads = context.Artifacts.Roads?.Roads;
            var artifactLinks = HighwayInfrastructurePolylineUtility.InsertResolvedBridgeApproaches(
                artifactRoads,
                resolved);
            var meshLinks = HighwayInfrastructurePolylineUtility.InsertResolvedBridgeApproaches(
                completeRoads,
                resolved);

            if (completeRoads != null && completeRoads.Count > 0)
            {
                var strips = ProceduralCityRoadBuilder.RebuildAsphalt(
                    cityBuilder.transform,
                    completeRoads,
                    settings,
                    SampleTerrainHeight);
                Debug.Log(
                    $"[BuildEasyRoadsMeshesStage] Bridge approach link: bridges={resolved.Count} " +
                    $"artifactRoadsLinked={artifactLinks} meshRoadsLinked={meshLinks} " +
                    $"asphaltStrips={strips}.");
            }
        }

        private static bool HasBridgeSpans(IReadOnlyList<WaterCrossing> crossings)
        {
            for (var i = 0; i < crossings.Count; i++)
            {
                var policy = crossings[i].Policy;
                if (policy == WaterCrossingPolicy.Bridge || policy == WaterCrossingPolicy.Causeway)
                    return true;
            }

            return false;
        }

        private static bool ResolveWriteRoadTerrainHeightmaps(WorldBuildContext context)
        {
            var options = context?.Options;
            if (options == null || options.RoadBuildQuality != RoadBuildQualityMode.FastIteration)
                return true;

            if (!CanUseFastIterationTerrainContract(context))
            {
                Debug.LogWarning(
                    "[BuildEasyRoadsMeshesStage] Fast road iteration requested but " +
                    "city-pad or post-refine highway-corridor bake prerequisites are missing; " +
                    "falling back to Full Fidelity terrain writes.");
                return true;
            }

            Debug.Log(
                "[BuildEasyRoadsMeshesStage] Fast road iteration: using pre-baked city pads and " +
                "highway corridors; road-stage terrain mutation is disabled.");
            return false;
        }

        private static bool CanUseFastIterationTerrainContract(WorldBuildContext context)
        {
            var artifacts = context?.Artifacts;
            return artifacts?.Landforms != null &&
                   artifacts.Sites != null &&
                   artifacts.Roads != null &&
                   artifacts.HighwayCorridorsBakedPostRefine;
        }

        private static void SyncPolylineRoadGraph(CityPrefabRoadNetworkBuilder cityBuilder)
        {
            var gameplay = Object.FindFirstObjectByType<RoadGameplayService>();
            if (gameplay == null || gameplay.RoadGraph == null)
                return;

            var settings = cityBuilder.HubRoadNetworkSettings
                           ?? Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            PolylineRoadGraphBuilder.PopulateGraph(
                gameplay.RoadGraph,
                cityBuilder.CachedPolylines,
                settings);
            gameplay.RebuildRuntimeCache();
        }

        private void PublishCityRoads(WorldBuildContext context)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            stage.ReplaceRoads(
                RoadSourceKind.CityGenerated,
                RoadStateGenerationProjection.CreateCityGeneratedRoads(
                    context.Session,
                    context.Artifacts?.Sites,
                    _cityPolylines));
        }
    }
}
