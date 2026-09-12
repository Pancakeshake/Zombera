using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Filters clipped road segments so EasyRoads is not built for interior dead-ends
    ///     (tile clips into hills, slope cuts, or dangling stubs).
    /// </summary>
    public static class RoadSegmentPlacementRules
    {
        public static bool ShouldPlaceSegment(
            IReadOnlyList<Vector3> points,
            Rect tileRect,
            int sourceRoadId,
            IReadOnlyList<RoadPolyline> network,
            IReadOnlyList<IReadOnlyList<Vector3>> tilePathPolylines,
            Terrain terrain,
            RoadNetworkSettings settings,
            RoadClass roadClass)
        {
            if (points == null || points.Count < 2 || settings == null)
                return false;

            if (!settings.rejectInteriorDeadEndSegments)
                return MeetsMinimumLength(points, settings);

            var snapMeters = Mathf.Max(1f, settings.markerSnapDistanceMeters);
            var borderMargin = Mathf.Max(1f, settings.tileBorderContinuityMarginMeters);
            var junctionIndex = RoadNetworkConnectivity.BuildJunctionIndex(network, snapMeters);

            var start = points[0];
            var end = points[points.Count - 1];
            var startXZ = new Vector2(start.x, start.z);
            var endXZ = new Vector2(end.x, end.z);

            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, roadClass);
            var maxSlopeRatio = maxSlopeDegrees > 0f ? Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) : float.MaxValue;
            var probeStep = Mathf.Max(1f, settings.slopeProbeStepMeters);

            var startValid = IsValidEndpoint(
                startXZ, points.Count > 1 ? points[1] : start,
                tileRect, borderMargin, sourceRoadId, junctionIndex, network, tilePathPolylines, snapMeters,
                terrain, maxSlopeRatio, probeStep);

            var endValid = IsValidEndpoint(
                endXZ, points.Count > 1 ? points[^2] : end,
                tileRect, borderMargin, sourceRoadId, junctionIndex, network, tilePathPolylines, snapMeters,
                terrain, maxSlopeRatio, probeStep);

            if (!startValid || !endValid)
                return false;

            return MeetsMinimumLength(points, settings);
        }

        private static bool IsValidEndpoint(
            Vector2 endpointXZ,
            Vector3 neighborPoint,
            Rect tileRect,
            float borderMargin,
            int sourceRoadId,
            RoadNetworkConnectivity.JunctionIndex junctionIndex,
            IReadOnlyList<RoadPolyline> network,
            IReadOnlyList<IReadOnlyList<Vector3>> tilePathPolylines,
            float snapMeters,
            Terrain terrain,
            float maxSlopeRatio,
            float probeStepMeters)
        {
            if (RoadNetworkConnectivity.IsTileContinuityExit(endpointXZ, tileRect, borderMargin))
                return true;

            if (junctionIndex.IsNetworkJunction(endpointXZ, sourceRoadId, snapMeters))
                return true;

            if (RoadNetworkConnectivity.IsTileRoadEndpointJunction(endpointXZ, tilePathPolylines, snapMeters))
                return true;

            if (terrain != null && LeadsIntoBlockedTerrain(endpointXZ, neighborPoint, terrain, maxSlopeRatio, probeStepMeters))
                return false;

            if (RoadNetworkConnectivity.IsAuthoredTerminus(endpointXZ, network, snapMeters))
                return true;

            // Interior point with no junction, border exit, or authored terminus.
            return false;
        }

        private static bool LeadsIntoBlockedTerrain(
            Vector2 endpointXZ,
            Vector3 neighborPoint,
            Terrain terrain,
            float maxSlopeRatio,
            float probeStepMeters)
        {
            var neighborXZ = new Vector2(neighborPoint.x, neighborPoint.z);
            var dir = endpointXZ - neighborXZ;
            if (dir.sqrMagnitude < 0.01f)
                return true;

            dir.Normalize();
            var aheadXZ = endpointXZ + dir * probeStepMeters;
            var baseHeight = terrain.SampleHeight(new Vector3(endpointXZ.x, 0f, endpointXZ.y));
            var aheadHeight = terrain.SampleHeight(new Vector3(aheadXZ.x, 0f, aheadXZ.y));
            var slope = Mathf.Abs(aheadHeight - baseHeight) / probeStepMeters;
            return slope > maxSlopeRatio;
        }

        private static bool MeetsMinimumLength(IReadOnlyList<Vector3> points, RoadNetworkSettings settings)
        {
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);

            return length >= Mathf.Max(0.5f, settings.minSegmentLength);
        }

        private static float ResolveMaxSlopeDegrees(RoadNetworkSettings settings, RoadClass roadClass)
        {
            if (!settings.usePerRoadClassSlopeLimits)
                return roadClass == RoadClass.Local ? settings.maxCityRoadSlopeDegrees : 0f;

            return roadClass switch
            {
                RoadClass.Highway => settings.maxHighwayRoadSlopeDegrees,
                RoadClass.Arterial => settings.maxArterialRoadSlopeDegrees,
                RoadClass.Local => settings.maxLocalRoadSlopeDegrees,
                _ => settings.maxCityRoadSlopeDegrees
            };
        }
    }
}
