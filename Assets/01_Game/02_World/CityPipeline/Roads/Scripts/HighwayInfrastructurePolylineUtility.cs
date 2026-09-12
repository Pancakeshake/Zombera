using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Inserts exact tunnel portal and bridge abutment coordinates into highway polylines
    ///     before asphalt splitting so sparse samples cannot leave road over spans.
    /// </summary>
    public static class HighwayInfrastructurePolylineUtility
    {
        private const float DuplicateEpsilonMeters = 0.5f;

        public static int InsertBoundaryPoints(RoadNetworkRuntime network)
        {
            if (network?.Roads == null)
                return 0;

            var inserted = 0;
            var tunnels = MountainTunnelBuildCache.Active;
            var crossings = WaterCrossingBuildCache.Active;

            for (var i = 0; i < network.Roads.Count; i++)
            {
                var road = network.Roads[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                if (tunnels != null)
                {
                    for (var t = 0; t < tunnels.Count; t++)
                    {
                        var tunnel = tunnels[t];
                        if (tunnel.RoadId != 0 && tunnel.RoadId != road.id)
                            continue;
                        inserted += InsertPoint(road.pointsXZ, tunnel.EntryXZ);
                        inserted += InsertPoint(road.pointsXZ, tunnel.ExitXZ);
                    }
                }

                if (crossings == null)
                    continue;

                for (var c = 0; c < crossings.Count; c++)
                {
                    var crossing = crossings[c];
                    if (crossing.RoadId != 0 && crossing.RoadId != road.id)
                        continue;
                    if (crossing.Policy != WaterCrossingPolicy.Bridge &&
                        crossing.Policy != WaterCrossingPolicy.Causeway)
                        continue;
                    if (crossing.RoadClass != RoadClass.Highway)
                        continue;
                    inserted += InsertPoint(road.pointsXZ, crossing.EntryXZ);
                    inserted += InsertPoint(road.pointsXZ, crossing.ExitXZ);
                }
            }

            return inserted;
        }

        public static int InsertResolvedBridgeApproaches(
            IReadOnlyList<RoadPolyline> roads,
            IReadOnlyList<ResolvedBridgeApproach> approaches)
        {
            if (roads == null || approaches == null)
                return 0;

            var inserted = 0;
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                for (var a = 0; a < approaches.Count; a++)
                {
                    var approach = approaches[a];
                    if (approach.RoadId != 0 && road.id != 0 && approach.RoadId != road.id)
                        continue;
                    if (approach.RoadClass != road.roadClass)
                        continue;
                    inserted += InsertPoint(road.pointsXZ, approach.EntryXZ);
                    inserted += InsertPoint(road.pointsXZ, approach.ExitXZ);
                }
            }

            return inserted;
        }

        private static int InsertPoint(List<Vector2> points, Vector2 point)
        {
            if (points == null || points.Count < 2)
                return 0;

            for (var i = 0; i < points.Count; i++)
            {
                if (Vector2.Distance(points[i], point) <= DuplicateEpsilonMeters)
                    return 0;
            }

            var bestSegment = 0;
            var bestDist = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
            {
                var dist = DistancePointToSegment(point, points[i - 1], points[i]);
                if (dist >= bestDist)
                    continue;
                bestDist = dist;
                bestSegment = i - 1;
            }

            // Reject points that are not near the polyline (wrong road).
            // Capsule-snapped mouths can sit farther from the scanned highway chord.
            if (bestDist > 220f)
                return 0;

            points.Insert(bestSegment + 1, point);
            return 1;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
