#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Zombera.AI;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace Zombera.Characters
{
    /// <summary>
    ///     Spawns the player Unit prefab at the designated world spawn point.
    ///     Runs in Awake so the Unit registers with UnitManager before BeginWorldSession applies stats.
    /// </summary>
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
        private static readonly int[] DefaultStartupSquadSkillTiers =
        {
            1, 10, 15, 20, 25,
            30, 35, 40, 45, 50,
            55, 60, 65, 70, 75,
            80, 85, 90, 95, 100
        };

        private static readonly WaitForSeconds StartupNavMeshRetryWait = new(0.5f);
        private static readonly int MainTexShaderPropertyId = Shader.PropertyToID("_MainTex");
        private static readonly CharacterSpawnDependencyStage[] OrderedSpawnDependencyStages =
        {
            CharacterSpawnDependencyStage.Terrain,
            CharacterSpawnDependencyStage.Roads,
            CharacterSpawnDependencyStage.Buildings,
            CharacterSpawnDependencyStage.NavMesh,
            CharacterSpawnDependencyStage.Objects
        };

        // ReSharper disable once InconsistentNaming
        private static bool s_loggedNestedSpawnerRemovalWarning;

        [Header("Prefab")]
        [Tooltip("Player prefab containing Unit, UnitController, UnitHealth, UnitCombat, UnitStats.")]
        [SerializeField]
        private GameObject playerPrefab;

        [Header("Spawn Point")]
        [Tooltip("Where the player is placed on world load. Defaults to this transform's position.")]
        [SerializeField]
        private Transform spawnPoint;

        [Header("Terrain Spawn")]
        [Tooltip("Optional terrain to sample for spawn height. Uses active terrain if unassigned.")]
        [SerializeField]
        private Terrain spawnTerrain;

        [Tooltip("When enabled, the resolved world terrain drives the main spawn position in World.")] [SerializeField]
        private bool useTerrainAsMainSpawn = true;

        [Tooltip(
            "Normalized X/Z location on the resolved terrain used for main spawn when terrain-driven spawn is enabled.")]
        [SerializeField]
        private Vector2 terrainMainSpawnNormalized = new(0.5f, 0.5f);

        [Tooltip("Extra height added after sampling terrain to avoid clipping into the ground.")] [SerializeField]
        private float terrainHeightOffset = 1f;

        [Tooltip("Logs all overlapping terrain samples at the spawn position to diagnose layered terrain setups.")]
        [SerializeField]
        private bool logSpawnTerrainDiagnostics;

        [Header("Diagnostics")]
        [Tooltip("Logs focused player/squad spawn diagnostics (scene ownership, navmesh triangles, and roster state).")]
        [SerializeField]
        private bool logFocusedSpawnDiagnostics;

        [Header("Camera")]
        [Tooltip("Optional world camera to link to PlayerInputController. Uses Camera.main if empty.")]
        [SerializeField]
        private Camera worldCamera;

        [Header("Startup Zombie Validation")]
        [Tooltip("Spawn one zombie near the player after spawn so Boot -> World startup can be validated quickly.")]
        [SerializeField]
        private bool spawnValidationZombieNearPlayer;

        [SerializeField] [Min(0f)] private float validationZombieSpawnDelaySeconds = 0.35f;
        [SerializeField] [Min(1f)] private float validationZombieSpawnDistanceFromPlayer = 7f;
        [SerializeField] [Min(0f)] private float validationZombieSpawnLateralJitter = 1.5f;
        [SerializeField] private ZombieType validationZombieType;
        [SerializeField] private bool logValidationZombieSpawn;

        [Header("Startup Squad (Test)")]
        [Tooltip("Spawns controllable squad NPCs at world start alongside the player.")]
        [SerializeField]
        private bool spawnStartupSquadOnWorldStart = true;

        [Tooltip("Total startup roster size, including the player.")] [SerializeField] [Min(1)]
        private int startupSquadTotalCount = 8;

        [Tooltip("Initial startup roster floor used when startup/minimum counts are configured lower.")]
        [SerializeField]
        [Min(1)]
        private int startupInitialCharacterCount = 8;

        [Tooltip("Optional dedicated prefab for startup squad members.")] [SerializeField]
        private GameObject startupSquadMemberPrefab;

        [Tooltip("Optional parent for runtime-started squad members.")] [SerializeField]
        private Transform startupSquadParent;

        [SerializeField] [Min(0.25f)] private float startupSquadRingRadius = 4.5f;
        [SerializeField] private bool applyStartupSquadSkillTiers = true;

        [Tooltip("Roster order levels (all stats): index 0 = player, then each spawned squad member.")] [SerializeField]
        private int[] startupSquadSkillTiers =
        {
            1, 10, 15, 20, 25,
            30, 35, 40, 45, 50,
            55, 60, 65, 70, 75,
            80, 85, 90, 95, 100
        };

        [Tooltip("Minimum startup roster size enforced at runtime (including player).")] [SerializeField] [Min(1)]
        private int minimumStartupSquadTotalCount = 8;

        [Tooltip("When enabled, startup squad members get randomized runtime visual variants.")] [SerializeField]
        [FormerlySerializedAs("randomizeStartupSquadUmaVisuals")]
        private bool randomizeStartupSquadVisualVariants = true;

        [SerializeField] private bool logStartupSquadSpawning;

        [Tooltip("When enabled, startup squad members are spawned over multiple frames to avoid CPU spikes.")]
        [SerializeField]
        private bool deferStartupSquadSpawning = true;

        [Tooltip("How many squad members to spawn per frame when deferred spawn is enabled.")]
        [SerializeField]
        [Range(1, 10)]
        private int startupSquadSpawnPerFrame = 1;

        [Header("Runtime NavMesh")]
        [Tooltip("Half-size in meters of the runtime navmesh bake area centered on spawn.")]
        [SerializeField]
        [Min(50f)]
        private float navMeshBakeRadius = 420f;

        [Tooltip("Vertical half-extent in meters for runtime navmesh baking.")] [SerializeField] [Min(20f)]
        private float navMeshVerticalExtent = 180f;

        [Tooltip(
            "When snapping spawn to NavMesh, allow the NavMesh point to be below sampled terrain by this many meters. Useful when runtime-baked NavMesh sits slightly under the terrain surface due to voxelization/agent settings.")]
        [SerializeField]
        [Min(0f)]
        private float spawnNavMeshBelowTerrainToleranceMeters = 8f;

        [Tooltip(
            "When enabled, selects a player spawn point on the largest connected NavMesh region found near the spawn origin. Helps avoid spawning on small isolated NavMesh islands.")]
        [SerializeField]
        private bool preferLargestConnectedNavMeshRegionForSpawn = true;

        [Tooltip("How many random NavMesh points to consider when searching for a better connected spawn region.")]
        [SerializeField]
        [Range(8, 128)]
        private int spawnNavMeshCandidateSamples = 20;

        [Tooltip("For each candidate spawn point, how many path probes to run to estimate region connectivity.")]
        [SerializeField]
        [Range(4, 64)]
        private int spawnNavMeshConnectivityProbes = 8;

        [Tooltip(
            "Retry runtime navmesh baking if the initial bake has no triangles (common with streaming/generated terrain).")]
        [SerializeField]
        [Range(0, 10)]
        private int navMeshRetryAttempts = 5;

        [Tooltip("Seconds to wait between navmesh retry attempts.")] [SerializeField] [Min(0.1f)]
        private float navMeshRetryDelaySeconds = 0.6f;

        [Tooltip("Rebuild runtime NavMesh when MapMagic reports tile generation complete.")] [SerializeField]
        private bool rebakeNavMeshOnMapMagicComplete = true;

        [Tooltip("Minimum seconds between MapMagic-triggered NavMesh rebakes.")] [SerializeField] [Min(0f)]
        private float mapMagicNavMeshRebakeCooldownSeconds = 8f;

        [Tooltip("When enabled, streaming tile navmesh uses coarser settings than base tuning to cut recast spikes.")]
        [SerializeField]
        private bool useCoarseStreamingNavMeshTuning = true;

        [SerializeField] [Min(1f)] private float streamingNavMeshVoxelScale = 3f;
        [SerializeField] [Range(64, 1024)] private int streamingNavMeshMinTileSize = 768;
        [SerializeField] [Min(0f)] private float streamingNavMeshMinRegionAreaFloor = 2.5f;

        [Header("Runtime MapMagic")]
        [Tooltip("Stabilizes MapMagic to a single tracker area at startup (prevents split generation zones).")]
        [SerializeField]
        private bool stabilizeMapMagicGenerationInPlayMode = true;

        [Tooltip("Stops new MapMagic tile expansion after initial stabilization in play mode.")] [SerializeField]
        private bool freezeMapMagicExpansionInPlayMode = true;

        [Tooltip("Force-disables MapMagic infinite generation at runtime to avoid continuous texture-apply spikes while playing.")]
        [SerializeField]
        private bool disableMapMagicInfiniteGenerationAtRuntime = true;

        [Tooltip("When infinite generation is disabled, also disable camera-tracker generation to prevent repeated terrain texture apply churn.")]
        [SerializeField]
        private bool disableMapMagicCameraTrackerGenerationAtRuntime = true;

        [Tooltip("Skips MapMagic SwitchLods call during runtime stabilization to reduce heavy terrain texture apply passes.")]
        [SerializeField]
        private bool skipMapMagicSwitchLodsInPlayMode = true;

        [Tooltip(
            "SafeDebug matches the bools above. Production forces infinite streaming with margins (freeze flag ignored).")]
        [SerializeField]
        private MapMagicPlayModeStreamingProfile mapMagicStreamingProfile =
            MapMagicPlayModeStreamingProfile.ProductionStreaming;

        [SerializeField] [Min(1)] private int productionStreamingMainRange = 1;
        [SerializeField] [Min(1)] private int productionStreamingGenerateRange = 1;
        [SerializeField] [Min(0)] private int productionStreamingRetainMargin = 0;

        [Header("Runtime NavMesh Streaming")]
        [Tooltip(
            "When assigned and Drive Runtime NavMesh is enabled on the component, NavMesh is built per MapMagic tile instead of one large runtime bake.")]
        [SerializeField]
        private StreamingNavMeshTileService streamingNavMeshTileService;

        [Header("Procedural Spawn Quality")]
        [Tooltip(
            "When enabled, delays spawning until at least one MapMagic terrain tile is active in the scene (prevents spawning at raw fallback points).")]
        [SerializeField]
        private bool deferSpawnUntilMapMagicTerrainReady = true;

        [SerializeField] [Min(0f)] private float maxSecondsToWaitForMapMagicTerrain = 6f;

        [Tooltip(
            "When enabled, final world spawn waits for ordered dependencies: MapMagic terrain -> roads -> buildings -> objects.")]
        [SerializeField]
        private bool deferFinalSpawnUntilWorldDependenciesReady = true;

        [SerializeField] [Min(0f)] private float fallbackOrderedSpawnTimeoutSeconds = 90f;
        [SerializeField] [Min(0.05f)] private float orderedSpawnDependencyPollSeconds = 0.25f;

        [Header("Ordered Spawn Stage Targets (seconds)")]
        [Tooltip("Target wait for terrain readiness before skipping this stage and continuing startup.")]
        [SerializeField]
        [Min(0f)]
        private float orderedSpawnTerrainStageTargetSeconds = 30f;

        [Tooltip("Target wait for road readiness before skipping this stage and continuing startup.")]
        [SerializeField]
        [Min(0f)]
        private float orderedSpawnRoadStageTargetSeconds = 20f;

        [Tooltip("Target wait for building readiness before skipping this stage and continuing startup.")]
        [SerializeField]
        [Min(0f)]
        private float orderedSpawnBuildingStageTargetSeconds = 30f;

        [Tooltip("Target wait for object readiness before skipping this stage and continuing startup.")]
        [SerializeField]
        [Min(0f)]
        private float orderedSpawnNavMeshStageTargetSeconds = 20f;

        private float orderedSpawnObjectStageTargetSeconds = 15f;

        [Tooltip(
            "When MapMagic is present, defer the first runtime NavMesh build until terrain exists or MapMagic reports completion. " +
            "A lightweight player instance is created first so WorldManager.InitializeWorld can resolve the player transform.")]
        [SerializeField]
        private bool deferInitialNavMeshUntilMapMagicReady = true;

        [SerializeField] [Min(0f)] private float maxSecondsToWaitForInitialMapMagicNavMesh = 12f;

        [Tooltip(
            "Extra margin (0-0.45) excluded from the deployed tile rect when searching for a flatter spawn point.")]
        [SerializeField]
        [Range(0f, 0.45f)]
        private float mapMagicSpawnRectEdgeMarginNormalized = 0.12f;

        [Tooltip(
            "Uniform random spawn attempts inside the inset MapMagic rect before the legacy min-slope search. " +
            "Picks one random valid point among those under Max Spawn Slope (avoids always spawning on tile edges). Set 0 to use only the min-slope pass.")]
        [SerializeField]
        [Range(0, 256)]
        private int mapMagicRandomUniformSpawnAttempts = 64;

        [Tooltip("How many random candidate points to test when selecting a flatter MapMagic spawn position.")]
        [SerializeField]
        [Range(8, 256)]
        private int mapMagicFlatterSpawnSamples = 24;

        [Tooltip("Maximum walkable slope (degrees) to consider for the initial spawn point search.")]
        [SerializeField]
        [Range(0f, 75f)]
        private float mapMagicMaxSpawnSlopeDegrees = 18f;

        [Header("Dev Spawn Inventory")]
        [Tooltip("Item definitions applied to player inventory when debug/dev mode is enabled.")]
        [SerializeField]
        private ItemDefinition[] devModeSpawnInventoryItems = Array.Empty<ItemDefinition>();

        [Tooltip(
            "When enabled in editor, keeps the dev spawn list synced to all ItemDefinition assets in the project.")]
        [SerializeField]
        private bool autoPopulateDevModeSpawnItemsInEditor = true;

        [Tooltip("Minimum quantity per item to ensure in player inventory during dev-mode spawn.")]
        [SerializeField]
        [Min(1)]
        private int devModeSpawnQuantityPerItem = 1;

        [Tooltip(
            "Minimum quantity per ammo item to ensure in player inventory during dev-mode spawn (testing helper).")]
        [SerializeField]
        [Min(1)]
        private int devModeSpawnAmmoQuantityPerItem = 999;

        [Tooltip("Minimum inventory weight limit used while applying the dev-mode spawn loadout.")]
        [SerializeField]
        [Min(1f)]
        private float devModeSpawnMinimumWeightLimit = 500f;

        [SerializeField] private bool logDevModeSpawnInventory = true;

        [Header("UI Binding Budget")]
        [Tooltip("Seconds between squad portrait strip binding refresh checks. Reduces per-frame UI churn.")]
        [SerializeField]
        [Range(0.05f, 2f)]
        private float portraitStripBindRefreshIntervalSeconds = 0.35f;

        private Unit _activeControlledUnit;
        private PlayerInputController _activeInputController;
        private bool _deferInitialNavMeshThisBootstrap;
        private bool _hasLastNavMeshCenter;
        private Vector3 _lastNavMeshCenter;
        private bool _loggedSpawnDeferral;
        private bool _mapMagicAllCompleteSinceBootstrap;
        private bool _startupSquadSpawnQueuedForNavMesh;
        private PlayerSpawnSnapper _playerSpawnSnapper;
        private bool _requestedValidationZombieSpawn;
        private DevModeInventoryHelper _devModeInventoryHelper;
        private RuntimeNavMeshBootstrapper _runtimeNavMeshBootstrapper;
        private Coroutine _finalWorldSpawnRoutine;
        private bool _spawnBootstrapStarted;
        private SquadControlUiCoordinator _squadControlUiCoordinator;
        private StartupSquadSpawner _startupSquadSpawner;
        private SpawnAppearanceStylingService _spawnAppearanceStylingService;
        private WorldManager _worldManagerForSpawnOrder;
        private float _portraitStripBindRefreshTicker;
        private bool _loadingWarmupPlayerPrefabDone;
        private bool _loadingWarmupSquadPrefabDone;

        public Unit SpawnedPlayer { get; private set; }

        public bool HasFinalizedWorldPlayerSpawn { get; private set; }
        public bool isLoadingSaveSession { get; set; }


        // Implementation is split across concern partials:
        // PlayerSpawner.Bootstrap.cs
        // PlayerSpawner.Deferred.cs
        // PlayerSpawner.SpawnPipeline.cs
        // PlayerSpawner.Utility.cs
        // PlayerSpawner.Editor.cs
    }
}
