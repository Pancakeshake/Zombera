using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Builds footpath strips on street-facing district edges, outward toward the road
    ///     so strips sit in the corridor between opposite street-facing lot fronts.
    /// </summary>
    internal static class CityDistrictEdgeFootpathBuilder
    {
        private const float SharedEdgeEpsilonMeters = 0.5f;
        private const float AxisAlignedEdgeEpsilonMeters = 0.05f;
        private const float MinEdgeSpanMeters = 1f;

        public static int Build(
            Transform footpathsParent,
            Transform areasRoot,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight)
        {
            if (footpathsParent == null || areasRoot == null || settings == null || resolveHeight == null)
                return 0;
            if (!settings.spawnFootpathMeshes)
                return 0;

            var material = CityFootpathPlacer.ResolveFootpathMaterialForSettings(settings);
            if (material == null)
                return 0;

            var markers = CollectMarkers(areasRoot);
            if (markers.Count == 0)
                return 0;

            var width = Mathf.Max(0.5f, settings.footpathWidthMeters);
            var curbExtra = Mathf.Max(0f, settings.footpathCurbExtraMeters);
            var filletSetback = Mathf.Max(1f, settings.markerSnapDistanceMeters);
            var lift = Mathf.Max(0f, settings.footpathSurfaceLiftMeters);
            var aboveFill = Mathf.Max(0f, settings.footpathAboveDistrictFillMeters);
            var uvTile = Mathf.Max(0.25f, settings.footpathUvWorldUnitsPerTile);
            var placed = 0;
            var fenceScratch = new List<CityLotFenceEdgeUtility.FenceEdgeSegment>(32);
            var edgePolyline = new List<Vector2>(2);

            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                var areaTransform = marker.transform;
                var blockBounds = marker.GetHubShiftedBoundsXZ();

                // Fence exclusion reuses the marker's hub-shifted outline scratch — collect
                // fences first, then copy the outline so edge iteration stays stable.
                fenceScratch.Clear();
                CityFootpathFenceExclusion.CollectFenceEdgesForArea(
                    areaTransform, marker, blockBounds, fenceScratch);

                var outlineSource = CityNamedAreaPolygonUtility.ResolveHubShiftedOutlineXZ(marker);
                if (outlineSource == null || outlineSource.Count < 3)
                    continue;

                var outline = new List<Vector2>(outlineSource);
                var centroid = ComputeCentroid(outline);

                for (var e = 0; e < outline.Count; e++)
                {
                    var a = outline[e];
                    var b = outline[(e + 1) % outline.Count];

                    // Skip fillet/curved segments — only straight block edges get footpaths.
                    if (!IsAxisAlignedStreetEdge(a, b))
                        continue;

                    if (IsSharedBlockEdge(blockBounds, a, b, markers, marker))
                        continue;

                    edgePolyline.Clear();
                    edgePolyline.Add(a);
                    edgePolyline.Add(b);

                    var inward = ResolveInwardNormal(a, b, centroid);
                    // Fence gaps are detected inside the block near the street edge.
                    var spans = CityFootpathFenceExclusion.SplitEdgePolylineExcludingFences(
                        edgePolyline, width, inward, fenceScratch, MinEdgeSpanMeters);

                    for (var s = 0; s < spans.Count; s++)
                    {
                        var span = spans[s];
                        if (span == null || span.Count < 2)
                            continue;

                        // Keep the straight edge; only pull ends back when a fillet is adjacent.
                        if (LeadsToCurve(outline, e))
                            ApplyCornerSetback(span, filletSetback);
                        else
                            ExtendSpanEnds(span, a, b);

                        if (span.Count < 2 || PolylineLength(span) < MinEdgeSpanMeters)
                            continue;

                        var groundY = resolveHeight(centroid) + lift + aboveFill;
                        float SampleY(Vector2 xz) => groundY;

                        // Outward toward the road: house side of sidewalk, between facing lots.
                        var outward = -inward;
                        var houseSide = OffsetPolyline(span, outward, curbExtra);
                        var roadSide = OffsetPolyline(span, outward, curbExtra + width);
                        var mesh = RoadMeshBuilder.BuildBandMeshBetweenPolylines(
                            roadSide, houseSide, SampleY, uvTile);
                        if (mesh == null)
                            continue;

                        var go = new GameObject(
                            $"DistrictFootpath_{marker.AreaId}_{e}_{s}");
                        go.transform.SetParent(footpathsParent, false);
#if UNITY_EDITOR
                        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create District Footpath");
#endif
                        var mf = go.AddComponent<MeshFilter>();
                        var mr = go.AddComponent<MeshRenderer>();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterial = material;
                        mr.sharedMaterial.renderQueue = 2002; // above lot terrain (2001)
                        placed++;

                        FlattenTerrainForSpan(roadSide, houseSide, groundY);
                    }
                }
            }

            return placed;
        }

        private static List<CityNamedAreaMarker> CollectMarkers(Transform areasRoot)
        {
            var list = new List<CityNamedAreaMarker>(areasRoot.childCount);
            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var marker = areasRoot.GetChild(i).GetComponent<CityNamedAreaMarker>();
                if (marker != null)
                    list.Add(marker);
            }

            return list;
        }

        private static bool IsAxisAlignedStreetEdge(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < AxisAlignedEdgeEpsilonMeters
                   || Mathf.Abs(a.y - b.y) < AxisAlignedEdgeEpsilonMeters;
        }

        /// <summary>
        ///     True when the edge is adjacent to a curved/fillet segment.
        /// </summary>
        private static bool LeadsToCurve(IReadOnlyList<Vector2> outline, int index)
        {
            var count = outline.Count;
            if (count <= 4)
                return false;

            var prev = outline[(index - 1 + count) % count];
            var prevB = outline[index];
            var nextA = outline[(index + 1) % count];
            var nextB = outline[(index + 2) % count];

            return !IsAxisAlignedStreetEdge(prev, prevB)
                   || !IsAxisAlignedStreetEdge(nextA, nextB);
        }

        private static void ExtendSpanEnds(List<Vector2> span, Vector2 edgeStart, Vector2 edgeEnd)
        {
            const float extend = 0.5f;
            if (span == null || span.Count < 2)
                return;

            var edgeDir = (edgeEnd - edgeStart).normalized;
            if (edgeDir.sqrMagnitude < 0.0001f)
                return;

            span[0] -= edgeDir * extend;
            span[^1] += edgeDir * extend;
        }

        private static List<Vector2> OffsetPolyline(
            IReadOnlyList<Vector2> polyline,
            Vector2 normal,
            float distance)
        {
            var output = new List<Vector2>(polyline.Count);
            for (var i = 0; i < polyline.Count; i++)
                output.Add(polyline[i] + normal * distance);

            return output;
        }

        private static bool IsSharedBlockEdge(
            Rect block,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            IReadOnlyList<CityNamedAreaMarker> markers,
            CityNamedAreaMarker self)
        {
            for (var i = 0; i < markers.Count; i++)
            {
                var other = markers[i];
                if (other == self)
                    continue;

                if (CityBlockEdgeAdjacencyUtility.IsSharedStreetBlockEdge(
                        block, other.GetHubShiftedBoundsXZ(), edgeStart, edgeEnd, SharedEdgeEpsilonMeters))
                    return true;
            }

            return false;
        }

        private static Vector2 ResolveInwardNormal(Vector2 edgeStart, Vector2 edgeEnd, Vector2 centroid)
        {
            var tangent = (edgeEnd - edgeStart).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;

            var left = new Vector2(-tangent.y, tangent.x);
            var right = new Vector2(tangent.y, -tangent.x);
            var mid = (edgeStart + edgeEnd) * 0.5f;
            var toCentroid = centroid - mid;
            if (toCentroid.sqrMagnitude < 0.0001f)
                return left;

            return Vector2.Dot(left, toCentroid) >= Vector2.Dot(right, toCentroid) ? left : right;
        }

        private static Vector2 ComputeCentroid(IReadOnlyList<Vector2> outline)
        {
            if (outline == null || outline.Count == 0)
                return Vector2.zero;

            var sum = Vector2.zero;
            for (var i = 0; i < outline.Count; i++)
                sum += outline[i];

            return sum / outline.Count;
        }

        private static void ApplyCornerSetback(List<Vector2> span, float setback)
        {
            if (span == null || span.Count < 2 || setback <= 0f)
                return;

            var edgeDir = (span[^1] - span[0]).normalized;
            if (edgeDir.sqrMagnitude < 0.0001f)
                return;

            var length = Vector2.Distance(span[0], span[^1]);
            var trim = Mathf.Min(setback, length * 0.45f);
            if (length - 2f * trim < MinEdgeSpanMeters)
                return;

            span[0] += edgeDir * trim;
            span[^1] -= edgeDir * trim;
        }

        private static float PolylineLength(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            var length = 0f;
            for (var i = 1; i < points.Count; i++)
                length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        private static void FlattenTerrainForSpan(
            List<Vector2> outer, List<Vector2> inner, float targetY)
        {
            if (outer == null || inner == null || outer.Count < 2 || inner.Count < 2)
                return;

            var terrains = new List<Terrain>();
            if (!CityHubTerrainFlattener.TryResolvePinnedTerrains(out var pinned) ||
                pinned == null ||
                pinned.Count == 0)
            {
                var active = Terrain.activeTerrains;
                if (active == null || active.Length == 0)
                    return;
                terrains.AddRange(active);
            }
            else
            {
                terrains.AddRange(pinned);
            }

            var allPoints = new List<Vector2>(outer.Count + inner.Count);
            allPoints.AddRange(outer);
            allPoints.AddRange(inner);
            var rect = ComputeBoundingRect(allPoints);

            const float blend = 0.5f;
            for (var i = 0; i < terrains.Count; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null) continue;
                var tRect = new Rect(t.transform.position.x, t.transform.position.z,
                    t.terrainData.size.x, t.terrainData.size.z);
                if (!tRect.Overlaps(rect)) continue;

                CityTerrainFootprintFlattener.FlattenOnTerrains(
                    new[] { t }, rect, rect, targetY, 0f, blend, out _);
            }
        }

        private static Rect ComputeBoundingRect(List<Vector2> points)
        {
            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            var yMin = float.MaxValue;
            var yMax = float.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p.x < xMin) xMin = p.x;
                if (p.x > xMax) xMax = p.x;
                if (p.y < yMin) yMin = p.y;
                if (p.y > yMax) yMax = p.y;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
