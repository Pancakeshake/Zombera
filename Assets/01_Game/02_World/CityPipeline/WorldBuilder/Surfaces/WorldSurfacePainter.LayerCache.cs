using System.Collections.Generic;
using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Semantic layer index cache for hot <see cref="WorldSurfacePainter"/> paint paths.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private WorldSurfacePalette _cachedLayerPalette;
        private readonly Dictionary<string, int> _layerIndexBySemantic = new(40);
        private BiomeField _cachedNaturalRecordBiomes;
        private int[] _naturalRecordBiomeIndices;
        private int _snowBiomeIndex = -1;
        private int _alpineSnowBiomeIndex = -1;
        private readonly HashSet<int> _microSplatBoundTerrains = new(32);
        private readonly HashSet<int> _terrainLayersReady = new(8);
        private int _layerSand = -1;
        private int _layerSand01 = -1;
        private int _layerWetSand = -1;
        private int _layerRiverSand = -1;
        private int _layerBlackSand = -1;
        private int _layerWetRock = -1;
        private int _layerDirt = -1;
        private int _layerGrassGreen = -1;
        private int _layerGrassYellow = -1;
        private int _layerGrass = -1;
        private int _layerSparseGrass = -1;
        private int _layerMeadowGrass = -1;
        private int _layerDryForestFloor = -1;
        private int _layerSnow = -1;
        private int _layerSnowRock = -1;
        private int _layerCliffBright = -1;
        private int _layerCliffDark = -1;
        private int _layerCliffPink = -1;
        private int _layerCliffRed = -1;
        private void EnsureLayerIndexCache(WorldSurfacePalette palette)
        {
            if (palette == null)
                return;
            if (palette == _cachedLayerPalette)
                return;
            _cachedLayerPalette = palette;
            _layerIndexBySemantic.Clear();
            var mappings = palette.Mappings;
            if (mappings == null)
                return;
            for (var i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.SemanticName))
                    continue;
                _layerIndexBySemantic[mapping.SemanticName] = mapping.LayerIndex;
            }
            _layerSand = ResolveLayerIndex("Sand");
            _layerSand01 = ResolveLayerIndex("Sand01");
            _layerWetSand = ResolveLayerIndex("WetSand");
            _layerRiverSand = ResolveLayerIndex("RiverSand");
            _layerBlackSand = ResolveLayerIndex("BlackSand");
            _layerWetRock = ResolveLayerIndex("WetRock");
            _layerDirt = ResolveLayerIndex("Dirt");
            _layerGrassGreen = ResolveLayerIndex("GrassGreen");
            _layerGrassYellow = ResolveLayerIndex("GrassYellow");
            _layerGrass = ResolveLayerIndex("Grass");
            _layerSparseGrass = ResolveLayerIndex("SparseGrass");
            _layerMeadowGrass = ResolveLayerIndex("MeadowGrass");
            _layerDryForestFloor = ResolveLayerIndex("DryForestFloor");
            _layerSnow = ResolveLayerIndex("Snow");
            _layerSnowRock = ResolveLayerIndex("SnowRock");
            _layerCliffBright = ResolveLayerIndex("CliffBright");
            _layerCliffDark = ResolveLayerIndex("CliffDark");
            _layerCliffPink = ResolveLayerIndex("CliffPink");
            _layerCliffRed = ResolveLayerIndex("CliffRed");
        }
        private int ResolveLayerIndex(string semantic) =>
            _layerIndexBySemantic.TryGetValue(semantic, out var layer) ? layer : -1;
        /// <summary>Public semantic→alphamap layer lookup for city seaward rock stamps.</summary>
        public int ResolveSemanticLayerIndex(string semantic) => ResolveLayerIndex(semantic);
        private CoastalSurfaceBlendUtility.CoastalLayerIndices BuildCoastalLayerIndices() =>
            new()
            {
                Sand = _layerSand,
                Sand01 = _layerSand01,
                WetSand = _layerWetSand,
                WetRock = _layerWetRock,
                BlackSand = _layerBlackSand,
                CliffBright = _layerCliffBright,
                CliffDark = _layerCliffDark,
                CliffPink = _layerCliffPink,
                CliffRed = _layerCliffRed,
                Dirt = _layerDirt,
                GrassGreen = _layerGrassGreen,
                GrassYellow = _layerGrassYellow,
                Grass = _layerGrass,
                SparseGrass = _layerSparseGrass,
                MeadowGrass = _layerMeadowGrass,
                DryForestFloor = _layerDryForestFloor
            };
        private static void AddLayerByIndex(float[,,] map, int z, int x, int layers, int layer, float amount)
        {
            if (amount <= 0f || layer < 0 || layer >= layers || !IsNaturalPaintLayer(layer))
                return;
            map[z, x, layer] += amount;
        }
        private void EnsureNaturalRecordIndices(BiomeField biomes, List<WorldBiomeRecord> records)
        {
            if (biomes == null || records == null)
                return;
            if (biomes == _cachedNaturalRecordBiomes && _naturalRecordBiomeIndices != null &&
                _naturalRecordBiomeIndices.Length == records.Count)
                return;
            _cachedNaturalRecordBiomes = biomes;
            _naturalRecordBiomeIndices = new int[records.Count];
            for (var i = 0; i < records.Count; i++)
            {
                var stableId = records[i]?.StableId;
                _naturalRecordBiomeIndices[i] = string.IsNullOrEmpty(stableId)
                    ? -1
                    : ResolveBiomeIndex(biomes, stableId);
            }
            _snowBiomeIndex = ResolveBiomeIndex(biomes, "Snow");
            _alpineSnowBiomeIndex = ResolveBiomeIndex(biomes, "AlpineSnow");
        }
    }
}
