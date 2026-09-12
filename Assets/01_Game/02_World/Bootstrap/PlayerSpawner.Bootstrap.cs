#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

namespace Zombera.Characters
{
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
        private WorldSpawnCoordinator _worldSpawnCoordinator;

        public bool TryGetOrderedSpawnDependencyProgress(out float progress01, out string status)
        {
            if (_worldSpawnCoordinator != null)
                return _worldSpawnCoordinator.TryGetProgress(out progress01, out status);

            progress01 = 0f;
            status = string.Empty;
            return false;
        }

        private void Awake()
        {
            if (IsAttachedToUnit())
            {
                var ownerUnit = ResolveOwningUnit();
                if (ownerUnit != null) SanitizeRuntimeUnitHierarchy(ownerUnit.gameObject);

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
                streamingNavMeshTileService = FindFirstObjectByType<StreamingNavMeshTileService>();

            _runtimeNavMeshBootstrapper = new RuntimeNavMeshBootstrapper(
                this,
                () => gameObject.scene,
                () => SpawnedPlayer,
                () => _hasLastNavMeshCenter,
                () => _lastNavMeshCenter,
                () => spawnPoint,
                ResolveDefaultSpawnPosition,
                () => streamingNavMeshTileService);

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

            _devModeInventoryHelper = new DevModeInventoryHelper(this);
            _spawnAppearanceStylingService = new SpawnAppearanceStylingService(this);
            _squadControlUiCoordinator = new SquadControlUiCoordinator(
                IsControllableSquadUnit,
                unit => ActivateControlledUnit(unit),
                () => _activeControlledUnit);
            _worldSpawnCoordinator = new WorldSpawnCoordinator(this, SpawnPlayer, ResolveWorldManagerForSpawnOrder);
            TryBeginSpawnBootstrap();
        }

        private void Update()
        {
            if (!_spawnBootstrapStarted)
            {
                if (IsAttachedToUnit()) return;

                TryBeginSpawnBootstrap();
                return;
            }

            if (_activeControlledUnit == null) return;

            _portraitStripBindRefreshTicker += Time.unscaledDeltaTime;
            if (_portraitStripBindRefreshTicker < Mathf.Max(0.05f, portraitStripBindRefreshIntervalSeconds)) return;

            _portraitStripBindRefreshTicker = 0f;
            _squadControlUiCoordinator?.TickBindPortraitStrips();
        }

        public bool TryWarmupCriticalPrefabsStep()
        {
            if (!_loadingWarmupPlayerPrefabDone)
            {
                _loadingWarmupPlayerPrefabDone = WarmupPrefabAsset(playerPrefab);
                return false;
            }

            if (!_loadingWarmupSquadPrefabDone)
            {
                var squadPrefab = startupSquadMemberPrefab != null ? startupSquadMemberPrefab : playerPrefab;
                _loadingWarmupSquadPrefabDone = WarmupPrefabAsset(squadPrefab);
                return _loadingWarmupSquadPrefabDone;
            }

            return true;
        }

        private static bool WarmupPrefabAsset(GameObject prefab)
        {
            if (prefab == null) return true;

            // Touch commonly expensive asset references so they are loaded before world interactivity starts.
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                var sharedMaterials = renderer.sharedMaterials;
                foreach (var material in sharedMaterials)
                {
                    if (material == null) continue;

                    _ = material.shader;
                    if (material.HasProperty(MainTexShaderPropertyId)) _ = material.GetTexture(MainTexShaderPropertyId);
                }
            }

            var animators = prefab.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
                if (animator != null)
                    _ = animator.runtimeAnimatorController;

            return true;
        }

        private void OnEnable()
        {
            if (IsAttachedToUnit()) return;
        }

        private void OnDisable()
        {
            _runtimeNavMeshBootstrapper?.ResetQueuedRebake();
            _worldSpawnCoordinator?.ResetTracking();
            _squadControlUiCoordinator?.UnbindPortraitStripCallbacks();
            _finalWorldSpawnRoutine = null;
        }

        private void OnDestroy()
        {
            _squadControlUiCoordinator?.UnbindPortraitStripCallbacks();
        }

        public int GetExpectedStartupSquadTotalRosterCount()
        {
            // Player + startup squad total expected at world entry for loading gate.
            // If startup squad is disabled, still expect at least the player.
            var expected = spawnStartupSquadOnWorldStart ? startupSquadTotalCount : 1;
            return Mathf.Max(1, expected);
        }

