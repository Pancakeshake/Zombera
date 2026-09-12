using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private struct QueueProcessingState
        {
            public int MaxBakesThisFrame;
            public int MaxConcurrentAsyncBakes;
            public int Processed;
            public bool BakedAny;
        }

        private void QueueTileBake(WorldTileInfo tile)
        {
            if (tile.Terrain == null) return;

            var coord = tile.Coord;
            _tileBakeVersions[coord] = _nextTileBakeVersion++;

            if (_pendingAsyncTileBakes.TryGetValue(coord, out var pending))
                CancelPendingAsyncTileBakeInternal(coord, pending);

            _pendingTileByCoord[coord] = tile;
            if (_pendingTileBakeSet.Add(coord))
                _pendingTileBakeQueue.Enqueue(coord);
        }

        private int QueueAllCachedTilesForBake(Vector3? focusWorldPosition = null)
        {
            if (worldTileStream == null) return 0;

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.ContentReady, _streamTileScratch);
            if (_streamTileScratch.Count == 0)
            {
                worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);
            }

            if (_streamTileScratch.Count == 0) return 0;

            if (focusWorldPosition.HasValue)
            {
                var focus = focusWorldPosition.Value;
                if (float.IsFinite(focus.x) && float.IsFinite(focus.z))
                {
                    _streamTileScratch.Sort((a, b) =>
                        ComputeTileFocusDistanceSq(a, focus).CompareTo(ComputeTileFocusDistanceSq(b, focus)));
                }
            }

            var enqueued = 0;
            for (var i = 0; i < _streamTileScratch.Count; i++)
            {
                var tile = _streamTileScratch[i];
                if (tile.Terrain == null || tile.Terrain.terrainData == null) continue;
                var before = _pendingTileBakeSet.Count;
                QueueTileBake(tile);
                if (_pendingTileBakeSet.Count > before) enqueued++;
            }

            return enqueued;
        }

        /// <summary>
        ///     After enterable tunnel floors are placed, requeue overlapping ContentReady tiles.
        /// </summary>
        public static void EnqueueOverlappingFloorsIfPresent()
        {
            var service = _runtimeInstance;
            if (service == null || !service.IsDrivingRuntimeNavMesh)
                return;
            if (TunnelRuntimeRegistry.FloorCount == 0)
                return;

            service.EnqueueTilesOverlappingTunnelFloors();
        }

        private void EnqueueTilesOverlappingTunnelFloors()
        {
            if (worldTileStream == null)
                return;

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.ContentReady, _streamTileScratch);
            if (_streamTileScratch.Count == 0)
                worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);

            _tunnelFloorScratch.Clear();
            // Expand query: collect all floors then match tile rects.
            var infinite = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            TunnelRuntimeRegistry.CollectFloorsIntersecting(infinite, _tunnelFloorScratch);
            if (_tunnelFloorScratch.Count == 0)
                return;

            for (var i = 0; i < _streamTileScratch.Count; i++)
            {
                var tile = _streamTileScratch[i];
                if (tile.Terrain == null)
                    continue;

                var rect = tile.WorldRectXZ;
                var tileBounds = new Bounds(
                    new Vector3(rect.center.x, 0f, rect.center.y),
                    new Vector3(rect.width + 16f, 500f, rect.height + 16f));

                var hit = false;
                for (var f = 0; f < _tunnelFloorScratch.Count; f++)
                {
                    var col = _tunnelFloorScratch[f];
                    if (col != null && col.bounds.Intersects(tileBounds))
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                    QueueTileBake(tile);
            }
        }

        private static float ComputeTileFocusDistanceSq(WorldTileInfo tile, Vector3 focusWorldPosition)
        {
            var rect = tile.WorldRectXZ;
            var centerX = rect.x + rect.width * 0.5f;
            var centerZ = rect.y + rect.height * 0.5f;
            var dx = centerX - focusWorldPosition.x;
            var dz = centerZ - focusWorldPosition.z;
            return dx * dx + dz * dz;
        }

        private void ProcessQueuedTileBakes()
        {
            if (!CanProcessQueuedTileBakes()) return;

            var state = CreateQueueProcessingState();
            while (CanContinueQueuedTileBakePass(state))
            {
                if (!TryDequeueNextValidTileForBake(out var tile))
                    continue;

                CompleteQueuedTileBake(ref state, ExecuteQueuedTileBake(tile));
            }

            FinalizeQueuedTileBakePass(state);
        }

        private bool CanProcessQueuedTileBakes()
        {
            if (!IsDrivingRuntimeNavMesh || !IsWorldSessionStateForNavMeshWork() || _pendingTileBakeQueue.Count == 0)
                return false;

            return Time.unscaledTime >= _nextAllowedTileApplyBakeTime;
        }

        private QueueProcessingState CreateQueueProcessingState()
        {
            return new QueueProcessingState
            {
                MaxBakesThisFrame = ResolveTileApplyBakesPerFrame(),
                MaxConcurrentAsyncBakes = ResolveMaxConcurrentAsyncTileBakes(),
                Processed = 0,
                BakedAny = false
            };
        }

        private bool CanContinueQueuedTileBakePass(QueueProcessingState state)
        {
            if (state.Processed >= state.MaxBakesThisFrame || _pendingTileBakeQueue.Count == 0)
                return false;

            if (Time.unscaledTime < _nextAllowedTileApplyBakeTime)
                return false;

            return !useAsyncTileBaking || _pendingAsyncTileBakes.Count < state.MaxConcurrentAsyncBakes;
        }

        private bool TryDequeueNextValidTileForBake(out WorldTileInfo tile)
        {
            tile = default;
            var coord = _pendingTileBakeQueue.Dequeue();
            _pendingTileBakeSet.Remove(coord);

            if (!_pendingTileByCoord.TryGetValue(coord, out tile))
                return false;

            _pendingTileByCoord.Remove(coord);

            if (tile.Terrain == null || tile.Terrain.terrainData == null)
                return false;

            if (tile.Terrain.gameObject != null
                && _ownerScene.IsValid()
                && tile.Terrain.gameObject.scene != _ownerScene)
            {
                LogSceneMismatch("queued tile bake", tile.Terrain.gameObject.scene);
                return false;
            }

            return true;
        }

        private bool ExecuteQueuedTileBake(WorldTileInfo tile)
        {
            var outcome = BakeTileDetailedForQueue(tile);
            if (outcome != TileBakeOutcome.Success && outcome != TileBakeOutcome.InProgress)
            {
                _worldGenerationBackend?.ReportNavigationResult(tile.Coord, false);
                return false;
            }

            if (outcome == TileBakeOutcome.Success)
            {
                LastBootstrapHadTriangles = true;
                _worldGenerationBackend?.ReportNavigationResult(tile.Coord, true);
                return true;
            }

            return false;
        }

        private void CompleteQueuedTileBake(ref QueueProcessingState state, bool bakeSucceeded)
        {
            if (bakeSucceeded)
                state.BakedAny = true;

            state.Processed++;
            _nextAllowedTileApplyBakeTime = Time.unscaledTime + ResolveMinSecondsBetweenTileApplyBakes();
        }

        private void FinalizeQueuedTileBakePass(QueueProcessingState state)
        {
            if (!state.BakedAny) return;
            MaybePruneAfterSuccessfulBake();
        }

        private TileBakeOutcome BakeTileDetailedForQueue(WorldTileInfo tile)
        {
            if (!useAsyncTileBaking)
                return BakeTileDetailed(tile);

            var outcome = TryStartAsyncTileBake(tile);
            return outcome == TileBakeOutcome.NoInstances ? BakeTileDetailed(tile) : outcome;
        }

        private static bool IsWorldSessionStateForNavMeshWork()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null) return true;

            var state = gameManager.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }

        private void EnsureSceneObjectCachesFresh(bool force = false)
        {
            var ownerSceneChanged = _sceneObjectScanOwner.handle != _ownerScene.handle;
            if (!force
                && !ownerSceneChanged
                && Time.unscaledTime - _sceneObjectScanTime < SceneObjectScanMinIntervalSeconds)
                return;

            _sceneObjectScanTime = Time.unscaledTime;
            _sceneObjectScanOwner = _ownerScene;
            _cachedTerrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cachedNavMeshAgents =
                FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void RemoveTile(WorldTileCoord coord)
        {
            if (_pendingAsyncTileBakes.TryGetValue(coord, out var pending))
                CancelPendingAsyncTileBakeInternal(coord, pending);

            if (!_tiles.Remove(coord, out var entry)) return;

            for (var i = 0; i < entry.Instances.Count; i++)
            {
                var inst = entry.Instances[i];
                if (inst.valid) inst.Remove();
            }

            for (var i = 0; i < entry.Datas.Count; i++)
                if (entry.Datas[i] != null)
                    Destroy(entry.Datas[i]);
        }

        private int ResolveRebuildTilesPerFrame()
        {
            if (!useStateBasedBakeBudget)
                return Mathf.Clamp(rebuildTilesPerFrame, 1, 32);

            return IsLoadingWorldState()
                ? Mathf.Clamp(loadingRebuildTilesPerFrame, 1, 32)
                : Mathf.Clamp(rebuildTilesPerFrame, 1, 32);
        }

        private int ResolveTileApplyBakesPerFrame()
        {
            if (!useStateBasedBakeBudget)
                return Mathf.Clamp(tileApplyBakesPerFrame, 1, 8);

            return IsLoadingWorldState()
                ? Mathf.Clamp(loadingTileApplyBakesPerFrame, 1, 8)
                : Mathf.Clamp(worldTileApplyBakesPerFrame, 1, 8);
        }

        private float ResolveMinSecondsBetweenTileApplyBakes()
        {
            if (!useStateBasedBakeBudget)
                return Mathf.Max(0f, minSecondsBetweenTileApplyBakes);

            return IsLoadingWorldState()
                ? Mathf.Max(0f, loadingMinSecondsBetweenTileApplyBakes)
                : Mathf.Max(0f, worldMinSecondsBetweenTileApplyBakes);
        }

        private int ResolveMaxConcurrentAsyncTileBakes()
        {
            if (!useAsyncTileBaking)
                return 1;

            return IsLoadingWorldState()
                ? Mathf.Clamp(loadingMaxConcurrentAsyncTileBakes, 1, 16)
                : Mathf.Clamp(worldMaxConcurrentAsyncTileBakes, 1, 8);
        }

        private static bool IsLoadingWorldState()
        {
            var gameManager = GameManager.Instance;
            return gameManager != null && gameManager.CurrentState == GameState.LoadingWorld;
        }

        private void MaybePruneAfterSuccessfulBake()
        {
            if (Time.unscaledTime < _nextAllowedPruneTime) return;

            PruneStaleTilesInPlayerScene();
            _nextAllowedPruneTime = Time.unscaledTime + Mathf.Max(0.1f, pruneCooldownSeconds);
        }

        private void RemoveAllTiles()
        {
            var keys = new List<WorldTileCoord>(_tiles.Keys);
            for (var i = 0; i < keys.Count; i++) RemoveTile(keys[i]);
        }
    }
}
