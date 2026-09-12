using UnityEngine;

namespace Zombera.World.Roads
{
    [CreateAssetMenu(menuName = "Zombera/World/Road Network Settings", fileName = "RoadNetworkSettings")]
    public sealed class RoadNetworkSettings : ScriptableObject
    {
        // ── City Constraints ──────────────────────────────────
        [Header("City Constraints")]
        [Range(1f, 45f)] public float maxCityRoadSlopeDegrees = 12f;
        [Range(1f, 60f)] public float maxHighwayRoadSlopeDegrees = 8f;
        [Range(1f, 60f)] public float maxArterialRoadSlopeDegrees = 10f;
        [Range(1f, 60f)] public float maxLocalRoadSlopeDegrees = 8f;
        [Min(1f)] public float slopeProbeStepMeters = 8f;

        // ── Dimensions ────────────────────────────────────────
        [Header("Dimensions")]
        [Min(100f)] public float globalRingRadius = 1800f;
        [Min(1)] public int globalSpokeCount = 6;

        [Min(100f)] public float cityGridRadius = 280f;
        [Min(10f)] public float cityGridSpacing = 80f;

        [Tooltip("Road class for routes leaving the city centre toward the open map.")]
        public RoadClass cityExitRoadClass = RoadClass.Highway;

        // ── Feature Toggles ──────────────────────────────────
        [Header("Feature Toggles")]

        [Tooltip("Outer ring highway around the world. Disable to avoid looping highways on badlands terrain.")]
        public bool generateGlobalRing;

        public bool generateCityGrids = true;

        [Tooltip("When enabled, city exits are straight start-to-end anchors (less S-curves before terrain A*).")]
        public bool cityExitUseDirectSpokes = true;

        // ── Multi-City Network ────────────────────────────────
        [Header("Multi-City Network")]
        [Min(1)] public int cityCount = 5;
        [Min(200f)] public float cityMinSeparationMeters = 700f;
        [Min(0.4f)] public float cityRadiusVariationMin = 0.6f;
        [Min(1f)] public float cityRadiusVariationMax = 1.4f;
        public bool connectCitiesWithHighways = true;
        public bool spawnTownNodes = true;
        [Range(0f, 1f)] public float highwayExtraLoopChance = 0.55f;

        [Tooltip("Extra inter-city chords forced through snow-elevation orogen so mountain tunnels bore the high range.")]
        [Min(0)] public int highwaySnowTunnelExtraLinks = 2;

        [Tooltip("Minimum peak terrain height along a chord to qualify as a snow-ridge tunnel extra (meters).")]
        [Min(0f)] public float highwaySnowTunnelMinPeakHeightMeters = 480f;

        [Tooltip("When enabled, inter-city highways are pathfound and carved into landforms before city pads.")]
        public bool planInterCityHighwaysBeforePads = true;

        [Tooltip("Sites closer than this are never linked directly by inter-city highways.")]
        [Min(50f)] public float highwayMinLinkDistanceMeters = 600f;

        [Tooltip("Per-city attempts to re-roll position when an MST highway edge fails pathfinding.")]
        [Min(0)] public int maxConnectivityRerollAttemptsPerCity = 3;

        [Tooltip("Blend distance for coarse highway corridor carving on the landform grid.")]
        [Min(8f)] public float highwayCoarseCorridorBlendMeters = 48f;

        [Tooltip("When enabled, world-map polylines are rerouted with slope/elevation-cost A* after terrain is available.")]
        public bool rerouteRoadsWithTerrainPathfinding = true;

        [Tooltip("Trim self-intersecting loops produced during terrain pathfinding.")]
        public bool removePathfindingLoops = true;

        [Tooltip("When disabled, failed A* hops are skipped instead of falling back to straight lines over mountains.")]
        public bool fallbackToStraightPathWhenPathfindingFails;

        [Tooltip("When terrain A* fails, allow only direct inter-city spans proven to contain a bridge and/or daylight-safe tunnel. This never enables the unvalidated straight-path fallback above.")]
        public bool enableTerrainValidatedInfrastructureFallback = true;

        [Tooltip("Extra distance around validated bridge abutments and tunnel mouths treated as their engineered transition zone (meters).")]
        [Min(0f)] public float infrastructureSpanTransitionMeters = 48f;

