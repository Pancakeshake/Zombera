using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Samples landform / biome / hydrology fields and optional live TerrainData.</summary>
    public sealed class WorldTerrainQueryService : IWorldTerrainQuery
    {
        private LandformField _landforms;
        private BiomeField _biomes;
        private HydrologyPlan _hydrology;
        private WorldGenerationProfile _profile;

        public void Bind(
            LandformField landforms,
            BiomeField biomes,
            HydrologyPlan hydrology,
            WorldGenerationProfile profile)
        {
            _landforms = landforms;
            _biomes = biomes;
            _hydrology = hydrology;
            _profile = profile;
        }

        public bool TrySample(Vector2 worldXZ, out WorldTerrainSample sample)
        {
            sample = default;
            if (!TrySampleHeight(worldXZ, out var height)) return false;
            TrySampleNormal(worldXZ, out var normal);
            TrySampleSlope(worldXZ, out var slope);
            TrySampleBiome(worldXZ, out var biome);
            TrySampleWater(worldXZ, out var water);

            sample = new WorldTerrainSample(
                worldXZ,
                height,
                normal,
                slope,
                biome,
                water,
                SampleBuildability(worldXZ),
                SampleNoBuildMask(worldXZ));
            return true;
        }

        public bool TrySampleHeight(Vector2 worldXZ, out float worldY)
        {
            worldY = 0f;
            if (_landforms == null) return false;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return false;
            return _landforms.TryGetHeight(x, z, out worldY);
        }

        public bool TrySampleNormal(Vector2 worldXZ, out Vector3 normal)
        {
            normal = Vector3.up;
            if (_landforms == null) return false;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return false;

            _landforms.TryGetHeight(Mathf.Max(0, x - 1), z, out var hL);
            _landforms.TryGetHeight(Mathf.Min(_landforms.Width - 1, x + 1), z, out var hR);
            _landforms.TryGetHeight(x, Mathf.Max(0, z - 1), out var hD);
            _landforms.TryGetHeight(x, Mathf.Min(_landforms.Height - 1, z + 1), out var hU);

            var dx = (hR - hL) / (_landforms.CellSize * 2f);
            var dz = (hU - hD) / (_landforms.CellSize * 2f);
            normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));
            return true;
        }

        public bool TrySampleSlope(Vector2 worldXZ, out float degrees)
        {
            degrees = 0f;
            if (!TrySampleNormal(worldXZ, out var normal)) return false;
            degrees = Vector3.Angle(normal, Vector3.up);
            return true;
        }

        public bool TrySampleBiome(Vector2 worldXZ, out WorldBiomeSample sample)
        {
            sample = default;
            if (_biomes == null || _landforms == null) return false;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return false;
            if (x >= _biomes.Width || z >= _biomes.Height) return false;

            var cell = z * _biomes.Width + x;
            var bestIndex = _biomes.DominantBiomeIndex[cell];
            var bestWeight = 0f;
            if (bestIndex >= 0 && bestIndex < _biomes.BiomeCount)
                bestWeight = _biomes.Weights[_biomes.WeightIndex(cell, bestIndex)];
            else
            {
                bestIndex = 0;
                for (var b = 0; b < _biomes.BiomeCount; b++)
                {
                    var w = _biomes.Weights[_biomes.WeightIndex(cell, b)];
                    if (w <= bestWeight) continue;
                    bestWeight = w;
                    bestIndex = b;
                }
            }

            _biomes.TryGetDominantStableId(cell, out var stableId);
            sample = new WorldBiomeSample(
                bestIndex,
                stableId,
                Mathf.Clamp01(bestWeight),
                _biomes.Temperature[cell],
                _biomes.Moisture[cell]);
            return true;
        }

        public bool TrySampleWater(Vector2 worldXZ, out WorldWaterSample sample)
        {
            sample = default;
            if (_hydrology == null || _landforms == null) return false;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return false;
            if (!_hydrology.TrySampleCell(x, z, out sample)) return false;
            return true;
        }

        public float SampleBuildability(Vector2 worldXZ)
        {
            // Provisional site pick (pre-biomes): treat unknown as buildable.
            if (_biomes == null || _landforms == null) return 1f;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return 0f;
            if (x >= _biomes.Width || z >= _biomes.Height) return 0f;
            return Mathf.Clamp01(_biomes.Buildability[z * _biomes.Width + x]);
        }

        public float SampleNoBuildMask(Vector2 worldXZ)
        {
            // Provisional site pick (pre-biomes): treat unknown as clear.
            if (_biomes == null || _landforms == null) return 0f;
            if (!TryGetCell(_landforms, worldXZ, out var x, out var z)) return 1f;
            if (x >= _biomes.Width || z >= _biomes.Height) return 1f;
            return _biomes.NoBuild[z * _biomes.Width + x] ? 1f : 0f;
        }

        public bool TryBuildCostField(Rect boundsXZ, WorldCostFieldOptions options, out IWorldCostField field)
        {
            field = null;
            if (_landforms == null) return false;

            options ??= new WorldCostFieldOptions();
            if (_profile != null)
                options.RoadSettings ??= _profile.RoadNetworkSettings;

            var cell = Mathf.Max(1f, options.CellSizeMeters);
            var width = Mathf.Max(2, Mathf.CeilToInt(boundsXZ.width / cell));
            var height = Mathf.Max(2, Mathf.CeilToInt(boundsXZ.height / cell));
            var count = width * height;

            var heights = new float[count];
            var slopes = new float[count];
            var buildability = new float[count];
            var noBuild = new float[count];
            var waterDepth = new float[count];
            var biomeMul = new float[count];
            var hasSample = new bool[count];
            var origin = new Vector2(boundsXZ.xMin, boundsXZ.yMin);

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var world = origin + new Vector2((x + 0.5f) * cell, (z + 0.5f) * cell);
                    var i = z * width + x;
                    if (!TrySample(world, out var sample)) continue;

                    hasSample[i] = true;
                    heights[i] = sample.HeightWorldY;
                    slopes[i] = sample.SlopeDegrees;
                    buildability[i] = sample.Buildability;
                    noBuild[i] = sample.NoBuildMask;
                    waterDepth[i] = sample.Water.DepthMeters;
                    biomeMul[i] = ResolveBiomeRoadMultiplier(sample.Biome.DominantBiomeIndex);
                }
            }

            field = new WorldCostField(
                boundsXZ,
                cell,
                width,
                height,
                heights,
                slopes,
                buildability,
                noBuild,
                waterDepth,
                biomeMul,
                hasSample,
                options);
            return true;
        }

        private float ResolveBiomeRoadMultiplier(int biomeIndex)
        {
            var palette = _profile != null ? _profile.Biomes : null;
            if (palette == null || _biomes?.BiomeStableIds == null) return 1f;
            if (biomeIndex < 0 || biomeIndex >= _biomes.BiomeStableIds.Length) return 1f;
            var stableId = _biomes.BiomeStableIds[biomeIndex];
            if (!palette.TryGetBiome(stableId, out var record) || record == null) return 1f;
            return Mathf.Max(0.01f, record.RoadCostMultiplier);
        }

        private static bool TryGetCell(LandformField field, Vector2 worldXZ, out int x, out int z)
        {
            x = 0;
            z = 0;
            if (field == null) return false;
            x = Mathf.FloorToInt((worldXZ.x - field.OriginXZ.x) / field.CellSize);
            z = Mathf.FloorToInt((worldXZ.y - field.OriginXZ.y) / field.CellSize);
            return x >= 0 && z >= 0 && x < field.Width && z < field.Height;
        }
    }
}
