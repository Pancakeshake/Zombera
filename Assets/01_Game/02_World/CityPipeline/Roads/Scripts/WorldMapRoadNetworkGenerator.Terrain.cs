using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Builds the authoritative seed-based road graph used for both editor preview and runtime tile builds.
    /// </summary>
    public static partial class WorldMapRoadNetworkGenerator
    {
        private static void RerouteRoadsThroughTerrain(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            Terrain[] terrains,
            Func<RoadPolyline, bool> roadFilter = null,
            bool useHighwayCellSize = false)
        {
            var networkBounds = TerrainRoadCostField.ComputeBounds(network, settings.pathfindingMarginMeters);
            if (networkBounds.width <= 0f || networkBounds.height <= 0f) return;

            var terrainBounds = ComputeTerrainBoundsXZ(terrains);
            var bounds = IntersectRects(networkBounds, terrainBounds);
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                Debug.Log(
                    "[WorldMapRoadNetworkGenerator] Terrain pathfinding skipped: no overlap between road bounds and active terrains.");
                return;
            }

            var cellSize = useHighwayCellSize
                ? Mathf.Max(4f, settings.highwayPathfindingCellSizeMeters)
                : Mathf.Max(4f, settings.pathfindingCellSizeMeters);
            var costField = TerrainRoadCostField.TryBuild(bounds, terrains, settings, cellSize);
            if (costField == null || !costField.IsValid) return;

            RerouteRoadsThroughCostField(network, settings, costField, roadFilter);
        }

        private static void RerouteRoadsThroughCostField(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            TerrainRoadCostField costField,
            Func<RoadPolyline, bool> roadFilter = null)
        {
            var reroutedCount = 0;
            var failedCount = 0;

            foreach (var road in network.Roads)
            {
                if (road != null && road.preserveWorldPath)
                    continue;

                if (!MatchesRoadFilter(road, roadFilter))
                    continue;

                if (!TryRerouteRoad(costField, road, settings, out var routed))
                {
                    failedCount++;
                    continue;
                }

                routed = TerrainRoadPathfinder.PostProcessPath(routed, settings);
                routed = Subdivide(routed, 24f);
                road.pointsXZ = settings.terrainRefinementStrength < 0.999f
                    ? BlendPolyline(road.pointsXZ, routed, settings.terrainRefinementStrength)
                    : routed;

                reroutedCount++;
            }

            Debug.Log(
                "[WorldMapRoadNetworkGenerator] Terrain pathfinding rerouted " + reroutedCount +
                " roads (" + failedCount + " kept original, grid " + costField.Width + "x" + costField.Height +
                " @ " + costField.CellSize.ToString("F1") + "m).");
        }

        private static bool TryRerouteRoad(
            TerrainRoadCostField costField, RoadPolyline road, RoadNetworkSettings settings, out List<Vector2> routed)
        {
            routed = null;
            if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                return false;

            var anchors = BuildPathfindingAnchors(road.pointsXZ, settings.ResolvePathfindingAnchorStride(road.roadClass));
            routed = TerrainRoadPathfinder.RouteThroughWaypoints(costField, anchors, road.roadClass, settings);
            return routed != null && routed.Count >= 2;
        }

        private static List<Vector2> BuildPathfindingAnchors(IReadOnlyList<Vector2> points, int stride)
        {
            stride = Mathf.Max(1, stride);
            if (points == null || points.Count < 2) return null;
            if (points.Count <= 3 || stride <= 1)
                return new List<Vector2>(points);

            var anchors = new List<Vector2>(points.Count / stride + 2) { points[0] };
            for (var i = stride; i < points.Count - 1; i += stride)
                anchors.Add(points[i]);
            anchors.Add(points[points.Count - 1]);
            return anchors;
        }

        private static List<Vector2> BlendPolyline(IReadOnlyList<Vector2> original, IReadOnlyList<Vector2> routed, float routedWeight)
        {
            routedWeight = Mathf.Clamp01(routedWeight);
            if (routedWeight <= 0.01f) return new List<Vector2>(original);
            if (routedWeight >= 0.999f) return new List<Vector2>(routed);

            var originalWeight = 1f - routedWeight;
            var count = Mathf.Max(original.Count, routed.Count);
            var blended = new List<Vector2>(count);

            for (var i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0f : i / (float)(count - 1);
                var o = SamplePolylineAtT(original, t);
                var r = SamplePolylineAtT(routed, t);
                blended.Add(o * originalWeight + r * routedWeight);
            }

            return blended;
        }

        private static Vector2 SamplePolylineAtT(IReadOnlyList<Vector2> points, float t)
        {
            if (points == null || points.Count == 0) return Vector2.zero;
            if (points.Count == 1) return points[0];

            t = Mathf.Clamp01(t);
            var totalLength = 0f;
            for (var i = 1; i < points.Count; i++)
                totalLength += Vector2.Distance(points[i - 1], points[i]);

            if (totalLength <= 0.01f) return points[0];

            var target = totalLength * t;
            var traveled = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var segLen = Vector2.Distance(points[i - 1], points[i]);
                if (traveled + segLen >= target)
                {
                    var segT = segLen <= 0.0001f ? 0f : (target - traveled) / segLen;
                    return Vector2.Lerp(points[i - 1], points[i], segT);
                }

                traveled += segLen;
            }

            return points[points.Count - 1];
        }

        private static void FilterSteepRoadPoints(RoadPolyline road, Terrain[] terrains, RoadNetworkSettings settings)
        {
            if (road?.pointsXZ == null || road.pointsXZ.Count < 2) return;

            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, road.roadClass);
            var maxSlopeRatio = maxSlopeDegrees > 0f ? Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) : float.MaxValue;
            var probeStep = Mathf.Max(1f, settings.slopeProbeStepMeters);
            var filtered = new List<Vector2>(road.pointsXZ.Count);

            for (var i = 0; i < road.pointsXZ.Count; i++)
            {
                var point = road.pointsXZ[i];
                if (!IsRoadPointTerrainAcceptable(point, terrains, maxSlopeRatio, probeStep))
                    continue;

                if (filtered.Count > 0 && (point - filtered[filtered.Count - 1]).sqrMagnitude < 1f)
                    continue;

                filtered.Add(point);
            }

            road.pointsXZ = filtered;
        }

        private static bool IsRoadPointTerrainAcceptable(
            Vector2 pointXZ,
            Terrain[] terrains,
            float maxSlopeRatio,
            float probeStepMeters)
        {
            if (!TrySampleTerrainHeight(terrains, new Vector3(pointXZ.x, 0f, pointXZ.y), out var centerHeight))
                return true;

            var probes = new[]
            {
                new Vector2(probeStepMeters, 0f),
                new Vector2(-probeStepMeters, 0f),
                new Vector2(0f, probeStepMeters),
                new Vector2(0f, -probeStepMeters)
            };

            for (var i = 0; i < probes.Length; i++)
            {
                var probe = pointXZ + probes[i];
                if (!TrySampleTerrainHeight(terrains, new Vector3(probe.x, 0f, probe.y), out var probeHeight))
                    continue;

                var slope = Mathf.Abs(probeHeight - centerHeight) / probeStepMeters;
                if (slope > maxSlopeRatio)
                    return false;
            }

            return true;
        }

        private static Rect ComputeTerrainBoundsXZ(Terrain[] terrains)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var found = false;

            foreach (var terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;

                var pos = terrain.transform.position;
                var size = terrain.terrainData.size;
                min = Vector2.Min(min, new Vector2(pos.x, pos.z));
                max = Vector2.Max(max, new Vector2(pos.x + size.x, pos.z + size.z));
                found = true;
            }

            return found ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        }

        private static Rect IntersectRects(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin) return default;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void TrimPolylineEndsForSlope(RoadPolyline road, Terrain[] terrains, RoadNetworkSettings settings)
        {
            if (road?.pointsXZ == null || road.pointsXZ.Count < 2) return;

            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, road.roadClass);
            if (maxSlopeDegrees <= 0f) return;

            var maxSlopeRatio = Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad);
            var probeStep = Mathf.Max(1f, settings.slopeProbeStepMeters);

            while (road.pointsXZ.Count >= 2 &&
                   IsEndpointStepTooSteep(road.pointsXZ[0], road.pointsXZ[1], terrains, maxSlopeRatio, probeStep))
                road.pointsXZ.RemoveAt(0);

            while (road.pointsXZ.Count >= 2)
            {
                var last = road.pointsXZ.Count - 1;
                if (!IsEndpointStepTooSteep(road.pointsXZ[last], road.pointsXZ[last - 1], terrains, maxSlopeRatio, probeStep))
                    break;

                road.pointsXZ.RemoveAt(last);
            }
        }

        private static bool IsEndpointStepTooSteep(
            Vector2 endpoint,
            Vector2 neighbor,
            Terrain[] terrains,
            float maxSlopeRatio,
            float probeStepMeters)
        {
            if (!TrySampleTerrainHeight(terrains, new Vector3(endpoint.x, 0f, endpoint.y), out var endHeight))
                return false;
            if (!TrySampleTerrainHeight(terrains, new Vector3(neighbor.x, 0f, neighbor.y), out var neighborHeight))
                return false;

            var distance = Vector2.Distance(endpoint, neighbor);
            if (distance <= 0.01f) return true;

            var slope = Mathf.Abs(endHeight - neighborHeight) / distance;
            if (slope > maxSlopeRatio) return true;

            var dir = (endpoint - neighbor).normalized;
            var ahead = endpoint + dir * probeStepMeters;
            if (!TrySampleTerrainHeight(terrains, new Vector3(ahead.x, 0f, ahead.y), out var aheadHeight))
                return false;

            var aheadSlope = Mathf.Abs(aheadHeight - endHeight) / probeStepMeters;
            return aheadSlope > maxSlopeRatio;
        }

        private static float ResolveMaxSlopeDegrees(RoadNetworkSettings settings, RoadClass roadClass)
        {
            if (!settings.usePerRoadClassSlopeLimits)
                return roadClass == RoadClass.Local ? settings.maxCityRoadSlopeDegrees : settings.maxArterialRoadSlopeDegrees;

            return roadClass switch
            {
                RoadClass.Highway => settings.maxHighwayRoadSlopeDegrees,
                RoadClass.Arterial => settings.maxArterialRoadSlopeDegrees,
                RoadClass.Local => settings.maxLocalRoadSlopeDegrees,
                _ => settings.maxCityRoadSlopeDegrees
            };
        }

        private static bool TrySampleTerrainHeight(Terrain[] terrains, Vector3 worldPos, out float height)
        {
            height = 0f;
            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;

                var tPos = terrain.transform.position;
                var tSize = terrain.terrainData.size;
                if (worldPos.x < tPos.x || worldPos.x > tPos.x + tSize.x ||
                    worldPos.z < tPos.z || worldPos.z > tPos.z + tSize.z)
                    continue;

                height = terrain.SampleHeight(worldPos);
                return true;
            }

            return false;
        }

        private static List<Vector2> Subdivide(List<Vector2> points, float maxDist)
        {
            if (points == null || points.Count < 2) return points ?? new List<Vector2>();

            var result = new List<Vector2>();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var dist = Vector2.Distance(a, b);
                var count = Mathf.Max(1, Mathf.CeilToInt(dist / maxDist));
                for (var j = 0; j < count; j++)
                    result.Add(Vector2.Lerp(a, b, j / (float)count));
            }

            result.Add(points[points.Count - 1]);
            return result;
        }
    }
}
