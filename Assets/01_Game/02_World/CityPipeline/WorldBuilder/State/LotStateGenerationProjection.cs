using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class LotStateGenerationProjection
    {
        private const string AreasContainerName = "CityNamedAreas";
        private const string DistrictLotsContainerName = "DistrictLots";
        private const string LegacyLotsContainerName = "ResidentialLots";

        public static void ReplaceLots(
            WorldBuildContext context,
            CityPrefabRoadNetworkBuilder builder)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(
                    context, WorldBuildStageId.GenerateDistrictLots, out var stage))
                return;

            var lots = CreateLots(context.Session, builder);
            stage.ReplaceLots(lots);
        }

        public static List<LotState> CreateLots(
            WorldMapSession session,
            CityPrefabRoadNetworkBuilder builder)
        {
            var lots = new List<LotState>();
            var areasRoot = builder != null ? builder.transform.Find(AreasContainerName) : null;
            if (areasRoot == null)
                return lots;

            var collisionRegistry = new Dictionary<WorldEntityId, string>();
            for (var i = 0; i < areasRoot.childCount; i++)
                AddAreaLots(session, areasRoot.GetChild(i), lots, collisionRegistry);

            return lots;
        }

        private static void AddAreaLots(
            WorldMapSession session,
            Transform area,
            List<LotState> lots,
            IDictionary<WorldEntityId, string> collisionRegistry)
        {
            if (!WorldStateEntityViewBinding.TryGetBoundId(
                    area.gameObject, WorldEntityKind.District, out var districtId))
                return;

            var container = area.Find(DistrictLotsContainerName) ?? area.Find(LegacyLotsContainerName);
            if (container == null)
                return;

            for (var i = 0; i < container.childCount; i++)
            {
                var lotTransform = container.GetChild(i);
                if (TryCreateLot(session, districtId, lotTransform, i, collisionRegistry, out var lot))
                    lots.Add(lot);
            }
        }

        private static bool TryCreateLot(
            WorldMapSession session,
            WorldEntityId districtId,
            Transform lotTransform,
            int ordinal,
            IDictionary<WorldEntityId, string> collisionRegistry,
            out LotState lot)
        {
            lot = null;
            var marker = lotTransform != null ? lotTransform.GetComponent<CityLotFacingMarker>() : null;
            if (marker == null)
                return false;

            var bounds = ExtractLotBounds(lotTransform);
            if (bounds.width <= 0f || bounds.height <= 0f)
                return false;

            var outline = ExtractLotOutline(lotTransform, bounds);
            var sourceId = "lot:" + districtId.value + ":" + ordinal;
            lot = new LotState
            {
                id = WorldStableIdFactory.CreateLotId(
                    session.Seed,
                    districtId,
                    sourceId,
                    ordinal,
                    outline,
                    collisionRegistry),
                sourceId = sourceId,
                districtId = districtId,
                sourceIndex = ordinal,
                boundsXZ = bounds,
                outlineXZ = outline,
                groundWorldY = lotTransform.position.y,
                streetFace = marker.streetFace,
                commercialKind = marker.commercialKind,
                isCornerLot = marker.isCornerLot,
                isCurvedLot = marker.isCurvedLot
            };

            WorldStateEntityViewBinding.Bind(lotTransform.gameObject, lot.id);
            return true;
        }

        private static List<Vector2> ExtractLotOutline(Transform lotTransform, Rect fallbackBounds)
        {
            var points = ExtractMeshOutline(lotTransform);
            if (points.Count >= 3)
                return points;

            points.Clear();
            points.Add(new Vector2(fallbackBounds.xMin, fallbackBounds.yMin));
            points.Add(new Vector2(fallbackBounds.xMax, fallbackBounds.yMin));
            points.Add(new Vector2(fallbackBounds.xMax, fallbackBounds.yMax));
            points.Add(new Vector2(fallbackBounds.xMin, fallbackBounds.yMax));
            return points;
        }

        private static Rect ExtractLotBounds(Transform lotTransform)
        {
            var mesh = lotTransform != null
                ? lotTransform.GetComponent<MeshFilter>()?.sharedMesh
                : null;
            if (mesh == null)
                return default;

            var localBounds = mesh.bounds;
            if (localBounds.size.x <= 0f || localBounds.size.z <= 0f)
                return default;

            var position = lotTransform.position;
            return Rect.MinMaxRect(
                position.x + localBounds.min.x,
                position.z + localBounds.min.z,
                position.x + localBounds.max.x,
                position.z + localBounds.max.z);
        }

        private static List<Vector2> ExtractMeshOutline(Transform lotTransform)
        {
            var points = new List<Vector2>();
            var mesh = lotTransform != null
                ? lotTransform.GetComponent<MeshFilter>()?.sharedMesh
                : null;
            if (mesh?.vertices == null)
                return points;

            var vertices = mesh.vertices;
            var position = lotTransform.position;
            for (var i = 0; i < vertices.Length; i++)
                points.Add(new Vector2(position.x + vertices[i].x, position.z + vertices[i].z));

            return points;
        }
    }
}