        [Tooltip("Maximum raw terrain slope permitted outside a validated bridge/tunnel span when using the terrain-validated infrastructure fallback.")]
        [Range(1f, 60f)] public float infrastructureFallbackMaxSurfaceSlopeDegrees = 45f;

        [Tooltip("Remove hairpin points after pathfinding to reduce highway spaghetti.")]
        public bool simplifySharpTurnsAfterPathfinding = true;

        [Tooltip("Drop road points that exceed the road-class slope limit after terrain refinement.")]
        public bool filterSteepRoadPointsAfterRefinement = true;

        [Tooltip("When enabled, slope clipping uses road-class specific limits instead of only maxCityRoadSlopeDegrees.")]
        public bool usePerRoadClassSlopeLimits = true;

        [Tooltip("Skip EasyRoads segments whose endpoints are interior dead-ends (not tile exits or network junctions).")]
        public bool rejectInteriorDeadEndSegments = true;

        [Tooltip("When enabled, roads designated via RoadGameplayAuthoringRoot GameObjects in the scene are used for terrain deformation and mesh building.")]
        public bool useAuthoringRootInput = true;

        [Tooltip("Legacy toggle kept for older assets. When true, overrides roadLayoutSource to MapMagic splines.")]
        public bool useMapMagicSplineOutput;

        [Tooltip("When using legacy MapMagic splines, fallback to the world-map graph if spline data is missing.")]
        public bool fallbackToDeterministicWhenMissing = true;

        [Tooltip("When enabled, stamp roads into terrain heights and splatmaps.")]
        public bool applyTerrainDeformationAndPaint = true;

        // ── Mountain Tunnels ──────────────────────────────────
        [Header("Mountain Tunnels")]
        [Tooltip("Scan refined highways for cut-peak tunnels and place a continuous modular bore through the mountain.")]
        public bool enableMountainTunnels = true;

        [Tooltip("When enabled: Terrain holes at mouths, mid MeshColliders as NavMesh floors, no portal gates. Opt-in until mouth stitch is proven.")]
        public bool tunnelEnterable = false;

        [Tooltip("Natural cover above endpoint-lerp design bed required to start a tunnel span (meters).")]
        [Min(4f)] public float tunnelMinCoverMeters = 18f;

        [Tooltip("Reject spans whose peak natural elevation is below this height above sea (meters). Tune to the generated world's vertical scale so valid mountain tunnels are not filtered out.")]
        [Min(0f)] public float tunnelMinPeakElevationAboveSeaMeters = 120f;

        [Tooltip("Only allow tunnel bores that cross the core of a generated major mountain range. This rejects small-hill cut-throughs.")]
        public bool tunnelRequireMajorOrogenCore = true;

        [Tooltip("Minimum generated mountain-core mask reached by a tunnel bore.")]
        [Range(0f, 1f)] public float tunnelMinOrogenCoreMask = 0.45f;

        [Min(24f)] public float tunnelMinLengthMeters = 48f;
        [Tooltip("Maximum continuous covered highway span emitted as one tunnel. Keep at map-scale so a ridge is one exact cover span.")]
        [Min(48f)] public float tunnelMaxLengthMeters = 8000f;

        [Tooltip("Portal daylighting carve length at each mouth before skip-carve core (terrain still carved at mouths).")]
        [Min(2f)] public float tunnelPortalDaylightMeters = 20f;

        [Tooltip("Extra hole paint length on the approach side of each mouth (meters).")]
        [Min(2f)] public float tunnelHoleApproachMeters = 12f;

        [Tooltip("Legacy compatibility value. Enterable mouth holes now use the measured bore clear width.")]
        [Min(0f)] public float tunnelHoleShoulderMeters = 2f;

        [Tooltip("Reject tunnel candidates this close to city pad outer bounds.")]
        [Min(0f)] public float tunnelPadExclusionMeters = 80f;

        [Tooltip("Minimum path/chord length ratio before a ridge winder may be replaced with a tunnel chord.")]
        [Min(1.05f)] public float tunnelChordMinPathToChordRatio = 1.4f;

        [Tooltip("Sample spacing along candidate tunnel chords when measuring cover (meters).")]
        [Min(4f)] public float tunnelChordSampleStepMeters = 24f;

        [Tooltip("Bump when chord-rewrite / tunnel-bed detection semantics change (cache / fingerprint).")]
        [Min(1)] public int tunnelChordAlgorithmVersion = 6;

