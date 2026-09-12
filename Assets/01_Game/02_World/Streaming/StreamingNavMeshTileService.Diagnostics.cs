using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private void MaybeLogSlowTileBake(
            WorldTileCoord coord,
            TileBakeOutcome outcome,
            int sourceCount,
            int agentTypeCount,
            float elapsedMs,
            bool isAsyncCompletion = false)
        {
            if (!CanLogSlowTileBake(isAsyncCompletion, elapsedMs)) return;

            Debug.LogWarning(
                BuildSlowTileBakeMessage(coord, outcome, sourceCount, agentTypeCount, elapsedMs, isAsyncCompletion),
                this);
        }

        private bool CanLogSlowTileBake(bool isAsyncCompletion, float elapsedMs)
        {
            var thresholdMs = isAsyncCompletion
                ? asyncTileBakeWallClockWarningMilliseconds
                : tileBakeSpikeWarningMilliseconds;

            if (thresholdMs <= 0f || elapsedMs < thresholdMs)
                return false;

            var now = Time.unscaledTime;
            if (now < _nextSlowTileBakeWarningAt)
                return false;

            _nextSlowTileBakeWarningAt = now + Mathf.Max(0.1f, tileBakeWarningCooldownSeconds);
            return true;
        }

        private string BuildSlowTileBakeMessage(
            WorldTileCoord coord,
            TileBakeOutcome outcome,
            int sourceCount,
            int agentTypeCount,
            float elapsedMs,
            bool isAsyncCompletion)
        {
            if (isAsyncCompletion)
            {
                return "[StreamingNavMeshTileService] Slow async tile completion: coord=" + coord +
                       ", outcome=" + outcome +
                       ", sources=" + sourceCount +
                       ", agentTypes=" + agentTypeCount +
                       ", elapsedMs=" + elapsedMs.ToString("0.0") +
                       ", queueRemaining=" + _pendingTileBakeQueue.Count +
                       ", note='wall-clock async completion; not direct main-thread stall'.";
            }

            return "[StreamingNavMeshTileService] Slow tile bake: coord=" + coord +
                   ", outcome=" + outcome +
                   ", sources=" + sourceCount +
                   ", agentTypes=" + agentTypeCount +
                   ", elapsedMs=" + elapsedMs.ToString("0.0") +
                   ", queueRemaining=" + _pendingTileBakeQueue.Count +
                   ".";
        }

        private void MaybeLogSlowAgentBake(WorldTileCoord coord, int agentTypeId, int sourceCount, float elapsedMs)
        {
            if (perAgentBakeSpikeWarningMilliseconds <= 0f || elapsedMs < perAgentBakeSpikeWarningMilliseconds)
                return;

            var now = Time.unscaledTime;
            if (now < _nextSlowAgentBakeWarningAt) return;
            _nextSlowAgentBakeWarningAt = now + Mathf.Max(0.1f, perAgentBakeWarningCooldownSeconds);

            Debug.LogWarning(
                "[StreamingNavMeshTileService] Slow BuildNavMeshData: coord=" + coord +
                ", agentTypeId=" + agentTypeId +
                ", sources=" + sourceCount +
                ", elapsedMs=" + elapsedMs.ToString("0.0") +
                ".",
                this);
        }

        private void LogFocusedDiagnostics(string context, int bakedTileCountThisPass, bool hadAnyBake)
        {
            if (!logFocusedNavMeshDiagnostics) return;

            var navMeshTriangleCount = GetCachedNavMeshTriangleCount();
            var streamLabel = worldTileStream != null ? worldTileStream.name : "<none>";

            Debug.Log(
                "[StreamingNavMeshTileService] " + context +
                ": ownerScene='" + SceneLabel(_ownerScene) +
                "', tileStream='" + streamLabel +
                "', bakedTilesThisPass=" + bakedTileCountThisPass +
                ", cachedBakedTiles=" + _tiles.Count +
                ", navMeshTriangles=" + navMeshTriangleCount +
                ", hadAnyBake=" + hadAnyBake + ".",
                this);
        }

        private int GetCachedNavMeshTriangleCount()
        {
            var now = Time.unscaledTime;
            if (now < _nextNavMeshTriangleSampleAt)
                return _cachedNavMeshTriangleCount;

            _cachedNavMeshTriangleCount = GetNavMeshTriangleCount();
            _nextNavMeshTriangleSampleAt = now + NavMeshTriangleCacheIntervalSeconds;
            return _cachedNavMeshTriangleCount;
        }

        private void LogSceneMismatch(string context, Scene otherScene)
        {
            if (!logFocusedNavMeshDiagnostics) return;

            var now = Time.unscaledTime;
            if (now < _nextSceneMismatchLogTime) return;

            _nextSceneMismatchLogTime = now + Mathf.Max(0.1f, sceneMismatchLogCooldownSeconds);

            Debug.LogWarning(
                "[StreamingNavMeshTileService] Scene mismatch while " + context +
                ": ownerScene='" + SceneLabel(_ownerScene) +
                "', otherScene='" + SceneLabel(otherScene) +
                "', pendingTileQueue=" + _pendingTileBakeQueue.Count + ".",
                this);
        }

        private static int GetNavMeshTriangleCount()
        {
            var tri = NavMesh.CalculateTriangulation();
            return tri.indices != null ? tri.indices.Length / 3 : 0;
        }

        private static string SceneLabel(Scene scene)
        {
            return scene.IsValid()
                ? scene.name + "#" + scene.handle
                : "<invalid>";
        }

        private static string FormatPassDiagnostics(TileBakePassDiagnostics diagnostics)
        {
            return "[passStats: tileEntries=" + diagnostics.TileEntriesVisited +
                   ", nullTiles=" + diagnostics.NullTiles +
                   ", missingTerrain=" + diagnostics.MissingTerrain +
                   ", noSources=" + diagnostics.NoSources +
                   ", noInstances=" + diagnostics.NoInstances +
                   ", success=" + diagnostics.Successes + "]";
        }

        private static bool HasAnyNavMeshTriangles()
        {
            var tri = NavMesh.CalculateTriangulation();
            return tri.indices != null && tri.indices.Length > 0;
        }
    }
}
