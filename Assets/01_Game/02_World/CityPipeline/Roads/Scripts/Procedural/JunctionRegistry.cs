using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public enum JunctionKind : byte
    {
        Corner = 0,
        T = 1,
        X = 2
    }

    public readonly struct JunctionRecord
    {
        public Vector2 PositionXZ { get; }
        public JunctionKind Kind { get; }
        public Vector2[] OutwardDirs { get; }
        public float RoadWidthMeters { get; }

        public JunctionRecord(
            Vector2 positionXZ,
            JunctionKind kind,
            Vector2[] outwardDirs,
            float roadWidthMeters = 0f)
        {
            PositionXZ = positionXZ;
            Kind = kind;
            OutwardDirs = outwardDirs;
            RoadWidthMeters = roadWidthMeters;
        }

        public JunctionRecord WithRoadWidth(float roadWidthMeters) =>
            new(PositionXZ, Kind, OutwardDirs, roadWidthMeters);
    }

    /// <summary>
    ///     Polyline-derived junction detection (endpoint clusters + axis-aligned crossings).
    ///     Data-only — no prefab placement.
    /// </summary>
    public static class JunctionRegistry
    {
        private const float JunctionInsetFallback = RoadKitPrefabs.JunctionFootprintMeters * 0.5f;
        private const float DefaultRoadWidthMeters = 7f;
        private const float JunctionWidthRadiusSq = 2.5f * 2.5f;

        private sealed class EndpointCluster
        {
            public readonly List<Vector2> Dirs = new(4);
            public Vector2 SumPos;
            public int Count;
        }

        public static List<JunctionRecord> Detect(IReadOnlyList<RoadPolyline> roads)
        {
            if (roads == null || roads.Count == 0)
                return new List<JunctionRecord>(0);

            var clusters = new Dictionary<Vector2Int, EndpointCluster>(64);
            CollectEndpointClusters(roads, clusters);

            var results = new List<JunctionRecord>(clusters.Count + 32);
            foreach (var kv in clusters)
            {
                var cluster = kv.Value;
                var dirs = DeduplicateDirections(cluster.Dirs);
                if (dirs.Count < 2)
                    continue;

                var kind = ClassifyJunction(dirs);
                if (kind == null)
                    continue;

                var pos = cluster.SumPos / Mathf.Max(1, cluster.Count);
                results.Add(new JunctionRecord(pos, kind.Value, dirs.ToArray()));
            }

            AppendAxisAlignedCrossings(roads, results);
            EnrichRoadWidths(results, roads);
            return results;
        }

        private static void EnrichRoadWidths(List<JunctionRecord> junctions, IReadOnlyList<RoadPolyline> roads)
        {
            if (junctions == null || junctions.Count == 0)
                return;

            for (var i = 0; i < junctions.Count; i++)
            {
                var junction = junctions[i];
                var width = ResolveMaxRoadWidthNear(junction.PositionXZ, roads);
                junctions[i] = junction.WithRoadWidth(width);
            }
        }

        private static float ResolveMaxRoadWidthNear(Vector2 position, IReadOnlyList<RoadPolyline> roads)
        {
            var max = DefaultRoadWidthMeters;
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;
                if (!RoadTouchesJunction(road, position, JunctionWidthRadiusSq))
                    continue;

                var width = road.widthMeters > 0f ? road.widthMeters : DefaultRoadWidthMeters;
                max = Mathf.Max(max, width);
            }

            return max;
        }

        private static bool RoadTouchesJunction(RoadPolyline road, Vector2 position, float radiusSq)
        {
            var points = road.pointsXZ;
            if ((points[0] - position).sqrMagnitude <= radiusSq)
                return true;

            var last = points.Count - 1;
            if ((points[last] - position).sqrMagnitude <= radiusSq)
                return true;

            for (var p = 0; p < last; p++)
            {
                if (DistancePointToSegmentSq(position, points[p], points[p + 1]) <= radiusSq)
                    return true;
            }

            return false;
        }

        private static float DistancePointToSegmentSq(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f)
                return (point - a).sqrMagnitude;

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            var closest = a + ab * t;
            return (point - closest).sqrMagnitude;
        }

        private static void CollectEndpointClusters(
            IReadOnlyList<RoadPolyline> roads,
            Dictionary<Vector2Int, EndpointCluster> clusters)
        {
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                AddEndpoint(clusters, road.pointsXZ[0], road.pointsXZ[1] - road.pointsXZ[0]);
                var last = road.pointsXZ.Count - 1;
                AddEndpoint(
                    clusters,
                    road.pointsXZ[last],
                    road.pointsXZ[last - 1] - road.pointsXZ[last]);
            }
        }

        private static void AddEndpoint(
            Dictionary<Vector2Int, EndpointCluster> clusters,
            Vector2 point,
            Vector2 towardRoad)
        {
            if (towardRoad.sqrMagnitude < 0.0001f)
                return;

            var key = Quantize(point);
            if (!clusters.TryGetValue(key, out var cluster))
            {
                cluster = new EndpointCluster();
                clusters[key] = cluster;
            }

            cluster.Dirs.Add(towardRoad.normalized);
            cluster.SumPos += point;
            cluster.Count++;
        }

        private static Vector2Int Quantize(Vector2 point) =>
            new(Mathf.RoundToInt(point.x * 2f), Mathf.RoundToInt(point.y * 2f));

        private static List<Vector2> DeduplicateDirections(List<Vector2> raw)
        {
            var result = new List<Vector2>(4);
            for (var i = 0; i < raw.Count; i++)
            {
                var dir = raw[i];
                var duplicate = false;
                for (var j = 0; j < result.Count; j++)
                {
                    if (Vector2.Dot(result[j], dir) > 0.85f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    result.Add(dir);
            }

            return result;
        }

        private static JunctionKind? ClassifyJunction(List<Vector2> dirs)
        {
            if (dirs.Count >= 4)
                return JunctionKind.X;
            if (dirs.Count == 3)
                return JunctionKind.T;
            if (dirs.Count != 2)
                return null;

            var dot = Mathf.Abs(Vector2.Dot(dirs[0], dirs[1]));
            if (dot < 0.35f)
                return JunctionKind.Corner;
            return null;
        }

        private static void AppendAxisAlignedCrossings(
            IReadOnlyList<RoadPolyline> roads,
            List<JunctionRecord> results)
        {
            CollectAxisSegments(roads, out var horizontals, out var verticals);
            var fourCardinals = new[]
            {
                Vector2.right, Vector2.left, Vector2.up, Vector2.down
            };

            for (var h = 0; h < horizontals.Count; h++)
            {
                var horiz = horizontals[h];
                for (var v = 0; v < verticals.Count; v++)
                {
                    var vert = verticals[v];
                    if (!TryAxisCrossing(horiz, vert, out var cross))
                        continue;
                    if (IsNearJunction(cross, results, 2.5f))
                        continue;

                    results.Add(new JunctionRecord(cross, JunctionKind.X, fourCardinals));
                }
            }
        }

        private readonly struct AxisSeg
        {
            public readonly float Fixed;
            public readonly float Min;
            public readonly float Max;

            public AxisSeg(float fixedAxis, float a, float b)
            {
                Fixed = fixedAxis;
                Min = Mathf.Min(a, b);
                Max = Mathf.Max(a, b);
            }
        }

        private static void CollectAxisSegments(
            IReadOnlyList<RoadPolyline> roads,
            out List<AxisSeg> horizontals,
            out List<AxisSeg> verticals)
        {
            horizontals = new List<AxisSeg>(64);
            verticals = new List<AxisSeg>(64);

            for (var i = 0; i < roads.Count; i++)
            {
                var pts = roads[i]?.pointsXZ;
                if (pts == null || pts.Count < 2)
                    continue;

                for (var p = 0; p < pts.Count - 1; p++)
                {
                    var a = pts[p];
                    var b = pts[p + 1];
                    var dx = Mathf.Abs(b.x - a.x);
                    var dz = Mathf.Abs(b.y - a.y);
                    if (dx < 0.5f && dz < 0.5f)
                        continue;

                    if (dx >= dz)
                        horizontals.Add(new AxisSeg(a.y, a.x, b.x));
                    else
                        verticals.Add(new AxisSeg(a.x, a.y, b.y));
                }
            }
        }

        private static bool TryAxisCrossing(AxisSeg horiz, AxisSeg vert, out Vector2 cross)
        {
            cross = default;
            const float margin = 1f;
            if (vert.Fixed <= horiz.Min + margin || vert.Fixed >= horiz.Max - margin)
                return false;
            if (horiz.Fixed <= vert.Min + margin || horiz.Fixed >= vert.Max - margin)
                return false;

            if (vert.Fixed <= horiz.Min + JunctionInsetFallback ||
                vert.Fixed >= horiz.Max - JunctionInsetFallback)
                return false;
            if (horiz.Fixed <= vert.Min + JunctionInsetFallback ||
                horiz.Fixed >= vert.Max - JunctionInsetFallback)
                return false;

            cross = new Vector2(vert.Fixed, horiz.Fixed);
            return true;
        }

        private static bool IsNearJunction(Vector2 point, List<JunctionRecord> junctions, float radius)
        {
            var rSq = radius * radius;
            for (var i = 0; i < junctions.Count; i++)
            {
                if ((junctions[i].PositionXZ - point).sqrMagnitude <= rSq)
                    return true;
            }

            return false;
        }
    }
}