        [Tooltip("Optional mouth cap at entry/exit. The bore itself is built from tunnelMidPrefab tiles.")]
        public GameObject tunnelPortalPrefab;

        [Tooltip("Required for a continuous tunnel: 10m mid segment tiled entry→exit.")]
        public GameObject tunnelMidPrefab;

        [Tooltip("Collider-only gate at mouths (non-enterable). Ignored when tunnelEnterable is true.")]
        public GameObject tunnelPortalGatePrefab;

        // ── Water Crossing Bridges ────────────────────────────
        [Header("Water Crossing Bridges")]
        [Tooltip("Optional abutment at entry/exit (bank seat).")]
        public GameObject bridgeAbutmentPrefab;

        [Tooltip("Required for a continuous deck: 10m mid segment tiled entry→exit.")]
        public GameObject bridgeMidPrefab;

        [Tooltip("Optional pier under longer spans.")]
        public GameObject bridgePierPrefab;

        [Tooltip("Collider-only gate at abutments (non-enterable v1).")]
        public GameObject bridgePortalGatePrefab;

        [Tooltip("Distance each bridge approach extends beyond the scanned water edge. Capsule-calibrated.")]
        [Min(0f)] public float bridgeBankExtensionMeters = 15f;

        [Tooltip("Road grade distance used to meet the exact bridge approach socket height.")]
        [Min(2f)] public float bridgeApproachMeters = 12f;

        // ── Mesh Spawning ─────────────────────────────────────
        [Header("Mesh Spawning")]
        [Tooltip("When enabled, spawns EasyRoads city road meshes via ProceduralRoadSystem.")]
        public bool spawnRoadMeshes = true;

        [Tooltip("EasyRoads road type name used when creating roads. Leave empty to auto-detect from: Default Road, Primary Road, Road, Local Road, Secondary Road.")]
        public string easyRoadsRoadTypeName;

        [Tooltip("When enabled, EasyRoads native sidewalks are configured on road prefabs (width/material come from the prefab).")]
        public bool spawnSidewalkMeshes;

        [Tooltip("Sidewalk width used for street-side offset math. Must match the native sidewalk width configured on the EasyRoads road prefab.")]
        [Min(0.5f)] public float sidewalkWidthMeters = 5f;

        /// <summary>
        ///     Sidewalk width for street-side offsets (lamps, signs, signals, block inset).
        ///     Matches the native EasyRoads sidewalk width configured on road prefabs.
        /// </summary>
        public float ResolveSidewalkWidthMeters()
        {
            return sidewalkWidthMeters > 0f ? sidewalkWidthMeters : 5f;
        }

        [Tooltip("When enabled, generate footpath strips outside sidewalks (house-facing side of the street).")]
        public bool spawnFootpathMeshes = true;

        [Tooltip("When enabled, place optional decal prefabs along generated road meshes.")]
        public bool spawnRoadDecals;

        [Tooltip("When enabled, place lamp prefabs on both sides of roads at regular intervals.")]
        public bool spawnStreetLamps;

        [Tooltip("When enabled, places stop-line and crosswalk DecalProjector decals at city junctions after road generation.")]
        public bool spawnJunctionDecals = true;

        // ── Junction Connectors ───────────────────────────────
        [Header("Junction Connectors")]
        [Tooltip("Places EasyRoads X/T connectors on city grid crossings.")]
        public bool createJunctionConnectors = true;

        [Tooltip("When enabled, only places T connectors (skips inner 4-way X junctions).")]
        public bool tIntersectionsOnly;

        [Tooltip("Vertical lift applied to road meshes to sit above flattened terrain.")]
        [Min(0f)] public float surfaceLiftMeters = 0.05f;

        // ── Footpaths ─────────────────────────────────────────
        [Header("Footpaths")]
        [Min(0.5f)] public float footpathWidthMeters = 2f;
        public Material footpathMaterial;
        [Min(0f)] public float footpathSurfaceLiftMeters = 0.06f;
        [Min(0f)] public float footpathAboveDistrictFillMeters = 0.03f;
        [Min(0f)] public float footpathCurbExtraMeters = 0.15f;
        [Min(0f)] public float footpathJunctionSetbackMeters = 8f;
        [Min(0.25f)] public float footpathUvWorldUnitsPerTile = 2f;

