using System;
using MapMagic.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.World;

namespace Zombera.Characters
{
    public static class SpawnPointSelector
    {
        public static Terrain FindFirstActiveMapMagicTerrain(Scene ownerScene)
        {
            MapMagicObject[] mapMagics = UnityEngine.Object.FindObjectsByType<MapMagicObject>(FindObjectsSortMode.None);
            if (mapMagics == null)
            {
                return null;
            }

            foreach (MapMagicObject mm in mapMagics)
            {
                if (mm == null || (ownerScene.IsValid() && mm.gameObject.scene != ownerScene))
                {
                    continue;
                }

                foreach (Terrain t in mm.tiles.AllActiveTerrains())
                {
                    if (t != null && t.terrainData != null)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        public static Terrain FindNearestActiveMapMagicTerrain(Vector3 position, Scene ownerScene)
        {
            MapMagicObject[] mapMagics = UnityEngine.Object.FindObjectsByType<MapMagicObject>(FindObjectsSortMode.None);
            if (mapMagics == null || mapMagics.Length == 0)
            {
                return null;
            }

            Terrain bestTerrain = null;
            float bestDist = float.PositiveInfinity;

            foreach (MapMagicObject mm in mapMagics)
            {
                if (mm == null || (ownerScene.IsValid() && mm.gameObject.scene != ownerScene))
                {
                    continue;
                }

                foreach (Terrain t in mm.tiles.AllActiveTerrains())
                {
                    if (t == null || t.terrainData == null)
                    {
                        continue;
                    }

                    if (TerrainResolver.TerrainContainsXZ(t, position))
                    {
                        return t;
                    }

                    Vector3 tp = t.transform.position;
                    Vector3 ts = t.terrainData.size;
                    float cx = Mathf.Clamp(position.x, tp.x, tp.x + ts.x);
                    float cz = Mathf.Clamp(position.z, tp.z, tp.z + ts.z);
                    float dx = cx - position.x;
                    float dz = cz - position.z;
                    float d = dx * dx + dz * dz;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestTerrain = t;
                    }
                }
            }

            return bestTerrain;
        }

        public static bool TryGetMapMagicDeployedWorldRect(Scene ownerScene, out Rect rect)
        {
            rect = default;

            MapMagicObject[] mapMagics = UnityEngine.Object.FindObjectsByType<MapMagicObject>(FindObjectsSortMode.None);
            if (mapMagics == null || mapMagics.Length == 0)
            {
                return false;
            }

            bool hasAny = false;
            float minX = float.PositiveInfinity;
            float minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;

            foreach (MapMagicObject mm in mapMagics)
            {
                if (mm == null || (ownerScene.IsValid() && mm.gameObject.scene != ownerScene))
                {
                    continue;
                }

                foreach (Terrain t in mm.tiles.AllActiveTerrains())
                {
                    if (t == null || t.terrainData == null)
                    {
                        continue;
                    }

                    hasAny = true;
                    Vector3 origin = t.transform.position;
                    Vector3 size = t.terrainData.size;
                    minX = Mathf.Min(minX, origin.x);
                    minZ = Mathf.Min(minZ, origin.z);
                    maxX = Mathf.Max(maxX, origin.x + size.x);
                    maxZ = Mathf.Max(maxZ, origin.z + size.z);
                }
            }

            if (!hasAny)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return rect.width > 0f && rect.height > 0f;
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
            UnityEngine.Object logContext,
            out Vector3 spawn)
        {
            spawn = default;

            Terrain anyTerrain = FindFirstActiveMapMagicTerrain(ownerScene);
            if (anyTerrain == null || anyTerrain.terrainData == null)
            {
                return false;
            }

            int safeSamples = Mathf.Clamp(samples, 1, 128);
            float safeEdgeMargin = Mathf.Clamp01(edgeMarginNormalized);
            float safeMaxSlope = Mathf.Clamp(maxSlopeDegrees, 0f, 89f);

            Vector2 clampedNormalized = new Vector2(Mathf.Clamp01(desiredNormalized.x), Mathf.Clamp01(desiredNormalized.y));
            Vector2 centerNormalized = new Vector2(
                Mathf.Lerp(safeEdgeMargin, 1f - safeEdgeMargin, clampedNormalized.x),
                Mathf.Lerp(safeEdgeMargin, 1f - safeEdgeMargin, clampedNormalized.y));

            float bestSlope = float.PositiveInfinity;
            Vector3 bestSpawn = default;

            for (int i = 0; i < safeSamples; i++)
            {
                float t = safeSamples <= 1 ? 0f : i / (safeSamples - 1f);
                float jitterRadius = Mathf.Lerp(0f, 0.5f, t);

                Vector2 jitter = UnityEngine.Random.insideUnitCircle * jitterRadius;
                Vector2 sampleNormalized = new Vector2(
                    Mathf.Clamp01(centerNormalized.x + jitter.x * 0.25f),
                    Mathf.Clamp01(centerNormalized.y + jitter.y * 0.25f));

                Vector3 candidate = new Vector3(
                    deployedRect.x + deployedRect.width * sampleNormalized.x,
                    anyTerrain.transform.position.y,
                    deployedRect.y + deployedRect.height * sampleNormalized.y);

                Terrain tTerrain = resolveTerrain != null ? resolveTerrain(candidate) : null;
                if (tTerrain == null || tTerrain.terrainData == null)
                {
                    tTerrain = FindNearestActiveMapMagicTerrain(candidate, ownerScene);
                }

                if (tTerrain == null || tTerrain.terrainData == null)
                {
                    continue;
                }

                Vector3 origin = tTerrain.transform.position;
                Vector3 size = tTerrain.terrainData.size;
                float clampedX = Mathf.Clamp(candidate.x, origin.x, origin.x + size.x);
                float clampedZ = Mathf.Clamp(candidate.z, origin.z, origin.z + size.z);

                float slope = tTerrain.terrainData.GetSteepness(
                    Mathf.InverseLerp(origin.x, origin.x + size.x, clampedX),
                    Mathf.InverseLerp(origin.z, origin.z + size.z, clampedZ));

                if (slope < bestSlope)
                {
                    float sampledY = tTerrain.SampleHeight(new Vector3(clampedX, origin.y, clampedZ)) + origin.y;
                    bestSlope = slope;
                    bestSpawn = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
                    if (bestSlope <= safeMaxSlope)
                    {
                        break;
                    }
                }
            }

            if (float.IsInfinity(bestSlope))
            {
                return false;
            }

            spawn = bestSpawn;

            if (logDiagnostics)
            {
                Debug.Log(
                    $"[PlayerSpawner] SpawnPointSelector: selected flatter MapMagic spawn {spawn} (bestSlope={bestSlope:0.0}°, maxSlope={safeMaxSlope:0.0}°, samples={safeSamples}).",
                    logContext);
            }

            return bestSlope <= safeMaxSlope;
        }
    }
}

