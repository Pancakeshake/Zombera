using System;
using System.Collections.Generic;
using Den.Tools;
using MapMagic.Core;
using MapMagic.Terrains;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.AI;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;

namespace Zombera.Characters
{
    public enum MapMagicPlayModeStreamingProfile
    {
        /// <summary>Single-tracker stabilization with optional frozen tile ring (reliable editor/debug).</summary>
        SafeDebugStreaming,
        /// <summary>Infinite MapMagic expansion with retention margins for traversal (production procedural).</summary>
        ProductionStreaming
    }

    /// <summary>
    /// Spawns the player Unit prefab at the designated world spawn point.
    /// Runs in Awake so the Unit registers with UnitManager before BeginWorldSession applies stats.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        private static readonly int[] DefaultStartupSquadSkillTiers =
        {
            1, 10, 15, 20, 25,
            30, 35, 40, 45, 50,
            55, 60, 65, 70, 75,
            80, 85, 90, 95, 100
        };
        private static readonly UnitSkillType[] AllUnitSkillTypes = (UnitSkillType[])System.Enum.GetValues(typeof(UnitSkillType));
        private UmaSpawnStylingService _umaSpawnStylingService;

        [Header("Prefab")]
        [Tooltip("Player prefab containing Unit, UnitController, UnitHealth, UnitCombat, UnitStats.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Spawn Point")]
        [Tooltip("Where the player is placed on world load. Defaults to this transform's position.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Terrain Spawn")]
        [Tooltip("Optional terrain to sample for spawn height. Uses active terrain if unassigned.")]
        [SerializeField] private Terrain spawnTerrain;
        [Tooltip("When enabled, the resolved world terrain drives the main spawn position in World.")]
        [SerializeField] private bool useTerrainAsMainSpawn = true;
        [Tooltip("Normalized X/Z location on the resolved terrain used for main spawn when terrain-driven spawn is enabled.")]
        [SerializeField] private Vector2 terrainMainSpawnNormalized = new Vector2(0.5f, 0.5f);
        [Tooltip("Extra height added after sampling terrain to avoid clipping into the ground.")]
        [SerializeField] private float terrainHeightOffset = 1f;
        [Tooltip("Logs all overlapping terrain samples at the spawn position to diagnose layered terrain setups.")]
        [SerializeField] private bool logSpawnTerrainDiagnostics = true;

        [Header("Camera")]
        [Tooltip("Optional world camera to link to PlayerInputController. Uses Camera.main if empty.")]
        [SerializeField] private Camera worldCamera;

        [Header("Startup Zombie Validation")]
        [Tooltip("Spawn one zombie near the player after spawn so Boot -> World startup can be validated quickly.")]
        [SerializeField] private bool spawnValidationZombieNearPlayer = true;
        [SerializeField, Min(0f)] private float validationZombieSpawnDelaySeconds = 0.35f;
        [SerializeField, Min(1f)] private float validationZombieSpawnDistanceFromPlayer = 7f;
        [SerializeField, Min(0f)] private float validationZombieSpawnLateralJitter = 1.5f;
        [SerializeField] private ZombieType validationZombieType;
        [SerializeField] private bool logValidationZombieSpawn = true;

        [Header("Startup Squad (Test)")]
        [Tooltip("Spawns controllable squad NPCs at world start alongside the player.")]
        [SerializeField] private bool spawnStartupSquadOnWorldStart = true;
        [Tooltip("Total startup roster size, including the player.")]
        [SerializeField, Min(1)] private int startupSquadTotalCount = 20;
        [Tooltip("Initial startup roster floor used when startup/minimum counts are configured lower.")]
        [SerializeField, Min(1)] private int startupInitialCharacterCount = 20;
        [Tooltip("Optional dedicated prefab for startup squad members.")]
        [SerializeField] private GameObject startupSquadMemberPrefab;
        [Tooltip("Optional parent for runtime-started squad members.")]
        [SerializeField] private Transform startupSquadParent;
        [SerializeField, Min(0.25f)] private float startupSquadRingRadius = 4.5f;
        [SerializeField] private bool applyStartupSquadSkillTiers = true;
        [Tooltip("Roster order levels (all stats): index 0 = player, then each spawned squad member.")]
        [SerializeField] private int[] startupSquadSkillTiers =
        {
            1, 10, 15, 20, 25,
            30, 35, 40, 45, 50,
            55, 60, 65, 70, 75,
            80, 85, 90, 95, 100
        };
        [Tooltip("Minimum startup roster size enforced at runtime (including player).")]
        [SerializeField, Min(1)] private int minimumStartupSquadTotalCount = 20;
        [Tooltip("When enabled, startup squad members get randomized runtime UMA visuals.")]
        [SerializeField] private bool randomizeStartupSquadUmaVisuals = true;
        [SerializeField] private bool logStartupSquadSpawning = true;
        [Tooltip("When enabled, startup squad members are spawned over multiple frames to avoid CPU spikes.")]
        [SerializeField] private bool deferStartupSquadSpawning = true;
        [Tooltip("How many squad members to spawn per frame when deferred spawn is enabled.")]
        [SerializeField, Range(1, 10)] private int startupSquadSpawnPerFrame = 2;

        [Header("Runtime NavMesh")]
        [Tooltip("Half-size in meters of the runtime navmesh bake area centered on spawn.")]
        [SerializeField, Min(50f)] private float navMeshBakeRadius = 420f;
        [Tooltip("Vertical half-extent in meters for runtime navmesh baking.")]
        [SerializeField, Min(20f)] private float navMeshVerticalExtent = 180f;
        [Tooltip("Runtime navmesh voxel size. Larger values reduce bake cost and tile count.")]
        [SerializeField, Min(0.1f)] private float navMeshVoxelSize = 0.25f;
        [Tooltip("Runtime navmesh tile size. Larger values reduce tile count.")]
        [SerializeField, Range(64, 1024)] private int navMeshTileSize = 256;
        [Tooltip("Maximum slope (degrees) considered walkable in the runtime NavMesh bake.")]
        [SerializeField, Range(0f, 75f)] private float navMeshMaxSlopeDegrees = 72f;
        [Tooltip("Maximum step height (meters) considered walkable in the runtime NavMesh bake.")]
        [SerializeField, Min(0f)] private float navMeshStepHeightMeters = 2.4f;
        [Tooltip("Discard isolated NavMesh regions smaller than this area (m^2). Helps reduce tiny broken islands on steep/terraced terrain.")]
        [SerializeField, Min(0f)] private float navMeshMinRegionArea = 0.1f;
        [Tooltip("When snapping spawn to NavMesh, allow the NavMesh point to be below sampled terrain by this many meters. Useful when runtime-baked NavMesh sits slightly under the terrain surface due to voxelization/agent settings.")]
        [SerializeField, Min(0f)] private float spawnNavMeshBelowTerrainToleranceMeters = 8f;
        [Tooltip("When enabled, selects a player spawn point on the largest connected NavMesh region found near the spawn origin. Helps avoid spawning on small isolated NavMesh islands.")]
        [SerializeField] private bool preferLargestConnectedNavMeshRegionForSpawn = true;
        [Tooltip("How many random NavMesh points to consider when searching for a better connected spawn region.")]
        [SerializeField, Range(8, 128)] private int spawnNavMeshCandidateSamples = 40;
        [Tooltip("For each candidate spawn point, how many path probes to run to estimate region connectivity.")]
        [SerializeField, Range(4, 64)] private int spawnNavMeshConnectivityProbes = 16;
        [Tooltip("Include MeshFilter sources in runtime navmesh baking. Disabled by default to avoid dynamic/render-only mesh pollution.")]
        [SerializeField] private bool includeMeshFilterSourcesInRuntimeNavMesh = false;
        [Tooltip("When MeshFilter source baking is enabled, only include GameObjects marked static.")]
        [SerializeField] private bool onlyUseStaticMeshFilterSources = true;
        [Tooltip("When enabled, navmesh bake bounds will expand to fit included terrain dimensions.")]
        [SerializeField] private bool fitNavMeshBoundsToTerrain = true;
        [Tooltip("Extra horizontal padding in meters when fitting navmesh bounds to terrain.")]
        [SerializeField, Min(0f)] private float navMeshTerrainBoundsPadding = 16f;
        [Tooltip("Retry runtime navmesh baking if the initial bake has no triangles (common with streaming/generated terrain).")]
        [SerializeField, Range(0, 10)] private int navMeshRetryAttempts = 5;
        [Tooltip("Seconds to wait between navmesh retry attempts.")]
        [SerializeField, Min(0.1f)] private float navMeshRetryDelaySeconds = 0.6f;
        [Tooltip("Rebuild runtime NavMesh when MapMagic reports tile generation complete.")]
        [SerializeField] private bool rebakeNavMeshOnMapMagicComplete = true;
        [Tooltip("Minimum seconds between MapMagic-triggered NavMesh rebakes.")]
        [SerializeField, Min(0f)] private float mapMagicNavMeshRebakeCooldownSeconds = 1.5f;
        [Tooltip("If NavMeshSurface components exist in the scene, build them at runtime during spawn bootstrap. Can cause large spikes on Continue.")]
        [SerializeField] private bool buildNavMeshSurfacesAtRuntime = false;

