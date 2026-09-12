using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Spatial index and nearest-segment queries for <see cref="PolylineHeightProfile"/>.
    /// </summary>
    public sealed partial class PolylineHeightProfile
    {
        private static Rect ComputeBounds(IReadOnlyList<Vector2> points)
        {
            var min = points[0];
            var max = points[0];
            for (var i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Dictionary<long, List<int>> BuildSegmentGrid(IReadOnlyList<Vector2> points, Rect bounds)
        {
            var grid = new Dictionary<long, List<int>>(Mathf.Max(8, points.Count));
            if (points.Count < 2)
                return grid;

            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var minX = Mathf.Min(a.x, b.x);
                var maxX = Mathf.Max(a.x, b.x);
                var minZ = Mathf.Min(a.y, b.y);
                var maxZ = Mathf.Max(a.y, b.y);
                var cx0 = CellIndex(minX, bounds.xMin);
                var cx1 = CellIndex(maxX, bounds.xMin);
                var cz0 = CellIndex(minZ, bounds.yMin);
                var cz1 = CellIndex(maxZ, bounds.yMin);
                var seg = i - 1;
                for (var cz = cz0; cz <= cz1; cz++)
                {
                    for (var cx = cx0; cx <= cx1; cx++)
                    {
                        var key = PackCell(cx, cz);
                        if (!grid.TryGetValue(key, out var list))
                        {
                            list = new List<int>(4);
                            grid[key] = list;
                        }

                        list.Add(seg);
                    }
                }
            }

            return grid;
        }

        private static float DistancePointToPolyline(
            Vector2 point,
            IReadOnlyList<Vector2> pointsXZ,
            float[] heights,
            Dictionary<long, List<int>> grid,
            Rect bounds,
            float maxUsefulDistanceMeters,
            out Vector2 closestPoint,
            out int segmentIndex,
            out float segmentT,
            out float localGradeAbs)
        {
            closestPoint = pointsXZ[0];
            segmentIndex = 0;
            segmentT = 0f;
            localGradeAbs = 0f;
            var best = float.MaxValue;
            var bounded = !float.IsInfinity(maxUsefulDistanceMeters) &&
                          maxUsefulDistanceMeters < float.MaxValue * 0.5f;
            var maxUseful = bounded ? Mathf.Max(0f, maxUsefulDistanceMeters) : float.MaxValue;

            if (grid != null && grid.Count > 0)
            {
                var cx = CellIndex(point.x, bounds.xMin);
                var cz = CellIndex(point.y, bounds.yMin);
                var maxRing = bounded
                    ? Mathf.Max(1, Mathf.CeilToInt(maxUseful / GridCellMeters) + 1)
                    : 1;

                for (var ring = 0; ring <= maxRing; ring++)
                {
                    SearchGridRing(
                        point, pointsXZ, heights, grid, cx, cz, ring, maxUseful,
                        ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
                }

                if (bounded || best < float.MaxValue)
                    return best;
            }

            if (bounded)
                return best;

            for (var i = 1; i < pointsXZ.Count; i++)
            {
                ConsiderSegment(
                    point, pointsXZ, heights, i - 1, maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
            }

            return best;
        }

        private static void SearchGridRing(
            Vector2 point,
            IReadOnlyList<Vector2> pointsXZ,
            float[] heights,
            Dictionary<long, List<int>> grid,
            int cx,
            int cz,
            int ring,
            float maxUseful,
            ref float best,
            ref Vector2 closestPoint,
            ref int segmentIndex,
            ref float segmentT,
            ref float localGradeAbs)
        {
            if (ring == 0)
            {
                if (!grid.TryGetValue(PackCell(cx, cz), out var centerSegs))
                    return;
                for (var s = 0; s < centerSegs.Count; s++)
                {
                    ConsiderSegment(
                        point, pointsXZ, heights, centerSegs[s], maxUseful,
                        ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
                }

                return;
            }

            for (var dx = -ring; dx <= ring; dx++)
            {
                TryConsiderCell(
                    point, pointsXZ, heights, grid, cx + dx, cz - ring, maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
                TryConsiderCell(
                    point, pointsXZ, heights, grid, cx + dx, cz + ring, maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
            }

            for (var dz = -ring + 1; dz <= ring - 1; dz++)
            {
                TryConsiderCell(
                    point, pointsXZ, heights, grid, cx - ring, cz + dz, maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
                TryConsiderCell(
                    point, pointsXZ, heights, grid, cx + ring, cz + dz, maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
            }
        }

        private static void TryConsiderCell(
            Vector2 point,
            IReadOnlyList<Vector2> pointsXZ,
            float[] heights,
            Dictionary<long, List<int>> grid,
            int cellX,
            int cellZ,
            float maxUseful,
            ref float best,
            ref Vector2 closestPoint,
            ref int segmentIndex,
            ref float segmentT,
            ref float localGradeAbs)
        {
            if (!grid.TryGetValue(PackCell(cellX, cellZ), out var segs))
                return;

            for (var s = 0; s < segs.Count; s++)
            {
                ConsiderSegment(
                    point, pointsXZ, heights, segs[s], maxUseful,
                    ref best, ref closestPoint, ref segmentIndex, ref segmentT, ref localGradeAbs);
            }
        }

        private static void ConsiderSegment(
            Vector2 point,
            IReadOnlyList<Vector2> pointsXZ,
            float[] heights,
            int seg,
            float maxUseful,
            ref float best,
            ref Vector2 closestPoint,
            ref int segmentIndex,
            ref float segmentT,
            ref float localGradeAbs)
        {
            var a = pointsXZ[seg];
            var b = pointsXZ[seg + 1];
            var closest = ClosestPointOnSegment(point, a, b);
            var dist = Vector2.Distance(point, closest);
            if (dist >= best || dist > maxUseful)
                return;

            best = dist;
            closestPoint = closest;
            segmentIndex = seg;
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            segmentT = lenSq < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(closest - a, ab) / lenSq);
            var length = Mathf.Sqrt(lenSq);
            localGradeAbs = length > 0.01f
                ? Mathf.Abs(heights[seg + 1] - heights[seg]) / length
                : 0f;
        }

        private static int CellIndex(float world, float origin) =>
            Mathf.FloorToInt((world - origin) / GridCellMeters);

        private static long PackCell(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return a;

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            return a + ab * t;
        }
    }
}
