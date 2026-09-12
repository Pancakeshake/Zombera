using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Pinned highway routing and approach-grade checks.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        internal static bool TryRoutePinnedHighway(
            WorldCitySite siteA,
            WorldCitySite siteB,
            RoadNetworkSettings settings,
            TerrainRoadCostField costField,
            IWorldTerrainQuery terrainQuery,
            LandformProfile landforms,
            out Vector2 start,
            out Vector2 end,
            out List<Vector2> routed,
            out string failReason)
        {
            start = default;
            end = default;
            routed = null;
            failReason = "uninitialized";

            if (settings == null)
            {
                failReason = "missing_settings";
                return false;
            }

            if (costField == null || !costField.IsValid)
            {
                if (settings.fallbackToStraightPathWhenPathfindingFails)
                {
                    start = ResolveSiteAnchor(siteA, siteB.CenterXZ, landforms, terrainQuery, 0);
                    end = ResolveSiteAnchor(siteB, siteA.CenterXZ, landforms, terrainQuery, 0);
                    routed = new List<Vector2> { start, end };
                    failReason = null;
                    return true;
                }

                failReason = "missing_cost_field";
                return false;
            }

            var maxSlope = Mathf.Max(1f, settings.maxHighwayRoadSlopeDegrees);
            // Large regional MST edges need more expansions than local city roads.
            var routeSettings = ResolveHighwayRouteSettings(settings);
            var ownsRouteSettings = !ReferenceEquals(routeSettings, settings);
            try
            {
                for (var faceA = 0; faceA < MaxFaceRetries; faceA++)
                {
                    for (var faceB = 0; faceB < MaxFaceRetries; faceB++)
                    {
                        // Keep retries cheap: primary pairs first, then one-sided alternates.
                        if (faceA > 0 && faceB > 0)
                            continue;

                        start = ResolveSiteAnchor(siteA, siteB.CenterXZ, landforms, terrainQuery, faceA);
                        end = ResolveSiteAnchor(siteB, siteA.CenterXZ, landforms, terrainQuery, faceB);
                        if ((start - end).sqrMagnitude < 1f)
                        {
                            failReason = "degenerate_anchors";
                            continue;
                        }

                        costField.ClearPinnedCells();
                        costField.TryPinEndpoint(start, siteA.CenterXZ, inwardCells: 3);
                        costField.TryPinEndpoint(end, siteB.CenterXZ, inwardCells: 3);

                        var path = TerrainRoadPathfinder.FindPath(
                            costField, start, end, RoadClass.Highway, routeSettings, pinEndpoints: true);
                        if (path == null || path.Count < 2)
                        {
                            failReason = "astar_failed";
                            continue;
                        }

                        path = TerrainRoadPathfinder.PostProcessPath(path, settings);
                        TerrainRoadPathfinder.EnforcePinnedEnds(path, start, end);

                        if (!MeetsApproachGrade(path, costField, maxSlope, ApproachGradeMeters))
                        {
                            failReason = "approach_grade";
                            continue;
                        }

                        routed = path;
                        failReason = null;
                        costField.ClearPinnedCells();
                        return true;
                    }
                }

                if (settings.fallbackToStraightPathWhenPathfindingFails)
                {
                    start = ResolveSiteAnchor(siteA, siteB.CenterXZ, landforms, terrainQuery, 0);
                    end = ResolveSiteAnchor(siteB, siteA.CenterXZ, landforms, terrainQuery, 0);
                    routed = new List<Vector2> { start, end };
                    failReason = null;
                    costField.ClearPinnedCells();
                    return true;
                }

                costField.ClearPinnedCells();
                return false;
            }
            finally
            {
                if (ownsRouteSettings && routeSettings != null)
                    Object.DestroyImmediate(routeSettings);
            }
        }

        /// <summary>
        /// Raises A* expansion budget for inter-city MST edges without mutating the shared asset permanently.
        /// </summary>
        private static RoadNetworkSettings ResolveHighwayRouteSettings(RoadNetworkSettings settings)
        {
            const int highwayMinExpandedNodes = 80000;
            if (settings.pathfindingMaxExpandedNodes >= highwayMinExpandedNodes)
                return settings;

            var clone = Object.Instantiate(settings);
            clone.pathfindingMaxExpandedNodes = highwayMinExpandedNodes;
            return clone;
        }

        private static bool MeetsApproachGrade(
            IReadOnlyList<Vector2> path,
            TerrainRoadCostField field,
            float maxSlopeDegrees,
            float approachMeters)
        {
            if (path == null || path.Count < 2 || field == null || !field.IsValid)
                return false;

            var maxRatio = Mathf.Tan(Mathf.Max(1f, maxSlopeDegrees) * Mathf.Deg2Rad);
            return MeetsApproachGradeAtEnd(path, field, maxRatio, approachMeters, fromStart: true) &&
                   MeetsApproachGradeAtEnd(path, field, maxRatio, approachMeters, fromStart: false);
        }

        private static bool MeetsApproachGradeAtEnd(
            IReadOnlyList<Vector2> path,
            TerrainRoadCostField field,
            float maxSlopeRatio,
            float approachMeters,
            bool fromStart)
        {
            var remaining = Mathf.Max(8f, approachMeters);
            if (fromStart)
            {
                for (var i = 0; i < path.Count - 1 && remaining > 0f; i++)
                {
                    if (!SegmentWithinGrade(path[i], path[i + 1], field, maxSlopeRatio, ref remaining))
                        return false;
                }

                return true;
            }

            for (var i = path.Count - 1; i > 0 && remaining > 0f; i--)
            {
                if (!SegmentWithinGrade(path[i], path[i - 1], field, maxSlopeRatio, ref remaining))
                    return false;
            }

            return true;
        }

        private static bool SegmentWithinGrade(
            Vector2 a,
            Vector2 b,
            TerrainRoadCostField field,
            float maxSlopeRatio,
            ref float remaining)
        {
            var segLen = Vector2.Distance(a, b);
            if (segLen < 0.01f)
                return true;

            if (!field.TryWorldToCell(a, out var ax, out var az) ||
                !field.TryWorldToCell(b, out var bx, out var bz))
                return false;
            if (!field.TryGetHeight(ax, az, out var ha) || !field.TryGetHeight(bx, bz, out var hb))
                return false;

            var slope = Mathf.Abs(hb - ha) / segLen;
            if (slope > maxSlopeRatio * 1.35f)
                return false;

            remaining -= segLen;
            return true;
        }
    }
}
