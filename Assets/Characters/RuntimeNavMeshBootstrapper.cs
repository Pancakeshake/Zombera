using System;
using System.Collections;
using MapMagic.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.World;

namespace Zombera.Characters
{
    public readonly struct RuntimeNavMeshStreamingTuning
    {
        public readonly float VoxelSize;
        public readonly int TileSize;
        public readonly float MaxSlopeDegrees;
        public readonly float StepHeightMeters;
        public readonly float MinRegionArea;
        public readonly float VerticalExtent;

        public RuntimeNavMeshStreamingTuning(
            float voxelSize,
            int tileSize,
            float maxSlopeDegrees,
            float stepHeightMeters,
            float minRegionArea,
            float verticalExtent)
        {
            VoxelSize = voxelSize;
            TileSize = tileSize;
            MaxSlopeDegrees = maxSlopeDegrees;
            StepHeightMeters = stepHeightMeters;
            MinRegionArea = minRegionArea;
            VerticalExtent = verticalExtent;
        }
    }

    public readonly struct RuntimeNavMeshRetryTuning
    {
        public readonly int Attempts;
        public readonly float DelaySeconds;

        public RuntimeNavMeshRetryTuning(int attempts, float delaySeconds)
        {
            Attempts = attempts;
            DelaySeconds = delaySeconds;
        }
    }

    public sealed class RuntimeNavMeshBootstrapper
    {
        private readonly MonoBehaviour _host;
        private readonly Func<Scene> _getOwnerScene;
        private readonly Func<Unit> _getSpawnedPlayer;
        private readonly Func<bool> _hasLastNavMeshCenter;
        private readonly Func<Vector3> _getLastNavMeshCenter;
        private readonly Func<Transform> _getSpawnPoint;
        private readonly Func<Vector3> _resolveDefaultSpawnPosition;
        private readonly Func<StreamingNavMeshTileService> _getStreamingService;
        private readonly Func<bool> _tryBuildSceneNavMeshSurfaces;
        private readonly Func<Vector3, bool> _bakeNavMeshGlobal;

        private bool _mapMagicNavMeshRebakeQueued;
        private float _lastMapMagicNavMeshRebakeTime = -999f;

        public RuntimeNavMeshBootstrapper(
            MonoBehaviour host,
            Func<Scene> getOwnerScene,
            Func<Unit> getSpawnedPlayer,
            Func<bool> hasLastNavMeshCenter,
            Func<Vector3> getLastNavMeshCenter,
            Func<Transform> getSpawnPoint,
            Func<Vector3> resolveDefaultSpawnPosition,
            Func<StreamingNavMeshTileService> getStreamingService,
            Func<bool> tryBuildSceneNavMeshSurfaces,
            Func<Vector3, bool> bakeNavMeshGlobal)
        {
            _host = host;
            _getOwnerScene = getOwnerScene;
            _getSpawnedPlayer = getSpawnedPlayer;
            _hasLastNavMeshCenter = hasLastNavMeshCenter;
            _getLastNavMeshCenter = getLastNavMeshCenter;
            _getSpawnPoint = getSpawnPoint;
            _resolveDefaultSpawnPosition = resolveDefaultSpawnPosition;
            _getStreamingService = getStreamingService;
            _tryBuildSceneNavMeshSurfaces = tryBuildSceneNavMeshSurfaces;
            _bakeNavMeshGlobal = bakeNavMeshGlobal;
        }

        public void ResetQueuedRebake()
        {
            _mapMagicNavMeshRebakeQueued = false;
        }

        public void SyncStreamingNavMeshTuning(RuntimeNavMeshStreamingTuning tuning)
        {
            StreamingNavMeshTileService service = _getStreamingService?.Invoke();
            if (service == null)
            {
                return;
            }

            service.ConfigureFromTuning(
                tuning.VoxelSize,
                tuning.TileSize,
                tuning.MaxSlopeDegrees,
                tuning.StepHeightMeters,
                tuning.MinRegionArea,
                tuning.VerticalExtent);
        }

        public bool TryBuildRuntimeNavMesh(Vector3 navMeshCenter, RuntimeNavMeshStreamingTuning tuning)
        {
            if (_tryBuildSceneNavMeshSurfaces != null && _tryBuildSceneNavMeshSurfaces())
            {
                return true;
            }

            StreamingNavMeshTileService streaming = _getStreamingService?.Invoke();
            if (streaming != null && streaming.IsDrivingRuntimeNavMesh)
            {
                SyncStreamingNavMeshTuning(tuning);
                return streaming.RebuildAllDeployedTilesInPlayerScene();
            }

            return _bakeNavMeshGlobal != null && _bakeNavMeshGlobal(navMeshCenter);
        }

