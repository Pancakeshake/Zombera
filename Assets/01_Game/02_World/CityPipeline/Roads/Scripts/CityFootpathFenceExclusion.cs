using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using FenceEdgeSegment = Zombera.World.City.CityLotFenceEdgeUtility.FenceEdgeSegment;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Collects fence segments for a district block and subtracts fenced spans from footpath edges.
    /// </summary>
    internal static class CityFootpathFenceExclusion
    {
        private const string FencesContainerName = "Fences";
        private const float DefaultFenceBufferMeters = 0.35f;

        public static void CollectFenceEdgesForArea(
            Transform areaTransform,
            CityNamedAreaMarker marker,
            Rect lotBounds,
            List<FenceEdgeSegment> edges)
        {
            if (areaTransform == null || marker == null || edges == null)
                return;

            var lots = CityLotFenceEdgeUtility.CollectLotSourcesFromArea(areaTransform);
            if (lots.Count > 0)
            {
                var outline = CityNamedAreaPolygonUtility.ResolveHubShiftedOutlineXZ(marker);
                var blockBounds = lotBounds.width > 0f && lotBounds.height > 0f
                    ? lotBounds
                    : marker.GetHubShiftedBoundsXZ();
                CityLotFenceEdgeUtility.CollectPredictedFenceEdgesForBlock(
                    blockBounds, lots, outline, edges);
                return;
            }

            CollectSceneFenceEdges(areaTransform, edges);
        }

        /// <param name="corridorNormal">
        ///     Unit normal pointing inward from the district outline toward the block interior.
        ///     The corridor band spans from the outline (depth 0) to outline + inward * corridorDepthMeters.
        /// </param>
        public static List<List<Vector2>> SplitEdgePolylineExcludingFences(
            IReadOnlyList<Vector2> edgePolyline,
            float corridorDepthMeters,
            Vector2 corridorNormal,
            IReadOnlyList<FenceEdgeSegment> fences,
            float minSpanMeters = 1f,
            float fenceBufferMeters = DefaultFenceBufferMeters)
        {
            var result = new List<List<Vector2>>();
            if (edgePolyline == null || edgePolyline.Count < 2)
                return result;

            var cumulative = BuildCumulativeLengths(edgePolyline, out var totalLen);
            if (totalLen < minSpanMeters)
                return result;

            if (fences == null || fences.Count == 0)
            {
                result.Add(new List<Vector2>(edgePolyline));
                return result;
            }

            var excluded = new List<(float start, float end)>();
            for (var i = 0; i < fences.Count; i++)
            {
                if (TryGetExcludedInterval(
                        edgePolyline, cumulative, totalLen, corridorDepthMeters, corridorNormal,
                        fences[i], fenceBufferMeters, out var start, out var end))
                    excluded.Add((start, end));
            }

            var kept = SubtractIntervals(totalLen, MergeIntervals(excluded), minSpanMeters);
            for (var i = 0; i < kept.Count; i++)
                result.Add(ExtractSubPolyline(edgePolyline, cumulative, kept[i].start, kept[i].end));

            return result;
        }

        private static void CollectSceneFenceEdges(Transform areaTransform, List<FenceEdgeSegment> edges)
        {
            var fencesRoot = areaTransform.Find(FencesContainerName);
            if (fencesRoot == null)
                return;

            for (var i = 0; i < fencesRoot.childCount; i++)
                AppendFenceEdgeFromInstance(fencesRoot.GetChild(i), edges);
        }

        private static void AppendFenceEdgeFromInstance(Transform instance, List<FenceEdgeSegment> edges)
        {
            if (instance == null || edges == null)
                return;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var center = new Vector2(bounds.center.x, bounds.center.z);
            var ext = bounds.extents;
            if (ext.x >= ext.z)
            {
                edges.Add(new FenceEdgeSegment(
                    center.x - ext.x, center.y,
                    center.x + ext.x, center.y));
            }
            else
            {
                edges.Add(new FenceEdgeSegment(
                    center.x, center.y - ext.z,
                    center.x, center.y + ext.z));
            }
        }

        private static bool TryGetExcludedInterval(
            IReadOnlyList<Vector2> edgePolyline,
            IReadOnlyList<float> cumulative,
            float totalLen,
            float corridorDepthMeters,
            Vector2 corridorNormal,
            FenceEdgeSegment fence,
            float fenceBufferMeters,
            out float start,
            out float end)
        {
            start = end = 0f;
            var f0 = fence.Start;
            var f1 = fence.End;
            var minDist = MinDistanceSegmentToPolyline(f0, f1, edgePolyline, out var along0, out var along1);
            if (minDist > corridorDepthMeters + fenceBufferMeters)
                return false;

            var mid = (f0 + f1) * 0.5f;
            if (!IsPointInCorridor(mid, edgePolyline, cumulative, corridorDepthMeters, corridorNormal, fenceBufferMeters))
                return false;

            start = Mathf.Max(0f, Mathf.Min(along0, along1) - fenceBufferMeters);
            end = Mathf.Min(totalLen, Mathf.Max(along0, along1) + fenceBufferMeters);
            return end - start >= 0.05f;
        }

        /// <param name="corridorNormal">Inward from district outline toward lots.</param>
        private static bool IsPointInCorridor(
            Vector2 point,
            IReadOnlyList<Vector2> edgePolyline,
            IReadOnlyList<float> cumulative,
            float corridorDepthMeters,
            Vector2 corridorNormal,
            float lateralToleranceMeters)
        {
            if (!TryProjectPointOntoPolyline(point, edgePolyline, cumulative, out var along, out var closest))
                return false;

            var lateral = point - closest;
            var depth = Vector2.Dot(lateral, corridorNormal);

            // Reject points on the road side of the outline.
            if (depth < -lateralToleranceMeters)
                return false;

            // Reject points deeper than the inward footpath band.
            if (depth > corridorDepthMeters + lateralToleranceMeters)
                return false;

            return along >= -lateralToleranceMeters &&
                   along <= cumulative[^1] + lateralToleranceMeters;
        }

        private static float MinDistanceSegmentToPolyline(
            Vector2 segStart,
            Vector2 segEnd,
            IReadOnlyList<Vector2> polyline,
            out float alongStart,
            out float alongEnd)
        {
            alongStart = 0f;
            alongEnd = 0f;
            var best = float.MaxValue;

            ProjectPointOntoPolyline(segStart, polyline, out alongStart, out _);
            ProjectPointOntoPolyline(segEnd, polyline, out alongEnd, out _);

            for (var i = 1; i < polyline.Count; i++)
            {
                var d = DistancePointToSegment(segStart, polyline[i - 1], polyline[i], out _);
                if (d < best) best = d;
                d = DistancePointToSegment(segEnd, polyline[i - 1], polyline[i], out _);
                if (d < best) best = d;
                d = DistanceSegmentToSegment(segStart, segEnd, polyline[i - 1], polyline[i]);
                if (d < best) best = d;
            }

            return best;
        }

        private static List<(float start, float end)> SubtractIntervals(
            float totalLen,
            List<(float start, float end)> excluded,
            float minSpanMeters)
        {
            var kept = new List<(float start, float end)>();
            var cursor = 0f;
            for (var i = 0; i < excluded.Count; i++)
            {
                var gapStart = cursor;
                var gapEnd = excluded[i].start;
                if (gapEnd - gapStart >= minSpanMeters)
                    kept.Add((gapStart, gapEnd));

                cursor = Mathf.Max(cursor, excluded[i].end);
            }

            if (totalLen - cursor >= minSpanMeters)
                kept.Add((cursor, totalLen));

            return kept;
        }

        private static List<(float start, float end)> MergeIntervals(List<(float start, float end)> intervals)
        {
            if (intervals == null || intervals.Count == 0)
                return new List<(float start, float end)>();

            intervals.Sort((a, b) => a.start.CompareTo(b.start));
            var merged = new List<(float start, float end)> { intervals[0] };
            for (var i = 1; i < intervals.Count; i++)
            {
                var last = merged[^1];
                var current = intervals[i];
                if (current.start <= last.end + 0.01f)
                {
                    merged[^1] = (last.start, Mathf.Max(last.end, current.end));
                    continue;
                }

                merged.Add(current);
            }

            return merged;
        }

        private static List<Vector2> ExtractSubPolyline(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            float startDist,
            float endDist)
        {
            var sub = new List<Vector2>();
            if (polyline == null || polyline.Count < 2)
                return sub;

            sub.Add(SamplePolylineAtDistance(polyline, cumulative, startDist));
            for (var i = 0; i < polyline.Count; i++)
            {
                var d = cumulative[i];
                if (d > startDist + 0.001f && d < endDist - 0.001f)
                    sub.Add(polyline[i]);
            }

            sub.Add(SamplePolylineAtDistance(polyline, cumulative, endDist));
            return sub;
        }

        private static Vector2 SamplePolylineAtDistance(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            float distance)
        {
            distance = Mathf.Clamp(distance, 0f, cumulative[^1]);
            for (var i = 1; i < polyline.Count; i++)
            {
                if (distance > cumulative[i])
                    continue;

                var segStart = cumulative[i - 1];
                var segLen = cumulative[i] - segStart;
                if (segLen < 0.0001f)
                    return polyline[i];

                var t = (distance - segStart) / segLen;
                return Vector2.Lerp(polyline[i - 1], polyline[i], t);
            }

            return polyline[^1];
        }

        private static List<float> BuildCumulativeLengths(IReadOnlyList<Vector2> polyline, out float totalLen)
        {
            var cumulative = new List<float>(polyline.Count) { 0f };
            totalLen = 0f;
            for (var i = 1; i < polyline.Count; i++)
            {
                totalLen += Vector2.Distance(polyline[i - 1], polyline[i]);
                cumulative.Add(totalLen);
            }

            return cumulative;
        }

        private static bool TryProjectPointOntoPolyline(
            Vector2 point,
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            out float along,
            out Vector2 closest)
        {
            along = 0f;
            closest = default;
            if (polyline == null || polyline.Count < 2)
                return false;

            var bestDist = float.MaxValue;
            var bestAlong = 0f;
            var bestPoint = polyline[0];
            for (var i = 1; i < polyline.Count; i++)
            {
                var d = DistancePointToSegment(point, polyline[i - 1], polyline[i], out var t);
                if (d >= bestDist)
                    continue;

                bestDist = d;
                var segLen = cumulative[i] - cumulative[i - 1];
                bestAlong = cumulative[i - 1] + segLen * t;
                bestPoint = Vector2.Lerp(polyline[i - 1], polyline[i], t);
            }

            along = bestAlong;
            closest = bestPoint;
            return true;
        }

        private static void ProjectPointOntoPolyline(
            Vector2 point,
            IReadOnlyList<Vector2> polyline,
            out float along,
            out Vector2 closest)
        {
            var cumulative = BuildCumulativeLengths(polyline, out _);
            TryProjectPointOntoPolyline(point, polyline, cumulative, out along, out closest);
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.000001f)
            {
                t = 0f;
                return Vector2.Distance(point, a);
            }

            t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            var closest = a + ab * t;
            return Vector2.Distance(point, closest);
        }

        private static float DistanceSegmentToSegment(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            return Mathf.Min(
                DistancePointToSegment(p1, q1, q2, out _),
                Mathf.Min(
                    DistancePointToSegment(p2, q1, q2, out _),
                    Mathf.Min(
                        DistancePointToSegment(q1, p1, p2, out _),
                        DistancePointToSegment(q2, p1, p2, out _))));
        }
    }
}