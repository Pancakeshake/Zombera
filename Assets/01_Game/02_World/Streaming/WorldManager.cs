#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder.Views;
using Zombera.World.Roads;
using Zombera.World.Simulation;

#endregion

namespace Zombera.World
{
    public enum CharacterSpawnDependencyStage
    {
        Terrain,
        Roads,
        Buildings,
        NavMesh,
        Objects
    }

    /// <summary>
    ///     Coordinates chunk streaming and world simulation ticks.
    /// </summary>
    public partial class WorldManager : MonoBehaviour
    {
        [Header("Streaming")] [SerializeField] private Transform playerTransform;

        [SerializeField] private ChunkLoader chunkLoader;
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private ChunkCache chunkCache;
        [SerializeField] private RegionSystem regionSystem;
        [SerializeField] private MapStateService mapStateService;

        [Header("Spawners")] [SerializeField] private MapSpawner mapSpawner;

        [SerializeField] private LootSpawner lootSpawner;

        [Header("Dynamic Events")] [SerializeField]
        private WorldEventSystem worldEventSystem;

        [SerializeField] private WorldSimulationManager worldSimulationManager;

        [Header("Simulation")] [SerializeField]
        private float chunkStreamingTickInterval = 0.25f;

        [SerializeField] private float worldSimulationInterval = 10f;
        [SerializeField] private bool initializeOnStart = true;

        [Header("Startup Validation")]
        [Tooltip(
            "When enabled, WorldManager periodically requests a startup validation zombie spawn. " +
            "PlayerSpawner already performs this by default, so this can usually stay off.")]
        [SerializeField]
        private bool runValidationZombieSpawnFromWorldManager;

        [SerializeField] [Min(0.25f)]
        private float validationZombieAttemptIntervalSeconds = 2f;

        [Header("Procedural Streaming")]
        [Tooltip(
            "When enabled, skips prototype static map spawn, binds MapMagic tile streaming to chunk loading, and seeds the world from World Seed instead of a random session tick.")]
        [SerializeField]
        private bool useProceduralStreamingWorld = true;

        [SerializeField] [Min(1)] private int worldSeed = 12345;

        [Tooltip(
            "When true (and procedural streaming is on), World Seed is replaced with a time-derived value each play session.")]
        [SerializeField]
        private bool randomizeWorldSeedEachSession;

        private WorldMapSizeTier _pendingMapSizeTier = WorldMapSizeTier.Medium;
        private bool _hasPendingSessionRequest;
        private ulong _pendingSavedPlanFingerprint;
        private bool _hasPendingSavedPlanFingerprint;
        private string _preparedWorldStateHash = string.Empty;
        private bool _preparedLegacyNoWorldState;

        [SerializeField] private WorldTileStreamSource tileStreamBridge;

        [Header("World Builder (optional)")]
        [Tooltip("Optional MonoBehaviour implementing IWorldGenerationBackend. When null, existing MapMagic path is used.")]
        [SerializeField]
        private MonoBehaviour _worldGenerationBackendSource;

        [Header("Procedural City")]
        [SerializeField]
        private bool enableStreamedCityBuilder = false;

        [SerializeField] private WorldStreamedCityBuilder streamedCityBuilder;
        [SerializeField] private StreamedCityCatalog streamedCityCatalog;

        [SerializeField]
        [Tooltip("When enabled, builds math-layout city districts and buildings on MapMagic City_Area tiles at runtime.")]
        private bool enableRuntimeCityAreaBuilder;

        [SerializeField] private WorldRuntimeCityAreaBuilder runtimeCityAreaBuilder;
        [SerializeField] private CityAreaRuntimeConfig cityAreaRuntimeConfig;
        [SerializeField] private WorldRoadNetworkSystem roadNetworkSystem;

        [SerializeField]
        [Tooltip("When enabled, EasyRoads runtime roads are synced into the gameplay-road graph before building spawn dependencies resolve.")]
        private bool enableEasyRoadsRoadBridge = false;

        [SerializeField]
        [Tooltip("When enabled, city/world polylines populate the gameplay road graph without EasyRoads.")]
        private bool enablePolylineRoadGraph = true;