        public void StartRetryBake(Vector3 navMeshCenter, RuntimeNavMeshRetryTuning retryTuning, RuntimeNavMeshStreamingTuning streamingTuning)
        {
            if (_host == null)
            {
                return;
            }

            _host.StartCoroutine(RetryBakeNavMesh(navMeshCenter, retryTuning, streamingTuning));
        }

        private IEnumerator RetryBakeNavMesh(Vector3 navMeshCenter, RuntimeNavMeshRetryTuning retryTuning, RuntimeNavMeshStreamingTuning streamingTuning)
        {
            int attempts = Mathf.Clamp(retryTuning.Attempts, 0, 10);
            if (attempts <= 0)
            {
                yield break;
            }

            float retryDelay = Mathf.Max(0.1f, retryTuning.DelaySeconds);

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                yield return new WaitForSeconds(retryDelay);

                if (TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning))
                {
                    Debug.Log($"[PlayerSpawner] NavMesh retry succeeded on attempt {attempt}.", _host);
                    yield break;
                }
            }

            Debug.LogWarning(
                $"[PlayerSpawner] NavMesh retry exhausted after {attempts} attempts near {navMeshCenter}. Agents will use transform fallback movement until a valid NavMesh is available.",
                _host);
        }

        public void QueueThrottledMapMagicNavMeshRebake(
            MapMagicObject mapMagic,
            bool rebakeNavMeshOnMapMagicComplete,
            float mapMagicNavMeshRebakeCooldownSeconds,
            RuntimeNavMeshStreamingTuning streamingTuning)
        {
            if (_host == null || !_host.isActiveAndEnabled)
            {
                return;
            }

            if (!IsWorldSessionStateForNavMeshWork())
            {
                return;
            }

            if (!rebakeNavMeshOnMapMagicComplete)
            {
                return;
            }

            if (mapMagic == null)
            {
                return;
            }

            Scene ownerScene = _getOwnerScene != null ? _getOwnerScene() : default;
            if (ownerScene.IsValid() && mapMagic.gameObject.scene != ownerScene)
            {
                return;
            }

            float cooldown = Mathf.Max(0f, mapMagicNavMeshRebakeCooldownSeconds);
            if (Time.unscaledTime - _lastMapMagicNavMeshRebakeTime < cooldown)
            {
                return;
            }

            if (_mapMagicNavMeshRebakeQueued)
            {
                return;
            }

            _mapMagicNavMeshRebakeQueued = true;
            _host.StartCoroutine(RebakeNavMeshAfterMapMagicComplete(streamingTuning));
        }

        private IEnumerator RebakeNavMeshAfterMapMagicComplete(RuntimeNavMeshStreamingTuning streamingTuning)
        {
            yield return null;

            Vector3 navMeshCenter = ResolveNavMeshRebakeCenter();
            bool navMeshBuilt = TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning);

            StreamingNavMeshTileService streaming = _getStreamingService?.Invoke();
            if (streaming != null && streaming.IsDrivingRuntimeNavMesh)
            {
                streaming.PruneStaleTilesInPlayerScene();
            }

            _lastMapMagicNavMeshRebakeTime = Time.unscaledTime;
            _mapMagicNavMeshRebakeQueued = false;

            if (navMeshBuilt)
            {
                Debug.Log($"[PlayerSpawner] Rebuilt NavMesh after MapMagic completion near {navMeshCenter}.", _host);
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawner] MapMagic completed but NavMesh rebake still found no valid sources near {navMeshCenter}.", _host);
            }
        }

        private Vector3 ResolveNavMeshRebakeCenter()
        {
            Unit spawned = _getSpawnedPlayer != null ? _getSpawnedPlayer() : null;
            if (spawned != null)
            {
                return spawned.transform.position;
            }

            if (_hasLastNavMeshCenter != null && _hasLastNavMeshCenter() && _getLastNavMeshCenter != null)
            {
                return _getLastNavMeshCenter();
            }

            Transform spawnPoint = _getSpawnPoint != null ? _getSpawnPoint() : null;
            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }

            return _resolveDefaultSpawnPosition != null ? _resolveDefaultSpawnPosition() : Vector3.zero;
        }

        public static bool IsWorldSessionStateForNavMeshWork()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return true;
            }

            GameState state = gameManager.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }
    }
}

