using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        [ContextMenu("Generate Driveway Meshes")]
        public void GenerateDrivewayMeshes()
        {
            var networkRoot = ResolveRoadContentRoot();
            if (networkRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            var settings = roadNetworkSettings != null
                ? roadNetworkSettings
                : Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] RoadNetworkSettings not found.", this);
                return;
            }

            var material = RoadMeshBuilder.ResolveMaterial(null, settings);
            var requests = CollectDrivewayRequests();
            var placed = ProceduralDrivewayPlacer.PlaceDriveways(
                networkRoot,
                requests,
                ResolveGroundHeight,
                material,
                surfaceLiftMeters: 0.05f);

            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed driveway meshes=" + placed + ".", this);
        }

        private List<LotDrivewayRequest> CollectDrivewayRequests()
        {
            var requests = new List<LotDrivewayRequest>(64);
            var areasRoot = transform.Find("CityNamedAreas");
            if (areasRoot == null)
                return requests;

            var terrainLayout = DistrictLotTerrainLayout != null
                ? DistrictLotTerrainLayout
                : districtLotTerrainLayout;
            var defaultDriveWidth = terrainLayout != null
                ? terrainLayout.residential.drivewayWidthMeters
                : 3f;

            for (var a = 0; a < areasRoot.childCount; a++)
            {
                var area = areasRoot.GetChild(a);
                var lotsRoot = area.Find("Lots");
                if (lotsRoot == null)
                    continue;

                var areaMarker = area.GetComponent<CityNamedAreaMarker>();
                var zoneLayout = terrainLayout != null && areaMarker != null
                    ? terrainLayout.GetLayout(areaMarker.DistrictType)
                    : null;
                var driveWidth = zoneLayout != null ? zoneLayout.drivewayWidthMeters : defaultDriveWidth;

                var anchors = CollectPlacedBuildingAnchors(area);
                for (var i = 0; i < lotsRoot.childCount; i++)
                {
                    var lot = lotsRoot.GetChild(i);
                    var lotRect = CityDistrictLotPlacement.ExtractLotRect(lot);
                    if (lotRect.width < 4f || lotRect.height < 4f)
                        continue;

                    var building = FindBuildingAnchorForLot(lotRect, anchors);
                    if (!building.HasValue || !building.Value.HasDoor)
                        continue;

                    if (!TryResolveStreetPointForLot(lotRect, building.Value.DoorXZ, out var streetPoint))
                        continue;

                    requests.Add(new LotDrivewayRequest(
                        streetPoint,
                        building.Value.DoorXZ,
                        driveWidth,
                        lot.GetInstanceID()));
                }
            }

            return requests;
        }

        private static bool TryResolveStreetPointForLot(Rect lotRect, Vector2 doorXZ, out Vector2 streetPoint)
        {
            streetPoint = default;
            var center = lotRect.center;
            var toDoor = doorXZ - center;
            if (toDoor.sqrMagnitude < 0.01f)
                return false;

            var absX = Mathf.Abs(toDoor.x);
            var absY = Mathf.Abs(toDoor.y);
            if (absX >= absY)
            {
                streetPoint = toDoor.x > 0f
                    ? new Vector2(lotRect.xMax, doorXZ.y)
                    : new Vector2(lotRect.xMin, doorXZ.y);
            }
            else
            {
                streetPoint = toDoor.y > 0f
                    ? new Vector2(doorXZ.x, lotRect.yMax)
                    : new Vector2(doorXZ.x, lotRect.yMin);
            }

            return true;
        }
    }
}
