using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Zombera.World.Roads
{
    public readonly struct CityHubFlattenSettings
    {
        public readonly Rect InnerFootprintXZ;
        public readonly Rect OuterFootprintXZ;
        public readonly Vector2 LayoutCenterXZ;
        public readonly System.Func<Vector2, float> SampleGroundHeight;
        public readonly float PaddingMeters;
        public readonly float BlendMeters;

        public CityHubFlattenSettings(
            Rect innerFootprintXZ,
            Rect outerFootprintXZ,
            Vector2 layoutCenterXZ,
            System.Func<Vector2, float> sampleGroundHeight,
            float paddingMeters,
            float blendMeters)
        {
            InnerFootprintXZ = innerFootprintXZ;
            OuterFootprintXZ = outerFootprintXZ;
            LayoutCenterXZ = layoutCenterXZ;
            SampleGroundHeight = sampleGroundHeight;
            PaddingMeters = paddingMeters;
            BlendMeters = blendMeters;
        }
    }

    public readonly struct HighwayFlattenOutcome
    {
        public readonly int RegisteredHighwayCount;
        public readonly int WrittenHighwayCount;
        public readonly long ProfileMs;
        public readonly long WriteMs;

        public HighwayFlattenOutcome(
            int registeredHighwayCount,
            int writtenHighwayCount,
            long profileMs,
            long writeMs)
        {
            RegisteredHighwayCount = registeredHighwayCount;
            WrittenHighwayCount = writtenHighwayCount;
            ProfileMs = profileMs;
            WriteMs = writeMs;
        }
    }

    public readonly struct HighwayRoadBedTiming
    {
        public readonly long WriteMs;
        public readonly long HeightmapReadMs;
        public readonly long HeightmapApplyMs;
        public readonly long HeightmapWriteMs;

        public HighwayRoadBedTiming(
            long writeMs,
            long heightmapReadMs = 0,
            long heightmapApplyMs = 0,
            long heightmapWriteMs = 0)
        {
            WriteMs = writeMs;
            HeightmapReadMs = heightmapReadMs;
            HeightmapApplyMs = heightmapApplyMs;
            HeightmapWriteMs = heightmapWriteMs;
        }
    }

    public readonly struct HighwayRoadBedOutcome
    {
        public readonly int HighwayCount;
        public readonly int TerrainCount;
        public readonly int SamplesChanged;
        public readonly float MaxAbsDeltaMeters;
        public readonly long WriteMs;
        public readonly long HeightmapReadMs;
        public readonly long HeightmapApplyMs;
        public readonly long HeightmapWriteMs;

        public HighwayRoadBedOutcome(
            int highwayCount,
            int terrainCount,
            int samplesChanged,
            float maxAbsDeltaMeters,
            in HighwayRoadBedTiming timing)
        {
            HighwayCount = highwayCount;
            TerrainCount = terrainCount;
            SamplesChanged = samplesChanged;
            MaxAbsDeltaMeters = maxAbsDeltaMeters;
            WriteMs = timing.WriteMs;
            HeightmapReadMs = timing.HeightmapReadMs;
            HeightmapApplyMs = timing.HeightmapApplyMs;
            HeightmapWriteMs = timing.HeightmapWriteMs;
        }
    }

    /// <summary>
    ///     Flattens terrain for the City Prefab Hub and procedural WorldTerrainGrid.
    /// </summary>
    public static class CityHubTerrainFlattener
    {
        public static bool TryResolvePinnedTerrains(out List<Terrain> terrains) =>
            WorldTileInfoUtility.TryGetPinnedTerrains(out terrains);

        public static bool FlattenInnerFootprint(
            CityHubFlattenSettings settings,
            out float targetWorldY,
            out string summary,
            bool recordUndo = true)
        {
            summary = string.Empty;
            targetWorldY = settings.SampleGroundHeight(settings.LayoutCenterXZ);

            if (!WorldTileInfoUtility.TryResolveTerrainsOverlapping(settings.InnerFootprintXZ, out var overlapping) ||
                overlapping.Count == 0)
            {
                summary = "Terrain flatten skipped (footprint doesn't overlap WorldTerrainGrid or pinned terrain).";
                return false;
            }

            if (recordUndo)
                RecordTerrainUndo(overlapping);
            if (!CityTerrainFootprintFlattener.FlattenOnTerrains(
                    overlapping,
                    settings.InnerFootprintXZ,
                    settings.OuterFootprintXZ,
                    targetWorldY,
                    settings.PaddingMeters,
                    settings.BlendMeters,
                    out summary))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Registers terrain-following highway height profiles for mesh placement.
        ///     Slope-clamped design beds stay exclusive to mountain-tunnel detection.
        /// </summary>
        public static HighwayFlattenOutcome FlattenHighwayRoads(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            System.Func<Vector2, float> sampleGroundHeight)
        {
            if (roads == null || sampleGroundHeight == null)
                return new HighwayFlattenOutcome(0, 0, 0, 0);

            HighwayRoadHeightProfiles.Clear();

            var surfaceSmoothing = settings != null
                ? Mathf.Clamp(settings.fastHighwayTerrainFollowSmoothingIterations, 0, 4)
                : 1;
            var maxSurfaceDeviation = ResolveProfileMaxDeviation(settings);
            System.Func<Vector2, float> sampleTerrain = SampleHeightmapFirst(sampleGroundHeight);

            var profileSw = Stopwatch.StartNew();
            var registered = 0;

            for (var i = 0; i < roads.Count; i++)
            {
                if (!TryRegisterHighwayProfile(roads[i], sampleTerrain, surfaceSmoothing, maxSurfaceDeviation))
                    continue;
                registered++;
            }

            // Wide corridor writes are intentionally disabled; callers apply BakeHighwayRoadBeds.
            return new HighwayFlattenOutcome(registered, 0, profileSw.ElapsedMilliseconds, 0);
        }

        /// <summary>
        ///     Minimal carriageway road-bed correction: shoulder + feather, clamped local delta.
        ///     Skips tunnel cores and bridge decks so surrounding mountain noise stays intact.
        /// </summary>
        public static HighwayRoadBedOutcome BakeHighwayRoadBeds(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            bool recordUndo = false,
            bool syncGpu = true)
        {
            if (roads == null || settings == null)
                return new HighwayRoadBedOutcome(0, 0, 0, 0f, new HighwayRoadBedTiming(0));

            var stopwatch = Stopwatch.StartNew();
            var requestsByTerrain = new Dictionary<Terrain, List<TerrainHeightFlattener.PolylineFlattenRequest>>();
            var highwayCount = CollectHighwayRoadBedRequests(roads, settings, requestsByTerrain);

            if (recordUndo && requestsByTerrain.Count > 0)
                RecordTerrainUndo(new List<Terrain>(requestsByTerrain.Keys));

            ApplyRoadBedBatches(
                requestsByTerrain,
                syncGpu,
                out var samplesChanged,
                out var maxAbsDelta,
                out var readMs,
                out var applyMs,
                out var writeMs);

            return new HighwayRoadBedOutcome(
                highwayCount,
                requestsByTerrain.Count,
                samplesChanged,
                maxAbsDelta,
                new HighwayRoadBedTiming(
                    stopwatch.ElapsedMilliseconds,
                    readMs,
                    applyMs,
                    writeMs));
        }

        /// <summary>GPU/collider flush for terrains written with deferred sync (after edge stitch).</summary>
        public static void SyncHighwayBedTerrains(ICollection<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0)
                return;

            foreach (var terrain in terrains)
            {
                if (terrain == null)
                    continue;
                TerrainHeightFlattener.SyncTerrainGpu(terrain);
            }

            Physics.SyncTransforms();
        }

        /// <summary> Backward-compatible alias for Fast Iteration callers.</summary>
        public static HighwayRoadBedOutcome BakeFastIterationHighwayRoadBeds(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            bool recordUndo = false) =>
            BakeHighwayRoadBeds(roads, settings, recordUndo);

        /// <summary>
        ///     Rebuilds terrain-following profiles from the live heightmap after road-bed bake
        ///     so asphalt samples the corrected terrain.
        /// </summary>
        public static int ResampleHighwayProfilesFromHeightmap(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            System.Func<Vector2, float> sampleGroundHeight)
        {
            if (roads == null || sampleGroundHeight == null)
                return 0;

            var surfaceSmoothing = settings != null
                ? Mathf.Clamp(settings.fastHighwayTerrainFollowSmoothingIterations, 0, 4)
                : 1;
            var maxSurfaceDeviation = ResolveProfileMaxDeviation(settings);
            System.Func<Vector2, float> sampleTerrain = SampleHeightmapFirst(sampleGroundHeight);
            var resampled = 0;

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;
                if (!HighwayRoadHeightProfiles.HasProfile(road.id))
                    continue;

                if (!TryRegisterHighwayProfile(road, sampleTerrain, surfaceSmoothing, maxSurfaceDeviation))
                    continue;
                resampled++;
            }

            return resampled;
        }

        private static bool TryRegisterHighwayProfile(
            RoadPolyline road,
            System.Func<Vector2, float> sampleTerrain,
            int surfaceSmoothing,
            float maxSurfaceDeviation)
        {
            if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                return false;

            var profile = PolylineHeightProfile.Build(
                road.pointsXZ,
                sampleTerrain,
                resampleSpacingMeters: 10f,
                smoothingIterations: surfaceSmoothing,
                maxSlopeDegrees: 0f,
                maxDeviationFromSamplesMeters: maxSurfaceDeviation);
            if (profile == null || !profile.IsValid)
                return false;

            HighwayRoadHeightProfiles.Register(road.id, profile);
            return true;
        }

        private static int CollectHighwayRoadBedRequests(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Dictionary<Terrain, List<TerrainHeightFlattener.PolylineFlattenRequest>> requestsByTerrain)
        {
            var shoulderMeters = settings.fastHighwayRoadBedShoulderMeters;
            var blendMeters = settings.fastHighwayRoadBedFeatherMeters;
            var highwayCount = 0;

            for (var i = 0; i < roads.Count; i++)
            {
                if (TryEnqueueHighwayRoadBed(roads[i], settings, shoulderMeters, blendMeters, requestsByTerrain))
                    highwayCount++;
            }

            return highwayCount;
        }

        private static bool TryEnqueueHighwayRoadBed(
            RoadPolyline road,
            RoadNetworkSettings settings,
            float shoulderMeters,
            float blendMeters,
            Dictionary<Terrain, List<TerrainHeightFlattener.PolylineFlattenRequest>> requestsByTerrain)
        {
            if (road?.roadClass != RoadClass.Highway ||
                !HighwayRoadHeightProfiles.TryGet(road.id, out var profile))
                return false;

            var width = road.widthMeters > 0f
                ? road.widthMeters
                : settings.ResolveWidthMeters(RoadClass.Highway);
            var halfWidth = width * 0.5f + shoulderMeters;
            var bounds = CityRegionSiteLayoutUtility.ExpandRect(
                road.BoundsXZ,
                halfWidth + blendMeters);
            if (!WorldTileInfoUtility.TryResolveAllTerrainsOverlapping(bounds, out var terrains))
                return false;

            var skipSampler = new HighwayRoadBedSkipSampler(
                road.id,
                bounds,
                halfWidth + 4f,
                halfWidth);
            var maxCutFill = settings.fastHighwayMaxCutFillMeters;
            var maxCutFillCeiling = Mathf.Max(maxCutFill, settings.fastHighwayMaxCutFillCeilingMeters);
            var overlapsTerrain = false;
            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (terrain == null)
                    continue;

                var request = new TerrainHeightFlattener.PolylineFlattenRequest(
                    profile,
                    halfWidth,
                    blendMeters,
                    settings.highwayTerrainBedClearanceMeters,
                    skipSampler.ShouldSkip,
                    maxCutFill,
                    maxCutFillCeiling,
                    settings.fastHighwaySteepGradeStartDegrees,
                    settings.fastHighwaySteepGradeFullDegrees,
                    settings.fastHighwaySteepFeatherExtraMeters);
                if (!requestsByTerrain.TryGetValue(terrain, out var requests))
                {
                    requests = new List<TerrainHeightFlattener.PolylineFlattenRequest>();
                    requestsByTerrain.Add(terrain, requests);
                }

                requests.Add(request);
                overlapsTerrain = true;
            }

            return overlapsTerrain;
        }

        private static float ResolveProfileMaxDeviation(RoadNetworkSettings settings)
        {
            if (settings == null)
                return 0.75f;

            return Mathf.Max(settings.fastHighwayMaxCutFillMeters, settings.fastHighwayMaxCutFillCeilingMeters);
        }

        private static void ApplyRoadBedBatches(
            Dictionary<Terrain, List<TerrainHeightFlattener.PolylineFlattenRequest>> requestsByTerrain,
            bool syncGpu,
            out int samplesChanged,
            out float maxAbsDelta,
            out long readMs,
            out long applyMs,
            out long writeMs)
        {
            samplesChanged = 0;
            maxAbsDelta = 0f;
            readMs = 0L;
            applyMs = 0L;
            writeMs = 0L;

            foreach (var pair in requestsByTerrain)
            {
                var stats = TerrainHeightFlattener.FlattenPolylinesBatch(pair.Key, pair.Value);
                samplesChanged += stats.SamplesChanged;
                maxAbsDelta = Mathf.Max(maxAbsDelta, stats.MaxAbsDeltaMeters);
                readMs += stats.ReadMs;
                applyMs += stats.ApplyMs;
                writeMs += stats.WriteMs;
            }

            // Prefer stitch-then-sync at the caller so mid-write LOD flush does not fight seam repair.
            if (!syncGpu || requestsByTerrain.Count == 0)
                return;

            foreach (var terrain in requestsByTerrain.Keys)
                TerrainHeightFlattener.SyncTerrainGpu(terrain);
            Physics.SyncTransforms();
        }

        private static System.Func<Vector2, float> SampleHeightmapFirst(
            System.Func<Vector2, float> sampleGroundHeight) =>
            xz =>
            {
                if (CityTerrainFootprintFlattener.TrySampleTerrainHeightmap(xz, out var heightmapY))
                    return heightmapY;
                return sampleGroundHeight(xz);
            };

        private static void RecordTerrainUndo(IReadOnlyList<Terrain> terrains)
        {
#if UNITY_EDITOR
            for (var i = 0; i < terrains.Count; i++)
            {
                if (terrains[i]?.terrainData != null)
                    UnityEditor.Undo.RecordObject(terrains[i].terrainData, "Flatten City Hub Terrain");
            }
#endif
        }
    }
}
