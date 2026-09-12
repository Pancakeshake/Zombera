using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        [SerializeField, HideInInspector] private CityStreetscapeConfig streetscapeConfig;

        private void EnsurePlacerCatalog()
        {
            if (streetscapeConfig != null && CityPlacerPrefabResolver.Catalog == null)
                CityPlacerPrefabResolver.Catalog = streetscapeConfig;
        }

        public CityStreetscapeConfig StreetscapeConfig => streetscapeConfig;

        private CityStreetscapeConfig ResolveStreetscapeConfig()
        {
            if (streetscapeConfig != null)
                return streetscapeConfig;
            return Resources.Load<CityStreetscapeConfig>("World/CityStreetscapeConfig")
                   ?? Resources.Load<CityStreetscapeConfig>("CityStreetscapeConfig");
        }

        [ContextMenu("Generate Traffic Lights")]
        public void GenerateTrafficLights()
        {
            EnsurePlacerCatalog();
            RefreshReferences();
            var settings = ResolveStreetscapeConfig();
            var prefab = streetscapeConfig != null ? streetscapeConfig.trafficSignalPrefab : null;
            if (settings == null || !settings.spawnTrafficLights || prefab == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Traffic lights disabled or missing prefab in streetscapeConfig.", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            var count = CityTrafficLightPlacer.PlaceForProceduralHub(
                roadNetwork,
                _lastJunctionRegistry,
                _cachedRoadPolylines,
                ResolveRoadSettings(),
                settings,
                ResolveGroundHeight);

            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed traffic signals=" + count + ".", this);
        }

        [ContextMenu("Clear Traffic Lights")]
        public void ClearTrafficLights()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork != null)
                CityTrafficLightPlacer.Clear(roadNetwork);
        }

        [ContextMenu("Generate Street Signs")]
        public void GenerateStreetSigns()
        {
            EnsurePlacerCatalog();
            RefreshReferences();
            EnsureRoadCache();
            var settings = ResolveStreetscapeConfig();
            var prefab = streetscapeConfig != null ? streetscapeConfig.streetSignPrefab : null;
            if (settings == null || !settings.spawnStreetSigns || prefab == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Street signs disabled or missing prefab in streetscapeConfig.", this);
                return;
            }

            if (_lastGeneratedRoadNetwork == null || _lastGeneratedRoadNetwork.Roads.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No generated roads available.", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            var count = CityStreetSignPlacer.PlaceForProceduralHub(
                roadNetwork,
                _lastJunctionRegistry,
                _cachedRoadPolylines,
                ResolveRoadSettings(),
                settings,
                layout,
                ResolveGroundHeight);

            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed street signs=" + count + ".", this);
        }

        [ContextMenu("Clear Street Signs")]
        public void ClearStreetSigns()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork != null)
                CityStreetSignPlacer.Clear(roadNetwork);
        }

        /// <summary>
        ///     Step 12: Place utility poles on the sidewalk strip in front of district
        ///     lots (offset onto the footpath), skipping each building's painted
        ///     driveway and front-door path, and string power wires between them.
        ///     Requires buildings to be placed first (Step 11).
        /// </summary>
        [ContextMenu("Generate Power Lines")]
        public void GeneratePowerLines()
        {
            EnsurePlacerCatalog();

            var areasRoot = transform.Find(CityNamedAreasContainerName);
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas and place buildings first.", this);
                return;
            }

            var settings = ResolveStreetscapeConfig();
            var prefab = streetscapeConfig != null ? streetscapeConfig.utilityPolePrefab : null;
            if (settings == null || !settings.spawnPowerLines || prefab == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Power lines disabled or missing prefab in streetscapeConfig.", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            EnsureRoadCache();
            var roads = _lastGeneratedRoadNetwork != null ? _lastGeneratedRoadNetwork.Roads : null;

            var count = CityPowerLinePlacer.PlaceForHub(
                roadNetwork, areasRoot, roads, settings,
                DistrictLotTerrainLayout, ResolveGroundHeight);

            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed utility poles=" + count + ".", this);
        }

        [ContextMenu("Clear Power Lines")]
        public void ClearPowerLines()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork != null)
                CityPowerLinePlacer.Clear(roadNetwork);
        }

        /// <summary>
        ///     Step 19: Park cars on residential lot driveways. Each house rolls a
        ///     per-lot chance from the streetscape config, so only a fraction of
        ///     houses get a car. Requires named areas, lots, buildings and lot
        ///     terrain (steps 2–5).
        /// </summary>
        [ContextMenu("Generate Parked Cars")]
        public void GenerateParkedCars()
        {
            EnsurePlacerCatalog();

            var settings = ResolveStreetscapeConfig();
            var prefabs = streetscapeConfig != null ? streetscapeConfig.parkedCarPrefabs : null;
            if (settings == null || !settings.spawnParkedCars || prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Parked cars disabled or missing prefabs in streetscapeConfig.", this);
                return;
            }

            var areasRoot = transform.Find(AreasContainerName);
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas, lots, buildings and lot terrain first.", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            var seed = Layout != null && Layout.layoutSeed != 0 ? Layout.layoutSeed : 12345;
            var count = CityParkedCarPlacer.PlaceOnLotDriveways(
                roadNetwork, areasRoot, districtLotTerrainLayout,
                settings, ResolveGroundHeight, seed);
            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed parked cars=" + count + ".", this);
        }

        [ContextMenu("Clear Parked Cars")]
        public void ClearParkedCars()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork != null)
                CityParkedCarPlacer.Clear(roadNetwork);
        }

        [ContextMenu("Generate Street Furniture")]
        public void GenerateStreetFurniture()
        {
            EnsurePlacerCatalog();
            EnsureRoadCache();
            if (_lastGeneratedRoadNetwork == null || _lastGeneratedRoadNetwork.Roads.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No generated roads available.", this);
                return;
            }

            var settings = ResolveStreetscapeConfig();
            if (settings == null || !settings.spawnStreetFurniture)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Street furniture disabled.", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found.", this);
                return;
            }

            ClearStreetFurniture();
            var seed = Layout != null && Layout.layoutSeed != 0 ? Layout.layoutSeed : 12345;
            var count = CityStreetFurniturePlacer.PlaceForHub(
                roadNetwork, _lastGeneratedRoadNetwork.Roads, _lastGeneratedBounds,
                ResolveRoadSettings(), settings, ResolveGroundHeight, seed);
            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed street furniture=" + count + ".", this);
        }

        [ContextMenu("Clear Street Furniture")]
        public void ClearStreetFurniture()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork != null)
                CityStreetFurniturePlacer.Clear(roadNetwork);
        }

        // ── Helpers ──

        private RoadNetworkSettings ResolveRoadSettings()
        {
            return roadNetworkSettings != null
                ? roadNetworkSettings
                : Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
        }

        private void ClearAllStreetscapeRoadPhase()
        {
            ClearTrafficLights();
            ClearStreetSigns();
            ClearPowerLines();
            ClearParkedCars();
            ClearStreetFurniture();
        }
    }
}
