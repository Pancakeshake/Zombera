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
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
        public void EnsureProvisionalPlayerForWorldSession()
        {
            if (HasFinalizedWorldPlayerSpawn || SpawnedPlayer != null) return;

            PrepareScenePlayerCandidatesForSpawn();

            var provisionalPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var provisionalRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            var instance = AcquireOrCreatePlayerRootInstance(provisionalPosition, provisionalRotation);
            if (instance == null) return;

            _ = SpawnAppearanceStylingService.PrepareAvatarForSanitizedSpawn(instance);
            SanitizeRuntimeUnitHierarchy(instance);

            var selectedName = CharacterSelectionState.SelectedCharacterName;
            instance.name = string.IsNullOrWhiteSpace(selectedName) ? "Player" : selectedName;

            SpawnedPlayer = instance.GetComponent<Unit>();
            if (SpawnedPlayer == null)
            {
                Debug.LogError("[PlayerSpawner] Provisional player root has no Unit component.", instance);
                return;
            }

            ApplyDevModeSpawnInventory(SpawnedPlayer);
        }

        private IEnumerator DeferredInitialNavMeshAndSpawnRoutine()
        {
            const float registrationTimeout = 45f;
            var registrationStart = Time.unscaledTime;

            while (SpawnedPlayer == null && Time.unscaledTime - registrationStart < registrationTimeout)
            {
                // Staged loading may take multiple frames before GameManager asks for provisional registration.
                // Keep trying here as well so deferred bootstrap cannot time out early and strand world startup.
                EnsureProvisionalPlayerForWorldSession();
                yield return null;
            }

            if (SpawnedPlayer == null)
            {
                Debug.LogWarning(
                    "[PlayerSpawner] Deferred MapMagic NavMesh bootstrap could not confirm a provisional player in time; " +
                    "continuing with fallback spawn path.",
                    this);
                _deferInitialNavMeshThisBootstrap = false;
                RequestFinalWorldSpawn();
                yield break;
            }

            var timeout = Mathf.Max(0f, maxSecondsToWaitForInitialMapMagicNavMesh);
            var start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeout)
            {
                if (SpawnPointSelector.FindFirstActiveMapMagicTerrain(gameObject.scene) != null ||
                    _mapMagicAllCompleteSinceBootstrap) break;

                yield return null;
            }

            if (SpawnPointSelector.FindFirstActiveMapMagicTerrain(gameObject.scene) == null &&
                !_mapMagicAllCompleteSinceBootstrap)
            {
                _ = TryRecoverMissingMapMagicTerrain(gameObject.scene,
                    "Deferred initial NavMesh wait timeout");
            }

            var navMeshCenter = ResolveMainSpawnPosition();
            if (float.IsNaN(navMeshCenter.x))
            {
                if (logSpawnTerrainDiagnostics)
                {
                    Debug.LogWarning(
                        "[PlayerSpawner] Deferred bootstrap could not resolve spawn position (NaN). MapMagic may have failed to generate tiles.",
                        this);
                }

                _deferInitialNavMeshThisBootstrap = false;
                yield break;
            }

            _lastNavMeshCenter = navMeshCenter;
            _hasLastNavMeshCenter = true;

            if (logSpawnTerrainDiagnostics) LogSpawnTerrainDiagnostics(navMeshCenter);

            var streamingTuning = BuildStreamingNavMeshTuning();

            var navMeshBuilt = _runtimeNavMeshBootstrapper != null &&
                               _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning);

            LogFocusedSpawnDiagnostics(
                "Deferred NavMesh build attempted: built=" + navMeshBuilt +
                ", mapMagicAllComplete=" + _mapMagicAllCompleteSinceBootstrap,
                navMeshCenter);

            if (!navMeshBuilt && navMeshRetryAttempts > 0)
            {
                var retryTuning = new RuntimeNavMeshRetryTuning(navMeshRetryAttempts, navMeshRetryDelaySeconds);
                for (var attempt = 1; attempt <= retryTuning.Attempts && !navMeshBuilt; attempt++)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.1f, retryTuning.DelaySeconds));
                    navMeshBuilt = _runtimeNavMeshBootstrapper != null &&
                                   _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(
                                       navMeshCenter,
                                       streamingTuning);

                    if (navMeshBuilt)
                        Debug.Log($"[PlayerSpawner] Deferred bootstrap NavMesh retry succeeded on attempt {attempt}.",
                            this);
                }
            }

            _deferInitialNavMeshThisBootstrap = false;

            if (deferSpawnUntilMapMagicTerrainReady && ShouldDeferSpawnUntilMapMagicTerrainReady(navMeshCenter))
            {
                var spawnTimeout = Mathf.Max(0f, maxSecondsToWaitForMapMagicTerrain);
                yield return WaitForMapMagicTerrainReadyWithRecovery(spawnTimeout,
                    "Deferred spawn terrain wait timeout");

                var updatedCenter = ResolveMainSpawnPosition();
                _lastNavMeshCenter = updatedCenter;
                _hasLastNavMeshCenter = true;

                _ = _runtimeNavMeshBootstrapper != null &&
                    _runtimeNavMeshBootstrapper.TryBuildRuntimeNavMesh(updatedCenter, streamingTuning);
            }

            RequestFinalWorldSpawn();
        }

        private GameObject AcquireOrCreatePlayerRootInstance(Vector3 worldPosition, Quaternion worldRotation)
        {
            var owningUnit = ResolveOwningUnit();
            Unit reuseUnit = null;
            foreach (var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                if (!CanReusePlayerCandidate(u, owningUnit)) continue;

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

            if (float.IsNaN(worldPosition.x))
                worldPosition = spawnPoint != null ? spawnPoint.position : transform.position;

            if (reuseUnit != null)
            {
                var go = reuseUnit.gameObject;
                reuseUnit.SetRole(UnitRole.Player);
                go.transform.position = worldPosition;
                go.transform.rotation = worldRotation;
                if (logSpawnTerrainDiagnostics)
                    Debug.Log(
                        $"[PlayerSpawner] Reusing scene Unit '{go.name}' for provisional spawn at {worldPosition}");

                return go;
            }

            if (playerPrefab == null)
            {
                Debug.LogWarning("[PlayerSpawner] No player prefab assigned and no reusable player Unit was found.",
                    this);
                return null;
            }

            var instance = Instantiate(playerPrefab, worldPosition, worldRotation);
            if (logSpawnTerrainDiagnostics)
                Debug.Log($"[PlayerSpawner] Instantiated provisional Player at {worldPosition}");

            return instance;
        }

        private bool ShouldDeferSpawnUntilMapMagicTerrainReady(Vector3 spawnOrigin)
        {
            // If a valid terrain already contains the origin, do not defer.
            var resolved = ResolveSpawnTerrain(spawnOrigin);
            if (resolved != null && resolved.terrainData != null) return false;

            if (TryGetFirstPartyTileStream(out var stream))
                return !HasGameplayReadyNear(stream, spawnOrigin);

            // If any active streamed/scene terrain exists, do not defer.
            var anyTerrain = SpawnPointSelector.FindFirstActiveTerrain(gameObject.scene);
            if (anyTerrain != null && anyTerrain.terrainData != null) return false;

            // Only defer when a tile stream exists (otherwise we'd stall scenes with static geometry).
            return SpawnPointSelector.HasTileStreamInScene(gameObject.scene);
        }

        private static bool TryGetFirstPartyTileStream(out WorldTileStreamSource stream)
        {
            stream = null;
            var worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager == null || worldManager.WorldGenerationBackend == null)
                return false;

            stream = worldManager.TileStreamBridge ?? worldManager.WorldGenerationBackend.TileStream;
            return stream != null;
        }

        private static bool HasGameplayReadyNear(WorldTileStreamSource stream, Vector3 worldPosition)
        {
            if (stream == null) return false;
            var xz = new Vector2(worldPosition.x, worldPosition.z);
            var buffer = new System.Collections.Generic.List<WorldTileInfo>(16);
            stream.CopyTilesAtOrAbove(WorldTileState.GameplayReady, buffer);
            for (var i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].WorldRectXZ.Contains(xz))
                    return true;
            }

            // Fall back to TerrainReady so spawn is not blocked before content stages land.
            buffer.Clear();
            stream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, buffer);
            for (var i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].WorldRectXZ.Contains(xz) && buffer[i].Terrain != null)
                    return true;
            }

            return false;
        }

        private IEnumerator SpawnPlayerWhenMapMagicTerrainReady()
        {
            var timeout = Mathf.Max(0f, maxSecondsToWaitForMapMagicTerrain);
            yield return WaitForMapMagicTerrainReadyWithRecovery(timeout, "Spawn terrain wait timeout");

            // Re-resolve center after tiles exist.
            var updatedCenter = ResolveMainSpawnPosition();
            _lastNavMeshCenter = updatedCenter;
            _hasLastNavMeshCenter = true;

            RequestFinalWorldSpawn();
        }

        public void RequestFinalWorldSpawn()
        {
            if (HasFinalizedWorldPlayerSpawn) return;
            if (_finalWorldSpawnRoutine != null) return;

            _finalWorldSpawnRoutine = StartCoroutine(FinalizeWorldSpawnWhenDependenciesReady());
        }

        private IEnumerator FinalizeWorldSpawnWhenDependenciesReady()
        {
            BuildOrderedSpawnOptions(out var deferUntilReady, out var fallbackTimeoutSeconds, out var pollSeconds);
            BuildOrderedSpawnTargets(out var terrainTargetSeconds, out var roadTargetSeconds,
                out var buildingTargetSeconds, out var navMeshTargetSeconds, out var objectTargetSeconds);

            if (_worldSpawnCoordinator != null)
            {
                yield return _worldSpawnCoordinator.FinalizeWorldSpawnRoutine(
                    deferUntilReady,
                    fallbackTimeoutSeconds,
                    pollSeconds,
                    terrainTargetSeconds,
                    roadTargetSeconds,
                    buildingTargetSeconds,
                    navMeshTargetSeconds,
                    objectTargetSeconds);
            }
            else
            {
                SpawnPlayer();
            }

            _finalWorldSpawnRoutine = null;
        }

        private void BuildOrderedSpawnOptions(
            out bool deferUntilReady,
            out float fallbackTimeoutSeconds,
            out float pollSeconds)
        {
            deferUntilReady = deferFinalSpawnUntilWorldDependenciesReady;
            fallbackTimeoutSeconds = Mathf.Max(0f, fallbackOrderedSpawnTimeoutSeconds);
            pollSeconds = Mathf.Max(0.05f, orderedSpawnDependencyPollSeconds);
        }

        private void BuildOrderedSpawnTargets(
            out float terrainTargetSeconds,
            out float roadTargetSeconds,
            out float buildingTargetSeconds,
            out float navMeshTargetSeconds,
            out float objectTargetSeconds)
        {
            terrainTargetSeconds = Mathf.Max(0f, orderedSpawnTerrainStageTargetSeconds);
            roadTargetSeconds = Mathf.Max(0f, orderedSpawnRoadStageTargetSeconds);
            buildingTargetSeconds = Mathf.Max(0f, orderedSpawnBuildingStageTargetSeconds);
            navMeshTargetSeconds = Mathf.Max(0f, orderedSpawnNavMeshStageTargetSeconds);
            objectTargetSeconds = Mathf.Max(0f, orderedSpawnObjectStageTargetSeconds);
        }

        private WorldManager ResolveWorldManagerForSpawnOrder()
        {
            if (_worldManagerForSpawnOrder != null) return _worldManagerForSpawnOrder;

            _worldManagerForSpawnOrder = FindFirstObjectByType<WorldManager>();
            return _worldManagerForSpawnOrder;
        }

        private void StabilizeMapMagicGeneration()
        {
            // Intentionally empty: Legacy MapMagic stabilizers self-enable in scene without World calling them.
            _ = stabilizeMapMagicGenerationInPlayMode;
            _ = mapMagicStreamingProfile;
            _ = productionStreamingMainRange;
            _ = productionStreamingGenerateRange;
            _ = productionStreamingRetainMargin;
            _ = freezeMapMagicExpansionInPlayMode;
            _ = disableMapMagicInfiniteGenerationAtRuntime;
            _ = disableMapMagicCameraTrackerGenerationAtRuntime;
            _ = skipMapMagicSwitchLodsInPlayMode;
        }

        private bool TryRecoverMissingMapMagicTerrain(Scene scene, string context)
        {
            _ = scene;
            _ = context;
            return SpawnPointSelector.FindFirstActiveTerrain(gameObject.scene) != null;
        }

        private bool HasActiveMapMagicTerrain()
        {
            return SpawnPointSelector.FindFirstActiveTerrain(gameObject.scene) != null
                   || SpawnPointSelector.HasGameplayReadyTile(gameObject.scene);
        }

        private IEnumerator WaitForMapMagicTerrainReadyWithRecovery(float timeoutSeconds, string timeoutContext)
        {
            var timeout = Mathf.Max(0f, timeoutSeconds);
            var start = Time.unscaledTime;
            var spawnOrigin = ResolveMainSpawnPosition();

            if (TryGetFirstPartyTileStream(out var stream))
            {
                while (Time.unscaledTime - start < timeout)
                {
                    if (HasGameplayReadyNear(stream, spawnOrigin)
                        || SpawnPointSelector.HasGameplayReadyTile(gameObject.scene))
                        yield break;
                    yield return null;
                }

                yield break;
            }

            while (Time.unscaledTime - start < timeout)
            {
                if (HasActiveMapMagicTerrain()) yield break;
                yield return null;
            }

            _ = timeoutContext;
            _ = TryRecoverMissingMapMagicTerrain(gameObject.scene, timeoutContext);
        }

    }
}
