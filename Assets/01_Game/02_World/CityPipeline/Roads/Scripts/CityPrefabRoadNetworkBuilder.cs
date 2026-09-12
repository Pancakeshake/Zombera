using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Scene component for the City Prefab Creation Hub: generates math-based city roads with EasyRoads connectors.
    /// </summary>
    [AddComponentMenu("Zombera/World/City Prefab Road Network Builder")]
    [DisallowMultipleComponent]
    public sealed partial class CityPrefabRoadNetworkBuilder : MonoBehaviour
    {
        [SerializeField, HideInInspector] private CityMathRoadLayoutAsset layoutAsset;
        [SerializeField, HideInInspector] private CityMathRoadLayout layout = new();

        [SerializeField, HideInInspector] private RoadNetworkSettings roadNetworkSettings;
        [SerializeField] private ProceduralRoadSystem proceduralRoadSystem;

        [Tooltip("When set, raycasts down to sample Y. Otherwise uses flatGroundHeight.")]
        [SerializeField] private Transform groundReference;

        [SerializeField, HideInInspector] private float flatGroundHeight;
        [SerializeField, HideInInspector] private float groundRaycastHeight = 500f;
        [SerializeField, HideInInspector] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

        [SerializeField, HideInInspector] private CityBuildConfig buildConfig;
        [SerializeField] private DistrictLotTerrainConfig districtLotTerrainConfig;
        [SerializeField] private DistrictLotTerrainLayout districtLotTerrainLayout;

        private bool CreateJunctionConnectors =>
            roadNetworkSettings == null || roadNetworkSettings.createJunctionConnectors;
        private bool TIntersectionsOnly =>
            roadNetworkSettings != null && roadNetworkSettings.tIntersectionsOnly;
        private bool FlattenTerrainOnGenerate =>
            roadNetworkSettings == null || roadNetworkSettings.flattenTerrain;

        // Terrain flatten snapshots the ENTIRE TerrainData of every overlapping terrain
        // into the editor undo stack; merging those records during the pipeline step's
        // undo-group collapse takes seconds. The World Builder window turns this off for
        // pipeline runs (its Reset step owns terrain restoration). Inspector/context-menu
        // generation keeps recording undo.
        [SerializeField, HideInInspector] private bool _recordTerrainUndo = true;

        /// <summary>
        ///     When true, editor destroys skip <see cref="UnityEditor.Undo"/> registration.
        ///     Set around hub Reset / Clear All so thousands of lot/prop deletes stay fast.
        /// </summary>
        internal static bool SuppressEditorDestroyUndo { get; set; }

        public bool RecordTerrainUndo
        {
            get => _recordTerrainUndo;
            set => _recordTerrainUndo = value;
        }

        /// <summary>
        ///     When false, pad/highway heightmap writes are skipped but highway profiles are still registered.
        ///     WorldBuilder hub sets this via run options; inspector/solo generate leaves it true.
        /// </summary>
        public bool WriteRoadTerrainHeightmaps { get; set; } = true;

        /// <summary>Uses the highway-specific A* resolution during hub iteration builds.</summary>
        public bool UseFastRoadBuildQuality { get; set; }

        public long LastRefineMs { get; private set; }
        public long LastPadFlattenMs { get; private set; }
        public long LastHighwayProfileMs { get; private set; }
        public long LastFastHighwayRoadBedMs { get; private set; }
        public long LastFastHighwayRoadBedReadMs { get; private set; }
        public long LastFastHighwayRoadBedApplyMs { get; private set; }
        public long LastFastHighwayRoadBedWriteMs { get; private set; }
        public int LastFastHighwayRoadBedCount { get; private set; }
        public int LastFastHighwayRoadBedTerrainCount { get; private set; }
        public int LastFastHighwayRoadBedSamplesChanged { get; private set; }
        public float LastFastHighwayRoadBedMaxDelta { get; private set; }
        public long LastTunnelPlaceMs { get; set; }
        public long LastBridgePlaceMs { get; set; }
        public bool LastSkippedRoadTerrainWrites { get; private set; }
        private float TerrainFlattenPaddingMeters =>
            roadNetworkSettings != null ? roadNetworkSettings.terrainPaddingMeters : 0.35f;
        private float TerrainFlattenBlendMeters =>
            roadNetworkSettings != null ? roadNetworkSettings.terrainBlendMeters : 32f;

        private float TerrainFlattenOuterMarginMeters
        {
            get
            {
                if (roadNetworkSettings == null)
                    return 48f;

                return Mathf.Max(
                    roadNetworkSettings.terrainFlattenOuterMarginMeters,
                    roadNetworkSettings.terrainBlendMeters);
            }
        }
        private float ResolvedFlatGroundHeight =>
            roadNetworkSettings != null ? roadNetworkSettings.flatGroundHeight : flatGroundHeight;
        private float ResolvedGroundRaycastHeight =>
            roadNetworkSettings != null ? roadNetworkSettings.groundRaycastHeight : groundRaycastHeight;
        private LayerMask ResolvedGroundLayers =>
            roadNetworkSettings != null ? roadNetworkSettings.groundLayers : groundLayers;

        private bool ResolvedUseTIntersectionTestLayout =>
            buildConfig?.useTIntersectionTestLayout ?? false;
        private float ResolvedTTestArmLengthMeters =>
            buildConfig?.tTestArmLengthMeters ?? 45f;
        private bool ResolvedAlignLayoutCenterToTransform =>
            buildConfig?.alignLayoutCenterToTransform ?? true;
        private bool ResolvedAutoEnsureMinimalRoadStack =>
            buildConfig?.autoEnsureMinimalRoadStack ?? true;
        private bool ResolvedAutoGenerateNamedAreas =>
            buildConfig?.autoGenerateNamedAreas ?? false;

        // Cache of the most recent road generation — consumed by downstream steps.
        // RoadNetworkRuntime isn't [Serializable], so we persist the RoadPolyline list.
        [System.NonSerialized] private RoadNetworkRuntime _lastGeneratedRoadNetwork;
        [SerializeField, HideInInspector] private List<RoadPolyline> _cachedRoadPolylines = new();
        [SerializeField, HideInInspector] private List<JunctionRecord> _lastJunctionRegistry = new();

        public IReadOnlyList<RoadPolyline> CachedPolylines => _cachedRoadPolylines;
        public IReadOnlyList<JunctionRecord> LastJunctionRegistry => _lastJunctionRegistry;
        [SerializeField, HideInInspector] private Rect _lastGeneratedBounds;



        public CityMathRoadLayout Layout => layoutAsset?.Data ?? layout;
        public CityMathRoadLayoutAsset LayoutAsset
        {
            get => layoutAsset;
            set => layoutAsset = value;
        }
        public RoadNetworkSettings HubRoadNetworkSettings => roadNetworkSettings;
        public CityBuildConfig BuildConfig
        {
            get => buildConfig;
            set => buildConfig = value;
        }
        public DistrictLotTerrainConfig DistrictLotTerrainConfig
        {
            get => districtLotTerrainConfig;
            set => districtLotTerrainConfig = value;
        }
        public DistrictLotTerrainLayout DistrictLotTerrainLayout
        {
            get => districtLotTerrainLayout;
            set => districtLotTerrainLayout = value;
        }
        public bool AutoEnsureMinimalRoadStack => ResolvedAutoEnsureMinimalRoadStack;
        public bool TIntersectionsOnlyDebug => TIntersectionsOnly;

        /// <summary>
        ///     True when the last road generation completed and published its network.
        ///     Cleared by <see cref="ClearGeneratedRoads"/>; used by the pipeline window
        ///     to detect aborted generations so stale EasyRoads containers get purged.
        /// </summary>
        public bool HasGeneratedRoadNetwork => _lastGeneratedRoadNetwork != null;

        private void Reset()
        {
            layout ??= new CityMathRoadLayout();
            CityAreaRuntimeConfig.ApplyAuthoringLayoutDefaults(layout);
            // Junction connector defaults are now in RoadNetworkSettings SO
            transform.position = new Vector3(CityAreaRuntimeConfig.AuthoringCenterX, transform.position.y, CityAreaRuntimeConfig.AuthoringCenterZ);
            groundReference = transform;
        }

        private void OnValidate()
        {
            layout ??= new CityMathRoadLayout();
            layout.Normalize();
            if (ResolvedAlignLayoutCenterToTransform)
                layout.centerXZ = new Vector2(transform.position.x, transform.position.z);
        }



        [ContextMenu("Generate T Intersection Test")]
        public void GenerateTIntersectionTest()
        {
            // T test forces junction connectors — handled by RoadNetworkSettings
            GenerateCityRoadNetwork(useTestLayout: true);
        }

        [ContextMenu("Generate City Road Network")]
        public void GenerateCityRoadNetwork()
        {
            GenerateCityRoadNetwork(useTestLayout: false);
        }

        public void GenerateCityRoadNetwork(bool useTestLayout)
        {
            // Drive the resumable build to completion — same work as the pipeline
            // runner's '1. Roads' step, but without per-frame editor repaints.
            var routine = GenerateCityRoadNetworkRoutine(useTestLayout);
            while (routine.MoveNext())
            {
                // Pump to completion — no editor repaint pacing needed for the
                // context-menu entry point.
            }
        }

        /// <summary>
        ///     Resumable road build — yields between phases and while road pieces /
        ///     junction cells are being created, so the Scene view repaints live.
        ///     Also drives <see cref="GenerateCityRoadNetwork(bool)"/>.
        /// </summary>
        public System.Collections.IEnumerator GenerateCityRoadNetworkRoutine(bool useTestLayout = false)
        {
            // Reuse the roads already in the scene when the build settings are
            // unchanged (region seed matches the previous build). Full rebuild when
            // the seed changed or reuse is disabled. Single-city keeps the legacy
            // "roads already generated" behavior.
            if (CanReuseCachedRoadsForCurrentBuild() &&
                _cachedRoadPolylines.Count > 0 &&
                HasReusableRoadMeshes())
            {
                EnsureRoadCache();
                EnsureHighwayHeightProfilesForCachedRoads();
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Roads reused from cache (" +
                    (RegionModeActive ? "region seed " + lastBuiltRegionSeed + " unchanged" : "single city") + ").",
                    this);
                yield break;
            }

            ClearGeneratedRoads();

            // A build that reaches here is not reusing cached roads, so roll the region
            // seed once now. ResolveRegionSeed() pins it for the whole build, keeping the
            // region layout, pad flattening and terrain gizmos on the same seed.
            RollRegionSeedForNewBuild();

            LastTunnelPlaceMs = 0;
            LastBridgePlaceMs = 0;
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var phaseSw = System.Diagnostics.Stopwatch.StartNew();
            var clearMs = phaseSw.ElapsedMilliseconds;
            yield return null;

            if (!TryPrepareRoadGeneration(out var settings))
            {
                ProceduralCityRoadBuilder.DestroyEmptyNetworkRoot(transform);
                yield break;
            }

            phaseSw.Restart();
            var workingLayout = PrepareWorkingLayout(!useTestLayout);
            if (!TryBuildRoadNetworkContext(workingLayout, settings, useTestLayout, out var buildContext))
            {
                ProceduralCityRoadBuilder.DestroyEmptyNetworkRoot(transform);
                yield break;
            }
            var contextMs = phaseSw.ElapsedMilliseconds;
            CityBuildUndoChunker.CollapseAfter("build context");
            yield return null;

            phaseSw.Restart();
            RefineHighwaysAgainstTerrain(buildContext.Network, settings);
            LastRefineMs = phaseSw.ElapsedMilliseconds;
            yield return null;

            phaseSw.Restart();
            var terrainFlattenSummary = TryFlattenHubTerrain(buildContext);
            var flattenWindowMs = phaseSw.ElapsedMilliseconds;
            CityBuildUndoChunker.CollapseAfter("terrain flatten");
            yield return null;

            // Lift road meshes slightly above flattened terrain so they sit on top
            // instead of z-fighting at the exact same Y.
            const float roadSurfaceLiftMeters = 0.05f;
            phaseSw.Restart();
            BuildRoadMeshes(buildContext, settings, roadSurfaceLiftMeters, phaseSw);
            yield return null;
            var roadCount = _roadMeshBuildMetrics.RoadCount;
            var buildComputeMs = _roadMeshBuildMetrics.BuildComputeMs;
            var terrainSamples = _roadMeshBuildMetrics.TerrainSamples;
            var terrainFallbacks = _roadMeshBuildMetrics.TerrainFallbacks;

            var buildWallMs = phaseSw.ElapsedMilliseconds;

            phaseSw.Restart();
            PublishGeneratedRoadNetwork(buildContext, terrainFlattenSummary, roadCount);
            var publishMs = phaseSw.ElapsedMilliseconds;

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Roads step phases: clear=" + clearMs +
                "ms context=" + contextMs +
                "ms refine=" + LastRefineMs +
                "ms padFlatten=" + LastPadFlattenMs +
                "ms highwayProfile=" + LastHighwayProfileMs +
                "ms fastHighwayBed=" + LastFastHighwayRoadBedMs +
                "ms (highways=" + LastFastHighwayRoadBedCount +
                ", terrains=" + LastFastHighwayRoadBedTerrainCount +
                ", samples=" + LastFastHighwayRoadBedSamplesChanged +
                ", maxDelta=" + LastFastHighwayRoadBedMaxDelta.ToString("F3") + "m" +
                ", read=" + LastFastHighwayRoadBedReadMs +
                "ms apply=" + LastFastHighwayRoadBedApplyMs +
                "ms write=" + LastFastHighwayRoadBedWriteMs + "ms)" +
                " flattenWindow=" + flattenWindowMs +
                " writeHeights=" + WriteRoadTerrainHeightmaps +
                " skippedWrites=" + LastSkippedRoadTerrainWrites +
                " buildWall=" + buildWallMs + "ms (compute=" + buildComputeMs +
                "ms asphalt=" + _roadMeshBuildMetrics.AsphaltMs +
                " sidewalk=" + _roadMeshBuildMetrics.SidewalkMs +
                " footpath=" + _roadMeshBuildMetrics.FootpathMs +
                " finalize=" + _roadMeshBuildMetrics.FinalizeMs +
                " sampleMs=" + _roadMeshBuildMetrics.TerrainSampleMs +
                " verts=" + _roadMeshBuildMetrics.FinalVertexCount +
                " yieldGaps=" + (buildWallMs - buildComputeMs) + "ms)" +
                " terrainSamples=" + terrainSamples +
                " terrainFallbacks=" + terrainFallbacks +
                " tunnelPlace=" + LastTunnelPlaceMs +
                "ms bridgePlace=" + LastBridgePlaceMs +
                "ms publish=" + publishMs + "ms, wall=" + totalSw.ElapsedMilliseconds + "ms",
                this);
        }

        internal Transform ResolveRoadContentRoot() =>
            ProceduralCityRoadBuilder.FindNetworkRoot(transform)
            ?? ProceduralCityRoadBuilder.EnsureNetworkRoot(transform);

        private bool HasReusableRoadMeshes()
        {
            var root = ProceduralCityRoadBuilder.FindNetworkRoot(transform);
            if (root == null)
                return false;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    return true;
            }

            return false;
        }

        private struct CityRoadNetworkBuildContext
        {
            public RoadNetworkRuntime Network;
            public Rect Bounds;
            public Rect InnerFlattenRect;
            public Rect OuterFlattenRect;
            public bool HasInnerFlattenRect;
        }
    }
}
