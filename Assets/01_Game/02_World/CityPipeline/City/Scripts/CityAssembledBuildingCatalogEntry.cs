using UnityEngine;

namespace Zombera.World.City
{
    public sealed class CityAssembledBuildingCatalogEntry
    {
        public string id = string.Empty;
        public string assetPath = string.Empty;
        public GameObject prefab;
        public GameObject proxyPrefab;
        public CityDistrictType districtType = CityDistrictType.Mixed;
        public float yawOffsetDegrees;
        public float doorYawOffsetDegrees;
        public bool doorYawResolved;
        public float footprintWidthMeters = 12f;
        public float footprintDepthMeters = 12f;
        public Vector2 footprintCenterOffsetXZ;
        public float proxyFootprintWidthMeters;
        public float proxyFootprintDepthMeters;
        public Vector2 proxyCenterOffsetXZ;
        public bool hasProxyFootprint;

        public void ApplyFootprint(BuildingFootprintInfo info)
        {
            if (!info.IsValid)
                return;

            footprintWidthMeters = Mathf.Max(CityBuildingPrefabFootprintUtility.ModularGridCellMeters, info.WidthMeters);
            footprintDepthMeters = Mathf.Max(CityBuildingPrefabFootprintUtility.ModularGridCellMeters, info.DepthMeters);
            footprintCenterOffsetXZ = info.CenterOffsetXZ;
        }

        public void ApplyProxyFootprint(BuildingFootprintInfo info)
        {
            if (!info.IsValid)
            {
                hasProxyFootprint = false;
                return;
            }

            hasProxyFootprint = true;
            proxyFootprintWidthMeters = Mathf.Max(CityBuildingPrefabFootprintUtility.ModularGridCellMeters, info.WidthMeters);
            proxyFootprintDepthMeters = Mathf.Max(CityBuildingPrefabFootprintUtility.ModularGridCellMeters, info.DepthMeters);
            proxyCenterOffsetXZ = info.CenterOffsetXZ;
        }

        public Vector2 ResolvePlacementCenterOffset(bool useProxy)
        {
            return useProxy && hasProxyFootprint ? proxyCenterOffsetXZ : footprintCenterOffsetXZ;
        }

        public float ResolvePlacementWidth(bool useProxy)
        {
            return useProxy && hasProxyFootprint ? proxyFootprintWidthMeters : footprintWidthMeters;
        }

        public float ResolvePlacementDepth(bool useProxy)
        {
            return useProxy && hasProxyFootprint ? proxyFootprintDepthMeters : footprintDepthMeters;
        }
    }
}