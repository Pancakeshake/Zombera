using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Alphamap grass gates for wilderness nature placement.</summary>
    public sealed partial class WorldNaturePlacer
    {
        private static readonly string[] GrassSemanticNames =
        {
            "GrassGreen",
            "GrassYellow",
            "Grass",
            "SparseGrass",
            "MeadowGrass"
        };

        private readonly int[] _grassLayerScratch = new int[GrassSemanticNames.Length];
        private readonly List<Terrain> _terrainCache = new(64);

        private void RebuildTerrainCache()
        {
            _terrainCache.Clear();
            if (WorldTileInfoUtility.TryGetPinnedTerrains(out var pinned) &&
                pinned != null &&
                pinned.Count > 0)
            {
                for (var i = 0; i < pinned.Count; i++)
                {
                    if (pinned[i] != null)
                        _terrainCache.Add(pinned[i]);
                }

                return;
            }

            if (WorldTileInfoUtility.TryGetWorldTerrainGridTerrains(out var grid) &&
                grid != null &&
                grid.Count > 0)
            {
                for (var i = 0; i < grid.Count; i++)
                {
                    if (grid[i] != null)
                        _terrainCache.Add(grid[i]);
                }

                return;
            }

            var active = Terrain.activeTerrains;
            if (active == null)
                return;

            for (var i = 0; i < active.Length; i++)
            {
                if (active[i] != null)
                    _terrainCache.Add(active[i]);
            }
        }

        private int ResolveGrassLayerIndices(WorldSurfacePalette palette, int[] dest)
        {
            var count = 0;
            if (palette == null || dest == null)
                return 0;

            for (var i = 0; i < GrassSemanticNames.Length && count < dest.Length; i++)
            {
                if (!palette.TryGetLayerIndex(GrassSemanticNames[i], out var layer) || layer < 0)
                    continue;
                dest[count++] = layer;
            }

            return count;
        }

        private bool TrySampleGrassWeight(
            Vector2 worldXZ,
            int[] grassLayers,
            int grassLayerCount,
            out float grassWeight)
        {
            grassWeight = 0f;
            if (grassLayerCount <= 0)
                return false;
            if (!TryResolveTerrainAtCached(worldXZ, out var terrain))
                return false;

            var data = terrain.terrainData;
            if (data == null)
                return false;

            if (!TryWorldToAlphamapCell(terrain, data, worldXZ, out var ax, out var az))
                return false;

            var layers = data.alphamapLayers;
            if (layers <= 0)
                return false;

            // Unity has no NonAlloc GetAlphamaps; terrain list is cached — this is the remaining per-hit cost.
            var map = data.GetAlphamaps(ax, az, 1, 1);
            if (map == null || map.GetLength(2) == 0)
                return false;

            var mapLayers = map.GetLength(2);
            var sum = 0f;
            for (var i = 0; i < grassLayerCount; i++)
            {
                var layer = grassLayers[i];
                if (layer < 0 || layer >= mapLayers)
                    continue;
                sum += map[0, 0, layer];
            }

            grassWeight = sum;
            return true;
        }

        private bool TryResolveTerrainAtCached(Vector2 worldXZ, out Terrain terrain)
        {
            terrain = null;
            for (var i = 0; i < _terrainCache.Count; i++)
            {
                var candidate = _terrainCache[i];
                if (candidate == null || candidate.terrainData == null)
                    continue;

                var pos = candidate.transform.position;
                var size = candidate.terrainData.size;
                if (worldXZ.x < pos.x || worldXZ.y < pos.z ||
                    worldXZ.x > pos.x + size.x || worldXZ.y > pos.z + size.z)
                    continue;

                terrain = candidate;
                return true;
            }

            return false;
        }

        private static bool TryWorldToAlphamapCell(
            Terrain terrain,
            TerrainData data,
            Vector2 worldXZ,
            out int ax,
            out int az)
        {
            ax = 0;
            az = 0;
            var pos = terrain.transform.position;
            var size = data.size;
            if (size.x <= 0.001f || size.z <= 0.001f)
                return false;

            var nx = (worldXZ.x - pos.x) / size.x;
            var nz = (worldXZ.y - pos.z) / size.z;
            if (nx < 0f || nz < 0f || nx > 1f || nz > 1f)
                return false;

            var w = data.alphamapWidth;
            var h = data.alphamapHeight;
            ax = Mathf.Clamp(Mathf.FloorToInt(nx * (w - 1)), 0, w - 1);
            az = Mathf.Clamp(Mathf.FloorToInt(nz * (h - 1)), 0, h - 1);
            return true;
        }

        private bool PassesGrassGate(
            WorldNatureEntry entry,
            Vector2 worldXZ,
            int[] grassLayers,
            int grassLayerCount)
        {
            if (entry == null || !entry.RequireGrassSurface)
                return true;

            if (grassLayerCount <= 0)
                return false;

            if (!TrySampleGrassWeight(worldXZ, grassLayers, grassLayerCount, out var weight))
                return false;

            return weight >= entry.MinGrassWeight;
        }
    }
}
