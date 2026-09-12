using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class CityMathRoadLayoutGenerator
    {
        // ──────────────────────────────────────────────
        //  Inter-city highways for region mode. Links sites with a minimum
        //  spanning tree over their centers; each edge becomes a gently-bowed
        //  RoadClass.Highway polyline anchored at real grid T-knots on both
        //  cities' arterial rings, so the standard split + junction wiring
        //  creates proper connectors at both ends.
        // ──────────────────────────────────────────────

        internal static int AddInterCityHighways(
            RoadNetworkRuntime network,
            IReadOnlyList<CityHubSite> sites,
            RoadNetworkSettings settings,
            int regionSeed,
            float minLinkDistanceMeters = 600f,
            int idBase = 8000)
        {
            if (sites == null || sites.Count < 2)
                return 0;

            var connected = InterCityHighwayTopology.CollectConnectedIndices(sites.Count, i =>
            {
                var site = sites[i];
                return site != null && site.connectToRegionHighways;
            });

            if (connected.Count < 2)
                return 0;

            var positions = new Vector2[sites.Count];
            for (var i = 0; i < sites.Count; i++)
                positions[i] = sites[i] != null ? sites[i].centerXZ : Vector2.zero;

            var links = InterCityHighwayTopology.BuildMinimumSpanningTree(
                positions,
                connected,
                Mathf.Max(50f, minLinkDistanceMeters));
            var width = settings != null ? settings.ResolveWidthMeters(RoadClass.Highway) : 12f;
            var rng = new System.Random(ResolveEffectiveLayoutSeed(regionSeed, 0) * 7919 + 13);

            // Track which ring midpoints already host a highway so two links to the
            // same city never share an anchor (that would force a 4-way X crossing).
            var usedAnchorCells = new HashSet<Vector2Int>();

            var added = 0;
            for (var l = 0; l < links.Count; l++)
            {
                var indexA = links[l].Item1;
                var indexB = links[l].Item2;
                var siteA = sites[indexA];
                var siteB = sites[indexB];

                // Anchor on the ACTUAL generated ring segment midpoints (straight
                // edges only, never corner arcs) so the highway lands between street
                // knots. The splitter cuts the ring there and the junction is a pure
                // T — ring north, ring south, highway.
                if (!TryResolveRingAnchorFromNetwork(network, indexA, siteA.centerXZ, siteB.centerXZ, usedAnchorCells, out var start, out var startOutward))
                    start = ResolveSiteEdgePoint(siteA, siteB.centerXZ, out startOutward);
                if (!TryResolveRingAnchorFromNetwork(network, indexB, siteB.centerXZ, siteA.centerXZ, usedAnchorCells, out var end, out var endOutward))
                    end = ResolveSiteEdgePoint(siteB, siteA.centerXZ, out endOutward);

                if ((start - end).sqrMagnitude < 1f)
                    continue;

                Debug.Log(
                    "[CityMathRoadLayoutGenerator] Region highway " + siteA.displayName + " → " + siteB.displayName +
                    ": " + start.ToString("F0") + " → " + end.ToString("F0"));

                var road = BuildHighwayLink(idBase + l, start, startOutward, end, endOutward, width, rng);
                if (road != null)
                {
                    network.AddRoad(road);
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        ///     Picks the midpoint of the generated arterial-ring STRAIGHT edge segment
        ///     that best faces the neighbour. Corner arcs are excluded: their chord
        ///     midpoints sit on the diagonal, so a cardinal lead-out would immediately
        ///     re-cross an adjacent ring edge and split the highway into a stub plus an
        ///     extra (merged 4-way) junction. Anchors already used by another highway
        ///     are skipped unless nothing else is available.
        /// </summary>
        private static bool TryResolveRingAnchorFromNetwork(
            RoadNetworkRuntime network,
            int siteIndex,
            Vector2 center,
            Vector2 toward,
            HashSet<Vector2Int> usedAnchorCells,
            out Vector2 knot,
            out Vector2 outward)
        {
            knot = default;
            outward = default;
            var dir = toward - center;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            var dirN = dir / dir.magnitude;

            var idMin = siteIndex * 100000;
            var idMax = idMin + 200;
            var bestUnusedDot = -1.1f;
            var bestAnyDot = -1.1f;
            var bestUnused = Vector2.zero;
            var bestAny = Vector2.zero;
            var hasUnused = false;
            var hasAny = false;

            for (var i = 0; i < network.Roads.Count; i++)
            {
                var road = network.Roads[i];
                if (road == null || road.roadClass != RoadClass.Arterial || road.id < idMin || road.id >= idMax)
                    continue;
                var pts = road.pointsXZ;
                if (pts == null || pts.Count != 2)
                    continue; // straight edge segments only — skip corner arcs

                var mid = (pts[0] + pts[1]) * 0.5f;
                var d = mid - center;
                if (d.sqrMagnitude < 1f)
                    continue;

                var dot = Vector2.Dot(d / d.magnitude, dirN);
                if (dot > bestAnyDot)
                {
                    bestAnyDot = dot;
                    bestAny = mid;
                    hasAny = true;
                }

                var cellUsed = usedAnchorCells != null && usedAnchorCells.Contains(QuantizeAnchorCell(mid));
                if (!cellUsed && dot > bestUnusedDot)
                {
                    bestUnusedDot = dot;
                    bestUnused = mid;
                    hasUnused = true;
                }
            }

            if (hasUnused)
                knot = bestUnused;
            else if (hasAny)
                knot = bestAny; // everything taken — reuse the best rather than none
            else
                return false;

            usedAnchorCells?.Add(QuantizeAnchorCell(knot));
            outward = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
            return true;
        }

        private const float AnchorCellSizeMeters = 5f;

        private static Vector2Int QuantizeAnchorCell(Vector2 point) =>
            new(Mathf.RoundToInt(point.x / AnchorCellSizeMeters), Mathf.RoundToInt(point.y / AnchorCellSizeMeters));

        private static Vector2 ResolveSiteEdgePoint(CityHubSite site, Vector2 toward, out Vector2 outward) =>
            InterCitySiteFootprintUtility.ResolveEdgePoint(
                site.centerXZ,
                site.halfWidthMeters,
                site.halfDepthMeters,
                toward,
                out outward);

        /// <summary>
        ///     Randomized winding highway: leaves each city with a long straight,
        ///     perpendicular lead-out, then wanders gently via a damped random walk.
        ///     Built from dense straight segments (curvedMarkers = false) so the road
        ///     follows the exact polyline — no spline overshoot into the cities' grids.
        /// </summary>
        private static RoadPolyline BuildHighwayLink(
            int id, Vector2 start, Vector2 startOutward, Vector2 end, Vector2 endOutward, float width, System.Random rng)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length < 1f)
                return null;

            var leadOut = Mathf.Clamp(length * 0.15f, 40f, 160f);
            var waypoints = new List<Vector2>(32)
            {
                start,
                start + startOutward * leadOut * 0.5f,
                start + startOutward * leadOut
            };

            // Wind between the two lead-out ends. The offset DECAYS toward zero at both
            // ends (fade = sin(pi*t)) so the polyline rejoins the straight centerline
            // smoothly — no sideways lurch that could slice through the far city.
            // endOutward points AWAY from the far city, so `end + endOutward * leadOut`
            // sits OUTSIDE its ring: the approach tail never crosses or overlaps the
            // far city's street grid.
            var p1 = waypoints[2];
            var p2 = end + endOutward * leadOut;
            var windDelta = p2 - p1;
            var windLength = windDelta.magnitude;
            var windDir = windLength > 0.0001f ? windDelta / windLength : Vector2.right;
            var windPerp = new Vector2(-windDir.y, windDir.x);
            var amplitude = Mathf.Clamp(windLength * 0.04f, 15f, 80f);

            var segments = Mathf.Clamp(Mathf.CeilToInt(windLength / 80f), 4, 16);
            var offset = 0f;
            for (var s = 1; s < segments; s++)
            {
                var t = s / (float)segments;
                var fade = Mathf.Sin(Mathf.PI * t);
                offset += (float)(rng.NextDouble() - 0.5) * amplitude * 0.6f;
                offset *= 0.9f;
                waypoints.Add(Vector2.Lerp(p1, p2, t) + windPerp * offset * fade);
            }

            // Straight approach into the far city (collinear tail, outside the ring).
            waypoints.Add(end + endOutward * leadOut * 0.5f);
            waypoints.Add(end);

            return new RoadPolyline
            {
                id = id,
                roadClass = RoadClass.Highway,
                widthMeters = width,
                curvedMarkers = false,
                pointsXZ = waypoints
            };
        }
    }
}