        // ── Junction & Snapping ───────────────────────────────
        [Header("Junction & Snapping")]
        [Min(0.5f)] public float markerCacheCellSizeMeters = 10f;
        [Min(0.1f)] public float markerSnapDistanceMeters = 7f;

        // ── Junction Decal Tuning ─────────────────────────────
        [Header("Junction Decal Tuning")]
        [Min(0f)] public float decalSurfaceLiftMeters = 0.05f;
        [Min(1f)] public float decalSurfaceRaycastHeightMeters = 50f;
        [Min(0.5f)] public float decalProjectionDepthMeters = 3f;
        [Min(0.1f)] public float junctionStopLineOffsetMeters = 1.8f;
        [Min(0.1f)] public float junctionCrosswalkOffsetMeters = 3.8f;

        // ── Junction Markings ─────────────────────────────────
        [Header("Junction Markings")]
        [Tooltip("Stop-line decal material (thin white bar). Uses a URP Decal shader. Leave null to auto-create.")]
        public Material junctionStopLineDecalMaterial;

        [Tooltip("Crosswalk decal material (zebra stripes). Uses a URP Decal shader. Leave null to auto-create with procedural stripes.")]
        public Material junctionCrosswalkDecalMaterial;

        // ── MapMagic Splines ──────────────────────────────────
        [Header("MapMagic Splines")]
        [Range(0.02f, 1f)] public float mapMagicSplineResPerMeter = 0.14f;
        [Min(2)] public int mapMagicSplineMinSamplesPerSegment = 3;
        [Min(2)] public int mapMagicSplineMaxSamplesPerSegment = 28;

        // ── Materials ─────────────────────────────────────────
        [Header("Materials")]
        [Tooltip("EasyRoads road surface material applied to all created road types.")]
        public Material easyRoadsRoadSurfaceMaterial;

        [Tooltip("EasyRoads crossing/junction material applied to all created connections.")]
        public Material easyRoadsCrossingMaterial;

        [Tooltip("Fallback material if per-class materials are not set.")]
        public Material roadMaterial;

        public Material highwayMaterial;
        public Material arterialMaterial;
        public Material localMaterial;
        public Material curveMaterial;

        [Header("Procedural Sidewalks")]
        public bool spawnProceduralSidewalkMeshes = true;
        [Min(0f)] public float kerbWidthMeters = 0.4f;
        [Min(0f)] public float proceduralSidewalkWidthMeters = 2f;
        [Min(0f)] public float proceduralSidewalkLiftMeters = 0.12f;
        [Tooltip("Horizontal chamfer width on the road-facing and outer edges of each sidewalk slab.")]
        [Min(0.05f)] public float proceduralSidewalkBevelWidthMeters = 0.3f;
        [Tooltip("Vertical drop applied to the outer base edge so the slab tapers slightly toward the lot.")]
        [Min(0f)] public float proceduralSidewalkOuterEdgeDropMeters = 0.04f;
        [Tooltip("Vertical clearance above the sampled terrain for asphalt. Prevents coplanar terrain z-fighting.")]
        [Min(0f)] public float roadSurfaceLiftMeters = 0.08f;
        [Min(0.25f)] public float roadUvWorldUnitsPerTile = 4f;
        public Material kerbMaterial;
        public Material sidewalkMaterial;
        [Min(0.5f)] public float highwayMeshSegmentLengthMeters = 1.25f;

        [Tooltip("Merge each layer (Asphalt, Sidewalks, etc.) into one mesh per material after build.")]
        public bool combineProceduralMeshesPerLayer = true;

        // ── Placement Quality ─────────────────────────────────
        [Header("Placement Quality")]
        [Min(1f)] public float tileBorderContinuityMarginMeters = 12f;
        [Min(0f)] public float junctionRoadsideExclusionRadiusMeters = 20f;

        // ── Refinement & Smoothing ────────────────────────────
        [Header("Refinement & Smoothing")]
        [Range(0f, 1f)] public float terrainRefinementStrength = 1f;
        [Range(0, 10)] public int verticalSmoothingIterations = 3;
        [Min(0.1f)] public float minSegmentLength = 15f;

        [Range(90f, 175f)]
        [Tooltip("Remove interior points that turn sharper than this angle.")]
        public float maxPathTurnAngleDegrees = 140f;

