using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class RoadStateGenerationProjection
    {
        public static List<RoadState> CreateWorldPlannedRoads(
            WorldMapSession session,
            RoadNetworkRuntime network)
        {
            return CreateRoads(session, null, network?.Roads, RoadSourceKind.WorldPlanned);
        }

        public static List<RoadState> CreateCityGeneratedRoads(
            WorldMapSession session,
            WorldSitePlan plan,
            IReadOnlyList<RoadPolyline> polylines)
        {
            return CreateRoads(session, plan, polylines, RoadSourceKind.CityGenerated);
        }

        private static List<RoadState> CreateRoads(
            WorldMapSession session,
            WorldSitePlan plan,
            IReadOnlyList<RoadPolyline> polylines,
            RoadSourceKind sourceKind)
        {
            var roads = new List<RoadState>();
            if (polylines == null)
                return roads;

            var collisionRegistry = new Dictionary<WorldEntityId, string>();
            for (var i = 0; i < polylines.Count; i++)
            {
                if (TryCreateRoad(session, plan, polylines[i], sourceKind, i, collisionRegistry, out var road))
                    roads.Add(road);
            }

            return roads;
        }

        private static bool TryCreateRoad(
            WorldMapSession session,
            WorldSitePlan plan,
            RoadPolyline polyline,
            RoadSourceKind sourceKind,
            int ordinal,
            IDictionary<WorldEntityId, string> collisionRegistry,
            out RoadState road)
        {
            road = null;
            if (polyline?.pointsXZ == null || polyline.pointsXZ.Count < 2)
                return false;

            ResolveParent(session, plan, polyline, sourceKind, out var parentKind, out var parentId, out var settlementId);
            var sourceId = sourceKind == RoadSourceKind.WorldPlanned
                ? "world-road:" + polyline.id
                : "city-road:" + polyline.id;

            road = new RoadState
            {
                id = WorldStableIdFactory.CreateRoadId(
                    session.Seed,
                    parentKind,
                    parentId,
                    sourceId,
                    ordinal,
                    polyline.pointsXZ,
                    collisionRegistry),
                sourceId = sourceId,
                sourceKind = sourceKind,
                regionId = WorldStateSiteGenerationProjection.CreateWorldRegionId(session),
                settlementId = settlementId,
                sourceRoadId = polyline.id,
                roadClass = polyline.roadClass,
                widthMeters = polyline.widthMeters,
                preserveWorldPath = polyline.preserveWorldPath,
                curvedMarkers = polyline.curvedMarkers,
                pointsXZ = new List<Vector2>(polyline.pointsXZ)
            };
            return true;
        }

        private static void ResolveParent(
            WorldMapSession session,
            WorldSitePlan plan,
            RoadPolyline polyline,
            RoadSourceKind sourceKind,
            out WorldEntityKind parentKind,
            out WorldEntityId parentId,
            out WorldEntityId settlementId)
        {
            settlementId = default;
            if (sourceKind == RoadSourceKind.CityGenerated &&
                WorldStateSiteGenerationProjection.TryFindContainingSettlement(
                    session, plan, polyline.BoundsXZ, out var settlement))
            {
                parentKind = WorldEntityKind.Settlement;
                parentId = settlement.id;
                settlementId = settlement.id;
                return;
            }

            parentKind = WorldEntityKind.Region;
            parentId = WorldStateSiteGenerationProjection.CreateWorldRegionId(session);
        }
    }
}