        [Header("Runtime MapMagic")]
        [Tooltip("Stabilizes MapMagic to a single tracker area at startup (prevents split generation zones).")]
        [SerializeField] private bool stabilizeMapMagicGenerationInPlayMode = true;
        [Tooltip("Stops new MapMagic tile expansion after initial stabilization in play mode.")]
        [SerializeField] private bool freezeMapMagicExpansionInPlayMode = true;
        [Tooltip("SafeDebug matches the bools above. Production forces infinite streaming with margins (freeze flag ignored).")]
        [SerializeField] private MapMagicPlayModeStreamingProfile mapMagicStreamingProfile = MapMagicPlayModeStreamingProfile.SafeDebugStreaming;
        [SerializeField, Min(1)] private int productionStreamingGenerateRange = 4;
        [SerializeField, Min(0)] private int productionStreamingRetainMargin = 3;

        [Header("Runtime NavMesh Streaming")]
        [Tooltip("When assigned and Drive Runtime NavMesh is enabled on the component, NavMesh is built per MapMagic tile instead of one large runtime bake.")]
        [SerializeField] private StreamingNavMeshTileService streamingNavMeshTileService;

        [Header("Procedural Spawn Quality")]
        [Tooltip("When enabled, delays spawning until at least one MapMagic terrain tile is active in the scene (prevents spawning at raw fallback points).")]
        [SerializeField] private bool deferSpawnUntilMapMagicTerrainReady = true;
        [SerializeField, Min(0f)] private float maxSecondsToWaitForMapMagicTerrain = 6f;
        [Tooltip("Extra margin (0-0.45) excluded from the deployed tile rect when searching for a flatter spawn point.")]
        [SerializeField, Range(0f, 0.45f)] private float mapMagicSpawnRectEdgeMarginNormalized = 0.12f;
        [Tooltip("How many random candidate points to test when selecting a flatter MapMagic spawn position.")]
        [SerializeField, Range(8, 256)] private int mapMagicFlatterSpawnSamples = 48;
        [Tooltip("Maximum walkable slope (degrees) to consider for the initial spawn point search.")]
        [SerializeField, Range(0f, 75f)] private float mapMagicMaxSpawnSlopeDegrees = 18f;

        [Header("Dev Spawn Inventory")]
        [Tooltip("Item definitions applied to player inventory when debug/dev mode is enabled.")]
        [SerializeField] private ItemDefinition[] devModeSpawnInventoryItems = new ItemDefinition[0];
        [Tooltip("When enabled in editor, keeps the dev spawn list synced to all ItemDefinition assets in the project.")]
        [SerializeField] private bool autoPopulateDevModeSpawnItemsInEditor = true;
        [Tooltip("Minimum quantity per item to ensure in player inventory during dev-mode spawn.")]
        [SerializeField, Min(1)] private int devModeSpawnQuantityPerItem = 1;
        [Tooltip("Minimum quantity per ammo item to ensure in player inventory during dev-mode spawn (testing helper).")]
        [SerializeField, Min(1)] private int devModeSpawnAmmoQuantityPerItem = 999;
        [Tooltip("Minimum inventory weight limit used while applying the dev-mode spawn loadout.")]
        [SerializeField, Min(1f)] private float devModeSpawnMinimumWeightLimit = 500f;
        [SerializeField] private bool logDevModeSpawnInventory = true;

        public Unit SpawnedPlayer { get; private set; }

        private static readonly Dictionary<int, NavMeshDataInstance> s_runtimeNavMeshInstances = new Dictionary<int, NavMeshDataInstance>();
        private static PlayerSpawner s_runtimeNavMeshOwner;
        private static bool s_loggedNestedSpawnerRemovalWarning;
        private bool _requestedValidationZombieSpawn;
        private RuntimeNavMeshBootstrapper _runtimeNavMeshBootstrapper;
        private StartupSquadSpawner _startupSquadSpawner;
        private PlayerSpawnSnapper _playerSpawnSnapper;
        private PlayerSpawnWiringService _playerSpawnWiringService;
        private Vector3 _lastNavMeshCenter;
        private bool _hasLastNavMeshCenter;
        private bool _ownsRuntimeNavMeshInstance;
        private bool _spawnBootstrapStarted;
        private bool _loggedSpawnDeferral;
        private PlayerInputController _activeInputController;
        private Unit _activeControlledUnit;
        private SquadControlUiCoordinator _squadControlUiCoordinator;

        private void OnEnable()
        {
            if (IsAttachedToUnit())
            {
                return;
            }

            TerrainTile.OnAllComplete -= HandleMapMagicAllComplete;
            TerrainTile.OnAllComplete += HandleMapMagicAllComplete;
        }

        private void OnDisable()
        {
            TerrainTile.OnAllComplete -= HandleMapMagicAllComplete;
            _runtimeNavMeshBootstrapper?.ResetQueuedRebake();
            _squadControlUiCoordinator?.UnbindPortraitStripCallbacks();
        }

        private void OnDestroy()
        {
            _squadControlUiCoordinator?.UnbindPortraitStripCallbacks();

            if (!_ownsRuntimeNavMeshInstance || s_runtimeNavMeshOwner != this)
            {
                return;
            }

            if (s_runtimeNavMeshInstances.Count == 0)
            {
                _ownsRuntimeNavMeshInstance = false;
                if (s_runtimeNavMeshOwner == this)
                {
                    s_runtimeNavMeshOwner = null;
                }
                return;
            }

            foreach (NavMeshDataInstance instance in s_runtimeNavMeshInstances.Values)
            {
                if (instance.valid)
                {
                    instance.Remove();
                }
            }

            s_runtimeNavMeshInstances.Clear();
            s_runtimeNavMeshOwner = null;
            _ownsRuntimeNavMeshInstance = false;
        }

        private void Awake()
        {
            if (IsAttachedToUnit())
            {
                Unit ownerUnit = ResolveOwningUnit();
                if (ownerUnit != null)
                {
                    SanitizeRuntimeUnitHierarchy(ownerUnit.gameObject);
                }

                if (!s_loggedNestedSpawnerRemovalWarning)
                {
                    s_loggedNestedSpawnerRemovalWarning = true;
                    Debug.LogWarning(
                        "[PlayerSpawner] PlayerSpawner was found on a Unit instance and is being removed to prevent recursive spawning. " +
                        "If this repeats during startup squad spawning, assign Startup Squad Member Prefab to a prefab without PlayerSpawner.",
                        this);
                }

                enabled = false;
                Destroy(this);
                return;
            }

            _requestedValidationZombieSpawn = false;
            if (streamingNavMeshTileService == null)
            {
                streamingNavMeshTileService = FindFirstObjectByType<StreamingNavMeshTileService>();
            }

            _runtimeNavMeshBootstrapper = new RuntimeNavMeshBootstrapper(
                this,
                () => gameObject.scene,
                () => SpawnedPlayer,
                () => _hasLastNavMeshCenter,
                () => _lastNavMeshCenter,
                () => spawnPoint,
                ResolveDefaultSpawnPosition,
                () => streamingNavMeshTileService,
                TryBuildSceneNavMeshSurfaces,
                BakeNavMeshGlobal);

            _runtimeNavMeshBootstrapper.SyncStreamingNavMeshTuning(BuildStreamingNavMeshTuning());

            _startupSquadSpawner = new StartupSquadSpawner(
                this,
                () => SpawnedPlayer,
                () => gameObject.scene,
                SanitizeRuntimeUnitHierarchy,
                TrySampleGroundFromPhysics);

            _playerSpawnSnapper = new PlayerSpawnSnapper(
                this,
                gameObject.scene,
                ResolveSpawnTerrain,
                TrySampleGroundFromPhysics);

            _umaSpawnStylingService = new UmaSpawnStylingService(this, this);
            _playerSpawnWiringService = new PlayerSpawnWiringService(this);
            _squadControlUiCoordinator = new SquadControlUiCoordinator(
                this,
                () => SpawnedPlayer,
                IsControllableSquadUnit,
                EnsurePlayerInputController,
                unit => ActivateControlledUnit(unit, syncPortraitSelection: true),
                () => _activeControlledUnit);
            TryBeginSpawnBootstrap();
        }