        // ── Road Decals ───────────────────────────────────────
        [Header("Road Decals")]
        public GameObject roadDecalPrefab;
        [Min(1f)] public float roadDecalSpacingMeters = 12f;

        // ── Road Layout Source ────────────────────────────────
        [Header("Road Layout Source")]
        [Tooltip("Mathematical world-map graph is shared by editor EasyRoads preview and runtime tile streaming.")]
        public RoadLayoutSource roadLayoutSource = RoadLayoutSource.MathematicalWorldMap;

        [Tooltip("Editor-only override for world-map road seed. 0 = use active session seed or MapMagic graph seed.")]
        public int editorPreviewWorldSeed;

        // ── Road Widths ───────────────────────────────────────
        [Header("Road Widths (meters)")]
        [Min(1f)] public float highwayWidth = 12f;
        [Min(1f)] public float arterialWidth = 9f;
        [Min(0.5f)] public float localWidth = 6f;

        [Tooltip("Road width used when converting MapMagic spline lines to runtime road polylines.")]
        [Min(0.5f)] public float mapMagicSplineWidthMeters = 8f;

        // ── Street Lamps ──────────────────────────────────────
        [Header("Street Lamps")]
        public GameObject streetLampPrefab;
        [Min(1f)] public float streetLampSpacingMeters = 30f;
        [Tooltip("Roadside offset for runtime tile lamp placement (the city hub build uses the district-outline offsets below).")]
        [Min(0f)] public float streetLampOffsetMeters = 1.5f;
        [Tooltip("City hub: lamps stand this far out from the district block outline (toward the road), placing them on the footpath between the block and the road.")]
        [Min(0.1f)] public float streetLampNamedAreaOffsetMeters = 1f;

        // ── Terrain Flattening ────────────────────────────────
        [Header("Terrain Flattening")]
        [Tooltip("When enabled, flattens terrain under the city footprint before road/building placement.")]
        public bool flattenTerrain = true;

        [Tooltip("Extra padding around the flatten footprint in meters.")]
        [Min(0f)] public float terrainPaddingMeters = 0.35f;

        [Tooltip("Blend distance past the outer flatten boundary into natural terrain, in meters.")]
        [Min(0f)] public float terrainBlendMeters = 32f;

        [Tooltip("Fade band from inner arterial flatten to the outer boundary, in meters.")]
        [Min(0f)] public float terrainFlattenOuterMarginMeters = 48f;

        [Tooltip("Extra shoulder width each side of highway corridors when cutting terrain.")]
        [Min(0f)] public float highwayTerrainShoulderMeters = 32f;

        [Tooltip("Blend distance past the highway corridor into natural terrain, in meters.")]
        [Min(0f)] public float highwayTerrainBlendMeters = 96f;

        [Tooltip("Terrain is flattened slightly below the sampled road bed so meshes sit on top.")]
        [Min(0f)] public float highwayTerrainBedClearanceMeters = 0.2f;

        [Header("Highway Terrain Following")]
        [Tooltip("Local smoothing passes for terrain-following highway surface heights (default 1).")]
        [Range(0, 4)] public int fastHighwayTerrainFollowSmoothingIterations = 1;

        [Tooltip("Base local cut/fill under highway road beds on flat grades (meters).")]
        [Min(0.05f)] public float fastHighwayMaxCutFillMeters = 0.75f;

        [Tooltip("Hard ceiling for slope-aware cut/fill under highway road beds (meters).")]
        [Min(0.05f)] public float fastHighwayMaxCutFillCeilingMeters = 4f;

        [Tooltip("Grade (degrees) where adaptive cut/fill begins rising above the flat base.")]
        [Min(0f)] public float fastHighwaySteepGradeStartDegrees = 3f;

        [Tooltip("Grade (degrees) where adaptive cut/fill reaches the ceiling.")]
        [Min(0.1f)] public float fastHighwaySteepGradeFullDegrees = 12f;

        [Tooltip("Extra feather meters applied only on steep grades.")]
        [Min(0f)] public float fastHighwaySteepFeatherExtraMeters = 2f;

        [Tooltip("Extra road-bed shoulder on each side of the carriageway (meters).")]
        [Min(0f)] public float fastHighwayRoadBedShoulderMeters = 2f;

