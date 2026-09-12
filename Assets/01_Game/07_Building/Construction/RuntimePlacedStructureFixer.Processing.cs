#region

using UnityEngine;
using UnityEngine.AI;
using Zombera.BuildingSystem;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class RuntimePlacedStructureFixer
    {
        // ── Collider ──────────────────────────────────────────────────

        private bool EnsureSolidCollider(GameObject root, Bounds fallbackWorldBounds)
        {
            if (root == null || !addBoxColliderWhenMissing) return false;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || !col.enabled || col.isTrigger) continue;
                return false;
            }

            var localBounds = ConvertWorldBoundsToLocalAabb(root.transform, fallbackWorldBounds);
            var minSize = Mathf.Max(0.1f, minimumStructureFootprintMeters * 0.1f);
            var clampedSize = new Vector3(
                Mathf.Max(minSize, localBounds.size.x),
                Mathf.Max(minSize, localBounds.size.y),
                Mathf.Max(minSize, localBounds.size.z));

            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();

            box.isTrigger = false;
            box.center = localBounds.center;
            box.size = clampedSize;
            box.enabled = true;
            return true;
        }

        // ── NavMesh Obstacle ──────────────────────────────────────────

        private void EnsureNavMeshBlocking(GameObject root, Bounds worldBounds)
        {
            if (root == null) return;

            var existingObstacles = root.GetComponentsInChildren<NavMeshObstacle>(true);
            if (existingObstacles.Length > 0)
            {
                for (var i = 0; i < existingObstacles.Length; i++)
                {
                    var obstacle = existingObstacles[i];
                    if (obstacle == null) continue;
                    obstacle.carving = true;
                }

                return;
            }

            // Door prefabs usually manage blockers through dedicated scripts.
            if (root.GetComponentInChildren<DoorController>(true) != null ||
                root.GetComponentInChildren<DoorBlocker>(true) != null ||
                root.GetComponentInChildren<DoorHealth>(true) != null)
                return;

            var rootObstacle = root.GetComponent<NavMeshObstacle>();
            if (rootObstacle == null) rootObstacle = root.AddComponent<NavMeshObstacle>();

            var localBounds = ConvertWorldBoundsToLocalAabb(root.transform, worldBounds);
            var padding = Mathf.Max(0f, navMeshObstaclePaddingMeters) * 2f;

            rootObstacle.shape = NavMeshObstacleShape.Box;
            rootObstacle.center = localBounds.center;
            rootObstacle.size = new Vector3(
                Mathf.Max(0.1f, localBounds.size.x + padding),
                Mathf.Max(0.1f, localBounds.size.y + padding),
                Mathf.Max(0.1f, localBounds.size.z + padding));
            rootObstacle.carving = true;
            rootObstacle.carveOnlyStationary = true;
            rootObstacle.enabled = true;
        }

        // ── Terrain Flattening ────────────────────────────────────────

        private void FlattenTerrainUnderStructure(GameObject root, Bounds worldBounds)
        {
            if (root == null) return;

            var coreMinX = worldBounds.min.x - Mathf.Max(0f, terrainFlattenPaddingMeters);
            var coreMaxX = worldBounds.max.x + Mathf.Max(0f, terrainFlattenPaddingMeters);
            var coreMinZ = worldBounds.min.z - Mathf.Max(0f, terrainFlattenPaddingMeters);
            var coreMaxZ = worldBounds.max.z + Mathf.Max(0f, terrainFlattenPaddingMeters);

            var blend = Mathf.Max(0f, terrainFlattenBlendMeters);
            var outerMinX = coreMinX - blend;
            var outerMaxX = coreMaxX + blend;
            var outerMinZ = coreMinZ - blend;
            var outerMaxZ = coreMaxZ + blend;

            var spanX = outerMaxX - outerMinX;
            var spanZ = outerMaxZ - outerMinZ;
            if (spanX <= 0.01f || spanZ <= 0.01f) return;

            if (spanX > Mathf.Max(1f, maxFlattenAreaPerAxisMeters) || spanZ > Mathf.Max(1f, maxFlattenAreaPerAxisMeters))
            {
                if (verboseLogs)
                    Debug.LogWarning(
                        $"[RuntimePlacedStructureFixer] Skipped terrain flatten for {root.name} due to oversized footprint ({spanX:0.0}m x {spanZ:0.0}m).",
                        root);
                return;
            }

            var targetWorldY = worldBounds.min.y + terrainFlattenTargetYOffsetMeters;

            var terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
                terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                TryFlattenOnTerrain(
                    terrain,
                    coreMinX,
                    coreMaxX,
                    coreMinZ,
                    coreMaxZ,
                    outerMinX,
                    outerMaxX,
                    outerMinZ,
                    outerMaxZ,
                    blend,
                    targetWorldY);
            }
        }

        private void TryFlattenOnTerrain(
            Terrain terrain,
            float coreMinX,
            float coreMaxX,
            float coreMinZ,
            float coreMaxZ,
            float outerMinX,
            float outerMaxX,
            float outerMinZ,
            float outerMaxZ,
            float blendDistance,
            float targetWorldY)
        {
            var terrainData = terrain.terrainData;
            if (terrainData == null) return;

            var origin = terrain.transform.position;
            var size = terrainData.size;
            if (size.x <= 0.01f || size.z <= 0.01f || size.y <= 0.01f) return;

            var terrainMinX = origin.x;
            var terrainMaxX = origin.x + size.x;
            var terrainMinZ = origin.z;
            var terrainMaxZ = origin.z + size.z;

            var sampleMinX = Mathf.Max(outerMinX, terrainMinX);
            var sampleMaxX = Mathf.Min(outerMaxX, terrainMaxX);
            var sampleMinZ = Mathf.Max(outerMinZ, terrainMinZ);
            var sampleMaxZ = Mathf.Min(outerMaxZ, terrainMaxZ);

            if (sampleMaxX <= sampleMinX || sampleMaxZ <= sampleMinZ) return;

            var heightRes = terrainData.heightmapResolution;
            if (heightRes <= 1) return;

            if (!TryBuildSampleRegion(sampleMinX, sampleMaxX, origin.x, size.x, heightRes, out var startX,
                    out var width)) return;
            if (!TryBuildSampleRegion(sampleMinZ, sampleMaxZ, origin.z, size.z, heightRes, out var startZ,
                    out var height)) return;

            var heights = terrainData.GetHeights(startX, startZ, width, height);
            var targetNormalized = Mathf.Clamp01((targetWorldY - origin.y) / Mathf.Max(0.01f, size.y));
            var changed = false;

            for (var z = 0; z < height; z++)
            for (var x = 0; x < width; x++)
            {
                var worldX = origin.x + ((startX + x) / (float)(heightRes - 1)) * size.x;
                var worldZ = origin.z + ((startZ + z) / (float)(heightRes - 1)) * size.z;

                var weight = ComputeFlattenWeight(worldX, worldZ, coreMinX, coreMaxX, coreMinZ, coreMaxZ,
                    blendDistance);
                if (weight <= 0f) continue;

                var current = heights[z, x];
                var next = Mathf.Lerp(current, targetNormalized, weight);
                if (Mathf.Abs(next - current) <= 0.00001f) continue;

                heights[z, x] = next;
                changed = true;
            }

            if (!changed) return;

            terrainData.SetHeightsDelayLOD(startX, startZ, heights);
            if (syncHeightmapImmediatelyAfterFlatten)
                terrainData.SyncHeightmap();
        }

        private static float ComputeFlattenWeight(
            float worldX,
            float worldZ,
            float coreMinX,
            float coreMaxX,
            float coreMinZ,
            float coreMaxZ,
            float blendDistance)
        {
            var dx = 0f;
            if (worldX < coreMinX) dx = coreMinX - worldX;
            else if (worldX > coreMaxX) dx = worldX - coreMaxX;

            var dz = 0f;
            if (worldZ < coreMinZ) dz = coreMinZ - worldZ;
            else if (worldZ > coreMaxZ) dz = worldZ - coreMaxZ;

            if (dx <= 0f && dz <= 0f) return 1f;
            if (blendDistance <= 0f) return 0f;

            var distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance >= blendDistance) return 0f;

            var t = 1f - distance / blendDistance;
            return t * t * (3f - 2f * t);
        }

        private static bool TryBuildSampleRegion(float worldMin, float worldMax, float terrainOrigin, float terrainSize,
            int resolution, out int start, out int length)
        {
            start = 0;
            length = 0;

            if (terrainSize <= 0.01f || resolution <= 1) return false;

            var normMin = Mathf.Clamp01((worldMin - terrainOrigin) / terrainSize);
            var normMax = Mathf.Clamp01((worldMax - terrainOrigin) / terrainSize);
            var minIndex = Mathf.Clamp(Mathf.FloorToInt(normMin * (resolution - 1)), 0, resolution - 1);
            var maxIndex = Mathf.Clamp(Mathf.CeilToInt(normMax * (resolution - 1)), 0, resolution - 1);
            if (maxIndex < minIndex) return false;

            start = minIndex;
            length = maxIndex - minIndex + 1;
            return length > 0;
        }

        // ── Bounds Utilities ──────────────────────────────────────────

        private static bool TryComputeWorldBounds(GameObject root, out Bounds worldBounds)
        {
            worldBounds = new Bounds(root.transform.position, Vector3.zero);
            var hasBounds = false;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || !col.enabled || col.isTrigger) continue;

                if (!hasBounds)
                {
                    worldBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(col.bounds);
                }
            }

            if (hasBounds) return true;

            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || !col.enabled) continue;

                if (!hasBounds)
                {
                    worldBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(col.bounds);
                }
            }

            if (hasBounds) return true;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                if (!hasBounds)
                {
                    worldBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Bounds ConvertWorldBoundsToLocalAabb(Transform reference, Bounds worldBounds)
        {
            var min = worldBounds.min;
            var max = worldBounds.max;

            var localBounds = new Bounds(reference.InverseTransformPoint(new Vector3(min.x, min.y, min.z)),
                Vector3.zero);
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            localBounds.Encapsulate(reference.InverseTransformPoint(new Vector3(max.x, max.y, max.z)));

            return localBounds;
        }
    }
}
