using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public static class CityNamedAreaPolygonUtility
    {
        public static IReadOnlyList<Vector2> ResolveOutlineXZ(CityNamedAreaMarker marker)
        {
            if (marker == null)
                return System.Array.Empty<Vector2>();

            var outline = marker.OutlineXZ;
            if (outline != null && outline.Length >= 3)
                return outline;

            return ResolveBoundsRectOutline(marker.BoundsXZ);
        }

        public static IReadOnlyList<Vector2> ResolveHubShiftedOutlineXZ(CityNamedAreaMarker marker)
        {
            if (marker == null)
                return System.Array.Empty<Vector2>();

            return marker.GetHubShiftedOutlineXZ();
        }

        private static Vector2[] ResolveBoundsRectOutline(Rect rect)
        {
            return new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };
        }

        public static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var intersects = pi.y > point.y != pj.y > point.y
                                 && point.x < (pj.x - pi.x) * (point.y - pi.y) / ((pj.y - pi.y) + 0.000001f) + pi.x;
                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        public static bool ContainsAxisAlignedRect(
            IReadOnlyList<Vector2> polygon,
            Vector2 center,
            float halfWidth,
            float halfDepth)
        {
            return ContainsAxisAlignedRectSampled(polygon, center, halfWidth, halfDepth, 1f);
        }

        public static bool ContainsAxisAlignedRectSampled(
            IReadOnlyList<Vector2> polygon,
            Vector2 center,
            float halfWidth,
            float halfDepth,
            float edgeSampleStepMeters)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            if (!ContainsPoint(polygon, center))
                return false;

            edgeSampleStepMeters = Mathf.Max(0.75f, edgeSampleStepMeters);
            var xMin = center.x - halfWidth;
            var xMax = center.x + halfWidth;
            var zMin = center.y - halfDepth;
            var zMax = center.y + halfDepth;

            for (var x = xMin; x <= xMax + 0.001f; x += edgeSampleStepMeters)
            {
                if (!ContainsPoint(polygon, new Vector2(x, zMin)))
                    return false;
                if (!ContainsPoint(polygon, new Vector2(x, zMax)))
                    return false;
            }

            for (var z = zMin; z <= zMax + 0.001f; z += edgeSampleStepMeters)
            {
                if (!ContainsPoint(polygon, new Vector2(xMin, z)))
                    return false;
                if (!ContainsPoint(polygon, new Vector2(xMax, z)))
                    return false;
            }

            return true;
        }

        public static float ComputeMaxDepthForAxisRect(
            IReadOnlyList<Vector2> polygon,
            float xMin,
            float zMin,
            float width,
            float maxDepth,
            float stepMeters)
        {
            if (polygon == null || polygon.Count < 3 || maxDepth <= 0.01f || width <= 0.01f)
                return 0f;

            stepMeters = Mathf.Max(0.75f, stepMeters);
            var best = 0f;
            for (var depth = stepMeters; depth <= maxDepth + 0.001f; depth += stepMeters)
            {
                var center = new Vector2(xMin + width * 0.5f, zMin + depth * 0.5f);
                if (!ContainsAxisAlignedRectSampled(polygon, center, width * 0.5f, depth * 0.5f, stepMeters))
                    break;

                best = depth;
            }

            return best;
        }

        /// <summary>
        ///     Symmetric to <see cref="ComputeMaxDepthForAxisRect"/> but shrinks width (X)
        ///     for east/west column clipping at filleted corners.
        /// </summary>
        public static float ComputeMaxWidthForAxisRect(
            IReadOnlyList<Vector2> polygon,
            float xMin,
            float zMin,
            float depth,
            float maxWidth,
            float stepMeters)
        {
            if (polygon == null || polygon.Count < 3 || maxWidth <= 0.01f || depth <= 0.01f)
                return 0f;

            stepMeters = Mathf.Max(0.75f, stepMeters);
            var best = 0f;
            for (var width = stepMeters; width <= maxWidth + 0.001f; width += stepMeters)
            {
                var center = new Vector2(xMin + width * 0.5f, zMin + depth * 0.5f);
                if (!ContainsAxisAlignedRectSampled(polygon, center, width * 0.5f, depth * 0.5f, stepMeters))
                    break;

                best = width;
            }

            return best;
        }

        /// <summary>
        ///     Shrinks <paramref name="lotRect"/> inward (depth first from the back row edge,
        ///     then width from the side closest to a rounded corner) until it fits entirely
        ///     inside <paramref name="polygon"/>.  Returns false if the result drops below
        ///     <paramref name="minDepth"/> or <paramref name="minWidth"/>.
        /// </summary>
        public static bool TryFitAxisAlignedLotInPolygon(
            IReadOnlyList<Vector2> polygon,
            ref Rect lotRect,
            float stepMeters,
            float minDepth,
            float minWidth)
        {
            if (polygon == null || polygon.Count < 3) return true; // no polygon → always fits

            stepMeters = Mathf.Max(0.75f, stepMeters);
            var halfW = lotRect.width * 0.5f;
            var halfD = lotRect.height * 0.5f;
            var center = lotRect.center;

            if (ContainsAxisAlignedRectSampled(polygon, center, halfW, halfD, stepMeters))
                return true;

            // Shrink depth from the "back" row edge (the edge farther from the block boundary).
            // Use the edge that is NOT on the block boundary as the shrink side.
            var originalDepth = lotRect.height;
            var originalYMin = lotRect.yMin;
            var maxDepth = ComputeMaxDepthForAxisRect(polygon, lotRect.xMin, lotRect.yMin,
                lotRect.width, lotRect.height, stepMeters);
            if (maxDepth < minDepth) return false;

            lotRect.yMax = lotRect.yMin + maxDepth;

            // Shrink width symmetrically from the side closest to a rounded corner.
            halfW = lotRect.width * 0.5f;
            halfD = lotRect.height * 0.5f;
            center = lotRect.center;
            if (!ContainsAxisAlignedRectSampled(polygon, center, halfW, halfD, stepMeters))
            {
                var maxWidth = ComputeMaxWidthForAxisRect(polygon, lotRect.xMin, lotRect.yMin,
                    lotRect.height, lotRect.width, stepMeters);
                if (maxWidth < minWidth) return false;

                lotRect.xMax = lotRect.xMin + maxWidth;
            }

            return true;
        }

        /// <summary>
        ///     Sutherland–Hodgman clip of <paramref name="polygon"/> (convex) against
        ///     <paramref name="rect"/>.  Result appended to <paramref name="output"/>.
        /// </summary>
        public static void TryClipRectToConvexPolygon(
            IReadOnlyList<Vector2> polygon,
            Rect rect,
            List<Vector2> output)
        {
            output.Clear();
            if (polygon == null || polygon.Count < 3) return;

            // Clip edges: left (x >= xMin), right (x <= xMax), bottom (y >= yMin), top (y <= yMax)
            var input = new List<Vector2>(polygon);
            var temp = new List<Vector2>();

            ClipPolygonAgainstLine(input, temp, 1f, 0f, rect.xMin);  // left
            ClipPolygonAgainstLine(temp, input, -1f, 0f, -rect.xMax); // right
            ClipPolygonAgainstLine(input, temp, 0f, 1f, rect.yMin);  // bottom
            ClipPolygonAgainstLine(temp, output, 0f, -1f, -rect.yMax); // top
        }

        private static void ClipPolygonAgainstLine(
            IReadOnlyList<Vector2> input,
            List<Vector2> output,
            float nx, float ny, float d)
        {
            output.Clear();
            if (input.Count == 0) return;

            for (int i = 0; i < input.Count; i++)
            {
                var current = input[i];
                var next = input[(i + 1) % input.Count];

                var dCur = nx * current.x + ny * current.y - d;
                var dNext = nx * next.x + ny * next.y - d;

                if (dCur >= 0f) output.Add(current);

                if (dCur * dNext < 0f)
                {
                    var t = dCur / (dCur - dNext);
                    output.Add(new Vector2(
                        current.x + t * (next.x - current.x),
                        current.y + t * (next.y - current.y)));
                }
            }
        }

        /// <summary>
        ///     Wraps <see cref="TryClipRectToConvexPolygon"/> with a minimum-area check.
        ///     Returns the clipped polygon and whether its area exceeds <paramref name="minArea"/>.
        /// </summary>
        public static bool TryComputeLotClipPolygon(
            IReadOnlyList<Vector2> outline,
            Rect lotRect,
            List<Vector2> output,
            float minArea)
        {
            TryClipRectToConvexPolygon(outline, lotRect, output);
            if (output.Count < 3) return false;

            var area = ComputePolygonArea(output);
            return area >= minArea;
        }

        public static float ComputePolygonArea(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0f;
            var area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        /// <summary>
        ///     Returns true when every sampled point along the edge from (x0,z0) to (x1,z1)
        ///     is within <paramref name="tolerance"/> of some segment in <paramref name="outline"/>.
        ///     Works for arc edges because fillet arc vertices are part of the outline.
        /// </summary>
        public static bool IsEdgeOnOuterOutline(
            float x0, float z0, float x1, float z1,
            IReadOnlyList<Vector2> outline, float tolerance)
        {
            if (outline == null || outline.Count < 3) return false;

            var length = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            var segments = Mathf.Max(1, Mathf.CeilToInt(length / 1.5f));
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                var px = x0 + (x1 - x0) * t;
                var pz = z0 + (z1 - z0) * t;
                var onOutline = false;
                for (var i = 0; i < outline.Count; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Count];
                    var abx = b.x - a.x;
                    var aby = b.y - a.y;
                    var apx = px - a.x;
                    var apy = pz - a.y;
                    var proj = Mathf.Clamp01((apx * abx + apy * aby) / Mathf.Max(0.0001f, abx * abx + aby * aby));
                    var cx = a.x + proj * abx;
                    var cy = a.y + proj * aby;
                    if ((px - cx) * (px - cx) + (pz - cy) * (pz - cy) <= tolerance * tolerance)
                    { onOutline = true; break; }
                }

                if (!onOutline) return false;
            }

            return true;
        }

        /// <summary>
        ///     Returns true when every sampled point along the edge is within
        ///     <paramref name="tolerance"/> of some outline segment.
        ///     Wider variant of <see cref="IsEdgeOnOuterOutline"/> for footpath
        ///     corridor exclusion on curved blocks after the block shrink.
        /// </summary>
        public static bool IsEdgeNearOuterOutline(
            float x0, float z0, float x1, float z1,
            IReadOnlyList<Vector2> outline, float tolerance)
        {
            return IsEdgeOnOuterOutline(x0, z0, x1, z1, outline, tolerance);
        }

        /// <summary>
        ///     Returns the L-shaped bounds from neighbour lot edges to the block corner
        ///     that the gap-fill corner lot should occupy.  Much larger than the old
        ///     filletR×2 reserve so the L-gap between rows/columns is filled.
        /// </summary>
        public static Rect GetCornerFillBounds(Rect block, CityBlockCornerMask corners,
            float filletRadius, float lotDepthMax, IReadOnlyList<Rect> squareLots)
        {
            // Default inner corner: at least filletR×3 from the block corner.
            var minExtent = Mathf.Max(filletRadius * 3f, lotDepthMax * 0.5f);
            float innerX, innerZ;

            if (Has(corners, CityBlockCornerMask.BottomRight))
            {
                innerX = MaxLotEdge(squareLots, block.yMin, axisX: true, blockSide: block.xMax) ?? block.xMax - minExtent;
                innerZ = MaxLotEdge(squareLots, block.xMax, axisX: false, blockSide: block.yMax) ?? block.yMax; // fill to top if no column lots
                return Rect.MinMaxRect(innerX, block.yMin, block.xMax, innerZ);
            }
            if (Has(corners, CityBlockCornerMask.BottomLeft))
            {
                innerX = MinLotEdge(squareLots, block.yMin, axisX: true, blockSide: block.xMin) ?? block.xMin + minExtent;
                innerZ = MaxLotEdge(squareLots, block.xMin, axisX: false, blockSide: block.yMax) ?? block.yMax;
                return Rect.MinMaxRect(block.xMin, block.yMin, innerX, innerZ);
            }
            if (Has(corners, CityBlockCornerMask.TopRight))
            {
                innerX = MaxLotEdge(squareLots, block.yMax, axisX: true, blockSide: block.xMax) ?? block.xMax - minExtent;
                innerZ = MinLotEdge(squareLots, block.xMax, axisX: false, blockSide: block.yMin) ?? block.yMin; // fill to bottom if no column lots
                return Rect.MinMaxRect(innerX, innerZ, block.xMax, block.yMax);
            }
            if (Has(corners, CityBlockCornerMask.TopLeft))
            {
                innerX = MinLotEdge(squareLots, block.yMax, axisX: true, blockSide: block.xMin) ?? block.xMin + minExtent;
                innerZ = MinLotEdge(squareLots, block.xMin, axisX: false, blockSide: block.yMin) ?? block.yMin;
                return Rect.MinMaxRect(block.xMin, innerZ, innerX, block.yMax);
            }
            return default;
        }

        private static float? MaxLotEdge(IReadOnlyList<Rect> lots, float edgeCoord, bool axisX, float blockSide)
        {
            float? best = null;
            if (lots == null) return best;
            for (var i = 0; i < lots.Count; i++)
            {
                var r = lots[i];
                if (axisX && Mathf.Abs(r.yMin - edgeCoord) < 0.05f && r.xMax > (best ?? float.MinValue))
                    best = r.xMax;
                else if (!axisX && Mathf.Abs(r.xMax - edgeCoord) < 0.05f && r.yMax > (best ?? float.MinValue))
                    best = r.yMax;
            }
            return best;
        }

        private static float? MinLotEdge(IReadOnlyList<Rect> lots, float edgeCoord, bool axisX, float blockSide)
        {
            float? best = null;
            if (lots == null) return best;
            for (var i = 0; i < lots.Count; i++)
            {
                var r = lots[i];
                if (axisX && Mathf.Abs(r.yMax - edgeCoord) < 0.05f && r.xMin < (best ?? float.MaxValue))
                    best = r.xMin;
                else if (!axisX && Mathf.Abs(r.xMin - edgeCoord) < 0.05f && r.yMin < (best ?? float.MaxValue))
                    best = r.yMin;
            }
            return best;
        }

        /// <summary>
        ///     Builds the corner gap-fill polygon: outline clipped to the L-shaped
        ///     bounds from neighbour lot edges to the block corner.
        /// </summary>
        public static bool TryBuildCornerGapPolygon(
            IReadOnlyList<Vector2> outline,
            Rect fillBounds,
            List<Vector2> output,
            float minArea)
        {
            output.Clear();
            if (fillBounds.width < 0.5f || fillBounds.height < 0.5f) return false;

            TryClipRectToConvexPolygon(outline, fillBounds, output);
            if (output.Count < 3) return false;

            return ComputePolygonArea(output) >= minArea;
        }

        /// <summary>
        ///     Returns true when <paramref name="polygon"/> is axis-aligned and its bounds
        ///     match <paramref name="rect"/> (within snap).  False when the fillet arc adds
        ///     extra verts.
        /// </summary>
        public static bool IsRectPolygon(IReadOnlyList<Vector2> polygon, Rect rect)
        {
            if (polygon == null || polygon.Count < 4 || polygon.Count > 4)
                return polygon != null && polygon.Count == 4; // quick fail/succeed

            const float snap = 0.05f;
            var hasMinX = false; var hasMaxX = false; var hasMinY = false; var hasMaxY = false;
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                if (Mathf.Abs(p.x - rect.xMin) < snap) hasMinX = true;
                if (Mathf.Abs(p.x - rect.xMax) < snap) hasMaxX = true;
                if (Mathf.Abs(p.y - rect.yMin) < snap) hasMinY = true;
                if (Mathf.Abs(p.y - rect.yMax) < snap) hasMaxY = true;
            }

            return hasMinX && hasMaxX && hasMinY && hasMaxY;
        }

        private static bool Has(CityBlockCornerMask mask, CityBlockCornerMask flag) => (mask & flag) != 0;

        public static Rect ComputeBounds(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0)
                return default;

            var minX = polygon[0].x;
            var maxX = polygon[0].x;
            var minZ = polygon[0].y;
            var maxZ = polygon[0].y;
            for (var i = 1; i < polygon.Count; i++)
            {
                var point = polygon[i];
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minZ) minZ = point.y;
                if (point.y > maxZ) maxZ = point.y;
            }

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }
    }
}