        [Tooltip("Terrain feather beyond the highway road-bed shoulder (meters).")]
        [Min(0f)] public float fastHighwayRoadBedFeatherMeters = 6f;

        [Tooltip("Apply a narrow final heightmap alignment pass after coarse corridor carving so highway asphalt cannot clip into terrain.")]
        public bool applyFinalHighwayRoadBedAlignment = true;

        [Tooltip("Wait for MapMagic main terrain (not draft-only) before flatten and build.")]
        public bool waitForMainTerrainBeforeBuild = true;

        [Header("Ground Sampling")]
        [Tooltip("Fallback height when no terrain or ground reference is available.")]
        public float flatGroundHeight;

        [Tooltip("Raycast origin height above the ground reference.")]
        [Min(0f)] public float groundRaycastHeight = 500f;

        [Tooltip("Layers used for ground-height raycasts.")]
        public LayerMask groundLayers = Physics.DefaultRaycastLayers;

        // ── Terrain Pathfinding ───────────────────────────────
        [Header("Terrain Pathfinding")]
        [Min(4f)] public float pathfindingCellSizeMeters = 12f;

        [Tooltip("Highway-only A* cell size. When >= 4, inter-city planning uses this instead of pathfindingCellSizeMeters.")]
        [Min(0f)] public float highwayPathfindingCellSizeMeters = 24f;

        [Tooltip("Hard A* slope wall for inter-city highway planning. Finished-road grade remains maxHighwayRoadSlopeDegrees.")]
        [Range(8f, 60f)] public float highwayPathfindingMaxSlopeDegrees = 22f;
        [Min(0f)] public float pathfindingMarginMeters = 64f;
        [Min(4f)] public float pathfindingMinSegmentMeters = 24f;
        [Min(0f)] public float slopeCostWeight = 32f;
        [Min(0f)] public float elevationCostWeight = 18f;
        [Min(256)] public int pathfindingMaxExpandedNodes = 12000;
        [Min(0f)] public float mountainProximityCostWeight = 30f;
        [Min(4f)] public float localReliefSampleRadiusMeters = 48f;

        [Min(1)] public int highwayPathfindingAnchorStride = 1;
        [Min(1)] public int arterialPathfindingAnchorStride = 2;
        [Min(1)] public int localPathfindingAnchorStride = 3;

        // ── Terrain Shaping ───────────────────────────────────
        [Header("Terrain Shaping")]
        [Min(0f)] public float terrainShoulderMeters = 8f;
        [Range(0f, 1f)] public float terrainBlendFalloff = 0.65f;

        // ── Tile Application ──────────────────────────────────
        [Header("Tile Application")]
        [Min(0f)] public float tileQueryMarginMeters = 80f;

        // ── Helpers ───────────────────────────────────────────
        public bool UsesMapMagicSplineLayout =>
            useMapMagicSplineOutput || roadLayoutSource == RoadLayoutSource.MapMagicSplinesLegacy;

        public bool UsesMathematicalWorldMapLayout => !UsesMapMagicSplineLayout;

        public int ResolvePathfindingAnchorStride(RoadClass roadClass)
        {
            return roadClass switch
            {
                RoadClass.Highway => Mathf.Max(1, highwayPathfindingAnchorStride),
                RoadClass.Arterial => Mathf.Max(1, arterialPathfindingAnchorStride),
                RoadClass.Local => Mathf.Max(1, localPathfindingAnchorStride),
                _ => Mathf.Max(1, localPathfindingAnchorStride)
            };
        }

        public float ResolveWidthMeters(RoadClass roadClass)
        {
            return roadClass switch
            {
                RoadClass.Highway => highwayWidth,
                RoadClass.Arterial => arterialWidth,
                RoadClass.Local => localWidth,
                _ => localWidth
            };
        }

        public float ResolveHighwayPathfindingMaxSlopeDegrees()
        {
            var planning = highwayPathfindingMaxSlopeDegrees;
            if (planning < 1f)
                planning = 22f;
            return Mathf.Max(maxHighwayRoadSlopeDegrees, planning);
        }

        private void OnValidate()
        {
            if (useMapMagicSplineOutput && roadLayoutSource == RoadLayoutSource.MathematicalWorldMap)
                roadLayoutSource = RoadLayoutSource.MapMagicSplinesLegacy;
        }
    }
}
