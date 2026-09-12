using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Validated bridge/tunnel fallback for inter-city highway A* failures.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        private const float InfrastructureSampleStepMeters = 12f;
        private const float WetDepthMeters = 0.05f;

        internal static bool TryRouteTerrainValidatedInfrastructure(
            WorldCitySite siteA,
            WorldCitySite siteB,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            LandformProfile landforms,
            LandformField landformField,
            OrogenPlan orogen,
            IWorldHydrologyQuery hydrologyQuery,
            out Vector2 start,
            out Vector2 end,
            out List<Vector2> routed,
            out string failReason)
        {
            start = default;
            end = default;
            routed = null;
            failReason = "infrastructure_fallback_uninitialized";

            var settings = profile?.RoadNetworkSettings;
            var hydrology = profile?.Hydrology;
            if (!CanUseInfrastructureFallback(
                    siteA, siteB, settings, hydrology, terrainQuery, landforms, landformField))
            {
                failReason = "infrastructure_fallback_unavailable";
                return false;
            }

            start = ResolveSiteAnchor(siteA, siteB.CenterXZ, landforms, terrainQuery, 0);
            end = ResolveSiteAnchor(siteB, siteA.CenterXZ, landforms, terrainQuery, 0);

            var direct = CreateDirectHighway(start, end, settings);
            var crossings = WaterCrossingScanner.Scan(direct, terrainQuery, hydrologyQuery, hydrology);
            var tunnels = MountainTunnelScanner.Scan(
                direct,
                landformField,
                settings,
                pads: null,
                crossings,
                hydrology.SeaLevelWorldY,
                orogen);
            if (HasInfrastructure(crossings, tunnels) ||
                IsProtectedDirectRoute(start, end, terrainQuery, settings, null, null))
            {
                routed = new List<Vector2>(2) { start, end };
                failReason = null;
                return true;
            }

            failReason = "no_safe_direct_route";
            return false;
        }

        private static bool CanUseInfrastructureFallback(
            WorldCitySite siteA,
            WorldCitySite siteB,
            RoadNetworkSettings settings,
            HydrologyProfile hydrology,
            IWorldTerrainQuery terrainQuery,
            LandformProfile landforms,
            LandformField landformField)
        {
            return siteA != null && siteB != null && settings != null && hydrology != null &&
                   terrainQuery != null && landforms != null && landformField != null;
        }

        private static RoadNetworkRuntime CreateDirectHighway(
            Vector2 start,
            Vector2 end,
            RoadNetworkSettings settings)
        {
            var network = new RoadNetworkRuntime(HighwayIdBase);
            network.AddRoad(new RoadPolyline
            {
                id = HighwayIdBase,
                roadClass = RoadClass.Highway,
                widthMeters = settings.highwayWidth,
                preserveWorldPath = true,
                pointsXZ = new List<Vector2>(2) { start, end }
            });
            return network;
        }

        private static bool HasInfrastructure(
            IReadOnlyList<WaterCrossing> crossings,
            IReadOnlyList<MountainTunnel> tunnels)
        {
            if (tunnels != null && tunnels.Count > 0)
                return true;

            for (var i = 0; crossings != null && i < crossings.Count; i++)
            {
                if (IsBridge(crossings[i]))
                    return true;
            }

            return false;
        }

        private static bool IsProtectedDirectRoute(
            Vector2 start,
            Vector2 end,
            IWorldTerrainQuery terrainQuery,
            RoadNetworkSettings settings,
            IReadOnlyList<WaterCrossing> crossings,
            IReadOnlyList<MountainTunnel> tunnels)
        {
            var distance = Vector2.Distance(start, end);
            var samples = Mathf.Max(2, Mathf.CeilToInt(distance / InfrastructureSampleStepMeters));
            var transition = Mathf.Max(0f, settings.infrastructureSpanTransitionMeters) + settings.highwayWidth * 0.5f;
            var maxSlope = Mathf.Max(
                settings.maxHighwayRoadSlopeDegrees,
                settings.infrastructureFallbackMaxSurfaceSlopeDegrees);

            for (var i = 1; i < samples; i++)
            {
                var point = Vector2.Lerp(start, end, i / (float)samples);
                if (!terrainQuery.TrySample(point, out var sample))
                    return false;

                var bridge = IsInsideBridgeTransition(point, crossings, transition);
                var tunnel = IsInsideTunnelTransition(point, tunnels, transition);
                if (!IsSampleSupported(sample, bridge, tunnel, maxSlope))
                    return false;
            }

            return true;
        }

        private static bool IsSampleSupported(
            WorldTerrainSample sample,
            bool bridge,
            bool tunnel,
            float maxSlope)
        {
            if (bridge)
                return true;
            if (sample.Water.DepthMeters > WetDepthMeters)
                return false;
            if (sample.NoBuildMask >= 0.5f && !tunnel)
                return false;
            return tunnel || sample.SlopeDegrees <= maxSlope;
        }

        private static bool IsInsideBridgeTransition(
            Vector2 point,
            IReadOnlyList<WaterCrossing> crossings,
            float transition)
        {
            for (var i = 0; crossings != null && i < crossings.Count; i++)
            {
                if (IsBridge(crossings[i]) &&
                    DistanceToSegment(point, crossings[i].EntryXZ, crossings[i].ExitXZ) <= transition)
                    return true;
            }

            return false;
        }

        private static bool IsInsideTunnelTransition(
            Vector2 point,
            IReadOnlyList<MountainTunnel> tunnels,
            float transition)
        {
            for (var i = 0; tunnels != null && i < tunnels.Count; i++)
            {
                if (tunnels[i].ContainsPointOnSpan(point, transition))
                    return true;
            }

            return false;
        }

        private static bool IsBridge(in WaterCrossing crossing)
        {
            return crossing.Policy == WaterCrossingPolicy.Bridge ||
                   crossing.Policy == WaterCrossingPolicy.Causeway;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return Vector2.Distance(point, a);

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
