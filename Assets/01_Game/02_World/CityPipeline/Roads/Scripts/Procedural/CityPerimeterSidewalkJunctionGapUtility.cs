using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Splits perimeter sidewalk spans only at highway ring exits, leaving a gap equal to
    ///     the highway road width.
    /// </summary>
    internal static class CityPerimeterSidewalkJunctionGapUtility
    {
        private const float HighwayAnchorMatchMeters = 8f;
        private const float MinEdgeSpanMeters = 1f;
        private const float CityCenterMatchMeters = 1f;

        internal readonly struct HighwayExitGap
        {
            public HighwayExitGap(Vector2 position, float gapWidthMeters, Vector2 cityCenter)
            {
                Position = position;
                GapWidthMeters = gapWidthMeters;
                CityCenter = cityCenter;
            }

            public Vector2 Position { get; }
            public float GapWidthMeters { get; }
            public Vector2 CityCenter { get; }
        }

        internal static List<HighwayExitGap> CollectHighwayExitGaps(
            IReadOnlyList<RoadPolyline> roads,
            IReadOnlyList<CityNamedArea> blocks,
            RoadNetworkSettings settings)
        {
            var gaps = new List<HighwayExitGap>(4);
            if (roads == null)
                return gaps;

            var cityCenters = CollectCityCenters(blocks);
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var start = road.pointsXZ[0];
                var end = road.pointsXZ[^1];
                var width = road.widthMeters > 0f
                    ? road.widthMeters
                    : settings != null
                        ? settings.ResolveWidthMeters(RoadClass.Highway)
                        : 12f;

                for (var c = 0; c < cityCenters.Count; c++)
                {
                    var cityCenter = cityCenters[c];
                    var startNear = (start - cityCenter).sqrMagnitude <= (end - cityCenter).sqrMagnitude;
                    var ringPoint = startNear ? start : end;
                    TryAddGap(gaps, ringPoint, width, cityCenter);
                }
            }

            return gaps;
        }

        private static void TryAddGap(
            List<HighwayExitGap> gaps,
            Vector2 position,
            float width,
            Vector2 cityCenter)
        {
            for (var i = 0; i < gaps.Count; i++)
            {
                var existing = gaps[i];
                if ((existing.Position - position).sqrMagnitude >= 0.25f)
                    continue;
                if ((existing.CityCenter - cityCenter).sqrMagnitude >= CityCenterMatchMeters * CityCenterMatchMeters)
                    continue;

                return;
            }

            gaps.Add(new HighwayExitGap(position, width, cityCenter));
        }

        private static List<Vector2> CollectCityCenters(IReadOnlyList<CityNamedArea> blocks)
        {
            var centers = new List<Vector2>(4);
            if (blocks == null)
                return centers;

            for (var i = 0; i < blocks.Count; i++)
            {
                var center = blocks[i].cityCenterXZ;
                if (center.sqrMagnitude < 0.01f)
                    continue;

                var duplicate = false;
                for (var c = 0; c < centers.Count; c++)
                {
                    if ((centers[c] - center).sqrMagnitude >= 1f)
                        continue;

                    duplicate = true;
                    break;
                }

                if (!duplicate)
                    centers.Add(center);
            }

            return centers;
        }

        internal static List<List<Vector2>> SplitSpanAtHighwayExitGaps(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<HighwayExitGap> gaps,
            Vector2 cityCenter)
        {
            var spans = new List<List<Vector2>>(2);
            if (polyline == null || polyline.Count < 2)
                return spans;
            if (gaps == null || gaps.Count == 0)
            {
                spans.Add(new List<Vector2>(polyline));
                return spans;
            }

            var cumulative = BuildCumulativeLengths(polyline);
            var totalLen = cumulative[^1];
            if (totalLen < MinEdgeSpanMeters)
                return spans;

            var blocked = new List<Vector2>(gaps.Count);
            for (var i = 0; i < gaps.Count; i++)
            {
                var gap = gaps[i];
                if ((gap.CityCenter - cityCenter).sqrMagnitude > CityCenterMatchMeters * CityCenterMatchMeters)
                    continue;

                if (!TryProjectAnchorDistance(polyline, cumulative, gap.Position, out var distance))
                    continue;

                var halfGap = Mathf.Max(1.5f, gap.GapWidthMeters * 0.5f);
                blocked.Add(new Vector2(
                    Mathf.Max(0f, distance - halfGap),
                    Mathf.Min(totalLen, distance + halfGap)));
            }

            if (blocked.Count == 0)
            {
                spans.Add(new List<Vector2>(polyline));
                return spans;
            }

            blocked.Sort((x, y) => x.x.CompareTo(y.x));
            var cursor = 0f;
            for (var i = 0; i < blocked.Count; i++)
            {
                var start = blocked[i].x;
                var end = blocked[i].y;
                if (start > cursor + MinEdgeSpanMeters)
                    spans.Add(ExtractSubPolyline(polyline, cumulative, cursor, start));
                cursor = Mathf.Max(cursor, end);
            }

            if (totalLen > cursor + MinEdgeSpanMeters)
                spans.Add(ExtractSubPolyline(polyline, cumulative, cursor, totalLen));

            return spans;
        }

        private static List<float> BuildCumulativeLengths(IReadOnlyList<Vector2> polyline)
        {
            var cumulative = new List<float>(polyline.Count) { 0f };
            for (var i = 1; i < polyline.Count; i++)
                cumulative.Add(cumulative[i - 1] + Vector2.Distance(polyline[i - 1], polyline[i]));
            return cumulative;
        }

        private static bool TryProjectAnchorDistance(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            Vector2 anchor,
            out float distance)
        {
            distance = 0f;
            var bestDist = HighwayAnchorMatchMeters;
            var found = false;

            for (var i = 0; i < polyline.Count - 1; i++)
            {
                var a = polyline[i];
                var b = polyline[i + 1];
                var projectedDist = DistancePointToSegment(anchor, a, b);
                if (projectedDist >= bestDist)
                    continue;

                var ab = b - a;
                var lenSq = ab.sqrMagnitude;
                if (lenSq < 1e-6f)
                    continue;

                var t = Mathf.Clamp01(Vector2.Dot(anchor - a, ab) / lenSq);
                bestDist = projectedDist;
                distance = cumulative[i] + Vector2.Distance(a, b) * t;
                found = true;
            }

            if (found)
                return true;

            for (var i = 0; i < polyline.Count; i++)
            {
                var vertexDist = Vector2.Distance(polyline[i], anchor);
                if (vertexDist >= bestDist)
                    continue;

                bestDist = vertexDist;
                distance = cumulative[i];
                found = true;
            }

            return found;
        }

        private static List<Vector2> ExtractSubPolyline(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            float startDistance,
            float endDistance)
        {
            var span = new List<Vector2>(8);
            span.Add(SamplePolylineAtDistance(polyline, cumulative, startDistance));

            for (var i = 0; i < polyline.Count; i++)
            {
                var d = cumulative[i];
                if (d > startDistance + 0.001f && d < endDistance - 0.001f)
                    AppendPoint(span, polyline[i]);
            }

            AppendPoint(span, SamplePolylineAtDistance(polyline, cumulative, endDistance));
            DedupeConsecutive(span);
            return span;
        }

        private static Vector2 SamplePolylineAtDistance(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<float> cumulative,
            float distance)
        {
            for (var i = 0; i < polyline.Count - 1; i++)
            {
                var segStart = cumulative[i];
                var segEnd = cumulative[i + 1];
                if (distance < segStart || distance > segEnd + 0.0001f)
                    continue;

                var segLen = segEnd - segStart;
                if (segLen < 1e-6f)
                    return polyline[i];

                var t = Mathf.Clamp01((distance - segStart) / segLen);
                return Vector2.Lerp(polyline[i], polyline[i + 1], t);
            }

            return polyline[^1];
        }

        private static void AppendPoint(List<Vector2> run, Vector2 point)
        {
            if (run.Count > 0 && (run[^1] - point).sqrMagnitude < 0.0001f)
                return;
            run.Add(point);
        }

        private static void DedupeConsecutive(List<Vector2> points)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude < 0.0001f)
                    points.RemoveAt(i);
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f)
                return Vector2.Distance(point, a);

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
