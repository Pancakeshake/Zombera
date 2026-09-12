using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Bakes <see cref="LandformField"/> world heights into scoped Unity Terrain heightmaps.</summary>
    public static class LandformHeightmapBaker
    {
        private static float[,] _heightScratch;
        private static int _heightScratchRes;
        private static float[] _worldXScratch;
        private static int _worldXScratchRes;
        private static float[] _worldZScratch;
        private static int _worldZScratchRes;

        /// <summary>Session counters for hub baseline / optimization (reset via <see cref="ResetBakeCounters"/>).</summary>
        public static int SetHeightsCallCount { get; private set; }

        public static int TilesBakedCount { get; private set; }

        public static void ResetBakeCounters()
        {
            SetHeightsCallCount = 0;
            TilesBakedCount = 0;
        }

        private static float[,] RentHeightBuffer(int res)
        {
            if (_heightScratch == null || _heightScratchRes != res)
            {
                _heightScratch = new float[res, res];
                _heightScratchRes = res;
            }

            return _heightScratch;
        }

        private static float[] RentWorldXScratch(int res, Vector3 origin, Vector3 size)
        {
            if (_worldXScratch == null || _worldXScratchRes != res)
            {
                _worldXScratch = new float[res];
                _worldXScratchRes = res;
            }

            var denom = Mathf.Max(1, res - 1);
            for (var x = 0; x < res; x++)
                _worldXScratch[x] = origin.x + x / (float)denom * size.x;

            return _worldXScratch;
        }

        private static float[] RentWorldZScratch(int res, Vector3 origin, Vector3 size)
        {
            if (_worldZScratch == null || _worldZScratchRes != res)
            {
                _worldZScratch = new float[res];
                _worldZScratchRes = res;
            }

            var denom = Mathf.Max(1, res - 1);
            for (var z = 0; z < res; z++)
                _worldZScratch[z] = origin.z + z / (float)denom * size.z;

            return _worldZScratch;
        }

        public static void BakeScopedTiles(
            LandformField field,
            TerrainGridProfile gridProfile,
            HydrologyProfile hydrologyProfile,
            WorldTileCatalog catalog,
            IReadOnlyList<WorldTileCoord> tiles,
            int detailSeed,
            bool applyDetailNoise = true)
        {
            if (field == null || catalog == null || tiles == null || gridProfile == null) return;

            var seaLevel = hydrologyProfile != null ? hydrologyProfile.SeaLevelWorldY : 0f;
            var baseY = gridProfile.GetTerrainBaseY(seaLevel);
            var vertical = Mathf.Max(1f, gridProfile.TerrainVerticalSize);
            var detailNoise = applyDetailNoise ? new DeterministicNoise2D(detailSeed) : null;

            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                BakeTerrain(terrain, field, baseY, vertical, detailNoise, seaLevel);
            }
        }

        public static void BakeTerrain(
            Terrain terrain,
            LandformField field,
            float terrainBaseY,
            float verticalSize,
            DeterministicNoise2D detailNoise,
            float seaLevel = float.NegativeInfinity)
        {
            if (terrain == null || terrain.terrainData == null || field == null) return;

            var data = terrain.terrainData;
            var res = data.heightmapResolution;
            var size = data.size;
            var origin = terrain.transform.position;
            var heights = RentHeightBuffer(res);
            var invVertical = 1f / Mathf.Max(0.01f, verticalSize);
            var worldXByColumn = RentWorldXScratch(res, origin, size);
            var worldZByRow = RentWorldZScratch(res, origin, size);

            for (var z = 0; z < res; z++)
            {
                var worldZ = worldZByRow[z];
                for (var x = 0; x < res; x++)
                {
                    var worldX = worldXByColumn[x];
                    var worldY = LandformFieldSampling.SampleBilinear(field, worldX, worldZ);
                    if (detailNoise != null && worldY > seaLevel - 12f)
                    {
                        var detail = detailNoise.Fbm(worldX / 36f, worldZ / 36f, 3);
                        var detailRidge = detailNoise.Ridged(worldX / 88f, worldZ / 88f, 2);
                        worldY += (detail - 0.5f) * 10f;
                        worldY += detailRidge * 8f;
                    }

                    heights[z, x] = Mathf.Clamp01((worldY - terrainBaseY) * invVertical);
                }
            }

            data.SetHeights(0, 0, heights);
            SetHeightsCallCount++;
            TilesBakedCount++;
        }
    }
}
