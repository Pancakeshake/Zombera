using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>Batch heightmap writes for several blend-only road beds on one terrain.</summary>
    public static partial class TerrainHeightFlattener
    {
        public readonly struct PolylineFlattenRequest
        {
            public readonly PolylineHeightProfile Profile;
            public readonly float HalfWidthMeters;
            public readonly float BlendMeters;
            public readonly float BedClearanceMeters;
            public readonly Func<Vector2, bool> SkipSample;
            public readonly float MaxTerrainDeltaMeters;
            public readonly float MaxTerrainDeltaCeilingMeters;
            public readonly float SteepGradeStartDegrees;
            public readonly float SteepGradeFullDegrees;
            public readonly float SteepFeatherExtraMeters;

            public PolylineFlattenRequest(
                PolylineHeightProfile profile,
                float halfWidthMeters,
                float blendMeters,
                float bedClearanceMeters,
                Func<Vector2, bool> skipSample,
                float maxTerrainDeltaMeters)
                : this(
                    profile,
                    halfWidthMeters,
                    blendMeters,
                    bedClearanceMeters,
                    skipSample,
                    maxTerrainDeltaMeters,
                    maxTerrainDeltaMeters,
                    0f,
                    0f,
                    0f)
            {
            }

            public PolylineFlattenRequest(
                PolylineHeightProfile profile,
                float halfWidthMeters,
                float blendMeters,
                float bedClearanceMeters,
                Func<Vector2, bool> skipSample,
                float maxTerrainDeltaMeters,
                float maxTerrainDeltaCeilingMeters,
                float steepGradeStartDegrees,
                float steepGradeFullDegrees,
                float steepFeatherExtraMeters)
            {
                Profile = profile;
                HalfWidthMeters = halfWidthMeters;
                BlendMeters = blendMeters;
                BedClearanceMeters = bedClearanceMeters;
                SkipSample = skipSample;
                MaxTerrainDeltaMeters = maxTerrainDeltaMeters;
                MaxTerrainDeltaCeilingMeters = Mathf.Max(maxTerrainDeltaMeters, maxTerrainDeltaCeilingMeters);
                SteepGradeStartDegrees = Mathf.Max(0f, steepGradeStartDegrees);
                SteepGradeFullDegrees = Mathf.Max(SteepGradeStartDegrees + 0.01f, steepGradeFullDegrees);
                SteepFeatherExtraMeters = Mathf.Max(0f, steepFeatherExtraMeters);
            }
        }

        public readonly struct PolylineBatchFlattenStats
        {
            public readonly int SamplesChanged;
            public readonly float MaxAbsDeltaMeters;
            public readonly long ReadMs;
            public readonly long ApplyMs;
            public readonly long WriteMs;

            public PolylineBatchFlattenStats(
                int samplesChanged,
                float maxAbsDeltaMeters,
                long readMs,
                long applyMs,
                long writeMs)
            {
                SamplesChanged = samplesChanged;
                MaxAbsDeltaMeters = maxAbsDeltaMeters;
                ReadMs = readMs;
                ApplyMs = applyMs;
                WriteMs = writeMs;
            }
        }

        /// <summary>
        ///     Applies requests in caller order using one heightmap read and write. This intentionally
        ///     matches sequential <see cref="FlattenPolyline"/> blend behaviour without repeated IO.
        /// </summary>
        public static PolylineBatchFlattenStats FlattenPolylinesBatch(
            Terrain terrain,
            IReadOnlyList<PolylineFlattenRequest> requests)
        {
            if (terrain?.terrainData == null || requests == null || requests.Count == 0)
                return default;

            if (!TryBuildUnionRegion(terrain, requests, out var startX, out var startZ, out var width, out var height))
                return default;

            var stopwatch = Stopwatch.StartNew();
            var heights = terrain.terrainData.GetHeights(startX, startZ, width, height);
            var readMs = stopwatch.ElapsedMilliseconds;
            var samplesChanged = 0;
            var maxAbsDelta = 0f;
            for (var i = 0; i < requests.Count; i++)
            {
                var stats = ApplyBlendRequest(
                    terrain, heights, startX, startZ, width, height, requests[i]);
                samplesChanged += stats.SamplesChanged;
                maxAbsDelta = Mathf.Max(maxAbsDelta, stats.MaxAbsDeltaMeters);
            }

            var applyMs = stopwatch.ElapsedMilliseconds - readMs;
            if (samplesChanged <= 0)
                return new PolylineBatchFlattenStats(0, 0f, readMs, applyMs, 0);

            PrepareHeightmapWrite(terrain, heights, startX, startZ, width, height);
            terrain.terrainData.SetHeightsDelayLOD(startX, startZ, heights);
            var writeMs = stopwatch.ElapsedMilliseconds - readMs - applyMs;
            return new PolylineBatchFlattenStats(samplesChanged, maxAbsDelta, readMs, applyMs, writeMs);
        }

        private static bool TryBuildUnionRegion(
            Terrain terrain,
            IReadOnlyList<PolylineFlattenRequest> requests,
            out int startX,
            out int startZ,
            out int width,
            out int height)
        {
            startX = startZ = width = height = 0;
            var terrainData = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = terrainData.size;
            var combined = default(Rect);
            var found = false;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.Profile == null || !request.Profile.IsValid)
                    continue;

                var radius = Mathf.Max(0.5f, request.HalfWidthMeters);
                var blendReach = Mathf.Max(0f, request.BlendMeters) + Mathf.Max(0f, request.SteepFeatherExtraMeters);
                var bounds = request.Profile.BoundsXZ;
                var expanded = Rect.MinMaxRect(
                    bounds.xMin - radius - blendReach,
                    bounds.yMin - radius - blendReach,
                    bounds.xMax + radius + blendReach,
                    bounds.yMax + radius + blendReach);
                var clipped = ClipToTerrain(expanded, origin, size);
                if (clipped.width <= 0f || clipped.height <= 0f)
                    continue;

                combined = found ? Union(combined, clipped) : clipped;
                found = true;
            }

            if (!found || terrainData.heightmapResolution <= 1)
                return false;

            var resolution = terrainData.heightmapResolution;
            return TryBuildSampleRegion(combined.xMin, combined.xMax, origin.x, size.x, resolution, out startX, out width) &&
                   TryBuildSampleRegion(combined.yMin, combined.yMax, origin.z, size.z, resolution, out startZ, out height);
        }

        private static PolylineFlattenStats ApplyBlendRequest(
            Terrain terrain,
            float[,] heights,
            int batchStartX,
            int batchStartZ,
            int batchWidth,
            int batchHeight,
            in PolylineFlattenRequest request)
        {
            if (request.Profile == null || !request.Profile.IsValid)
                return PolylineFlattenStats.Empty;

            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = data.size;
            var radius = Mathf.Max(0.5f, request.HalfWidthMeters);
            var blend = Mathf.Max(0f, request.BlendMeters);
            var steepFeather = Mathf.Max(0f, request.SteepFeatherExtraMeters);
            var maxReach = radius + blend + steepFeather;
            var bounds = request.Profile.BoundsXZ;
            var expanded = Rect.MinMaxRect(
                bounds.xMin - maxReach, bounds.yMin - maxReach,
                bounds.xMax + maxReach, bounds.yMax + maxReach);
            var clipped = ClipToTerrain(expanded, origin, size);
            if (clipped.width <= 0f || clipped.height <= 0f)
                return PolylineFlattenStats.Empty;

            var resolution = data.heightmapResolution;
            if (!TryBuildSampleRegion(clipped.xMin, clipped.xMax, origin.x, size.x, resolution, out var startX, out var width) ||
                !TryBuildSampleRegion(clipped.yMin, clipped.yMax, origin.z, size.z, resolution, out var startZ, out var height))
                return PolylineFlattenStats.Empty;

            var invHeight = 1f / Mathf.Max(0.01f, size.y);
            var clearance = Mathf.Max(0f, request.BedClearanceMeters);
            var samplesChanged = 0;
            var maxAbsDelta = 0f;
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!TryApplyBlendCell(
                            heights,
                            batchStartX,
                            batchStartZ,
                            batchWidth,
                            batchHeight,
                            request,
                            origin,
                            size,
                            resolution,
                            startX + x,
                            startZ + z,
                            radius,
                            blend,
                            maxReach,
                            clearance,
                            invHeight,
                            out var cellDelta))
                        continue;

                    maxAbsDelta = Mathf.Max(maxAbsDelta, cellDelta);
                    samplesChanged++;
                }
            }

            return new PolylineFlattenStats(samplesChanged, maxAbsDelta);
        }

        private static bool TryApplyBlendCell(
            float[,] heights,
            int batchStartX,
            int batchStartZ,
            int batchWidth,
            int batchHeight,
            in PolylineFlattenRequest request,
            Vector3 origin,
            Vector3 size,
            int resolution,
            int mapX,
            int mapZ,
            float radius,
            float blend,
            float maxReach,
            float clearance,
            float invHeight,
            out float absDeltaMeters)
        {
            absDeltaMeters = 0f;
            var worldPoint = new Vector2(
                origin.x + mapX / (float)(resolution - 1) * size.x,
                origin.z + mapZ / (float)(resolution - 1) * size.z);

            // Reject outside half-width+feather before infrastructure skip checks.
            var distance = request.Profile.QueryNearest(
                worldPoint,
                maxReach,
                out _,
                out var pathHeight,
                out var localGradeAbs);
            var adaptiveBlend = ResolveAdaptiveBlend(blend, localGradeAbs, request);
            var weight = ComputePolylineFlattenWeight(distance, radius, adaptiveBlend);
            if (weight <= 0f)
                return false;
            if (request.SkipSample != null && request.SkipSample(worldPoint))
                return false;

            var sourceX = mapX - batchStartX;
            var sourceZ = mapZ - batchStartZ;
            if (sourceX < 0 || sourceZ < 0 || sourceX >= batchWidth || sourceZ >= batchHeight)
                return false;

            var current = heights[sourceZ, sourceX];
            var currentWorldY = current * size.y + origin.y;
            var targetWorldY = pathHeight - clearance;
            var maxDelta = ResolveAdaptiveMaxDelta(localGradeAbs, request);
            if (maxDelta > 0f)
            {
                targetWorldY = Mathf.Clamp(
                    targetWorldY,
                    currentWorldY - maxDelta,
                    currentWorldY + maxDelta);
            }

            var target = Mathf.Clamp01((targetWorldY - origin.y) * invHeight);
            var next = Mathf.Lerp(current, target, weight);
            if (Mathf.Abs(next - current) <= 0.00001f)
                return false;

            absDeltaMeters = Mathf.Abs(next - current) * size.y;
            heights[sourceZ, sourceX] = next;
            return true;
        }

        private static float ResolveAdaptiveMaxDelta(float localGradeAbs, in PolylineFlattenRequest request)
        {
            var flat = Mathf.Max(0f, request.MaxTerrainDeltaMeters);
            var ceiling = Mathf.Max(flat, request.MaxTerrainDeltaCeilingMeters);
            if (ceiling <= flat || request.SteepGradeFullDegrees <= request.SteepGradeStartDegrees)
                return flat;

            var gradeDegrees = Mathf.Atan(localGradeAbs) * Mathf.Rad2Deg;
            var t = Mathf.InverseLerp(
                request.SteepGradeStartDegrees,
                request.SteepGradeFullDegrees,
                gradeDegrees);
            return Mathf.Lerp(flat, ceiling, t);
        }

        private static float ResolveAdaptiveBlend(
            float baseBlend,
            float localGradeAbs,
            in PolylineFlattenRequest request)
        {
            if (request.SteepFeatherExtraMeters <= 0f)
                return baseBlend;

            var gradeDegrees = Mathf.Atan(localGradeAbs) * Mathf.Rad2Deg;
            var t = Mathf.InverseLerp(
                request.SteepGradeStartDegrees,
                request.SteepGradeFullDegrees,
                gradeDegrees);
            return baseBlend + request.SteepFeatherExtraMeters * t;
        }

        private static Rect ClipToTerrain(Rect bounds, Vector3 origin, Vector3 size) =>
            Rect.MinMaxRect(
                Mathf.Max(bounds.xMin, origin.x), Mathf.Max(bounds.yMin, origin.z),
                Mathf.Min(bounds.xMax, origin.x + size.x), Mathf.Min(bounds.yMax, origin.z + size.z));

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }

    /// <summary>Pre-filters infrastructure spans for a single highway road-bed write.</summary>
    internal sealed class HighwayRoadBedSkipSampler
    {
        private readonly List<MountainTunnel> _tunnels = new();
        private readonly List<WaterCrossing> _crossings = new();
        private readonly float _tunnelLateralSlopMeters;
        private readonly float _crossingLateralSlopMeters;

        public HighwayRoadBedSkipSampler(
            int roadId,
            Rect roadWriteBounds,
            float tunnelLateralSlopMeters,
            float crossingLateralSlopMeters)
        {
            _tunnelLateralSlopMeters = tunnelLateralSlopMeters;
            _crossingLateralSlopMeters = crossingLateralSlopMeters;
            AddRelevantTunnels(roadId);
            AddRelevantCrossings(roadWriteBounds);
        }

        public bool ShouldSkip(Vector2 worldXZ)
        {
            for (var i = 0; i < _tunnels.Count; i++)
            {
                if (_tunnels[i].IsInCoreCarveSkip(worldXZ, _tunnelLateralSlopMeters))
                    return true;
            }

            for (var i = 0; i < _crossings.Count; i++)
            {
                var crossing = _crossings[i];
                if (DistanceToSegment(worldXZ, crossing.EntryXZ, crossing.ExitXZ) <= _crossingLateralSlopMeters)
                    return true;
            }

            return false;
        }

        private void AddRelevantTunnels(int roadId)
        {
            var active = MountainTunnelBuildCache.Active;
            if (active == null)
                return;

            for (var i = 0; i < active.Count; i++)
            {
                var tunnel = active[i];
                if (tunnel.RoadId != 0 && roadId != 0 && tunnel.RoadId != roadId)
                    continue;
                _tunnels.Add(tunnel);
            }
        }

        private void AddRelevantCrossings(Rect roadWriteBounds)
        {
            var active = WaterCrossingBuildCache.Active;
            if (active == null)
                return;

            for (var i = 0; i < active.Count; i++)
            {
                var crossing = active[i];
                if (crossing.Policy != WaterCrossingPolicy.Bridge &&
                    crossing.Policy != WaterCrossingPolicy.Causeway)
                    continue;
                if (!ExpandedSpanBounds(crossing, _crossingLateralSlopMeters).Overlaps(roadWriteBounds))
                    continue;
                _crossings.Add(crossing);
            }
        }

        private static Rect ExpandedSpanBounds(WaterCrossing crossing, float lateralSlopMeters)
        {
            var min = Vector2.Min(crossing.EntryXZ, crossing.ExitXZ);
            var max = Vector2.Max(crossing.EntryXZ, crossing.ExitXZ);
            var padding = Mathf.Max(0f, lateralSlopMeters);
            return Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            var lengthSq = delta.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / lengthSq);
            return Vector2.Distance(point, a + delta * t);
        }
    }
}
