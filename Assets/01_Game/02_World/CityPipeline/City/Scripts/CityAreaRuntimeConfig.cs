using UnityEngine;
using Zombera.World.Roads;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    [CreateAssetMenu(menuName = "Zombera/World/City Area Runtime Config", fileName = "CityAreaRuntimeConfig")]
    public sealed class CityAreaRuntimeConfig : ScriptableObject
    {
        [Header("Config SOs")]
        [Tooltip("Biome gating settings. Fallback to inline fields if unassigned.")]
        public CityBiomeConfig biomeConfig;

        [Tooltip("Building placement settings. Fallback to inline fields if unassigned.")]
        public CityBuildingPlacementConfig buildingPlacementConfig;

        [Header("Layout")]
        public CityMathRoadLayout layout = new();

        public RoadNetworkSettings roadNetworkSettings;

        [Header("Buildings")]
        public StreamedCityCatalog buildingCatalog;

        public CityNamedAreaBuildingLayoutSettings buildingLayoutSettings =
            CityNamedAreaBuildingLayoutSettings.CreateDefault();

        public bool useProxyPrefabs = true;

        [Header("Terrain — fallback (used when terrainConfig SO is unassigned)")]
        [Min(0f)] public float terrainFlattenPaddingMeters = 0.35f;

        [Min(0f)] public float terrainFlattenBlendMeters = 32f;

        [Tooltip("Wait for MapMagic main terrain (not draft-only) before flatten and city build.")]
        public bool waitForMainTerrainBeforeBuild = true;

        [Header("Biome — fallback (used when biomeConfig SO is unassigned)")]
        [Range(0f, 1f)] public float biomeDominanceThreshold = WorldTileInfoUtility.DefaultCityAreaDominanceThreshold;

        [Tooltip("When enabled, the single-tile stress scene always builds a city on the pinned tile.")]
        public bool forceBuildOnSingleTileStressSession = true;

        [Header("Generation")]
        public int layoutSeed = 12345;

        public bool useWorldSeedForLayout;

        [Tooltip("When enabled, spawns EasyRoads city road meshes via ProceduralRoadSystem (world roads stay separate).")]
        public bool spawnCityRoadMeshes = true;

        [Tooltip("Places EasyRoads X/T connectors on city grid crossings.")]
        public bool createJunctionConnectors = true;

        public bool tIntersectionsOnly;

        // ── Resolved accessors: prefer roadNetworkSettings, fall back to inline ──

        public float ResolvedTerrainFlattenPaddingMeters =>
            roadNetworkSettings != null ? roadNetworkSettings.terrainPaddingMeters : terrainFlattenPaddingMeters;

        public float ResolvedTerrainFlattenBlendMeters =>
            roadNetworkSettings != null ? roadNetworkSettings.terrainBlendMeters : terrainFlattenBlendMeters;

        public bool ResolvedWaitForMainTerrainBeforeBuild =>
            roadNetworkSettings != null ? roadNetworkSettings.waitForMainTerrainBeforeBuild : waitForMainTerrainBeforeBuild;

        public bool ResolvedFlattenTerrain =>
            roadNetworkSettings == null || roadNetworkSettings.flattenTerrain;

        public float ResolvedBiomeDominanceThreshold =>
            biomeConfig != null ? biomeConfig.biomeDominanceThreshold : biomeDominanceThreshold;

        public bool ResolvedForceBuildOnSingleTileStressSession =>
            biomeConfig != null ? biomeConfig.forceBuildOnSingleTileStressSession : forceBuildOnSingleTileStressSession;

        public bool ResolvedSpawnCityRoadMeshes =>
            roadNetworkSettings != null ? roadNetworkSettings.spawnRoadMeshes : spawnCityRoadMeshes;

        public bool ResolvedCreateJunctionConnectors =>
            roadNetworkSettings != null ? roadNetworkSettings.createJunctionConnectors : createJunctionConnectors;

        public bool ResolvedTIntersectionsOnly =>
            roadNetworkSettings != null ? roadNetworkSettings.tIntersectionsOnly : tIntersectionsOnly;

        public bool ResolvedUseProxyPrefabs =>
            buildingPlacementConfig != null ? buildingPlacementConfig.useProxyPrefabs : useProxyPrefabs;

        public StreamedCityCatalog ResolvedBuildingCatalog =>
            buildingPlacementConfig != null && buildingPlacementConfig.buildingCatalog != null
                ? buildingPlacementConfig.buildingCatalog
                : buildingCatalog;

        public int ResolvedLayoutSeed =>
            buildingPlacementConfig != null ? buildingPlacementConfig.layoutSeed : layoutSeed;

        public bool ResolvedUseWorldSeedForLayout =>
            buildingPlacementConfig != null ? buildingPlacementConfig.useWorldSeedForLayout : useWorldSeedForLayout;

        // ── Authoring constants ──

        public const float AuthoringCenterX = 500f;
        public const float AuthoringCenterZ = 500f;
        public const float AuthoringHalfExtentMeters = 400f;
        public const float AuthoringCityRadiusMeters = 280f;
        public const float AuthoringStreetSpacingMeters = 80f;
        public const float AuthoringBlockSpacingMinMeters = 55f;
        public const float AuthoringBlockSpacingMaxMeters = 95f;
        public const float AuthoringBlockSpacingJitter = 0.22f;

        public static void ApplyAuthoringLayoutDefaults(CityMathRoadLayout layout)
        {
            if (layout == null)
                return;

            layout.centerXZ = new Vector2(AuthoringCenterX, AuthoringCenterZ);
            layout.footprintShape = CityFootprintShape.Square;
            layout.cityRadiusMeters = AuthoringCityRadiusMeters;
            layout.streetSpacingMeters = AuthoringStreetSpacingMeters;
            layout.randomizeCityExtents = false;
            layout.cityHalfWidthMinMeters = AuthoringHalfExtentMeters;
            layout.cityHalfWidthMaxMeters = AuthoringHalfExtentMeters;
            layout.cityHalfDepthMinMeters = AuthoringHalfExtentMeters;
            layout.cityHalfDepthMaxMeters = AuthoringHalfExtentMeters;
            layout.randomizeBlockSpacing = true;
            layout.blockSpacingMinMeters = AuthoringBlockSpacingMinMeters;
            layout.blockSpacingMaxMeters = AuthoringBlockSpacingMaxMeters;
            layout.blockSpacingJitter = AuthoringBlockSpacingJitter;
            layout.generateStreetGrid = true;
            layout.generateArterialRing = true;
            layout.clipLocalStreetsToArterialRing = true;
            layout.generateHighwayExits = false;
        }

        public void ApplyHubDefaults()
        {
            layout ??= new CityMathRoadLayout();
            ApplyAuthoringLayoutDefaults(layout);
            buildingLayoutSettings = CityNamedAreaBuildingLayoutSettings.CreateDefault();
            forceBuildOnSingleTileStressSession = true;
            waitForMainTerrainBeforeBuild = true;
            spawnCityRoadMeshes = true;
            createJunctionConnectors = true;
            tIntersectionsOnly = false;
        }

        public int ResolveLayoutSeed(int worldSeed)
        {
            if (ResolvedUseWorldSeedForLayout && worldSeed != 0)
                return worldSeed;

            if (layout.layoutSeed != 0)
                return layout.layoutSeed;

            return ResolvedLayoutSeed != 0 ? ResolvedLayoutSeed : 12345;
        }
    }
}
