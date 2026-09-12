using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Debugging;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private struct DeferredRebuildProgress
        {
            public bool Any;
            public int BakedTileCount;
            public int BakedThisFrame;
        }

        public bool RebuildAllDeployedTilesInPlayerScene(Vector3? focusWorldPosition = null)
        {
            if (!IsDrivingRuntimeNavMesh) return false;
            if (!IsWorldSessionStateForNavMeshWork()) return false;

            if (_rebuildAllCoroutine != null)
                return LastBootstrapHadTriangles || HasAnyBakedTiles;

            if (_pendingTileBakeQueue.Count > 0)
                return LastBootstrapHadTriangles || HasAnyBakedTiles;

            EnsureOwnerScene();
            using (PerfTrace.Measure("NavMesh: EnsureSceneObjectCachesFresh", this))
            {
                EnsureSceneObjectCachesFresh();
            }

            var queuedTileCount = QueueAllCachedTilesForBake(focusWorldPosition);
            if (queuedTileCount > 0 || _pendingTileBakeQueue.Count > 0)
            {
                LogFocusedDiagnostics("Bootstrap queued tile bakes=" + queuedTileCount, 0, false);
                return LastBootstrapHadTriangles || HasAnyBakedTiles;
            }

            if (!allowBootstrapFullRebuildFallback)
            {
                LogFocusedDiagnostics("Bootstrap skipped full rebuild (no active tiles queued)", 0, false);
                return LastBootstrapHadTriangles || HasAnyBakedTiles;
            }

            LogFocusedDiagnostics("Bootstrap rebuild requested", 0, false);
            _rebuildAllCoroutine = StartCoroutine(RebuildAllTilesDeferred());
            return LastBootstrapHadTriangles;
        }

        [ContextMenu("Bake All Cached Tiles (Debug)")]
        private void BakeAllCachedTilesFromContextMenu()
        {
            EnsureOwnerScene();
            EnsureSceneObjectCachesFresh();
            var any = BakeAllCachedTiles(out var bakedTileCount, out var diagnostics);
            LastBootstrapHadTriangles = any && HasAnyNavMeshTriangles();
            LogFocusedDiagnostics(
                "Context-menu rebuild completed " + FormatPassDiagnostics(diagnostics),
                bakedTileCount,
                any);
        }

        private void EnsureOwnerScene()
        {
            var previousOwnerScene = _ownerScene;

            if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (playerSpawner != null) _ownerScene = playerSpawner.gameObject.scene;

            if (!_ownerScene.IsValid() && worldTileStream != null)
                _ownerScene = worldTileStream.gameObject.scene;

            if (logFocusedNavMeshDiagnostics
                && previousOwnerScene.handle != _ownerScene.handle
                && _ownerScene.IsValid())
            {
                Debug.Log(
                    "[StreamingNavMeshTileService] Owner scene resolved to '" + SceneLabel(_ownerScene) +
                    "'. (previous='" + SceneLabel(previousOwnerScene) + "')",
                    this);
            }
        }

        private bool BakeAllCachedTiles(
            out int bakedTileCount,
            out TileBakePassDiagnostics diagnostics)
        {
            bakedTileCount = 0;
            diagnostics = new TileBakePassDiagnostics();
            if (worldTileStream == null) return false;

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.ContentReady, _streamTileScratch);
            if (_streamTileScratch.Count == 0)
                worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);

            var any = false;
            for (var i = 0; i < _streamTileScratch.Count; i++)
            {
                diagnostics.TileEntriesVisited++;
                var tile = _streamTileScratch[i];
                if (tile.Terrain == null)
                {
                    diagnostics.NullTiles++;
                    continue;
                }

                var outcome = BakeTileDetailed(tile);
                ApplyBakeOutcomeToDiagnostics(outcome, ref diagnostics, ref any, ref bakedTileCount);
            }

            return any;
        }

        private static void ApplyBakeOutcomeToDiagnostics(
            TileBakeOutcome outcome,
            ref TileBakePassDiagnostics diagnostics,
            ref bool any,
            ref int bakedTileCount)
        {
            switch (outcome)
            {
                case TileBakeOutcome.Success:
                    diagnostics.Successes++;
                    any = true;
                    bakedTileCount++;
                    break;
                case TileBakeOutcome.MissingTerrain:
                    diagnostics.MissingTerrain++;
                    break;
                case TileBakeOutcome.NoSources:
                    diagnostics.NoSources++;
                    break;
                case TileBakeOutcome.NoInstances:
                    diagnostics.NoInstances++;
                    break;
            }
        }

        private IEnumerator RebuildAllTilesDeferred()
        {
            var perFrame = ResolveRebuildTilesPerFrame();
            var diagnostics = new TileBakePassDiagnostics();
            var progress = new DeferredRebuildProgress();

            if (worldTileStream == null)
            {
                _rebuildAllCoroutine = null;
                yield break;
            }

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.ContentReady, _streamTileScratch);
            if (_streamTileScratch.Count == 0)
                worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);

            for (var i = 0; i < _streamTileScratch.Count; i++)
            {
                ProcessDeferredRebuildTileEntry(_streamTileScratch[i], ref diagnostics, ref progress);
                if (!ShouldYieldDeferredRebuildSlice(ref progress, perFrame)) continue;

                if (progress.Any) LastBootstrapHadTriangles = true;
                yield return null;

                if (CanContinueDeferredRebuild()) continue;

                LogFocusedDiagnostics(
                    "Deferred rebuild aborted (runtime state changed) " + FormatPassDiagnostics(diagnostics),
                    progress.BakedTileCount,
                    progress.Any);
                _rebuildAllCoroutine = null;
                yield break;
            }

            LastBootstrapHadTriangles = progress.Any || HasAnyBakedTiles;
            LogFocusedDiagnostics(
                "Deferred rebuild completed " + FormatPassDiagnostics(diagnostics),
                progress.BakedTileCount,
                progress.Any);
            _rebuildAllCoroutine = null;
        }

        private void ProcessDeferredRebuildTileEntry(
            WorldTileInfo tile,
            ref TileBakePassDiagnostics diagnostics,
            ref DeferredRebuildProgress progress)
        {
            diagnostics.TileEntriesVisited++;
            if (tile.Terrain == null)
            {
                diagnostics.NullTiles++;
                progress.BakedThisFrame++;
                return;
            }

            ApplyDeferredRebuildTileOutcome(BakeTileDetailed(tile), ref diagnostics, ref progress);
            progress.BakedThisFrame++;
        }

        private static void ApplyDeferredRebuildTileOutcome(
            TileBakeOutcome outcome,
            ref TileBakePassDiagnostics diagnostics,
            ref DeferredRebuildProgress progress)
        {
            switch (outcome)
            {
                case TileBakeOutcome.Success:
                    diagnostics.Successes++;
                    progress.Any = true;
                    progress.BakedTileCount++;
                    break;
                case TileBakeOutcome.MissingTerrain:
                    diagnostics.MissingTerrain++;
                    break;
                case TileBakeOutcome.NoSources:
                    diagnostics.NoSources++;
                    break;
                case TileBakeOutcome.NoInstances:
                    diagnostics.NoInstances++;
                    break;
            }
        }

        private static bool ShouldYieldDeferredRebuildSlice(ref DeferredRebuildProgress progress, int perFrame)
        {
            if (progress.BakedThisFrame < perFrame)
                return false;

            progress.BakedThisFrame = 0;
            return true;
        }

        private bool CanContinueDeferredRebuild()
        {
            return IsDrivingRuntimeNavMesh && IsWorldSessionStateForNavMeshWork();
        }

        /// <summary>
        ///     Removes NavMesh tiles whose coords are no longer present on the tile stream.
        /// </summary>
        public void PruneStaleTilesInPlayerScene()
        {
            if (!_ownerScene.IsValid() || worldTileStream == null) return;

            EnsureSceneObjectCachesFresh();
            var live = _liveCoordBuffer;
            live.Clear();

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);
            for (var i = 0; i < _streamTileScratch.Count; i++)
                live.Add(_streamTileScratch[i].Coord);

            var stale = _staleCoordBuffer;
            stale.Clear();
            foreach (var key in _tiles.Keys)
                if (!live.Contains(key))
                    stale.Add(key);

            for (var i = 0; i < stale.Count; i++) RemoveTile(stale[i]);
        }
    }
}
