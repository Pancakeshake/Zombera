using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Highway edge classification for <see cref="CityPerimeterSidewalkBuilder"/>.
    /// </summary>
    internal static class CityPerimeterSidewalkHighwayCapUtility
    {
        internal static bool IsOutboundHighwayEdge(
            Vector2 a,
            Vector2 b,
            IReadOnlyList<RoadPolyline> roads,
            Vector2 cityCenter)
        {
            if (!IsHighwaySegment(a, b, roads))
                return false;

            var mid = (a + b) * 0.5f;
            var midDist = (mid - cityCenter).sqrMagnitude;
            return midDist >= (a - cityCenter).sqrMagnitude && midDist >= (b - cityCenter).sqrMagnitude;
        }

        private static bool IsHighwaySegment(Vector2 a, Vector2 b, IReadOnlyList<RoadPolyline> roads)
        {
            if (roads == null)
                return false;

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
