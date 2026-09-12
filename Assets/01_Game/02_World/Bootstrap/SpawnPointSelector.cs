#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Spawn candidate selection over streamed world terrains (no MapMagic types).
    /// </summary>
    public static class SpawnPointSelector
    {
        private static readonly List<WorldTileInfo> TileScratch = new(64);

        public static Terrain FindFirstActiveMapMagicTerrain(Scene ownerScene) =>
            FindFirstActiveTerrain(ownerScene);

        public static Terrain FindFirstActiveTerrain(Scene ownerScene)
        {
            if (TryCollectStreamTerrains(ownerScene, out var first, out _))
                return first;

            return FindFirstSceneTerrain(ownerScene);
        }

        public static Terrain FindNearestActiveMapMagicTerrain(Vector3 position, Scene ownerScene) =>
            FindNearestActiveTerrain(position, ownerScene);

        public static Terrain FindNearestActiveTerrain(Vector3 position, Scene ownerScene)
        {
            Terrain bestTerrain = null;
            var bestDist = float.PositiveInfinity;

            if (TryFillTerrainsFromStream(ownerScene, TileScratch))
            {
                for (var i = 0; i < TileScratch.Count; i++)
                {
                    var t = TileScratch[i].Terrain;
                    if (!TryConsiderTerrain(t, position, ref bestTerrain, ref bestDist))
                        continue;
                    if (TerrainResolver.TerrainContainsXZ(t, position))
                        return t;
                }

                if (bestTerrain != null) return bestTerrain;
            }

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || (ownerScene.IsValid() && t.gameObject.scene != ownerScene)) continue;
                if (t.terrainData == null) continue;
                if (TerrainResolver.TerrainContainsXZ(t, position)) return t;
                TryConsiderTerrain(t, position, ref bestTerrain, ref bestDist);
            }

            return bestTerrain;
        }

        public static bool TryGetMapMagicDeployedWorldRect(Scene ownerScene, out Rect rect) =>
            TryGetDeployedWorldRect(ownerScene, out rect);

        public static bool TryGetDeployedWorldRect(Scene ownerScene, out Rect rect)
        {
            rect = default;
            var stream = ResolveStream(ownerScene);
            if (stream != null)
            {
                TileScratch.Clear();
                stream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, TileScratch);
                if (TileScratch.Count == 0) return false;

                var minX = float.PositiveInfinity;
                var minZ = float.PositiveInfinity;
                var maxX = float.NegativeInfinity;
                var maxZ = float.NegativeInfinity;
                var hasAny = false;

                for (var i = 0; i < TileScratch.Count; i++)
                {
                    var tileRect = TileScratch[i].WorldRectXZ;
                    if (tileRect.width <= 0f || tileRect.height <= 0f) continue;
                    hasAny = true;
                    minX = Mathf.Min(minX, tileRect.xMin);
                    minZ = Mathf.Min(minZ, tileRect.yMin);
                    maxX = Mathf.Max(maxX, tileRect.xMax);
                    maxZ = Mathf.Max(maxZ, tileRect.yMax);
                }

                if (!hasAny) return false;
                rect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
                return rect is { width: > 0f, height: > 0f };
            }

            return TryGetRectFromSceneTerrains(ownerScene, out rect);
        }

        public static bool HasGameplayReadyTile(Scene ownerScene)
        {
            var stream = ResolveStream(ownerScene);
            if (stream == null) return FindFirstActiveTerrain(ownerScene) != null;

            TileScratch.Clear();
            stream.CopyTilesAtOrAbove(WorldTileState.GameplayReady, TileScratch);
            if (TileScratch.Count > 0) return true;

            TileScratch.Clear();
            stream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, TileScratch);
            return TileScratch.Count > 0;
        }

        public static bool HasTileStreamInScene(Scene ownerScene)
        {
            return ResolveStream(ownerScene) != null;
        }

        public static bool TrySelectFlatterSpawnInMapMagicRect(
            Rect deployedRect,
            Scene ownerScene,
            Vector2 desiredNormalized,
            float edgeMarginNormalized,
            int samples,
            float maxSlopeDegrees,
            float terrainHeightOffset,
            Func<Vector3, Terrain> resolveTerrain,
            bool logDiagnostics,
            Object logContext,
            out Vector3 spawn,
            int randomUniformSpawnAttempts = 48)
        {
            spawn = default;

            var anyTerrain = FindFirstActiveTerrain(ownerScene);
            if (anyTerrain is not { terrainData: not null }) return false;

            var safeSamples = Mathf.Clamp(samples, 1, 128);
            var safeEdgeMargin = Mathf.Clamp01(edgeMarginNormalized);
            var safeMaxSlope = Mathf.Clamp(maxSlopeDegrees, 0f, 89f);

            var clampedNormalized = new Vector2(Mathf.Clamp01(desiredNormalized.x), Mathf.Clamp01(desiredNormalized.y));
            var centerNormalized = new Vector2(
                Mathf.Lerp(safeEdgeMargin, 1f - safeEdgeMargin, clampedNormalized.x),
                Mathf.Lerp(safeEdgeMargin, 1f - safeEdgeMargin, clampedNormalized.y));

            var randomAttempts = Mathf.Clamp(randomUniformSpawnAttempts, 0, 256);
            if (randomAttempts > 0 &&
                TryPickRandomSpawnUnderMaxSlope(
                    deployedRect,
                    ownerScene,
                    safeEdgeMargin,
                    randomAttempts,
                    safeMaxSlope,
                    terrainHeightOffset,
                    resolveTerrain,
                    logDiagnostics,
                    logContext,
                    out spawn))
                return true;

            var bestSlope = float.PositiveInfinity;
            Vector3 bestSpawn = default;

            for (var i = 0; i < safeSamples; i++)
            {
                var t = safeSamples <= 1 ? 0f : i / (safeSamples - 1f);
                var jitterRadius = Mathf.Lerp(0f, 0.5f, t);
                var jitter = Random.insideUnitCircle * jitterRadius;
                var sampleNormalized = new Vector2(
                    Mathf.Clamp01(centerNormalized.x + jitter.x * 0.25f),
                    Mathf.Clamp01(centerNormalized.y + jitter.y * 0.25f));

                var candidate = new Vector3(
                    deployedRect.x + deployedRect.width * sampleNormalized.x,
                    anyTerrain.transform.position.y,
                    deployedRect.y + deployedRect.height * sampleNormalized.y);

                if (!TryEvaluateSpawnCandidate(
                        candidate,
                        ownerScene,
                        terrainHeightOffset,
                        resolveTerrain,
                        out var evaluated,
                        out var slope))
                    continue;

                if (slope >= bestSlope) continue;

                bestSlope = slope;
                bestSpawn = evaluated;
                if (bestSlope <= safeMaxSlope) break;
            }

            if (float.IsInfinity(bestSlope)) return false;

            spawn = bestSpawn;
            if (logDiagnostics)
                Debug.Log(
                    $"[PlayerSpawner] SpawnPointSelector: selected flatter spawn {spawn} (bestSlope={bestSlope:0.0}°, maxSlope={safeMaxSlope:0.0}°, samples={safeSamples}).",
                    logContext);

            return bestSlope <= safeMaxSlope;
        }

        private static bool TryPickRandomSpawnUnderMaxSlope(
            Rect deployedRect,
            Scene ownerScene,
            float edgeMarginNormalized,
            int attempts,
            float maxSlopeDegrees,
            float terrainHeightOffset,
            Func<Vector3, Terrain> resolveTerrain,
            bool logDiagnostics,
            Object logContext,
            out Vector3 spawn)
        {
            spawn = default;
            if (attempts <= 0) return false;

            var margin = Mathf.Clamp01(edgeMarginNormalized);
            if (margin >= 0.499f) return false;

            var uMin = margin;
            var uMax = 1f - margin;
            var baseY = FindFirstActiveTerrain(ownerScene)?.transform.position.y ?? 0f;

            Vector3 chosen = default;
            var pickCount = 0;

            for (var a = 0; a < attempts; a++)
            {
                var nx = Random.Range(uMin, uMax);
                var nz = Random.Range(uMin, uMax);
                var candidate = new Vector3(
                    deployedRect.x + deployedRect.width * nx,
                    baseY,
                    deployedRect.y + deployedRect.height * nz);

                if (!TryEvaluateSpawnCandidate(
                        candidate,
                        ownerScene,
                        terrainHeightOffset,
                        resolveTerrain,
                        out var evaluated,
                        out var slope))
                    continue;

                if (slope > maxSlopeDegrees) continue;

                pickCount++;
                if (Random.Range(0, pickCount) == 0) chosen = evaluated;
            }

            if (pickCount <= 0) return false;

            spawn = chosen;
            if (logDiagnostics)
                Debug.Log(
                    $"[PlayerSpawner] SpawnPointSelector: random spawn {spawn} (picked 1 of {pickCount} valid / {attempts} tries, maxSlope={maxSlopeDegrees:0.0}°).",
                    logContext);

            return true;
        }

        private static bool TryEvaluateSpawnCandidate(
            Vector3 candidate,
            Scene ownerScene,
            float terrainHeightOffset,
            Func<Vector3, Terrain> resolveTerrain,
            out Vector3 spawnWithHeight,
            out float slopeDegrees)
        {
            spawnWithHeight = default;
            slopeDegrees = float.PositiveInfinity;

            var tTerrain = resolveTerrain?.Invoke(candidate);
            if (tTerrain is not { terrainData: not null })
                tTerrain = FindNearestActiveTerrain(candidate, ownerScene);

            if (tTerrain is not { terrainData: not null }) return false;

            var origin = tTerrain.transform.position;
            var size = tTerrain.terrainData.size;
            var clampedX = Mathf.Clamp(candidate.x, origin.x, origin.x + size.x);
            var clampedZ = Mathf.Clamp(candidate.z, origin.z, origin.z + size.z);

            slopeDegrees = tTerrain.terrainData.GetSteepness(
                Mathf.InverseLerp(origin.x, origin.x + size.x, clampedX),
                Mathf.InverseLerp(origin.z, origin.z + size.z, clampedZ));

            var sampledY = tTerrain.SampleHeight(new Vector3(clampedX, origin.y, clampedZ)) + origin.y;
            spawnWithHeight = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
            return true;
        }

        private static bool TryCollectStreamTerrains(Scene ownerScene, out Terrain first, out int count)
        {
            first = null;
            count = 0;
            if (!TryFillTerrainsFromStream(ownerScene, TileScratch)) return false;

            for (var i = 0; i < TileScratch.Count; i++)
            {
                var t = TileScratch[i].Terrain;
                if (t is not { terrainData: not null }) continue;
                count++;
                if (first == null) first = t;
            }

            return first != null;
        }

        private static bool TryFillTerrainsFromStream(Scene ownerScene, List<WorldTileInfo> buffer)
        {
            buffer.Clear();
            var stream = ResolveStream(ownerScene);
            if (stream == null) return false;
            stream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, buffer);
            return buffer.Count > 0;
        }

        private static WorldTileStreamSource ResolveStream(Scene ownerScene)
        {
            var streams = Object.FindObjectsByType<WorldTileStreamSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < streams.Length; i++)
            {
                var stream = streams[i];
                if (stream == null) continue;
                if (!ownerScene.IsValid() || stream.gameObject.scene == ownerScene)
                    return stream;
            }

            return streams.Length > 0 ? streams[0] : null;
        }

        private static Terrain FindFirstSceneTerrain(Scene ownerScene)
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t is not { terrainData: not null }) continue;
                if (ownerScene.IsValid() && t.gameObject.scene != ownerScene) continue;
                return t;
            }

            return null;
        }

        private static bool TryGetRectFromSceneTerrains(Scene ownerScene, out Rect rect)
        {
            rect = default;
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var hasAny = false;
            var minX = float.PositiveInfinity;
            var minZ = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxZ = float.NegativeInfinity;

            for (var i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t is not { terrainData: not null }) continue;
                if (ownerScene.IsValid() && t.gameObject.scene != ownerScene) continue;

                hasAny = true;
                var origin = t.transform.position;
                var size = t.terrainData.size;
                minX = Mathf.Min(minX, origin.x);
                minZ = Mathf.Min(minZ, origin.z);
                maxX = Mathf.Max(maxX, origin.x + size.x);
                maxZ = Mathf.Max(maxZ, origin.z + size.z);
            }

            if (!hasAny) return false;
            rect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return rect is { width: > 0f, height: > 0f };
        }

        private static bool TryConsiderTerrain(
            Terrain t,
            Vector3 position,
            ref Terrain bestTerrain,
            ref float bestDist)
        {
            if (t is not { terrainData: not null }) return false;

            var tp = t.transform.position;
            var ts = t.terrainData.size;
            var cx = Mathf.Clamp(position.x, tp.x, tp.x + ts.x);
            var cz = Mathf.Clamp(position.z, tp.z, tp.z + ts.z);
            var dx = cx - position.x;
            var dz = cz - position.z;
            var d = dx * dx + dz * dz;
            if (d >= bestDist) return true;

            bestDist = d;
            bestTerrain = t;
            return true;
        }
    }
}
