using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class DistrictStateGenerationProjection
    {
        private const string AreasContainerName = "CityNamedAreas";

        public static void ReplaceDistricts(
            WorldBuildContext context,
            CityPrefabRoadNetworkBuilder builder)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(
                    context, WorldBuildStageId.GenerateNamedAreas, out var stage))
                return;

            var districts = CreateDistricts(context.Session, context.Artifacts?.Sites, builder);
            stage.ReplaceDistricts(districts);
        }

        public static List<DistrictState> CreateDistricts(
            WorldMapSession session,
            WorldSitePlan plan,
            CityPrefabRoadNetworkBuilder builder)
        {
            var districts = new List<DistrictState>();
            var areasRoot = builder != null ? builder.transform.Find(AreasContainerName) : null;
            if (areasRoot == null)
                return districts;

            var collisionRegistry = new Dictionary<WorldEntityId, string>();
            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var marker = areasRoot.GetChild(i).GetComponent<CityNamedAreaMarker>();
                if (TryCreateDistrict(session, plan, marker, i, collisionRegistry, out var district))
                    districts.Add(district);
            }

            return districts;
        }

        private static bool TryCreateDistrict(
            WorldMapSession session,
            WorldSitePlan plan,
            CityNamedAreaMarker marker,
            int ordinal,
            IDictionary<WorldEntityId, string> collisionRegistry,
            out DistrictState district)
        {
            district = null;
            if (marker == null)
                return false;

            if (!TryResolveSettlement(session, plan, marker, out var settlement))
                return false;

            var outline = CopyOutline(marker.GetHubShiftedOutlineXZ());
            var sourceId = "district:" + marker.AreaId;
            var bounds = marker.GetHubShiftedBoundsXZ();
            district = new DistrictState
            {
                id = WorldStableIdFactory.CreateDistrictId(
                    session.Seed,
                    settlement.id,
                    sourceId,
                    ordinal,
                    outline,
                    collisionRegistry),
                sourceId = sourceId,
                settlementId = settlement.id,
                sourceAreaId = marker.AreaId,
                displayName = marker.DisplayName ?? string.Empty,
                clusterName = marker.ClusterName ?? string.Empty,
                districtType = marker.DistrictType,
                gridX = marker.GridX,
                gridZ = marker.GridZ,
                boundsXZ = bounds,
                centerXZ = bounds.center,
                groundWorldY = marker.transform.position.y,
                areaSquareMeters = marker.AreaSquareMeters,
                roundedCorners = marker.RoundedCorners,
                arterialCornerRadiusMeters = marker.ArterialCornerRadiusMeters,
                outlineXZ = outline
            };

            WorldStateEntityViewBinding.Bind(marker.gameObject, district.id);
            return true;
        }

        private static bool TryResolveSettlement(
            WorldMapSession session,
            WorldSitePlan plan,
            CityNamedAreaMarker marker,
            out SettlementState settlement)
        {
            var bounds = marker.GetHubShiftedBoundsXZ();
            if (WorldStateSiteGenerationProjection.TryFindContainingSettlement(
                    session, plan, bounds, out settlement))
                return true;

            return WorldStateSiteGenerationProjection.TryFindSettlementAtPoint(
                session, plan, bounds.center, out settlement);
        }

        private static List<Vector2> CopyOutline(IReadOnlyList<Vector2> outline)
        {
            var result = new List<Vector2>();
            if (outline == null)
                return result;

            for (var i = 0; i < outline.Count; i++)
                result.Add(outline[i]);

            return result;
        }
    }
}