        private void Update()
        {
            if (!_spawnBootstrapStarted)
            {
                if (IsAttachedToUnit())
                {
                    return;
                }

                TryBeginSpawnBootstrap();
                return;
            }

            if (_activeControlledUnit == null)
            {
                return;
            }

            _squadControlUiCoordinator?.TickBindPortraitStrips();
        }

        private void TryBeginSpawnBootstrap()
        {
            if (_spawnBootstrapStarted || !CanBeginSpawnBootstrap())
            {
                return;
            }

            _spawnBootstrapStarted = true;
            _loggedSpawnDeferral = false;

            BeginSpawnBootstrap();
        }

        private bool CanBeginSpawnBootstrap()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return true;
            }

            GameState state = gameManager.CurrentState;
            bool canBegin =
                state == GameState.LoadingWorld ||
                state == GameState.Playing ||
                state == GameState.Paused;

            if (!canBegin && !_loggedSpawnDeferral)
            {
                Debug.Log($"[PlayerSpawner] Deferring world spawn while GameManager state is '{state}'.", this);
                _loggedSpawnDeferral = true;
            }

            return canBegin;
        }

        private void BeginSpawnBootstrap()
        {
            if (logSpawnTerrainDiagnostics)
            {
                Debug.Log("[PlayerSpawner] Beginning world spawn bootstrap.", this);
            }

            PrepareScenePlayerCandidatesForSpawn();

            if (stabilizeMapMagicGenerationInPlayMode)
            {
                StabilizeMapMagicGeneration();
            }

            _runtimeNavMeshBootstrapper?.SyncStreamingNavMeshTuning(BuildStreamingNavMeshTuning());

            Vector3 navMeshCenter = ResolveMainSpawnPosition();
            _lastNavMeshCenter = navMeshCenter;
            _hasLastNavMeshCenter = true;

            if (logSpawnTerrainDiagnostics)
            {
                LogSpawnTerrainDiagnostics(navMeshCenter);
            }

            bool navMeshBuilt = (_runtimeNavMeshBootstrapper != null) &&
                                _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(navMeshCenter, BuildStreamingNavMeshTuning());

            if (!navMeshBuilt && navMeshRetryAttempts > 0)
            {
                _runtimeNavMeshBootstrapper?.StartRetryBake(
                    navMeshCenter,
                    new RuntimeNavMeshRetryTuning(navMeshRetryAttempts, navMeshRetryDelaySeconds),
                    BuildStreamingNavMeshTuning());
            }

            if (deferSpawnUntilMapMagicTerrainReady && ShouldDeferSpawnUntilMapMagicTerrainReady(navMeshCenter))
            {
                StartCoroutine(SpawnPlayerWhenMapMagicTerrainReady(navMeshCenter));
                return;
            }

            SpawnPlayer();
        }

        private bool ShouldDeferSpawnUntilMapMagicTerrainReady(Vector3 spawnOrigin)
        {
            // If a valid terrain already contains the origin, do not defer.
            Terrain resolved = ResolveSpawnTerrain(spawnOrigin);
            if (resolved != null && resolved.terrainData != null)
            {
                return false;
            }

            // If MapMagic has any active terrain, do not defer.
            Terrain anyMapMagicTerrain = SpawnPointSelector.FindFirstActiveMapMagicTerrain(gameObject.scene);
            if (anyMapMagicTerrain != null && anyMapMagicTerrain.terrainData != null)
            {
                return false;
            }

            // Only defer when MapMagic exists (otherwise we'd stall scenes with static geometry).
            MapMagicObject mm = FindFirstObjectByType<MapMagicObject>();
            return mm != null;
        }

        private System.Collections.IEnumerator SpawnPlayerWhenMapMagicTerrainReady(Vector3 spawnOrigin)
        {
            float timeout = Mathf.Max(0f, maxSecondsToWaitForMapMagicTerrain);
            float start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeout)
            {
                if (SpawnPointSelector.FindFirstActiveMapMagicTerrain(gameObject.scene) != null)
                {
                    break;
                }

                yield return null;
            }

            // Re-resolve center after tiles exist.
            Vector3 updatedCenter = ResolveMainSpawnPosition();
            _lastNavMeshCenter = updatedCenter;
            _hasLastNavMeshCenter = true;

            // If we were waiting, give NavMesh a chance to build at least once.
            _ = (_runtimeNavMeshBootstrapper != null) &&
                _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(updatedCenter, BuildStreamingNavMeshTuning());

            SpawnPlayer();
        }

        private void StabilizeMapMagicGeneration()
        {
            MapMagicObject[] mapMagics = FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (mapMagics == null || mapMagics.Length == 0)
            {
                return;
            }

            Scene playerScene = gameObject.scene;
            MapMagicObject primary = null;
            int disabledExtraMapMagic = 0;

            for (int i = 0; i < mapMagics.Length; i++)
            {
                MapMagicObject candidate = mapMagics[i];
                if (candidate == null || candidate.gameObject.scene != playerScene)
                {
                    continue;
                }

                if (primary == null)
                {
                    primary = candidate;
                    continue;
                }

                if (candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(false);
                    disabledExtraMapMagic++;
                }
            }

            if (primary == null)
            {
                return;
            }

            int hiddenDraftTerrains = HideActiveDraftTerrains(playerScene);

            primary.tiles.genAroundMainCam = true;
            primary.tiles.genAroundObjsTag = false;
            primary.tiles.genAroundTag = string.Empty;
            primary.tiles.genAroundTfms = false;
            primary.tiles.genAroundTfmsList = new Transform[0];
            primary.tiles.genAroundCoordinates = false;
            primary.tiles.genCoordinates = new Coord[0];

            if (mapMagicStreamingProfile == MapMagicPlayModeStreamingProfile.ProductionStreaming)
            {
                primary.draftsInPlaymode = false;
                primary.tiles.generateInfinite = true;
                primary.tiles.retainMargin = productionStreamingRetainMargin;
                primary.tiles.generateRange = Mathf.Max(primary.mainRange, productionStreamingGenerateRange);
            }
            else
            {
                primary.draftsInPlaymode = false;
                primary.tiles.generateRange = primary.mainRange;
                primary.tiles.retainMargin = 0;

                if (freezeMapMagicExpansionInPlayMode)
                {
                    primary.tiles.generateInfinite = false;
                }
            }

            primary.SwitchLods();

            Debug.Log(
                "[PlayerSpawner] MapMagic stabilized for playmode. " +
                $"Profile={mapMagicStreamingProfile}, Primary='{primary.name}', Disabled extra MapMagic objects={disabledExtraMapMagic}, Hidden draft terrains={hiddenDraftTerrains}, " +
                $"GenerateInfinite={(primary.tiles.generateInfinite ? "on" : "off")}, MainRange={primary.mainRange}, GenerateRange={primary.tiles.generateRange}, RetainMargin={primary.tiles.retainMargin}.");
        }

        private static int HideActiveDraftTerrains(Scene scene)
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int hiddenCount = 0;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.gameObject.scene != scene)
                {
                    continue;
                }

                GameObject terrainObject = terrain.gameObject;
                if (!terrainObject.activeSelf)
                {
                    continue;
                }

                if (terrainObject.name != "Draft Terrain")
                {
                    continue;
                }

                terrainObject.SetActive(false);
                hiddenCount++;
            }

            return hiddenCount;
        }

        private bool BakeNavMesh(Vector3 navMeshCenter)
        {
            System.Diagnostics.Stopwatch bakeTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return _runtimeNavMeshBootstrapper != null &&
                       _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(navMeshCenter, BuildStreamingNavMeshTuning());
            }
            finally
            {
                bakeTimer.Stop();
                StreamedWorldMetrics.RecordNavMeshBakeMilliseconds((float)bakeTimer.Elapsed.TotalMilliseconds);
            }
        }

        private RuntimeNavMeshStreamingTuning BuildStreamingNavMeshTuning()
        {
            return new RuntimeNavMeshStreamingTuning(
                navMeshVoxelSize,
                navMeshTileSize,
                navMeshMaxSlopeDegrees,
                navMeshStepHeightMeters,
                navMeshMinRegionArea,
                navMeshVerticalExtent);
        }

        private StartupSquadSpawnConfig BuildStartupSquadConfig()
        {
            return new StartupSquadSpawnConfig(
                spawnStartupSquadOnWorldStart,
                startupSquadTotalCount,
                minimumStartupSquadTotalCount,
                startupInitialCharacterCount,
                startupSquadRingRadius,
                randomizeStartupSquadUmaVisuals,
                applyStartupSquadSkillTiers,
                logStartupSquadSpawning,
                startupSquadSkillTiers,
                DefaultStartupSquadSkillTiers,
                playerPrefab,
                startupSquadMemberPrefab,
                startupSquadParent,
                terrainHeightOffset,
                navMeshVerticalExtent);
        }

        private PlayerSpawnSnapConfig BuildSpawnSnapConfig()
        {
            return new PlayerSpawnSnapConfig(
                terrainHeightOffset,
                navMeshBakeRadius,
                navMeshVerticalExtent,
                spawnNavMeshBelowTerrainToleranceMeters,
                preferLargestConnectedNavMeshRegionForSpawn,
                spawnNavMeshCandidateSamples,
                spawnNavMeshConnectivityProbes,
                logSpawnTerrainDiagnostics);
        }

        private bool BakeNavMeshGlobal(Vector3 navMeshCenter)
        {
            if (!IsFiniteVector3(navMeshCenter))
            {
                Debug.LogError($"[PlayerSpawner] BakeNavMesh aborted due to invalid center: {navMeshCenter}");
                return false;
            }

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            float sqrBakeRadius = navMeshBakeRadius * navMeshBakeRadius;
            int skippedInvalidTransforms = 0;
            int skippedOutsideRadius = 0;
            int skippedFilteredMeshSources = 0;
            int skippedNonStaticMeshSources = 0;
            int acceptedMeshSources = 0;

            Scene ownerScene = gameObject.scene;
            if (includeMeshFilterSourcesInRuntimeNavMesh)
            {
                foreach (MeshFilter mf in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mf == null || mf.sharedMesh == null)
                    {
                        continue;
                    }

                    if (ownerScene.IsValid() && mf.gameObject.scene != ownerScene)
                    {
                        continue;
                    }

                    if (!mf.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (onlyUseStaticMeshFilterSources && !mf.gameObject.isStatic)
                    {
                        skippedNonStaticMeshSources++;
                        continue;
                    }

                    if (ShouldSkipMeshFilterNavMeshSource(mf))
                    {
                        skippedFilteredMeshSources++;
                        continue;
                    }

                    Transform sourceTransform = mf.transform;
                    if (!IsValidNavMeshTransform(sourceTransform, requireNonZeroScale: true))
                    {
                        skippedInvalidTransforms++;
                        continue;
                    }

                    Vector3 meshPosition = sourceTransform.position;
                    Vector2 planarOffset = new Vector2(meshPosition.x - navMeshCenter.x, meshPosition.z - navMeshCenter.z);
                    if (planarOffset.sqrMagnitude > sqrBakeRadius)
                    {
                        skippedOutsideRadius++;
                        continue;
                    }

                    sources.Add(new NavMeshBuildSource
                    {
                        shape        = NavMeshBuildSourceShape.Mesh,
                        sourceObject = mf.sharedMesh,
                        transform    = sourceTransform.localToWorldMatrix,
                        area         = 0
                    });

                    acceptedMeshSources++;
                }
            }

            HashSet<Terrain> includedTerrains = new HashSet<Terrain>();
            int mapMagicTerrainSources = 0;

            foreach (Terrain terrain in FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (terrain == null)
                {
                    continue;
                }

                if (ownerScene.IsValid() && terrain.gameObject.scene != ownerScene)
                {
                    continue;
                }

                if (TryAddTerrainSource(
                        terrain,
                        navMeshCenter,
                        navMeshBakeRadius,
                        sources,
                        includedTerrains,
                        ref skippedInvalidTransforms,
                        ref skippedOutsideRadius))
                {
                    mapMagicTerrainSources++;
                }
            }

            // MapMagic tiles can be enabled/disabled frequently; ensure currently active tile terrains are considered.
            MapMagicObject[] mapMagicObjects = FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                MapMagicObject mapMagic = mapMagicObjects[i];
                if (mapMagic == null)
                {
                    continue;
                }

                if (ownerScene.IsValid() && mapMagic.gameObject.scene != ownerScene)
                {
                    continue;
                }

                foreach (Terrain terrain in mapMagic.tiles.AllActiveTerrains())
                {
                    if (TryAddTerrainSource(
                            terrain,
                            navMeshCenter,
                            navMeshBakeRadius,
                            sources,
                            includedTerrains,
                            ref skippedInvalidTransforms,
                            ref skippedOutsideRadius))
                    {
                        mapMagicTerrainSources++;
                    }
                }
            }

            _lastNavMeshCenter = navMeshCenter;
            _hasLastNavMeshCenter = true;

            if (sources.Count == 0)
            {
                Debug.LogWarning(
                    "[PlayerSpawner] BakeNavMesh: no valid sources found. " +
                    $"Skipped invalid transforms: {skippedInvalidTransforms}, outside radius: {skippedOutsideRadius}, " +
                    $"MapMagic/terrain candidates accepted: {mapMagicTerrainSources}.");
                return false;
            }

            Terrain navTerrain = ResolveSpawnTerrain(navMeshCenter);
            float centerY = navMeshCenter.y;
            if (navTerrain != null && navTerrain.terrainData != null)
            {
                centerY = navTerrain.SampleHeight(navMeshCenter) + navTerrain.transform.position.y;
            }

            if (TrySampleGroundFromPhysics(navMeshCenter, out float physicsGroundY))
            {
                centerY = Mathf.Max(centerY, physicsGroundY);
            }

            Vector3 boundsCenter = new Vector3(navMeshCenter.x, centerY, navMeshCenter.z);
            Vector3 boundsSize = new Vector3(navMeshBakeRadius * 2f, navMeshVerticalExtent * 2f, navMeshBakeRadius * 2f);
            bool usingTerrainFittedBounds = false;

            if (fitNavMeshBoundsToTerrain && TryGetCombinedTerrainBounds(includedTerrains, out Bounds combinedTerrainBounds))
            {
                float padding = Mathf.Max(0f, navMeshTerrainBoundsPadding);
                boundsCenter = new Vector3(combinedTerrainBounds.center.x, centerY, combinedTerrainBounds.center.z);
                boundsSize = new Vector3(
                    Mathf.Max(navMeshBakeRadius * 2f, combinedTerrainBounds.size.x + padding * 2f),
                    Mathf.Max(navMeshVerticalExtent * 2f, combinedTerrainBounds.size.y + Mathf.Max(4f, terrainHeightOffset * 2f + 6f)),
                    Mathf.Max(navMeshBakeRadius * 2f, combinedTerrainBounds.size.z + padding * 2f));
                usingTerrainFittedBounds = true;

                // Promote _lastNavMeshCenter to the terrain-fitted XZ so SpawnPlayer samples NavMesh
                // at the correct location even when the original navMeshCenter was the unresolved origin.
                _lastNavMeshCenter = new Vector3(combinedTerrainBounds.center.x, _lastNavMeshCenter.y, combinedTerrainBounds.center.z);
            }

            if (!IsFiniteVector3(boundsCenter) || !IsFiniteVector3(boundsSize))
            {
                Debug.LogError($"[PlayerSpawner] BakeNavMesh aborted due to invalid bounds. Center={boundsCenter}, Size={boundsSize}");
                return false;
            }

            Bounds bounds = new Bounds(
                boundsCenter,
                boundsSize);

            // Bake navmesh data for each agent type present in the scene so NavMeshAgent.Warp/isOnNavMesh
            // works even when unit prefabs are configured to a non-default agent type.
            HashSet<int> agentTypeIds = new HashSet<int>();
            foreach (NavMeshAgent navAgent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (navAgent == null)
                {
                    continue;
                }

                if (ownerScene.IsValid() && navAgent.gameObject.scene != ownerScene)
                {
                    continue;
                }

                agentTypeIds.Add(navAgent.agentTypeID);
            }

            if (agentTypeIds.Count == 0)
            {
                agentTypeIds.Add(0);
            }

            // Remove any previously baked runtime navmesh instances before adding new ones.
            if (s_runtimeNavMeshInstances.Count > 0)
            {
                foreach (NavMeshDataInstance instance in s_runtimeNavMeshInstances.Values)
                {
                    if (instance.valid)
                    {
                        instance.Remove();
                    }
                }

                s_runtimeNavMeshInstances.Clear();

                if (s_runtimeNavMeshOwner != null)
                {
                    s_runtimeNavMeshOwner._ownsRuntimeNavMeshInstance = false;
                }

                s_runtimeNavMeshOwner = null;
            }

            bool addedAny = false;
            foreach (int agentTypeId in agentTypeIds)
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeId);
                settings.overrideVoxelSize = true;
                settings.voxelSize = Mathf.Max(0.1f, navMeshVoxelSize);
                settings.overrideTileSize = true;
                settings.tileSize = Mathf.Clamp(navMeshTileSize, 64, 1024);
                settings.agentSlope = Mathf.Clamp(navMeshMaxSlopeDegrees, 0f, 75f);
                settings.agentClimb = Mathf.Max(0f, navMeshStepHeightMeters);
                settings.minRegionArea = Mathf.Max(0f, navMeshMinRegionArea);

                NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                    settings, sources, bounds, Vector3.zero, Quaternion.identity);

                if (data == null)
                {
                    Debug.LogError($"[PlayerSpawner] BakeNavMesh: BuildNavMeshData returned null for agentTypeId={agentTypeId}.");
                    continue;
                }

                NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
                if (instance.valid)
                {
                    s_runtimeNavMeshInstances[agentTypeId] = instance;
                    addedAny = true;
                }
            }

            _ownsRuntimeNavMeshInstance = addedAny;
            s_runtimeNavMeshOwner = addedAny ? this : null;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int triangleCount = tri.indices != null ? tri.indices.Length / 3 : 0;
            bool hasTriangles = triangleCount > 0;

            string message =
                $"[PlayerSpawner] NavMesh baked around {navMeshCenter}. BoundsCenter: {bounds.center}, BoundsSize: {bounds.size}. Sources: {sources.Count}, " +
                $"Skipped invalid transforms: {skippedInvalidTransforms}, Skipped out-of-range: {skippedOutsideRadius}, " +
                $"Mesh sources enabled: {(includeMeshFilterSourcesInRuntimeNavMesh ? "yes" : "no")}, Mesh accepted: {acceptedMeshSources}, Mesh filtered: {skippedFilteredMeshSources}, Mesh non-static skipped: {skippedNonStaticMeshSources}, " +
                $"MapMagic/terrain candidates accepted: {mapMagicTerrainSources}, Terrain-fitted bounds: {(usingTerrainFittedBounds ? "yes" : "no")}. " +
                $"Vertices: {tri.vertices.Length}, Triangles: {triangleCount}";

            if (hasTriangles)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }

            return addedAny && hasTriangles;
        }

        private bool TryAddTerrainSource(
            Terrain terrain,
            Vector3 navMeshCenter,
            float bakeRadius,
            List<NavMeshBuildSource> sources,
            HashSet<Terrain> includedTerrains,
            ref int skippedInvalidTransforms,
            ref int skippedOutsideRadius)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            if (includedTerrains != null && includedTerrains.Contains(terrain))
            {
                return false;
            }

            Transform terrainTransform = terrain.transform;
            if (!IsValidNavMeshTransform(terrainTransform, requireNonZeroScale: false))
            {
                skippedInvalidTransforms++;
                return false;
            }

            bool terrainContainsCenter = TerrainResolver.TerrainContainsXZ(terrain, navMeshCenter);
            if (!terrainContainsCenter && !IsTerrainWithinBakeRadius(terrain, navMeshCenter, bakeRadius))
            {
                skippedOutsideRadius++;
                return false;
            }

            Matrix4x4 terrainTransformMatrix = Matrix4x4.TRS(
                terrainTransform.position,
                terrainTransform.rotation,
                Vector3.one);

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Terrain,
                sourceObject = terrain.terrainData,
                transform = terrainTransformMatrix,
                area = 0
            });

            includedTerrains?.Add(terrain);
            return true;
        }

        private static bool ShouldSkipMeshFilterNavMeshSource(MeshFilter meshFilter)
        {
            if (meshFilter == null)
            {
                return true;
            }

            GameObject go = meshFilter.gameObject;
            if (go == null)
            {
                return true;
            }

            // Skip runtime/generated overlays and unit visuals that should never contribute walkable topology.
            if (go.GetComponentInParent<FogOfWarVisionOverlay>(true) != null)
            {
                return true;
            }

            if (go.GetComponentInParent<Unit>(true) != null)
            {
                return true;
            }

            if (go.GetComponentInParent<ZombieAI>(true) != null)
            {
                return true;
            }

            if (go.GetComponentInParent<NavMeshAgent>(true) != null)
            {
                return true;
            }

            if (go.GetComponentInParent<Canvas>(true) != null)
            {
                return true;
            }

            if (go.GetComponentInParent<ParticleSystem>(true) != null)
            {
                return true;
            }

            string objectName = go.name;
            if (!string.IsNullOrEmpty(objectName))
            {
                if (objectName.IndexOf("FogVisionOverlay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("OverlayMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBuildSceneNavMeshSurfaces()
        {
            if (!buildNavMeshSurfacesAtRuntime)
            {
                return false;
            }

            NavMeshSurface[] surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (surfaces == null || surfaces.Length == 0)
            {
                return false;
            }

            Scene ownerScene = gameObject.scene;
            int builtCount = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                NavMeshSurface surface = surfaces[i];
                if (surface == null)
                {
                    continue;
                }

                if (ownerScene.IsValid() && surface.gameObject.scene != ownerScene)
                {
                    continue;
                }

                if (!surface.gameObject.activeSelf)
                {
                    surface.gameObject.SetActive(true);
                }

                if (!surface.enabled)
                {
                    surface.enabled = true;
                }

                surface.BuildNavMesh();
                builtCount++;
            }

            if (builtCount == 0)
            {
                return false;
            }

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int triangleCount = tri.indices != null ? tri.indices.Length / 3 : 0;
            bool hasTriangles = triangleCount > 0;

            string message =
                $"[PlayerSpawner] Built {builtCount} NavMeshSurface component(s). " +
                $"Vertices: {tri.vertices.Length}, Triangles: {triangleCount}";

            if (hasTriangles)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }

            return hasTriangles;
        }

        private void HandleMapMagicAllComplete(MapMagicObject mapMagic)
        {
            StreamedWorldMetrics.RecordMapMagicAllComplete();
            _runtimeNavMeshBootstrapper?.QueueThrottledMapMagicNavMeshRebake(
                mapMagic,
                rebakeNavMeshOnMapMagicComplete,
                mapMagicNavMeshRebakeCooldownSeconds,
                BuildStreamingNavMeshTuning());
        }

        private void SpawnPlayer()
        {
            Vector3 spawnPosition = ResolveMainSpawnPosition();
            Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            if (_playerSpawnSnapper != null)
            {
                spawnPosition = _playerSpawnSnapper.SnapSpawnPosition(
                    spawnPosition,
                    spawnPoint == null,
                    transform.position,
                    _hasLastNavMeshCenter,
                    _lastNavMeshCenter,
                    BuildSpawnSnapConfig());
            }

            // Prefer reusing an already-live PLAYER unit over instantiating a new one.
            // Do not reuse or destroy non-player units (for example, placed zombies).
            Unit reuseUnit = null;
            foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!IsPlayerCandidate(u))
                {
                    continue;
                }

                if (reuseUnit == null)
                {
                    reuseUnit = u;
                }
                else
                {
                    Debug.Log($"[PlayerSpawner] Destroying extra player candidate Unit: {u.gameObject.name}");
                    Destroy(u.gameObject);
                }
            }

            GameObject instance;
            if (reuseUnit != null)
            {
                instance = reuseUnit.gameObject;
                reuseUnit.SetRole(UnitRole.Player);
                instance.transform.position = spawnPosition;
                instance.transform.rotation = spawnRotation;
                Debug.Log($"[PlayerSpawner] Reusing scene Unit '{instance.name}' at {spawnPosition}");
            }
            else
            {
                if (playerPrefab == null)
                {
                    Debug.LogWarning("[PlayerSpawner] No player prefab assigned and no reusable player Unit was found.", this);
                    return;
                }

                instance = Instantiate(playerPrefab, spawnPosition, spawnRotation);
                Debug.Log($"[PlayerSpawner] Instantiated new Player at {spawnPosition}");
            }

            var dca = _umaSpawnStylingService?.PrepareUmaAvatarForSanitizedSpawn(instance);
            SanitizeRuntimeUnitHierarchy(instance);

            string selectedName = CharacterSelectionState.SelectedCharacterName;
            instance.name = string.IsNullOrWhiteSpace(selectedName) ? "Player" : selectedName;

            SpawnedPlayer = instance.GetComponent<Unit>();

            if (SpawnedPlayer == null)
            {
                Debug.LogError("[PlayerSpawner] Spawned prefab has no Unit component.", instance);
                return;
            }

            ApplyDevModeSpawnInventory(SpawnedPlayer);

            // NavMesh is already baked — enable/warp the agent now.
            UnitController unitController = instance.GetComponent<UnitController>();
            if (unitController != null)
            {
                unitController.ForceEnableAgent();
            }

            EnsureStartupTestSquad(SpawnedPlayer);
            InitializeSquadControlSwap(SpawnedPlayer);

            if (_playerSpawnWiringService != null)
            {
                worldCamera = _playerSpawnWiringService.EnsureWorldCamera(worldCamera, instance.transform.position);
                _playerSpawnWiringService.BindCameraToUnit(instance, worldCamera);
                _playerSpawnWiringService.WireInputSystems(instance);
            }
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            _playerSpawnWiringService?.BindHudToUnit(SpawnedPlayer);

            // Apply UMA recipe after one frame so DCA.Start() runs first.
            UnitController rewarpController = instance != null ? instance.GetComponent<UnitController>() : null;
            _umaSpawnStylingService?.StartApplySelectedUmaRecipeNextFrame(instance, dca, rewarpController);

            if (spawnValidationZombieNearPlayer && !_requestedValidationZombieSpawn)
            {
                _requestedValidationZombieSpawn = true;
                StartCoroutine(SpawnValidationZombieNearPlayer(instance.transform));
            }
        }

        private void EnsureStartupTestSquad(Unit playerUnit)
        {
            if (_startupSquadSpawner == null)
            {
                return;
            }

            StartupSquadSpawnConfig config = BuildStartupSquadConfig();
            if (deferStartupSquadSpawning)
            {
                _startupSquadSpawner.StartEnsureStartupSquadDeferred(playerUnit, config, startupSquadSpawnPerFrame);
            }
            else
            {
                _ = _startupSquadSpawner.EnsureStartupSquad(playerUnit, config);
            }
        }

        private Vector3 ResolveDefaultSpawnPosition()
        {
            Terrain terrain = ResolveSpawnTerrain(transform.position);
            if (terrain == null || terrain.terrainData == null)
            {
                // No terrain at the spawner's own position (common when MapMagic tiles are at a
                // different world origin). Prefer the center of the currently deployed MapMagic tile rect.
                if (SpawnPointSelector.TryGetMapMagicDeployedWorldRect(gameObject.scene, out Rect deployedRect))
                {
                    Vector3 deployedCenter = new Vector3(
                        deployedRect.x + deployedRect.width * 0.5f,
                        transform.position.y,
                        deployedRect.y + deployedRect.height * 0.5f);

                    if (logSpawnTerrainDiagnostics)
                    {
                        Debug.Log(
                            $"[PlayerSpawner] ResolveDefaultSpawnPosition: no terrain at spawner origin — using MapMagic deployed rect center {deployedCenter} (rect={deployedRect}).",
                            this);
                    }

                    return deployedCenter;
                }

                return transform.position;
            }

            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            return new Vector3(
                terrainOrigin.x + terrainSize.x * 0.5f,
                transform.position.y,
                terrainOrigin.z + terrainSize.z * 0.5f);
        }

        private Vector3 ResolveMainSpawnPosition()
        {
            Vector3 fallbackPosition = spawnPoint != null ? spawnPoint.position : ResolveDefaultSpawnPosition();

            if (!useTerrainAsMainSpawn)
            {
                return fallbackPosition;
            }

            // When MapMagic is the terrain authority, prefer the center of the currently deployed tile rect.
            // This avoids spawning on the edge when only a subset of tiles has generated at bootstrap.
            if (SpawnPointSelector.TryGetMapMagicDeployedWorldRect(gameObject.scene, out Rect deployedRect))
            {
                Vector2 deployedNormalized = new Vector2(
                    Mathf.Clamp01(terrainMainSpawnNormalized.x),
                    Mathf.Clamp01(terrainMainSpawnNormalized.y));

                if (SpawnPointSelector.TrySelectFlatterSpawnInMapMagicRect(
                        deployedRect,
                        gameObject.scene,
                        deployedNormalized,
                        mapMagicSpawnRectEdgeMarginNormalized,
                        mapMagicFlatterSpawnSamples,
                        mapMagicMaxSpawnSlopeDegrees,
                        terrainHeightOffset,
                        ResolveSpawnTerrain,
                        logSpawnTerrainDiagnostics,
                        this,
                        out Vector3 flatterSpawn))
                {
                    return flatterSpawn;
                }

                Vector3 deployedSpawn = new Vector3(
                    deployedRect.x + deployedRect.width * deployedNormalized.x,
                    fallbackPosition.y,
                    deployedRect.y + deployedRect.height * deployedNormalized.y);

                // Use heightmap sampling if we can resolve an actual terrain under this point.
                Terrain deployedTerrain = ResolveSpawnTerrain(deployedSpawn);
                if (deployedTerrain != null && deployedTerrain.terrainData != null)
                {
                    Vector3 origin = deployedTerrain.transform.position;
                    float deployedSampledY = deployedTerrain.SampleHeight(deployedSpawn) + origin.y;
                    deployedSpawn.y = deployedSampledY + Mathf.Max(0f, terrainHeightOffset);
                    return deployedSpawn;
                }

                return deployedSpawn;
            }

            Terrain terrain = ResolveSpawnTerrain(fallbackPosition);
            if (terrain == null || terrain.terrainData == null)
            {
                return fallbackPosition;
            }

            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            Vector2 normalized = new Vector2(
                Mathf.Clamp01(terrainMainSpawnNormalized.x),
                Mathf.Clamp01(terrainMainSpawnNormalized.y));

            Vector3 terrainSpawnPosition = new Vector3(
                terrainOrigin.x + terrainSize.x * normalized.x,
                terrainOrigin.y,
                terrainOrigin.z + terrainSize.z * normalized.y);

            float sampledY = terrain.SampleHeight(terrainSpawnPosition) + terrainOrigin.y;
            terrainSpawnPosition.y = sampledY + Mathf.Max(0f, terrainHeightOffset);
            return terrainSpawnPosition;
        }

        // MapMagic spawn selection helpers moved to SpawnPointSelector.

        private static bool TrySampleGroundFromPhysics(Vector3 worldPosition, out float groundY)
        {
            Vector3 rayOrigin = worldPosition + Vector3.up * 1200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = worldPosition.y;
            return false;
        }

        private static bool IsTerrainWithinBakeRadius(Terrain terrain, Vector3 worldCenter, float bakeRadius)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;

            float minX = terrainPosition.x;
            float maxX = terrainPosition.x + terrainSize.x;
            float minZ = terrainPosition.z;
            float maxZ = terrainPosition.z + terrainSize.z;

            float closestX = Mathf.Clamp(worldCenter.x, minX, maxX);
            float closestZ = Mathf.Clamp(worldCenter.z, minZ, maxZ);

            float dx = worldCenter.x - closestX;
            float dz = worldCenter.z - closestZ;

            return dx * dx + dz * dz <= bakeRadius * bakeRadius;
        }

        private static bool TryGetCombinedTerrainBounds(IEnumerable<Terrain> terrains, out Bounds combinedBounds)
        {
            combinedBounds = default;
            if (terrains == null)
            {
                return false;
            }

            bool hasBounds = false;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Bounds terrainBounds = GetTerrainWorldBounds(terrain);
                if (!hasBounds)
                {
                    combinedBounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(terrainBounds);
                }
            }

            return hasBounds;
        }

        private static Bounds GetTerrainWorldBounds(Terrain terrain)
        {
            Vector3 terrainOrigin = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 terrainCenter = terrainOrigin + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f);
            return new Bounds(terrainCenter, terrainSize);
        }

        private static bool IsValidNavMeshTransform(Transform sourceTransform, bool requireNonZeroScale)
        {
            if (sourceTransform == null)
            {
                return false;
            }

            Vector3 position = sourceTransform.position;
            Quaternion rotation = sourceTransform.rotation;
            Vector3 scale = sourceTransform.lossyScale;

            if (!IsFiniteVector3(position) || !IsFiniteQuaternion(rotation) || !IsFiniteVector3(scale))
            {
                return false;
            }

            if (!requireNonZeroScale)
            {
                return true;
            }

            return Mathf.Abs(scale.x) >= 0.0001f && Mathf.Abs(scale.y) >= 0.0001f && Mathf.Abs(scale.z) >= 0.0001f;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void LogSpawnTerrainDiagnostics(Vector3 worldPosition)
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                Debug.LogWarning($"[PlayerSpawner] Spawn terrain diagnostics: no Terrain objects found near {worldPosition}.");
                return;
            }

            int overlapCount = 0;
            float highestSampleY = float.NegativeInfinity;
            string highestTerrainName = string.Empty;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (!TerrainResolver.TerrainContainsXZ(terrain, worldPosition))
                {
                    continue;
                }

                float sampleY = terrain.SampleHeight(worldPosition) + terrain.GetPosition().y;
                overlapCount++;

                if (sampleY > highestSampleY)
                {
                    highestSampleY = sampleY;
                    highestTerrainName = terrain.name;
                }

                Debug.Log(
                    $"[PlayerSpawner] Terrain overlap[{overlapCount}] '{terrain.name}': SampleY={sampleY:F2}, BaseY={terrain.GetPosition().y:F2}, " +
                    $"Size={terrain.terrainData.size}, Active={terrain.gameObject.activeInHierarchy}, Enabled={terrain.enabled}");
            }

            if (overlapCount <= 0)
            {
                Debug.LogWarning($"[PlayerSpawner] Spawn terrain diagnostics: no overlapping terrain contains {worldPosition}. Total terrains in scene: {terrains.Length}.");
                return;
            }

            if (overlapCount > 1)
            {
                Debug.LogWarning($"[PlayerSpawner] Spawn terrain diagnostics: detected {overlapCount} overlapping terrains at {worldPosition}. Highest sample terrain: '{highestTerrainName}' ({highestSampleY:F2}).");
            }
        }

        private Terrain ResolveSpawnTerrain(Vector3 spawnPosition)
        {
            Terrain resolvedTerrain = TerrainResolver.ResolveTerrainForPosition(spawnPosition, spawnTerrain);

            if (resolvedTerrain != null)
            {
                spawnTerrain = resolvedTerrain;
            }

            return resolvedTerrain;
        }

        // Camera + input wiring extracted to PlayerSpawnWiringService.

        private void InitializeSquadControlSwap(Unit defaultUnit)
        {
            if (defaultUnit == null)
            {
                return;
            }

            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (!IsControllableSquadUnit(unit))
                {
                    continue;
                }

                PlayerInputController input = EnsurePlayerInputController(unit);
                if (input == null)
                {
                    continue;
                }

                input.enabled = false;
                if (_playerSpawnWiringService != null)
                {
                    worldCamera = _playerSpawnWiringService.EnsureWorldCamera(worldCamera, unit.transform.position);
                    _playerSpawnWiringService.BindCameraToUnit(unit.gameObject, worldCamera);
                    _playerSpawnWiringService.WireInputSystems(unit.gameObject);
                }
            }

            ActivateControlledUnit(defaultUnit, syncPortraitSelection: false);
            _squadControlUiCoordinator?.TryBindPortraitStrips();
        }

        private bool ActivateControlledUnit(Unit unit, bool syncPortraitSelection = true)
        {
            if (!IsControllableSquadUnit(unit))
            {
                return false;
            }

            PlayerInputController targetInput = EnsurePlayerInputController(unit);
            if (targetInput == null)
            {
                return false;
            }

            if (_activeInputController != null && _activeInputController != targetInput)
            {
                _activeInputController.enabled = false;
            }

            DisableOtherControllableInputs(targetInput);

            if (_playerSpawnWiringService != null)
            {
                worldCamera = _playerSpawnWiringService.EnsureWorldCamera(worldCamera, unit.transform.position);
                _playerSpawnWiringService.BindCameraToUnit(unit.gameObject, worldCamera);
                _playerSpawnWiringService.WireInputSystems(unit.gameObject);
            }

            targetInput.enabled = true;
            _activeInputController = targetInput;
            _activeControlledUnit = unit;

            if (_playerSpawnWiringService != null)
            {
                _playerSpawnWiringService.BindHudToUnit(unit);
            }

            if (syncPortraitSelection)
            {
                _squadControlUiCoordinator?.SyncPortraitSelection(unit);
            }

            return true;
        }

        private static PlayerInputController EnsurePlayerInputController(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            PlayerInputController input = unit.GetComponent<PlayerInputController>();
            if (input != null)
            {
                return input;
            }

            return unit.gameObject.AddComponent<PlayerInputController>();
        }

        private void DisableOtherControllableInputs(PlayerInputController activeInput)
        {
            PlayerInputController[] allInputs = FindObjectsByType<PlayerInputController>(FindObjectsSortMode.None);
            for (int i = 0; i < allInputs.Length; i++)
            {
                PlayerInputController input = allInputs[i];
                if (input == null || input == activeInput)
                {
                    continue;
                }

                Unit unit = input.GetComponent<Unit>();
                if (!IsControllableSquadUnit(unit))
                {
                    continue;
                }

                input.enabled = false;
            }
        }

        // Portrait strip + squad control UI extracted to SquadControlUiCoordinator.

        private bool IsControllableSquadUnit(Unit unit)
        {
            if (unit == null || !unit.IsAlive)
            {
                return false;
            }

            if (unit == SpawnedPlayer)
            {
                return true;
            }

            if (unit.GetComponent<SquadMember>() != null)
            {
                return true;
            }

            return unit.Role == UnitRole.Player
                || unit.Role == UnitRole.SquadMember
                || unit.Role == UnitRole.Survivor;
        }

        private System.Collections.IEnumerator SpawnValidationZombieNearPlayer(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                yield break;
            }

            if (validationZombieSpawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(validationZombieSpawnDelaySeconds);
            }
            else
            {
                yield return null;
            }

            ZombieManager zombieManager = FindFirstObjectByType<ZombieManager>();

            if (zombieManager == null)
            {
                GameObject runtimeRoot = GameObject.Find("RuntimeWorldSystems");

                if (runtimeRoot == null)
                {
                    runtimeRoot = new GameObject("RuntimeWorldSystems");
                }

                zombieManager = runtimeRoot.AddComponent<ZombieManager>();
            }

            if (zombieManager == null)
            {
                Debug.LogWarning("[PlayerSpawner] Validation zombie spawn skipped (ZombieManager missing).", this);
                yield break;
            }

            if (!zombieManager.IsInitialized)
            {
                zombieManager.Initialize();
            }

            Vector3 forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
                if (randomDirection.sqrMagnitude <= 0.0001f)
                {
                    randomDirection = Vector2.right;
                }

                forward = new Vector3(randomDirection.x, 0f, randomDirection.y);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float forwardDistance = Mathf.Max(1f, validationZombieSpawnDistanceFromPlayer);
            float lateralJitter = UnityEngine.Random.Range(-Mathf.Max(0f, validationZombieSpawnLateralJitter), Mathf.Max(0f, validationZombieSpawnLateralJitter));

            Vector3 requestedSpawnPosition = playerTransform.position + forward * forwardDistance + right * lateralJitter;

            const int walkableAreaMask = 1 << 0;
            Vector3 navSampleOrigin = requestedSpawnPosition + Vector3.up * 2f;
            bool hasNavSample =
                NavMesh.SamplePosition(navSampleOrigin, out NavMeshHit navHit, 24f, walkableAreaMask) ||
                NavMesh.SamplePosition(navSampleOrigin, out navHit, 24f, NavMesh.AllAreas);

            if (hasNavSample)
            {
                requestedSpawnPosition = navHit.position;
            }

            ZombieAI spawned = zombieManager.SpawnZombie(validationZombieType, requestedSpawnPosition);

            if (spawned == null)
            {
                zombieManager.TickAmbientSpawn(playerTransform.position);
                Debug.LogWarning($"[PlayerSpawner] Validation zombie spawn failed near {requestedSpawnPosition}; requested ambient fallback tick.", this);
                yield break;
            }

            if (logValidationZombieSpawn)
            {
                Vector3 offset = spawned.transform.position - playerTransform.position;
                offset.y = 0f;
                Debug.Log($"[PlayerSpawner] Validation zombie spawned at {spawned.transform.position} (planar distance {offset.magnitude:F1}m from player).", spawned);
            }
        }

        private void PrepareScenePlayerCandidatesForSpawn()
        {
            Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (!IsPlayerCandidate(unit))
                {
                    continue;
                }

                _umaSpawnStylingService?.PrepareUmaAvatarForSanitizedSpawn(unit.gameObject);
                SanitizeRuntimeUnitHierarchy(unit.gameObject);
            }
        }

        private static bool IsPlayerCandidate(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.Role == UnitRole.Player)
            {
                return true;
            }

            return unit.GetComponent<PlayerInputController>() != null;
        }

        private Unit ResolveOwningUnit()
        {
            Unit directUnit = GetComponent<Unit>();
            if (directUnit != null)
            {
                return directUnit;
            }

            Unit parentUnit = GetComponentInParent<Unit>();
            if (parentUnit != null)
            {
                return parentUnit;
            }

            return GetComponentInChildren<Unit>(true);
        }

        private void SanitizeRuntimeUnitHierarchy(GameObject unitRoot)
        {
            _umaSpawnStylingService?.SanitizeRuntimeUnitHierarchy(unitRoot, this);
        }

        private bool IsAttachedToUnit()
        {
            if (GetComponent<Unit>() != null)
            {
                return true;
            }

            if (GetComponentInParent<Unit>() != null)
            {
                return true;
            }

            return GetComponentInChildren<Unit>(true) != null;
        }

        private static UnitInventory EnsureUnitInventory(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            UnitInventory inventory = unit.Inventory;
            if (inventory != null)
            {
                return inventory;
            }

            inventory = unit.GetComponent<UnitInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            return unit.gameObject.AddComponent<UnitInventory>();
        }

        private void ApplyDevModeSpawnInventory(Unit playerUnit)
        {
            if (playerUnit == null || !ShouldApplyDevModeSpawnInventory())
            {
                return;
            }

            UnitInventory inventory = EnsureUnitInventory(playerUnit);

            if (inventory == null)
            {
                Debug.LogWarning("[PlayerSpawner] Failed to resolve UnitInventory for spawned player.", playerUnit);
                return;
            }

            ItemDefinition[] loadoutItems = ResolveDevModeSpawnItems();
            if (loadoutItems == null || loadoutItems.Length == 0)
            {
                if (logDevModeSpawnInventory)
                {
                    Debug.LogWarning("[PlayerSpawner] Dev-mode inventory spawn enabled, but no ItemDefinition assets were found.", this);
                }

                return;
            }

            int quantityPerItem = Mathf.Max(1, devModeSpawnQuantityPerItem);
            int quantityPerAmmoItem = Mathf.Max(1, devModeSpawnAmmoQuantityPerItem);
            float requiredWeightLimit = Mathf.Max(1f, inventory.WeightLimit);

            for (int i = 0; i < loadoutItems.Length; i++)
            {
                ItemDefinition item = loadoutItems[i];
                if (item == null)
                {
                    continue;
                }

                int targetQuantity = ResolveDevModeSpawnTargetQuantity(item, quantityPerItem, quantityPerAmmoItem);
                int currentQuantity = inventory.GetQuantity(item);
                int neededQuantity = Mathf.Max(0, targetQuantity - currentQuantity);
                if (neededQuantity <= 0)
                {
                    continue;
                }

                requiredWeightLimit += Mathf.Max(0f, item.weight) * neededQuantity;
            }

            requiredWeightLimit = Mathf.Max(requiredWeightLimit, devModeSpawnMinimumWeightLimit);
            if (inventory.WeightLimit < requiredWeightLimit)
            {
                inventory.SetWeightLimit(requiredWeightLimit);
            }

            int addedOrSatisfied = 0;
            int failedAdds = 0;

            for (int i = 0; i < loadoutItems.Length; i++)
            {
                ItemDefinition item = loadoutItems[i];
                if (item == null)
                {
                    continue;
                }

                int targetQuantity = ResolveDevModeSpawnTargetQuantity(item, quantityPerItem, quantityPerAmmoItem);
                int currentQuantity = inventory.GetQuantity(item);
                int neededQuantity = Mathf.Max(0, targetQuantity - currentQuantity);

                if (neededQuantity <= 0)
                {
                    addedOrSatisfied++;
                    continue;
                }

                bool added = inventory.AddItem(item, neededQuantity);
                if (added)
                {
                    addedOrSatisfied++;
                }
                else
                {
                    failedAdds++;
                }
            }

            if (logDevModeSpawnInventory)
            {
                Debug.Log(
                    $"[PlayerSpawner] Dev-mode spawn inventory applied: Items={loadoutItems.Length}, Satisfied={addedOrSatisfied}, Failed={failedAdds}, " +
                    $"WeightLimit={inventory.WeightLimit:F1}, CurrentWeight={inventory.CurrentWeight:F1}.",
                    playerUnit);
            }
        }

        private static int ResolveDevModeSpawnTargetQuantity(ItemDefinition item, int quantityPerItem, int quantityPerAmmoItem)
        {
            if (item != null && item.itemType == ItemType.Ammo)
            {
                return Mathf.Max(1, quantityPerAmmoItem);
            }

            return Mathf.Max(1, quantityPerItem);
        }

        private bool ShouldApplyDevModeSpawnInventory()
        {
            if (!IsDevModeEnabled())
            {
                return false;
            }

            DebugSettings debugSettings = DebugManager.Instance != null
                ? DebugManager.Instance.Settings
                : null;

            return debugSettings == null || debugSettings.enableDevSpawnFullInventory;
        }

        private static bool IsDevModeEnabled()
        {
            if (DebugManager.Instance != null)
            {
                return DebugManager.Instance.DebugEnabled;
            }

            return Debug.isDebugBuild;
        }

        private ItemDefinition[] ResolveDevModeSpawnItems()
        {
            List<ItemDefinition> resolved = new List<ItemDefinition>();
            AddUniqueItems(resolved, devModeSpawnInventoryItems);

#if UNITY_EDITOR
            if (resolved.Count == 0)
            {
                string[] itemGuids = UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition");
                for (int i = 0; i < itemGuids.Length; i++)
                {
                    string itemPath = UnityEditor.AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                    ItemDefinition item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                    if (item != null && !resolved.Contains(item))
                    {
                        resolved.Add(item);
                    }
                }
            }
#endif

            if (resolved.Count == 0)
            {
                ItemDefinition[] loadedItems = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                AddUniqueItems(resolved, loadedItems);
            }

            return resolved.ToArray();
        }

        private static void AddUniqueItems(List<ItemDefinition> destination, ItemDefinition[] source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                ItemDefinition item = source[i];
                if (item == null || destination.Contains(item))
                {
                    continue;
                }

                destination.Add(item);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            startupSquadTotalCount = Mathf.Max(1, startupSquadTotalCount);
            startupInitialCharacterCount = Mathf.Max(1, startupInitialCharacterCount);
            minimumStartupSquadTotalCount = Mathf.Max(1, minimumStartupSquadTotalCount);
            startupSquadRingRadius = Mathf.Max(0.25f, startupSquadRingRadius);
            ClampStartupSquadSkillTierValues();

            if (!autoPopulateDevModeSpawnItemsInEditor)
            {
                return;
            }

            List<ItemDefinition> discoveredItems = new List<ItemDefinition>();
            string[] itemGuids = UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition");

            for (int i = 0; i < itemGuids.Length; i++)
            {
                string itemPath = UnityEditor.AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemDefinition item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);

                if (item == null || discoveredItems.Contains(item))
                {
                    continue;
                }

                discoveredItems.Add(item);
            }

            if (IsSameItemSet(devModeSpawnInventoryItems, discoveredItems))
            {
                return;
            }

            devModeSpawnInventoryItems = discoveredItems.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void ClampStartupSquadSkillTierValues()
        {
            if (startupSquadSkillTiers == null || startupSquadSkillTiers.Length == 0)
            {
                startupSquadSkillTiers = (int[])DefaultStartupSquadSkillTiers.Clone();
                return;
            }

            for (int i = 0; i < startupSquadSkillTiers.Length; i++)
            {
                startupSquadSkillTiers[i] = Mathf.Clamp(startupSquadSkillTiers[i], UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
            }
        }

        private static bool IsSameItemSet(ItemDefinition[] existingItems, List<ItemDefinition> discoveredItems)
        {
            if (existingItems == null)
            {
                return discoveredItems == null || discoveredItems.Count == 0;
            }

            if (discoveredItems == null)
            {
                return existingItems.Length == 0;
            }

            if (existingItems.Length != discoveredItems.Count)
            {
                return false;
            }

            for (int i = 0; i < existingItems.Length; i++)
            {
                if (!discoveredItems.Contains(existingItems[i]))
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
