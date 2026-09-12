using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class CityMathRoadLayoutGenerator
    {
        private readonly struct HighwayExitEdgeRequest
        {
            public HighwayExitEdgeRequest(
                List<float> eligibleKnots,
                float fallbackAxis,
                float worldZ,
                bool isHorizontal,
                float outwardSign,
                float totalDistance,
                float windingAmplitude)
            {
                EligibleKnots = eligibleKnots;
                FallbackAxis = fallbackAxis;
                WorldZ = worldZ;
                IsHorizontal = isHorizontal;
                OutwardSign = outwardSign;
                TotalDistance = totalDistance;
                WindingAmplitude = windingAmplitude;
            }

            public List<float> EligibleKnots { get; }

            /// <summary>Ring-edge midpoint used when no eligible grid knot exists.</summary>
            public float FallbackAxis { get; }

            public float WorldZ { get; }
            public bool IsHorizontal { get; }
            public float OutwardSign { get; }
            public float TotalDistance { get; }
            public float WindingAmplitude { get; }
        }

        /// <summary>
        ///     Highway exits are placed at existing T-intersection knots on each arterial ring edge.
        ///     On each edge, the two corner-most T-intersections are excluded, then at most one
        ///     interior T-intersection is converted at random (50% chance per edge of no exit).
        /// </summary>
        private static void AddSquareHighwayExits(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            if (!layout.generateStreetGrid || !layout.generateArterialRing)
                return;

            ResolveEffectiveSquareArterialRingBounds(
                layout, resolved, center,
                out var x0, out var x1, out var z0, out var z1);

            var cityXMin = center.x - resolved.HalfWidthMeters;
            var cityXMax = center.x + resolved.HalfWidthMeters;
            var cityZMin = center.y - resolved.HalfDepthMeters;
            var cityZMax = center.y + resolved.HalfDepthMeters;
            var rowLines = BuildAxisStreetLines(cityZMin, cityZMax, layout, resolved.Seed, 101);
            var colLines = BuildAxisStreetLines(cityXMin, cityXMax, layout, resolved.Seed, 303);
            var interiorCols = FilterOpenInterval(colLines, x0, x1);
            var interiorRows = FilterOpenInterval(rowLines, z0, z1);

            var cornerRadius = ResolveArterialCornerRadius(layout, resolved, x0, x1, z0, z1);
            var trimX0 = x0 + cornerRadius;
            var trimX1 = x1 - cornerRadius;
            var trimZ0 = z0 + cornerRadius;
            var trimZ1 = z1 - cornerRadius;

            // Only proceed if corners exist and are large enough.
            if (cornerRadius < 2f || trimX1 - trimX0 < 4f || trimZ1 - trimZ0 < 4f)
                return;

            var trimmedCols = FilterOpenInterval(
                interiorCols, trimX0 + CornerJunctionClearanceMeters, trimX1 - CornerJunctionClearanceMeters);
            var trimmedRows = FilterOpenInterval(
                interiorRows, trimZ0 + CornerJunctionClearanceMeters, trimZ1 - CornerJunctionClearanceMeters);

            // Interior knots only: exclude the two corner-most T-intersections per edge.
            // When a side has 2 or fewer knots, keep all of them so guaranteed exits can
            // still anchor on a real grid junction instead of falling back to a
            // junction-less ring-edge midpoint.
            var chainCols = trimmedCols.Count > 2
                ? trimmedCols.GetRange(1, trimmedCols.Count - 2) : new List<float>(trimmedCols);
            var chainRows = trimmedRows.Count > 2
                ? trimmedRows.GetRange(1, trimmedRows.Count - 2) : new List<float>(trimmedRows);

            var width = ResolveUnifiedCityRoadWidthMeters(settings);
            var rng = new System.Random(resolved.Seed);
            var exitId = 1000;

            // Compute outward distance for exit roads. Prefer MapMagic terrain bounds when set
            // by the builder; otherwise fall back to city-footprint-based distance.
            var hasTerrainBounds = layout.worldTerrainXMax > layout.worldTerrainXMin
                && layout.worldTerrainZMax > layout.worldTerrainZMin;

            var exitNorthDist = hasTerrainBounds
                ? layout.worldTerrainZMax - z1 + 5f
                : cityZMax - z1 + Mathf.Max(20f, Mathf.Max(resolved.HalfWidthMeters, resolved.HalfDepthMeters) * 0.15f);

            var exitSouthDist = hasTerrainBounds
                ? z0 - layout.worldTerrainZMin + 5f
                : z0 - cityZMin + Mathf.Max(20f, Mathf.Max(resolved.HalfWidthMeters, resolved.HalfDepthMeters) * 0.15f);

            var exitEastDist = hasTerrainBounds
                ? layout.worldTerrainXMax - x1 + 5f
                : cityXMax - x1 + Mathf.Max(20f, Mathf.Max(resolved.HalfWidthMeters, resolved.HalfDepthMeters) * 0.15f);

            var exitWestDist = hasTerrainBounds
                ? x0 - layout.worldTerrainXMin + 5f
                : x0 - cityXMin + Mathf.Max(20f, Mathf.Max(resolved.HalfWidthMeters, resolved.HalfDepthMeters) * 0.15f);

            // Winding amplitude scales with the actual distance (larger = more room to wander).
            var maxExitDist = Mathf.Max(exitNorthDist, exitSouthDist, exitEastDist, exitWestDist);
            var windingAmplitude = Mathf.Clamp(maxExitDist * 0.18f, 20f, 120f);

            var edgeRequests = new[]
            {
                new HighwayExitEdgeRequest(
                    chainCols, (x0 + x1) * 0.5f, z1, isHorizontal: true, outwardSign: 1f, exitNorthDist, windingAmplitude),
                new HighwayExitEdgeRequest(
                    chainCols, (x0 + x1) * 0.5f, z0, isHorizontal: true, outwardSign: -1f, exitSouthDist, windingAmplitude),
                new HighwayExitEdgeRequest(
                    chainRows, (z0 + z1) * 0.5f, x1, isHorizontal: false, outwardSign: 1f, exitEastDist, windingAmplitude),
                new HighwayExitEdgeRequest(
                    chainRows, (z0 + z1) * 0.5f, x0, isHorizontal: false, outwardSign: -1f, exitWestDist, windingAmplitude)
            };

            var guaranteed = layout.guaranteedHighwayExitCount;
            if (guaranteed > 0)
            {
                TryPlaceGuaranteedHighwayExits(network, ref exitId, rng, width, edgeRequests, guaranteed);
                return;
            }

            // North edge (z = z1): columns crossing the top ring.
            TryPlaceHighwayExitOnEdge(network, ref exitId, rng, width, edgeRequests[0]);

            TryPlaceHighwayExitOnEdge(network, ref exitId, rng, width, edgeRequests[1]);

            TryPlaceHighwayExitOnEdge(network, ref exitId, rng, width, edgeRequests[2]);

            TryPlaceHighwayExitOnEdge(network, ref exitId, rng, width, edgeRequests[3]);
        }

        /// <summary>
        ///     Places up to <paramref name="guaranteed"/> exits across the ring edges in a
        ///     deterministic shuffled order, bypassing the legacy 50% skip roll.
        /// </summary>
        private static void TryPlaceGuaranteedHighwayExits(
            RoadNetworkRuntime network,
            ref int exitId,
            System.Random rng,
            float width,
            HighwayExitEdgeRequest[] edgeRequests,
            int guaranteed)
        {
            var order = new[] { 0, 1, 2, 3 };
            for (var i = 3; i > 0; i--)
            {
                var j = rng.Next(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var remaining = guaranteed;
            for (var e = 0; e < order.Length && remaining > 0; e++)
            {
                if (TryPlaceHighwayExitOnEdge(network, ref exitId, rng, width, edgeRequests[order[e]], forcePlace: true))
                    remaining--;
            }

            if (remaining > 0)
                Debug.LogWarning(
                    "[CityMathRoadLayoutGenerator] Only " + (guaranteed - remaining) + "/" + guaranteed +
                    " guaranteed highway exits could be placed (not enough eligible ring knots).");
        }

        /// <summary>
        ///     On a single ring edge, picks 0 or 1 eligible interior T-intersection knot
        ///     (50% chance of none unless forced) and emits a Highway-class exit road that
        ///     winds outward to the map boundary with randomized curving and straight sections.
        /// </summary>
        private static bool TryPlaceHighwayExitOnEdge(
            RoadNetworkRuntime network,
            ref int exitId,
            System.Random rng,
            float width,
            HighwayExitEdgeRequest request,
            bool forcePlace = false)
        {
            var hasKnots = request.EligibleKnots != null && request.EligibleKnots.Count > 0;
            if (!hasKnots)
            {
                // Forced exits fall back to the ring-edge midpoint so small cities
                // with few grid knots still get guaranteed highways.
                if (!forcePlace)
                    return false;
            }
            else if (!forcePlace && rng.NextDouble() < 0.5)
            {
                return false;
            }

            var knotAxis = hasKnots
                ? request.EligibleKnots[rng.Next(0, request.EligibleKnots.Count)]
                : request.FallbackAxis;

            Vector2 inner, outwardDir, perpendicularDir;
            if (request.IsHorizontal)
            {
                inner = new Vector2(knotAxis, request.WorldZ);
                outwardDir = new Vector2(0f, request.OutwardSign);
                perpendicularDir = new Vector2(1f, 0f);
            }
            else
            {
                inner = new Vector2(request.WorldZ, knotAxis);
                outwardDir = new Vector2(request.OutwardSign, 0f);
                perpendicularDir = new Vector2(0f, 1f);
            }

            var waypoints = GenerateHighwayExitWaypoints(
                inner, outwardDir, perpendicularDir, request.TotalDistance, width, request.WindingAmplitude, rng);

            if (waypoints.Count < 2)
                return false;

            var exit = new RoadPolyline
            {
                id = exitId++,
                roadClass = RoadClass.Highway,
                widthMeters = width,
                curvedMarkers = true
            };
            exit.pointsXZ.AddRange(waypoints);
            network.AddRoad(exit);
            return true;
        }

        /// <summary>
        ///     Generates a randomized sequence of waypoints from the ring-edge start outward
        ///     to the map boundary. Segments may be straight or gently curved, with random
        ///     perpendicular offsets that produce natural-looking winding highways.
        /// </summary>
        private static List<Vector2> GenerateHighwayExitWaypoints(
            Vector2 start,
            Vector2 outwardDir,
            Vector2 perpendicularDir,
            float totalDistance,
            float roadWidth,
            float windingAmplitude,
            System.Random rng)
        {
            var waypoints = new List<Vector2> { start };
            if (totalDistance < 10f) return waypoints;

            var minSegmentLength = Mathf.Max(15f, roadWidth * 4f);
            var maxSegments = Mathf.Clamp(
                Mathf.FloorToInt(totalDistance / minSegmentLength), 2, 7);

            var numSegments = rng.Next(2, maxSegments + 1);

            var remainingDist = totalDistance;
            var currentPos = start;
            var amp = Mathf.Max(5f, windingAmplitude);

            for (var seg = 0; seg < numSegments && remainingDist > 1f; seg++)
            {
                var isLast = seg == numSegments - 1;
                var baseSegLen = remainingDist / (numSegments - seg);
                var segLen = baseSegLen * (0.7f + (float)rng.NextDouble() * 0.6f);

                if (isLast)
                    segLen = remainingDist;

                segLen = Mathf.Clamp(segLen, minSegmentLength, remainingDist);

                // Winding offset (zero on first and last segments: the first leaves the
                // ring perpendicularly for a clean T junction, the last hits the boundary
                // cleanly).
                var perpOffset = (isLast || seg == 0) ? 0f : (float)(rng.NextDouble() - 0.5) * 2f * amp;

                // Occasional midpoint for a visible bend (not on first/last segment).
                if (seg > 0 && !isLast && segLen > minSegmentLength * 1.5f && rng.NextDouble() < 0.55f)
                {
                    var midPerp = (float)(rng.NextDouble() - 0.5) * 2f * amp * 0.55f;
                    var midPoint = currentPos + outwardDir * (segLen * 0.45f) + perpendicularDir * midPerp;
                    AppendDistinctPointXZ(waypoints, midPoint);
                }

                currentPos = currentPos + outwardDir * segLen + perpendicularDir * perpOffset;
                AppendDistinctPointXZ(waypoints, currentPos);
                remainingDist -= segLen;
            }

            return waypoints;
        }

    }
}
