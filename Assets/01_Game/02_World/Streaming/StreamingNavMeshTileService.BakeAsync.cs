using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private TileBakeOutcome TryStartAsyncTileBake(WorldTileInfo tile)
        {
            var bakeStartedAt = Time.realtimeSinceStartup;
            var sourceCount = 0;
            var agentTypeCount = 0;

            var terrain = tile.Terrain;
            if (terrain == null || terrain.terrainData == null) return TileBakeOutcome.MissingTerrain;

            RemoveTile(tile.Coord);

            var planarBounds = BuildTileBounds(terrain, tile.WorldRectXZ);
            EnsureSceneObjectCachesFresh();

            var sources = BuildNavMeshSources(terrain, planarBounds);
            sourceCount = sources.Count;
            if (sourceCount == 0) return TileBakeOutcome.NoSources;

            var agentTypeIds = CollectAgentTypeIds();
            agentTypeCount = agentTypeIds.Count;

            var sourceSnapshot = new List<NavMeshBuildSource>(sources.Count);
            sourceSnapshot.AddRange(sources);

            var started = StartAsyncBuildAndRegisterInstances(
                tile,
                planarBounds,
                sourceSnapshot,
                agentTypeIds,
                sourceCount,
                agentTypeCount,
                bakeStartedAt);

            return started ? TileBakeOutcome.InProgress : TileBakeOutcome.NoInstances;
        }

        private bool StartAsyncBuildAndRegisterInstances(
            WorldTileInfo tile,
            Bounds planarBounds,
            List<NavMeshBuildSource> sourceSnapshot,
            HashSet<int> agentTypeIds,
            int sourceCount,
            int agentTypeCount,
            float bakeStartedAt)
        {
            var coord = tile.Coord;
            if (!_tileBakeVersions.TryGetValue(coord, out var version))
                version = _nextTileBakeVersion++;

            var pending = new PendingAsyncTileBake
            {
                Coord = coord,
                Version = version,
                StartedAtRealtime = bakeStartedAt,
                SourceCount = sourceCount,
                AgentTypeCount = agentTypeCount,
                Sources = sourceSnapshot
            };

            foreach (var agentTypeId in agentTypeIds)
            {
                var settings = NavMesh.GetSettingsByID(agentTypeId);
                ApplyActiveNavMeshBuildSettings(ref settings);

                var data = new NavMeshData(agentTypeId);
                var instance = NavMesh.AddNavMeshData(data);
                if (!instance.valid)
                {
                    Destroy(data);
                    continue;
                }

                AsyncOperation operation;
                try
                {
                    operation = NavMeshBuilder.UpdateNavMeshDataAsync(
                        data,
                        settings,
                        sourceSnapshot,
                        planarBounds);
                }
                catch (Exception)
                {
                    instance.Remove();
                    Destroy(data);
                    continue;
                }

                if (operation == null)
                {
                    instance.Remove();
                    Destroy(data);
                    continue;
                }

                pending.Entry.Datas.Add(data);
                pending.Entry.Instances.Add(instance);
                pending.Operations.Add(operation);
            }

            if (pending.Operations.Count == 0)
            {
                CleanupTileNavInstances(pending.Entry);
                return false;
            }

            _pendingAsyncTileBakes[coord] = pending;
            return true;
        }

        private bool DrainCompletedAsyncTileBakes()
        {
            if (_pendingAsyncTileBakes.Count == 0) return false;

            CollectCompletedAsyncBakeCoords();

            var bakedAny = false;
            for (var i = 0; i < _completedAsyncBakeCoords.Count; i++)
                if (FinalizeCompletedAsyncTileBake(_completedAsyncBakeCoords[i]))
                    bakedAny = true;

            return bakedAny;
        }

        private void CollectCompletedAsyncBakeCoords()
        {
            _completedAsyncBakeCoords.Clear();
            foreach (var kvp in _pendingAsyncTileBakes)
                if (IsCompletedAsyncBakeReadyForFinalize(kvp.Value))
                    _completedAsyncBakeCoords.Add(kvp.Key);
        }

        private static bool IsCompletedAsyncBakeReadyForFinalize(PendingAsyncTileBake pending)
        {
            if (pending == null || pending.Operations == null || pending.Operations.Count == 0)
                return true;

            for (var i = 0; i < pending.Operations.Count; i++)
            {
                var op = pending.Operations[i];
                if (op != null && !op.isDone)
                    return false;
            }

            return true;
        }

        private bool FinalizeCompletedAsyncTileBake(WorldTileCoord coord)
        {
            if (!_pendingAsyncTileBakes.TryGetValue(coord, out var pending))
                return false;

            _pendingAsyncTileBakes.Remove(coord);
            if (IsPendingAsyncBakeVersionStale(coord, pending))
            {
                CleanupTileNavInstances(pending.Entry);
                return false;
            }

            var hasValidInstance = HasAnyValidNavMeshInstance(pending.Entry);
            var outcome = hasValidInstance ? TileBakeOutcome.Success : TileBakeOutcome.NoInstances;
            var elapsedMs = (Time.realtimeSinceStartup - pending.StartedAtRealtime) * 1000f;
            MaybeLogSlowTileBake(coord, outcome, pending.SourceCount, pending.AgentTypeCount, elapsedMs, true);

            if (!hasValidInstance)
            {
                CleanupTileNavInstances(pending.Entry);
                _worldGenerationBackend?.ReportNavigationResult(coord, false);
                return false;
            }

            _tiles[coord] = pending.Entry;
            if (_pendingTileByCoord.TryGetValue(coord, out var info)
                || TryResolveTileInfoFromStream(coord, out info))
            {
                RebindNearbyUnitsAfterWorldTileBaked(info);
            }

            _worldGenerationBackend?.ReportNavigationResult(coord, true);
            return true;
        }

        private bool TryResolveTileInfoFromStream(WorldTileCoord coord, out WorldTileInfo info)
        {
            info = default;
            if (worldTileStream == null) return false;
            return worldTileStream.TryGetTile(coord, out info);
        }

        private bool IsPendingAsyncBakeVersionStale(WorldTileCoord coord, PendingAsyncTileBake pending)
        {
            return _tileBakeVersions.TryGetValue(coord, out var latestVersion)
                   && latestVersion != pending.Version;
        }

        private static bool HasAnyValidNavMeshInstance(TileNavInstances entry)
        {
            for (var i = 0; i < entry.Instances.Count; i++)
                if (entry.Instances[i].valid)
                    return true;

            return false;
        }

        private void CancelAllPendingAsyncTileBakes()
        {
            if (_pendingAsyncTileBakes.Count == 0) return;

            _completedAsyncBakeCoords.Clear();
            foreach (var kvp in _pendingAsyncTileBakes)
                _completedAsyncBakeCoords.Add(kvp.Key);

            for (var i = 0; i < _completedAsyncBakeCoords.Count; i++)
            {
                var coord = _completedAsyncBakeCoords[i];
                if (!_pendingAsyncTileBakes.TryGetValue(coord, out var pending)) continue;
                CancelPendingAsyncTileBakeInternal(coord, pending);
            }
        }

        private void CancelPendingAsyncTileBakeInternal(WorldTileCoord coord, PendingAsyncTileBake pending)
        {
            _pendingAsyncTileBakes.Remove(coord);
            CleanupTileNavInstances(pending.Entry);
        }

        private static void CleanupTileNavInstances(TileNavInstances entry)
        {
            if (entry == null) return;

            for (var i = 0; i < entry.Instances.Count; i++)
            {
                var inst = entry.Instances[i];
                if (inst.valid) inst.Remove();
            }

            for (var i = 0; i < entry.Datas.Count; i++)
                if (entry.Datas[i] != null)
                    Destroy(entry.Datas[i]);

            entry.Instances.Clear();
            entry.Datas.Clear();
        }
    }
}
