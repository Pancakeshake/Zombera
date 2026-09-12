using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>TerrainData tree prototype sync, instance buffers, and scope-aware flush/clear.</summary>
    public sealed partial class WorldNaturePlacer
    {
        private readonly Dictionary<Terrain, List<TreeInstance>> _pendingTrees = new();
        private readonly Dictionary<Terrain, Dictionary<int, int>> _prototypeIndexByPrefabId = new();
        private readonly List<TreeInstance> _mergeScratch = new(256);

        private void BeginTerrainTreePass(IReadOnlyList<WorldNatureEntry> entries)
        {
            _pendingTrees.Clear();
            _prototypeIndexByPrefabId.Clear();

            for (var t = 0; t < _terrainCache.Count; t++)
            {
                var terrain = _terrainCache[t];
                if (terrain == null || terrain.terrainData == null)
                    continue;

                EnsureTreePrototypes(terrain, entries);
            }
        }

        private void EnsureTreePrototypes(Terrain terrain, IReadOnlyList<WorldNatureEntry> entries)
        {
            var data = terrain.terrainData;
            var existing = data.treePrototypes;
            var byInstanceId = new Dictionary<int, int>(existing != null ? existing.Length + 8 : 8);
            var list = new List<TreePrototype>(existing != null ? existing.Length + 8 : 8);

            if (existing != null)
            {
                for (var i = 0; i < existing.Length; i++)
                {
                    var proto = existing[i];
                    list.Add(proto);
                    if (proto?.prefab == null)
                        continue;
                    var id = proto.prefab.GetInstanceID();
                    if (!byInstanceId.ContainsKey(id))
                        byInstanceId[id] = i;
                }
            }

            var changed = false;
            for (var e = 0; e < entries.Count; e++)
            {
                var entry = entries[e];
                if (entry == null ||
                    entry.PlacementMode != WorldNaturePlacementMode.TerrainTree ||
                    entry.Prefab == null)
                    continue;

                var id = entry.Prefab.GetInstanceID();
                if (byInstanceId.ContainsKey(id))
                    continue;

                byInstanceId[id] = list.Count;
                list.Add(new TreePrototype { prefab = entry.Prefab });
                changed = true;
            }

            if (changed)
                data.treePrototypes = list.ToArray();

            _prototypeIndexByPrefabId[terrain] = byInstanceId;
        }

        private bool TryBufferTerrainTree(
            WorldNatureEntry entry,
            WorldTerrainSample sample,
            DeterministicRng rng)
        {
            if (!TryResolveTerrainAtCached(sample.WorldXZ, out var terrain))
                return false;
            if (!_prototypeIndexByPrefabId.TryGetValue(terrain, out var byPrefab) ||
                !byPrefab.TryGetValue(entry.Prefab.GetInstanceID(), out var protoIndex))
                return false;
            if (!TryWorldToTreeNormalized(terrain, sample, out var normalized))
                return false;

            var scaleMin = entry.ScaleRange.x > 0f ? entry.ScaleRange.x : 0.85f;
            var scaleMax = entry.ScaleRange.y > scaleMin ? entry.ScaleRange.y : scaleMin + 0.3f;
            var scale = Mathf.Lerp(scaleMin, scaleMax, rng.NextFloat01());
            var yawRad = rng.NextFloat01() * Mathf.PI * 2f;

            if (!_pendingTrees.TryGetValue(terrain, out var list))
            {
                list = new List<TreeInstance>(64);
                _pendingTrees[terrain] = list;
            }

            list.Add(new TreeInstance
            {
                prototypeIndex = protoIndex,
                position = normalized,
                widthScale = scale,
                heightScale = scale,
                rotation = yawRad,
                color = Color.white,
                lightmapColor = Color.white
            });
            return true;
        }

        private static bool TryWorldToTreeNormalized(
            Terrain terrain,
            WorldTerrainSample sample,
            out Vector3 normalized)
        {
            normalized = default;
            var data = terrain.terrainData;
            if (data == null)
                return false;

            var pos = terrain.transform.position;
            var size = data.size;
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f)
                return false;

            var nx = (sample.WorldXZ.x - pos.x) / size.x;
            var nz = (sample.WorldXZ.y - pos.z) / size.z;
            if (nx < 0f || nz < 0f || nx > 1f || nz > 1f)
                return false;

            var ny = Mathf.Clamp01((sample.HeightWorldY - pos.y) / size.y);
            normalized = new Vector3(nx, ny, nz);
            return true;
        }

        private void FlushTerrainTrees(Rect boundsXZ)
        {
            for (var t = 0; t < _terrainCache.Count; t++)
            {
                var terrain = _terrainCache[t];
                if (terrain == null || terrain.terrainData == null)
                    continue;

                _pendingTrees.TryGetValue(terrain, out var pending);
                ApplyScopedTreeInstances(terrain, boundsXZ, pending);
            }

            _pendingTrees.Clear();
        }

        private void ClearTerrainTreesInScope(Rect boundsXZ)
        {
            RebuildTerrainCache();
            for (var t = 0; t < _terrainCache.Count; t++)
            {
                var terrain = _terrainCache[t];
                if (terrain == null || terrain.terrainData == null)
                    continue;

                ApplyScopedTreeInstances(terrain, boundsXZ, null);
            }
        }

        private void ApplyScopedTreeInstances(
            Terrain terrain,
            Rect boundsXZ,
            List<TreeInstance> replacementsInsideScope)
        {
            var data = terrain.terrainData;
            var existing = data.treeInstances;
            _mergeScratch.Clear();

            if (existing != null)
            {
                for (var i = 0; i < existing.Length; i++)
                {
                    var instance = existing[i];
                    if (IsTreeWorldXZInBounds(terrain, instance.position, boundsXZ))
                        continue;
                    _mergeScratch.Add(instance);
                }
            }

            if (replacementsInsideScope != null)
            {
                for (var i = 0; i < replacementsInsideScope.Count; i++)
                    _mergeScratch.Add(replacementsInsideScope[i]);
            }

            data.treeInstances = _mergeScratch.Count == 0
                ? System.Array.Empty<TreeInstance>()
                : _mergeScratch.ToArray();
        }

        private static bool IsTreeWorldXZInBounds(
            Terrain terrain,
            Vector3 normalizedPosition,
            Rect boundsXZ)
        {
            var data = terrain.terrainData;
            if (data == null)
                return false;

            var origin = terrain.transform.position;
            var size = data.size;
            var worldX = origin.x + normalizedPosition.x * size.x;
            var worldZ = origin.z + normalizedPosition.z * size.z;
            return boundsXZ.Contains(new Vector2(worldX, worldZ));
        }
    }
}
