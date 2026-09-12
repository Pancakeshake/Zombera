using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static class GameplayRoadBaker
    {
        private const float MinimumSampleSpacingMeters = 0.5f;

        public static bool BuildRuntimeCache(
            GameplayRoadGraph graph,
            float sampleSpacingMeters,
            List<RoadSamplePoint> sampleBuffer,
            List<ResolvedRoadSpawnPoint> spawnBuffer)
        {
            if (sampleBuffer == null || spawnBuffer == null) return false;

            sampleBuffer.Clear();
            spawnBuffer.Clear();

            if (graph == null) return false;

            var spacing = Mathf.Max(MinimumSampleSpacingMeters, sampleSpacingMeters);
            var polylineBuffer = new List<Vector3>(64);

            var segments = graph.Segments;
            foreach (var segment in segments)
            {
                if (segment == null) continue;
                if (!graph.TryGetSegmentPolyline(segment, polylineBuffer)) continue;

                AppendSegmentSamples(polylineBuffer, segment, spacing, sampleBuffer);
            }

            ResolveSpawnPoints(graph, spawnBuffer, polylineBuffer);
            return sampleBuffer.Count > 0 || spawnBuffer.Count > 0;
        }

        public static bool BakeIntoAsset(
            GameplayRoadGraph graph,
            GameplayRoadDerivedData output,
            float sampleSpacingMeters)
        {
            if (graph == null || output == null) return false;

            var sampleBuffer = new List<RoadSamplePoint>(512);
            var spawnBuffer = new List<ResolvedRoadSpawnPoint>(128);

            if (!BuildRuntimeCache(graph, sampleSpacingMeters, sampleBuffer, spawnBuffer))
                return false;

            output.SampleSpacingMeters = sampleSpacingMeters;
            output.ReplaceSamples(sampleBuffer);
            output.ReplaceResolvedSpawnPoints(spawnBuffer);
            output.SetSourceStats(graph);
            return true;
        }

        private static void AppendSegmentSamples(
            IReadOnlyList<Vector3> polyline,
            RoadGraphSegment segment,
            float spacingMeters,
            List<RoadSamplePoint> output)
        {
            if (polyline == null || polyline.Count < 2 || output == null || segment == null) return;

            if (!TryComputePolylineLength(polyline, out var totalLength) || totalLength <= 0.001f)
                return;

            var step = Mathf.Max(MinimumSampleSpacingMeters, spacingMeters);
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(totalLength / step) + 1);

            for (var i = 0; i < sampleCount; i++)
            {
                var t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
                var distance = t * totalLength;
                if (!TryEvaluatePolylineAtDistance(polyline, distance, out var position, out var tangent)) continue;

                output.Add(new RoadSamplePoint
                {
                    position = position,
                    tangent = tangent,
                    segmentId = segment.id,
                    roadType = segment.roadType,
                    cityZone = segment.cityZone,
                    widthMeters = Mathf.Max(0.5f, segment.widthMeters),
                    laneCount = Mathf.Max(1, segment.laneCount),
                    laneWidthMeters = Mathf.Max(0.5f, segment.laneWidthMeters),
                    speedModifier = Mathf.Max(0.1f, segment.speedModifier),
                    noiseLevel = Mathf.Clamp01(segment.noiseLevel),
                    navArea = segment.navArea,
                    contributesPathArea = segment.contributesPathArea,
                    fromBiomeId = segment.fromBiomeId,
                    toBiomeId = segment.toBiomeId,
                    biomeTransition01 = Mathf.Clamp01(t),
                    patrolRoute = segment.contributesPatrolRoutes,
                    vehicleRoute = segment.supportsVehicles,
                    lootZone = segment.contributesLootZone,
                    lootZoneWeight = Mathf.Clamp01(segment.lootZoneWeight),
                    supportsSidewalks = segment.supportsSidewalks,
                    sidewalkWidthMeters = Mathf.Max(0f, segment.sidewalkWidthMeters),
                    supportsDecals = segment.supportsDecals,
                    decalDensity = Mathf.Clamp01(segment.decalDensity),
                    roadsideLotEligible = segment.contributesRoadsideLots,
                    roadsideLotWeight = Mathf.Clamp01(segment.roadsideLotWeight),
                    roadsideLotSpacingMeters = Mathf.Max(1f, segment.roadsideLotSpacingMeters),
                    roadsideLotDepthMeters = Mathf.Max(1f, segment.roadsideLotDepthMeters)
                });
            }
        }

        private static void ResolveSpawnPoints(
            GameplayRoadGraph graph,
            List<ResolvedRoadSpawnPoint> output,
            List<Vector3> polylineBuffer)
        {
            if (graph == null || output == null || polylineBuffer == null) return;

            var spawnPoints = graph.SpawnPoints;
            foreach (var source in spawnPoints)
            {
                if (source == null) continue;
                if (!graph.TryGetSegment(source.segmentId, out var segment) || segment == null) continue;
                if (!graph.TryGetSegmentPolyline(segment, polylineBuffer)) continue;
                if (!TryComputePolylineLength(polylineBuffer, out var totalLength) || totalLength <= 0.001f) continue;

                var distance = Mathf.Clamp01(source.normalizedDistance) * totalLength;
                if (!TryEvaluatePolylineAtDistance(polylineBuffer, distance, out var position, out var tangent)) continue;

                output.Add(new ResolvedRoadSpawnPoint
                {
                    spawnId = source.id,
                    segmentId = source.segmentId,
                    position = position,
                    tangent = tangent,
                    role = source.role,
                    radiusMeters = Mathf.Max(0f, source.radiusMeters),
                    weight = Mathf.Clamp01(source.weight),
                    requiresNavMesh = source.requiresNavMesh,
                    tag = source.tag
                });
            }
        }

        private static bool TryComputePolylineLength(IReadOnlyList<Vector3> polyline, out float totalLength)
        {
            totalLength = 0f;
            if (polyline == null || polyline.Count < 2) return false;

            for (var i = 1; i < polyline.Count; i++)
                totalLength += Vector3.Distance(polyline[i - 1], polyline[i]);

            return totalLength > 0.001f;
        }

        private static bool TryEvaluatePolylineAtDistance(
            IReadOnlyList<Vector3> polyline,
            float distance,
            out Vector3 position,
            out Vector3 tangent)
        {
            position = default;
            tangent = Vector3.forward;

            if (polyline == null || polyline.Count < 2) return false;

            var remaining = Mathf.Max(0f, distance);
            for (var i = 1; i < polyline.Count; i++)
            {
                var a = polyline[i - 1];
                var b = polyline[i];
                var seg = b - a;
                var segLen = seg.magnitude;
                if (segLen <= 0.0001f) continue;

                if (remaining <= segLen)
                {
                    var t = remaining / segLen;
                    position = Vector3.Lerp(a, b, t);
                    tangent = seg / segLen;
                    return true;
                }

                remaining -= segLen;
            }

            var last = polyline[^1];
            var prev = polyline[^2];
            var tail = last - prev;
            tangent = tail.sqrMagnitude > 0.0001f ? tail.normalized : Vector3.forward;
            position = last;
            return true;
        }
    }
}
