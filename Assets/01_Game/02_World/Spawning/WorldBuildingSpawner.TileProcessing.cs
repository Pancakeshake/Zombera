#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Random = System.Random;

#endregion

namespace Zombera.World.Spawning
{
    public sealed partial class WorldBuildingSpawner
    {
        private void ProcessTile(WorldTileInfo tile)
        {
            var terrain = tile.Terrain;
            if (terrain == null || terrain.terrainData == null) return;

            ResolveRuntimePlacedStructureFixer();

            var key = (tile.Coord.X, tile.Coord.Z);
            FlushSpawnQueueImmediate();

            if (_tileRoots.TryGetValue(key, out var oldRoot) && oldRoot != null)
                ReclaimTileRoot(oldRoot);

            var splines = new List<SplineSampleLine>(8);
            // MapMagic spline products are owned by Legacy; World path is a no-op for that source.
            if (!TryExtractSplines(terrain, splines)) return;
            if (entries == null || entries.Count == 0) return;

            var tileRoot = new GameObject($"Buildings_Tile_{key.X}_{key.Z}");
            tileRoot.transform.SetParent(terrain.transform, false);
            _tileRoots[key] = tileRoot;

            var tileSeed = unchecked(
                (ProceduralWorldSession.IsActive ? ProceduralWorldSession.WorldSeed : 12345) ^
                (key.X * 73856093) ^
                (key.Z * 19349663));
            var rng = new Random(tileSeed);

            var placed = new List<(Vector3 pos, float radius)>(64);
            CollapseBatchTracker collapseBatch = null;
            if (disableEasyBuildCollapseForSpawnedBuildings)
                collapseBatch = new CollapseBatchTracker(tileRoot.transform);

            foreach (var splineLine in splines)
                SpawnAlongLine(splineLine, terrain, rng, tileRoot.transform, placed, collapseBatch);

            if (logSpawns)
            {
                Debug.Log(
                    $"[WorldBuildingSpawner] Tile ({key.X},{key.Z}): queued {placed.Count} buildings across {splines.Count} splines (drain budget {maxBuildingSpawnsPerFrame}/frame).",
                    this);
            }
        }

        private void ReclaimTileRoot(GameObject root)
        {
            if (root == null) return;

            var poolRoot = GetPoolRoot();
            for (var i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                var marker = child.GetComponent<PooledWorldBuilding>();
                if (marker == null || marker.SourcePrefab == null) continue;

                child.SetParent(poolRoot, false);
                GetBuildingPool(marker.SourcePrefab).Enqueue(child.gameObject);
            }

            Destroy(root);
        }

        private Transform GetPoolRoot()
        {
            if (_poolRoot != null) return _poolRoot;

            var poolRootObject = new GameObject("PooledWorldBuildings");
            poolRootObject.SetActive(false);
            poolRootObject.transform.SetParent(transform, false);
            _poolRoot = poolRootObject.transform;
            return _poolRoot;
        }

        private Queue<GameObject> GetBuildingPool(GameObject prefab)
        {
            if (_buildingPools.TryGetValue(prefab, out var pool)) return pool;

            pool = new Queue<GameObject>();
            _buildingPools[prefab] = pool;
            return pool;
        }

        private GameObject TakeBuildingFromPool(GameObject prefab)
        {
            if (prefab == null || !_buildingPools.TryGetValue(prefab, out var pool)) return null;

            while (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled != null) return pooled;
            }

            return null;
        }

        private readonly struct TileRequest
        {
            public readonly WorldTileInfo Tile;

            public TileRequest(WorldTileInfo tile)
            {
                Tile = tile;
            }
        }
    }
}
