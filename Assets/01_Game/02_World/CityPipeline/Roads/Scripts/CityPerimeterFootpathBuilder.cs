using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Places tiled footpath bands outside perimeter block cell outlines, including
    ///     rounded arterial corners, stopping at highway exit anchors.
    /// </summary>
    internal static partial class CityPerimeterFootpathBuilder
    {
        private const float SharedEdgeEpsilonMeters = 0.5f;
        private const float MinEdgeSpanMeters = 1f;
        private const float FootpathOutsetMeters = 0.35f;

        public static int Build(
            Transform footpathsParent,
            IReadOnlyList<CityNamedArea> blocks,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralLayerMeshAccumulator accumulator = null)
        {
            if (footpathsParent == null || blocks == null || blocks.Count == 0 || settings == null ||
                resolveHeight == null)
                return 0;
            if (!settings.spawnFootpathMeshes)
                return 0;

            var material = ResolvePerimeterFootpathMaterial(settings);
            if (material == null)
                return 0;

            var districtInset = CityMathBlockLayoutGenerator.ResolveDistrictBlockInsetMeters(settings);
            var footpathWidth = Mathf.Max(0.5f, settings.footpathWidthMeters);
            var lift = Mathf.Max(0f, settings.footpathSurfaceLiftMeters);
            var aboveFill = Mathf.Max(0f, settings.footpathAboveDistrictFillMeters);
            var uvTile = Mathf.Max(0.25f, settings.footpathUvWorldUnitsPerTile);
            var exitSetback = Mathf.Max(2f, settings.footpathJunctionSetbackMeters);
            var cityCenter = ResolveCityCenter(blocks);
            var highwayAnchors = CollectHighwayRingAnchors(roads, cityCenter);
            var placed = 0;

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var cellRect = ExpandRect(block.boundsXZ, districtInset);
                var cellOutline = BuildCellOutline(block, districtInset);
                if (cellOutline.Count < 3)
                    continue;

                if (!HasExteriorEdge(cellRect, cellOutline, blocks, block, districtInset))
                    continue;

                var centroid = CityNamedAreaOutlineBuilder.ComputePolygonCentroid(cellOutline);
                var groundY = resolveHeight(centroid) + lift + aboveFill;
                float SampleY(Vector2 xz) => groundY;

                var spanRun = new List<Vector2>(16);
                for (var e = 0; e < cellOutline.Count; e++)
                {
                    var a = cellOutline[e];
                    var b = cellOutline[(e + 1) % cellOutline.Count];
                    if (!IsExteriorCellEdge(cellRect, a, b, blocks, block, districtInset) ||
                        IsHighwaySegment(a, b, roads))
                    {
                        placed += FlushRun(
                            footpathsParent, block.id, placed, spanRun, centroid, highwayAnchors, exitSetback,
                            footpathWidth, material, SampleY, uvTile, accumulator);
                        continue;
                    }

                    AppendPoint(spanRun, a);
                    AppendPoint(spanRun, b);
                }

                placed += FlushRun(
                    footpathsParent, block.id, placed, spanRun, centroid, highwayAnchors, exitSetback,
                    footpathWidth, material, SampleY, uvTile, accumulator);
            }

            return placed;
        }

        private static bool HasExteriorEdge(
            Rect cellRect,
            IReadOnlyList<Vector2> cellOutline,
            IReadOnlyList<CityNamedArea> blocks,
            CityNamedArea block,
            float districtInset)
        {
            for (var e = 0; e < cellOutline.Count; e++)
            {
                var a = cellOutline[e];
                var b = cellOutline[(e + 1) % cellOutline.Count];
                if (IsExteriorCellEdge(cellRect, a, b, blocks, block, districtInset))
                    return true;
            }

            return false;
        }

        private static Material ResolvePerimeterFootpathMaterial(RoadNetworkSettings settings)
        {
            if (settings.footpathMaterial != null)
                return settings.footpathMaterial;

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                RoadKitPrefabs.DefaultPerimeterFootpathMaterialPath);
#else
            return null;
