using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Forces highway polylines through tunnel mouths so asphalt leads up to
    ///     <c>Socket_Approach</c>, skips the bore, and resumes at the far mouth.
    /// </summary>
    public static class HighwayTunnelApproachLinker
    {
        private const float MaxMouthSnapMeters = 220f;
        private const float DuplicateEpsilonMeters = 0.75f;

        private struct PolylineProjection
        {
            public int SegmentIndex;
            public float DistanceAlong;
            public float Distance;
        }

        public static int SnapHighwaysToTunnelMouths(
            IReadOnlyList<RoadPolyline> roads,
            IReadOnlyList<MountainTunnel> tunnels)
        {
            if (roads == null || tunnels == null || tunnels.Count == 0)
                return 0;

            var linked = 0;
            for (var t = 0; t < tunnels.Count; t++)
            {
                if (TrySnapOne(roads, tunnels[t]))
                    linked++;
            }

            return linked;
        }

        public static int SnapHighwaysToTunnelMouths(
            RoadNetworkRuntime network,
            IReadOnlyList<MountainTunnel> tunnels)
        {
            if (network?.Roads == null)
                return 0;
            return SnapHighwaysToTunnelMouths(network.Roads, tunnels);
        }

        private static bool TrySnapOne(IReadOnlyList<RoadPolyline> roads, in MountainTunnel tunnel)
        {
            if (!TryFindHighway(roads, tunnel, out var road) || road.pointsXZ == null || road.pointsXZ.Count < 2)
                return false;

            var points = road.pointsXZ;
            if (!TryProject(points, tunnel.EntryXZ, out var entryProjection))
                return false;
            if (!TryProject(points, tunnel.ExitXZ, out var exitProjection))
                return false;
            if (entryProjection.Distance > MaxMouthSnapMeters || exitProjection.Distance > MaxMouthSnapMeters)
                return false;

            var entryFirst = entryProjection.DistanceAlong <= exitProjection.DistanceAlong;
            var firstProjection = entryFirst ? entryProjection : exitProjection;
            var secondProjection = entryFirst ? exitProjection : entryProjection;
            var firstMouth = entryFirst ? tunnel.EntryXZ : tunnel.ExitXZ;
            var secondMouth = entryFirst ? tunnel.ExitXZ : tunnel.EntryXZ;

            // Rewrite the span: approach … → mouthA → mouthB → … far approach.
            var rewritten = new List<Vector2>(points.Count);
            for (var i = 0; i <= firstProjection.SegmentIndex; i++)
                rewritten.Add(points[i]);

            SnapOrAppend(rewritten, firstMouth);
            SnapOrAppend(rewritten, secondMouth);

            for (var i = secondProjection.SegmentIndex + 1; i < points.Count; i++)
                rewritten.Add(points[i]);

            DeduplicateInPlace(rewritten);
            if (rewritten.Count < 2)
                return false;

            road.pointsXZ.Clear();
            road.pointsXZ.AddRange(rewritten);
            return true;
        }

        private static bool TryFindHighway(
            IReadOnlyList<RoadPolyline> roads,
            in MountainTunnel tunnel,
            out RoadPolyline road)
        {
            road = null;
            if (tunnel.RoadId != 0)
            {
                for (var i = 0; i < roads.Count; i++)
                {
                    var candidate = roads[i];
                    if (candidate == null || candidate.roadClass != RoadClass.Highway)
                        continue;
                    if (candidate.id != tunnel.RoadId)
                        continue;
                    road = candidate;
                    return true;
                }
            }

            var bestDist = float.MaxValue;
            for (var i = 0; i < roads.Count; i++)
            {
                var candidate = roads[i];
                if (candidate == null || candidate.roadClass != RoadClass.Highway ||
                    candidate.pointsXZ == null || candidate.pointsXZ.Count < 2)
                    continue;
                var dist = DistanceToPolyline(tunnel.MidXZ, candidate.pointsXZ);
                if (dist >= bestDist)
                    continue;
                bestDist = dist;
                road = candidate;
            }

            return road != null && bestDist <= MaxMouthSnapMeters;
        }

        private static bool TryProject(
            List<Vector2> points,
            Vector2 target,
            out PolylineProjection projection)
        {
            projection = new PolylineProjection { Distance = float.MaxValue };
            var distanceAlong = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var delta = points[i] - a;
                var length = delta.magnitude;
                if (length > 0.0001f)
                {
                    var t = Mathf.Clamp01(Vector2.Dot(target - a, delta) / (length * length));
                    var distance = Vector2.Distance(target, a + delta * t);
                    if (distance < projection.Distance)
                    {
                        projection.SegmentIndex = i - 1;
                        projection.DistanceAlong = distanceAlong + length * t;
                        projection.Distance = distance;
                    }
                }

                distanceAlong += length;
            }

            return projection.Distance < float.MaxValue;
        }

        private static void SnapOrAppend(List<Vector2> points, Vector2 mouth)
        {
            if (points.Count == 0)
            {
                points.Add(mouth);
                return;
            }

            if (Vector2.Distance(points[points.Count - 1], mouth) <= DuplicateEpsilonMeters)
            {
                points[points.Count - 1] = mouth;
                return;
            }

            points.Add(mouth);
        }

        private static void DeduplicateInPlace(List<Vector2> points)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                if (Vector2.Distance(points[i], points[i - 1]) > DuplicateEpsilonMeters)
                    continue;
                points.RemoveAt(i);
            }
        }

        private static float DistanceToPolyline(Vector2 point, List<Vector2> points)
        {
            var best = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
            {
                var d = DistancePointToSegment(point, points[i - 1], points[i]);
                if (d < best)
                    best = d;
            }

            return best;
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