        private void TryBeginSpawnBootstrap()
        {
            if (_spawnBootstrapStarted || !CanBeginSpawnBootstrap()) return;

            _spawnBootstrapStarted = true;
            _loggedSpawnDeferral = false;

            BeginSpawnBootstrap();
        }

        private bool CanBeginSpawnBootstrap()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null) return true;

            var state = gameManager.CurrentState;
            var canBegin = state is GameState.LoadingWorld or GameState.Playing or GameState.Paused;
            if (canBegin || _loggedSpawnDeferral) return canBegin;

            Debug.Log($"[PlayerSpawner] Deferring world spawn while GameManager state is '{state}'.", this);
            _loggedSpawnDeferral = true;
            return false;
        }

        private void BeginSpawnBootstrap()
        {
            if (logSpawnTerrainDiagnostics) Debug.Log("[PlayerSpawner] Beginning world spawn bootstrap.", this);
            LogFocusedSpawnDiagnostics("BeginSpawnBootstrap");

            HasFinalizedWorldPlayerSpawn = false;
            _worldSpawnCoordinator?.ResetTracking();
            _mapMagicAllCompleteSinceBootstrap = false;
            _deferInitialNavMeshThisBootstrap = false;

            PrepareScenePlayerCandidatesForSpawn();

            // MapMagic stabilization (if needed) is owned by Legacy scene components, not World.

            var streamingTuning = BuildStreamingNavMeshTuning();
            _runtimeNavMeshBootstrapper?.SyncStreamingNavMeshTuning(streamingTuning);

            var tileStreamPresent = SpawnPointSelector.HasTileStreamInScene(gameObject.scene);
            var deferInitialNavMesh = deferInitialNavMeshUntilMapMagicReady && tileStreamPresent;

            if (deferInitialNavMesh)
            {
                _deferInitialNavMeshThisBootstrap = true;
                StartCoroutine(DeferredInitialNavMeshAndSpawnRoutine());
                return;
            }

            var navMeshCenter = ResolveMainSpawnPosition();
            if (float.IsNaN(navMeshCenter.x))
            {
                if (logSpawnTerrainDiagnostics)
                    Debug.Log("[PlayerSpawner] Main spawn position is NaN. Deferring initial NavMesh build until world tiles are ready.", this);

                if (tileStreamPresent || deferSpawnUntilMapMagicTerrainReady)
                {
                    StartCoroutine(SpawnPlayerWhenMapMagicTerrainReady());
                    return;
                }

                navMeshCenter = Vector3.zero;
            }

            _lastNavMeshCenter = navMeshCenter;
            _hasLastNavMeshCenter = true;

            if (logSpawnTerrainDiagnostics) LogSpawnTerrainDiagnostics(navMeshCenter);

            var navMeshBuilt = _runtimeNavMeshBootstrapper != null &&
                               _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning);

            LogFocusedSpawnDiagnostics(
                "Initial NavMesh build attempted: built=" + navMeshBuilt +
                ", deferInitialNavMesh=" + deferInitialNavMesh,
                navMeshCenter);

            if (!navMeshBuilt && navMeshRetryAttempts > 0)
                _runtimeNavMeshBootstrapper?.StartRetryBake(
                    navMeshCenter,
                    new RuntimeNavMeshRetryTuning(navMeshRetryAttempts, navMeshRetryDelaySeconds),
                    streamingTuning);

            if (deferSpawnUntilMapMagicTerrainReady && ShouldDeferSpawnUntilMapMagicTerrainReady(navMeshCenter))
            {
                StartCoroutine(SpawnPlayerWhenMapMagicTerrainReady());
                return;
            }

            // Note: Final spawn is now requested explicitly by GameManager during the 'Players' loading stage 
            // to ensure all world dependencies (Roads, Buildings, NavMesh) are fully settled.
            if (GameManager.Instance == null)
            {
                RequestFinalWorldSpawn();
            }
        }

        /// <summary>
        ///     Instantiates (or reuses) the player at a conservative position so <see cref="WorldManager" /> and character
        ///     selection
        ///     can run before MapMagic terrain and NavMesh are ready. Does not snap to NavMesh or enable the NavMeshAgent.
        /// </summary>
    }
}
