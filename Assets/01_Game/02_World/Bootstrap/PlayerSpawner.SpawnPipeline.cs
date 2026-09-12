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
        private RuntimeNavMeshStreamingTuning BuildStreamingNavMeshTuning()
        {
            var profile = MovementGroundingSettings.Active;
            var voxelSize = profile.NavMeshVoxelSize;
            var tileSize = profile.NavMeshTileSize;
            var minRegionArea = profile.NavMeshMinRegionArea;

            if (useCoarseStreamingNavMeshTuning)
            {
                voxelSize = Mathf.Max(0.1f, voxelSize * Mathf.Max(1f, streamingNavMeshVoxelScale));
                tileSize = Mathf.Clamp(Mathf.Max(tileSize, streamingNavMeshMinTileSize), 64, 1024);
                minRegionArea = Mathf.Max(minRegionArea, streamingNavMeshMinRegionAreaFloor);
            }

            return new RuntimeNavMeshStreamingTuning(
                voxelSize,
                tileSize,
                profile.NavMeshMaxSlopeDegrees,
                profile.NavMeshStepHeightMeters,
                minRegionArea,
                profile.NavMeshVerticalHalfExtent);
        }

        private StartupSquadSpawnConfig BuildStartupSquadConfig()
        {
            return new StartupSquadSpawnConfig(
                spawnStartupSquadOnWorldStart,
                startupSquadTotalCount,
                minimumStartupSquadTotalCount,
                startupInitialCharacterCount,
                startupSquadRingRadius,
                randomizeStartupSquadVisualVariants,
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

        private void HandleMapMagicAllComplete()
        {
            _mapMagicAllCompleteSinceBootstrap = true;

            if (_deferInitialNavMeshThisBootstrap) return;

            _runtimeNavMeshBootstrapper?.QueueThrottledWorldTileNavMeshRebake(
                rebakeNavMeshOnMapMagicComplete,
                mapMagicNavMeshRebakeCooldownSeconds,
                BuildStreamingNavMeshTuning());
        }

        private void SpawnPlayer()
        {
            var spawnPosition = ResolveMainSpawnPosition();
            if (float.IsNaN(spawnPosition.x))
            {
                if (ShouldDeferSpawnUntilMapMagicTerrainReady(spawnPosition))
                {
                    if (logSpawnTerrainDiagnostics)
                        Debug.Log(
                            "[PlayerSpawner] SpawnPlayer deferred until MapMagic terrain is ready (spawn position unresolved).",
                            this);

                    StartCoroutine(SpawnPlayerWhenMapMagicTerrainReady());
                    return;
                }

                spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            }

            var spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            LogFocusedSpawnDiagnostics("SpawnPlayer starting", spawnPosition);

            if (_playerSpawnSnapper != null)
                spawnPosition = _playerSpawnSnapper.SnapSpawnPosition(
                    spawnPosition,
                    spawnPoint == null,
                    transform.position,
                    _hasLastNavMeshCenter,
                    _lastNavMeshCenter,
                    BuildSpawnSnapConfig());

            var instance = AcquireOrCreatePlayerRootInstance(spawnPosition, spawnRotation);
            if (instance == null) return;

            var avatarRoot = SpawnAppearanceStylingService.PrepareAvatarForSanitizedSpawn(instance);
            SanitizeRuntimeUnitHierarchy(instance);

            var selectedName = CharacterSelectionState.SelectedCharacterName;
            instance.name = string.IsNullOrWhiteSpace(selectedName) ? "Player" : selectedName;

            // Re-enable player-specific components that were disabled during sanitization
            SpawnAppearanceStylingService.EnablePlayerSpecificComponents(instance);

            SpawnedPlayer = instance.GetComponent<Unit>();

            if (SpawnedPlayer == null)
            {
                Debug.LogError("[PlayerSpawner] Spawned prefab has no Unit component.", instance);
                return;
            }

            ApplyDevModeSpawnInventory(SpawnedPlayer);

            // NavMesh is already baked — enable/warp the agent now.
            var unitController = instance.GetComponent<UnitController>();
            if (unitController != null)
            {
                unitController.LogGroundingState("SpawnPlayer.BeforeEnableAgent");
                if (TryHasNearbyNavMesh(instance.transform.position))
                {
                    unitController.ForceEnableAgent();
                }
                else
                {
                    StartCoroutine(ForceEnableAgentWhenNavMeshReady(unitController));
                }
                unitController.LogGroundingState("SpawnPlayer.AfterEnableAgent");
            }

            EnsureStartupTestSquad(SpawnedPlayer);
            InitializeSquadControlSwap(SpawnedPlayer);
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            // Fallback: ensure the active player's input controller is enabled after spawn wiring.
            // This protects against rare startup-order races where selection logic disables inputs
            // before IsAlive/state flags are fully settled.
            var activeInput = instance.GetComponent<PlayerInputController>();
            if (activeInput != null && !activeInput.enabled)
                activeInput.enabled = true;

            // Apply menu/creator appearance for new games only; load sessions restore from save data later.
            if (!isLoadingSaveSession && (GameManager.Instance == null || !GameManager.Instance.IsLoadingSession))
            {
                var rewarpController = instance.GetComponent<UnitController>();
                var profileJson = CharacterSelectionState.SelectedAppearanceProfileJson;
                _spawnAppearanceStylingService?.StartApplySelectedAppearanceNextFrame(instance, profileJson, avatarRoot,
                    rewarpController);
            }

            if (spawnValidationZombieNearPlayer && !_requestedValidationZombieSpawn)
            {
                _requestedValidationZombieSpawn = true;
                StartCoroutine(SpawnValidationZombieNearPlayer(instance.transform));
            }

            HasFinalizedWorldPlayerSpawn = true;
            LogFocusedSpawnDiagnostics("SpawnPlayer completed", SpawnedPlayer != null ? SpawnedPlayer.transform.position : spawnPosition);
        }

        private void EnsureStartupTestSquad(Unit playerUnit)
        {
            if (isLoadingSaveSession || (GameManager.Instance != null && GameManager.Instance.IsLoadingSession))
            {
                LogFocusedSpawnDiagnostics("EnsureStartupTestSquad skipped: loading existing save session", playerUnit != null ? playerUnit.transform.position : transform.position);
                return;
            }

            if (_startupSquadSpawner == null) return;
            if (playerUnit == null) return;

            LogFocusedSpawnDiagnostics("EnsureStartupTestSquad invoked", playerUnit.transform.position);

            if (!TryHasNearbyNavMesh(playerUnit.transform.position))
            {
                if (!_startupSquadSpawnQueuedForNavMesh)
                {
                    _startupSquadSpawnQueuedForNavMesh = true;
                    StartCoroutine(EnsureStartupSquadWhenNavMeshReady(playerUnit));
                }

                LogFocusedSpawnDiagnostics("EnsureStartupTestSquad waiting for NavMesh", playerUnit.transform.position);

                return;
            }

            _startupSquadSpawnQueuedForNavMesh = false;

            var config = BuildStartupSquadConfig();
            if (deferStartupSquadSpawning)
                _startupSquadSpawner.StartEnsureStartupSquadDeferred(playerUnit, config, startupSquadSpawnPerFrame);
            else
                _ = _startupSquadSpawner.EnsureStartupSquad(playerUnit, config);

            LogFocusedSpawnDiagnostics(
                "EnsureStartupTestSquad requested spawn (defer=" + deferStartupSquadSpawning + ")",
                playerUnit.transform.position);
        }

        public int EnsureLoadedSaveSquadMembers(int savedSquadCount)
        {
            var requiredMembers = Mathf.Max(0, savedSquadCount);
            if (requiredMembers <= 0) return 0;

            if (!TryResolveLoadBootstrapContext(out var playerUnit, out var unitManager))
            {
                LogFocusedSpawnDiagnostics(
                    "EnsureLoadedSaveSquadMembers skipped: missing player or startup squad spawner");
                return 0;
            }

            const int maxBootstrapAttempts = 3;
            var ensuredCount = BootstrapLoadSquadMembers(playerUnit, unitManager, requiredMembers, maxBootstrapAttempts);

            LogFocusedSpawnDiagnostics(
                "EnsureLoadedSaveSquadMembers requested=" + requiredMembers + ", ensured=" + ensuredCount,
                playerUnit.transform.position);

            if (ensuredCount < requiredMembers)
            {
                Debug.LogWarning(
                    "[PlayerSpawner] EnsureLoadedSaveSquadMembers could not reach required saved squad count. " +
                    "Required=" + requiredMembers + ", ActiveAfterBootstrap=" + ensuredCount + ".",
                    this);
            }

            return ensuredCount;
        }

    }
}