#endif
        }

        private static List<Vector2> BuildCellOutline(CityNamedArea block, float districtInset)
        {
            var cellRect = ExpandRect(block.boundsXZ, districtInset);
            if (block.HasRoundedOutline)
            {
                return CityNamedAreaOutlineBuilder.BuildMeshOutline(
                    cellRect,
                    block.roundedCorners,
                    block.arterialCornerRadiusMeters,
                    0f);
            }

            return CityNamedAreaOutlineBuilder.BuildMeshOutline(
                cellRect,
                CityBlockCornerMask.None,
                0f,
                0f);
        }

        private static bool IsExteriorCellEdge(
            Rect cellRect,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            IReadOnlyList<CityNamedArea> blocks,
            CityNamedArea self,
            float districtInset)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var other = blocks[i];
                if (other.id == self.id)
                    continue;

                var otherCell = ExpandRect(other.boundsXZ, districtInset);
                if (CityBlockEdgeAdjacencyUtility.IsSharedStreetBlockEdge(
                        cellRect, otherCell, edgeStart, edgeEnd, SharedEdgeEpsilonMeters))
                    return false;
            }

            return true;
        }

        private static bool IsHighwaySegment(Vector2 a, Vector2 b, IReadOnlyList<RoadPolyline> roads)
        {
            var mid = (a + b) * 0.5f;
            var span = Vector2.Distance(a, b);
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                for (var p = 0; p < road.pointsXZ.Count - 1; p++)
                {
                    if (DistancePointToSegment(mid, road.pointsXZ[p], road.pointsXZ[p + 1]) <=
                        Mathf.Max(road.widthMeters * 0.5f, 4f) + span * 0.5f)
                        return true;
                }
            }

            return false;
        }

        private static Vector2 ResolveOutwardNormal(Vector2 edgeStart, Vector2 edgeEnd, Vector2 centroid)
        {
            var tangent = (edgeEnd - edgeStart).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;

            var left = new Vector2(-tangent.y, tangent.x);
            var right = new Vector2(tangent.y, -tangent.x);
            var mid = (edgeStart + edgeEnd) * 0.5f;
            var toCentroid = centroid - mid;
            if (toCentroid.sqrMagnitude < 0.0001f)
                return -left;

            var inward = Vector2.Dot(left, toCentroid) >= Vector2.Dot(right, toCentroid) ? left : right;
            return -inward;
        }

        private static List<List<Vector2>> SplitPolylineExcludingAnchors(
            IReadOnlyList<Vector2> polyline,
            IReadOnlyList<Vector2> anchors,
            float exclusionRadius)
        {
            var spans = new List<List<Vector2>>(2);
            if (polyline == null || polyline.Count < 2)
                return spans;
            if (anchors == null || anchors.Count == 0 || exclusionRadius <= 0f)
            {
                spans.Add(new List<Vector2>(polyline));
                return spans;
            }

            var cumulative = BuildCumulativeLengths(polyline);
            var totalLen = cumulative[^1];
            if (totalLen < MinEdgeSpanMeters)
                return spans;

            var blocked = new List<Vector2>(anchors.Count * 2);
            for (var i = 0; i < anchors.Count; i++)
            {
                if (!TryProjectAnchorDistance(polyline, cumulative, anchors[i], out var distance))
                    continue;

                blocked.Add(new Vector2(
                    Mathf.Max(0f, distance - exclusionRadius),
                    Mathf.Min(totalLen, distance + exclusionRadius)));
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
            var bestDist = float.MaxValue;
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

        private static float PolylineLength(IReadOnlyList<Vector2> polyline)
        {
            var length = 0f;
            for (var i = 1; i < polyline.Count; i++)
                length += Vector2.Distance(polyline[i - 1], polyline[i]);
            return length;
        }

        private static Vector2 ResolveCityCenter(IReadOnlyList<CityNamedArea> blocks)
        {
            if (blocks[0].cityCenterXZ.sqrMagnitude > 0.01f)
                return blocks[0].cityCenterXZ;

            var sum = Vector2.zero;
            for (var i = 0; i < blocks.Count; i++)
                sum += blocks[i].centerXZ;
            return sum / blocks.Count;
        }

        private static List<Vector2> CollectHighwayRingAnchors(
            IReadOnlyList<RoadPolyline> roads,
            Vector2 cityCenter)
        {
            var anchors = new List<Vector2>(4);
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var start = road.pointsXZ[0];
                var end = road.pointsXZ[^1];
                anchors.Add((start - cityCenter).sqrMagnitude <= (end - cityCenter).sqrMagnitude ? start : end);
            }

            return anchors;
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

        private static Rect ExpandRect(Rect rect, float padding) =>
            Rect.MinMaxRect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.xMax + padding,
                rect.yMax + padding);
    }
}