        [SerializeField] private EasyRoadsRoadGameplayBridge easyRoadsRoadBridge;
        [SerializeField] private WorldGenerationManager worldGenerationManager;
        [SerializeField] private ProceduralRoadSystem proceduralRoadSystem;
        [SerializeField] private RoadGameplayService roadGameplayService;
        [SerializeField] private StreamingNavMeshTileService navMeshTileService;
        [SerializeField] private WorldBuildingMaterializer worldBuildingMaterializer;

        [Header("Road Runtime Stack")]
        [SerializeField]
        [Tooltip("When enabled, a single RoadGameplayService object owns the road runtime stack (tile bridge + gameplay bridge).")]
        private bool useSingleRoadGameplayStackObject = true;

        [SerializeField]
        [Tooltip("Name used when auto-creating the unified road runtime stack object.")]
        private string roadGameplayStackObjectName = "RoadGameplayService";

        [SerializeField]
        [Tooltip("Disable player-driven building input while procedural streamed city spawning is active.")]
        private bool forceWorldSpawnedBuildingsOnly = true;

        [SerializeField] [Range(0.25f, 5f)]
        private float buildInputSuppressionPollSeconds = 1f;

        [SerializeField] [Range(1f, 15f)]
        private float buildInputSuppressionIdlePollSeconds = 5f;

        [SerializeField]
        [Tooltip("Also disables active MindCodeInteractive EasyBuild runtime MonoBehaviours scene-wide while spawn-only mode is active.")]
        private bool suppressEasyBuildRuntimeBehavioursInSpawnOnlyMode = true;

        [SerializeField] [Range(8, 8192)]
        private int maxEasyBuildRuntimeDisablePerScan = 512;

        [SerializeField]
        [Tooltip("Logs how many EasyBuild runtime behaviours were disabled per suppression pass.")]
        private bool logEasyBuildRuntimeSuppression;

        [SerializeField] [Range(0.25f, 60f)]
        [Tooltip("How often to rescan the scene for new EasyBuild runtime behaviours when spawn-only suppression is active.")]
        private float easyBuildRuntimeRescanSeconds = 10f;

        [Header("Spawn Dependency Order")]
        [Tooltip("When enabled, character spawning waits for terrain -> roads -> buildings -> objects readiness.")]
        [SerializeField]
        private bool enforceOrderedCharacterSpawn = true;

        [SerializeField] [Min(0f)] private float maxSecondsToWaitForSpawnDependencyOrder = 20f;

        [SerializeField]
        [Tooltip("Disable EasyBuild automatic save load/save while world-spawned building mode is active.")]
        private bool disableEasyBuildAutoPersistenceInSpawnOnlyMode = true;

        private const string DefaultStreamedCityCatalogResourcesPath = "World/StreamedCityCatalog";
        private bool _loggedMissingCityCatalogWarning;
        private bool _loggedMissingTileStreamBridge;
        private readonly List<Unit> _playerUnitBuffer = new();
        private float _nextBuildInputSuppressionAt;
        private float _nextReferenceResolveAt;
        private float _nextEasyBuildRuntimeRescanAt;
        private bool _forceEasyBuildRuntimeRescan = true;
        private bool _easyBuildAutoPersistenceSuppressed;
        private bool _foundSuppressibleBuilderInputLastScan;
        private readonly List<MonoBehaviour> _easyBuildRuntimeBehaviourBuffer = new(256);
        private PlayerSpawner _cachedPlayerSpawner;

        private const string EasyBuildManagerTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Managers.BuildingManager";

        private const string EasyBuildRuntimeNamespacePrefix = "MindCodeInteractive.EasyBuildSystem.";

        private float _chunkStreamingTickTimer;
        private float _worldSimulationTimer;
        private float _nextValidationZombieAttemptAt;
        private bool _validationZombieAttemptFinished;
        [SerializeField] private ZombieManager zombieManager;

        public bool IsSimulationActive { get; private set; }
        public bool UseProceduralStreamingWorld => useProceduralStreamingWorld;
        public WorldTileStreamSource TileStreamBridge => tileStreamBridge;

        public IWorldGenerationBackend WorldGenerationBackend =>
            _worldGenerationBackendSource as IWorldGenerationBackend;

        public bool EnforceOrderedCharacterSpawn => enforceOrderedCharacterSpawn;
        public float MaxSecondsToWaitForSpawnDependencyOrder => Mathf.Max(0f, maxSecondsToWaitForSpawnDependencyOrder);
    }
}
