#region

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.World;

#endregion

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
        private readonly Func<Vector3> _getLastNavMeshCenter;
        private readonly Func<Scene> _getOwnerScene;
        private readonly Func<Unit> _getSpawnedPlayer;
        private readonly Func<Transform> _getSpawnPoint;
        private readonly Func<StreamingNavMeshTileService> _getStreamingService;
        private readonly Func<bool> _hasLastNavMeshCenter;
        private readonly MonoBehaviour _host;
        private readonly Func<Vector3> _resolveDefaultSpawnPosition;
        private float _lastWorldTileNavMeshRebakeTime = -999f;

        private bool _loggedMissingStreamingAuthorityWarning;
        private bool _worldTileNavMeshRebakeQueued;

        public RuntimeNavMeshBootstrapper(
            MonoBehaviour host,
            Func<Scene> getOwnerScene,
            Func<Unit> getSpawnedPlayer,
            Func<bool> hasLastNavMeshCenter,
            Func<Vector3> getLastNavMeshCenter,
            Func<Transform> getSpawnPoint,
            Func<Vector3> resolveDefaultSpawnPosition,
            Func<StreamingNavMeshTileService> getStreamingService)
        {
            _host = host;
            _getOwnerScene = getOwnerScene;
            _getSpawnedPlayer = getSpawnedPlayer;
            _hasLastNavMeshCenter = hasLastNavMeshCenter;
            _getLastNavMeshCenter = getLastNavMeshCenter;
            _getSpawnPoint = getSpawnPoint;
            _resolveDefaultSpawnPosition = resolveDefaultSpawnPosition;
            _getStreamingService = getStreamingService;
        }

        public void ResetQueuedRebake()
        {
            _worldTileNavMeshRebakeQueued = false;
        }

        public void SyncStreamingNavMeshTuning(RuntimeNavMeshStreamingTuning tuning)
        {
            _ = tuning;
        }

        public bool TryBuildRuntimeNavMesh(Vector3 navMeshCenter, RuntimeNavMeshStreamingTuning tuning)
        {
            if (float.IsNaN(navMeshCenter.x)) return false;

            if (HasNearbyNavMesh(navMeshCenter)) return true;

            if (_getStreamingService?.Invoke() is not { IsDrivingRuntimeNavMesh: true } streaming)
            {
                if (!_loggedMissingStreamingAuthorityWarning)
                {
                    _loggedMissingStreamingAuthorityWarning = true;
                    Debug.LogWarning(
                        "[PlayerSpawner] Runtime navmesh rebuild skipped because StreamingNavMeshTileService is missing or not driving runtime navmesh. Scene/global fallback bakes are disabled.",
                        _host);
                }

                return false;
            }

            _loggedMissingStreamingAuthorityWarning = false;

            SyncStreamingNavMeshTuning(tuning);
            return streaming.RebuildAllDeployedTilesInPlayerScene(navMeshCenter) && HasNearbyNavMesh(navMeshCenter);
        }

        public void StartRetryBake(Vector3 navMeshCenter, RuntimeNavMeshRetryTuning retryTuning,
            RuntimeNavMeshStreamingTuning streamingTuning)
        {
            if (_host == null) return;

            _host.StartCoroutine(RetryBakeNavMesh(navMeshCenter, retryTuning, streamingTuning));
        }

        private IEnumerator RetryBakeNavMesh(Vector3 navMeshCenter, RuntimeNavMeshRetryTuning retryTuning,
            RuntimeNavMeshStreamingTuning streamingTuning)
        {
            var attempts = Mathf.Clamp(retryTuning.Attempts, 0, 10);
            if (attempts <= 0) yield break;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                yield return new WaitForSeconds(Mathf.Max(0.1f, retryTuning.DelaySeconds));

                if (!TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning)) continue;

                Debug.Log($"[PlayerSpawner] NavMesh retry succeeded on attempt {attempt}.", _host);
                yield break;
            }

            Debug.LogWarning(
                $"[PlayerSpawner] NavMesh retry exhausted after {attempts} attempts near {navMeshCenter}. Agents will use transform fallback movement until a valid NavMesh is available.",
                _host);
        }

        public void QueueThrottledWorldTileNavMeshRebake(
            bool rebakeOnComplete,
            float rebakeCooldownSeconds,
            RuntimeNavMeshStreamingTuning streamingTuning)
        {
            if (_host == null || !_host.isActiveAndEnabled) return;
            if (!IsWorldSessionStateForNavMeshWork()) return;
            if (!rebakeOnComplete) return;

            if (Time.unscaledTime - _lastWorldTileNavMeshRebakeTime < Mathf.Max(0f, rebakeCooldownSeconds))
                return;

            if (_worldTileNavMeshRebakeQueued) return;

            _worldTileNavMeshRebakeQueued = true;
            _host.StartCoroutine(RebakeNavMeshAfterWorldTilesReady(streamingTuning));
        }

        private IEnumerator RebakeNavMeshAfterWorldTilesReady(RuntimeNavMeshStreamingTuning streamingTuning)
        {
            yield return null;

            var navMeshCenter = ResolveNavMeshRebakeCenter();
            var streaming = _getStreamingService?.Invoke();
            var usingStreamingService = streaming is { IsDrivingRuntimeNavMesh: true };

            if (usingStreamingService)
            {
                streaming.PruneStaleTilesInPlayerScene();

                _lastWorldTileNavMeshRebakeTime = Time.unscaledTime;
                _worldTileNavMeshRebakeQueued = false;

                Debug.Log(
                    $"[PlayerSpawner] World tile batch near {navMeshCenter}; streaming tile callbacks handle navmesh updates (skipping full rebuild).",
                    _host);
                yield break;
            }

            _lastWorldTileNavMeshRebakeTime = Time.unscaledTime;
            _worldTileNavMeshRebakeQueued = false;

            if (TryBuildRuntimeNavMesh(navMeshCenter, streamingTuning))
                Debug.Log($"[PlayerSpawner] Rebuilt NavMesh after world tiles ready near {navMeshCenter}.", _host);
            else
                Debug.LogWarning(
                    $"[PlayerSpawner] World tiles ready but NavMesh rebake still found no valid sources near {navMeshCenter}.",
                    _host);
        }

        private Vector3 ResolveNavMeshRebakeCenter()
        {
            if (_getSpawnedPlayer?.Invoke() is { } spawned) return spawned.transform.position;

            if (_hasLastNavMeshCenter?.Invoke() == true && _getLastNavMeshCenter != null)
                return _getLastNavMeshCenter.Invoke();

            if (_getSpawnPoint?.Invoke() is { } spawnPoint) return spawnPoint.position;

            return _resolveDefaultSpawnPosition?.Invoke() ?? Vector3.zero;
        }

        private static bool HasNearbyNavMesh(Vector3 worldPosition)
        {
            return UnitNavUtils.IsNavMeshReadyAt(worldPosition);
        }

        private static bool IsWorldSessionStateForNavMeshWork()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null) return true;

            var state = gameManager.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }
    }
}
