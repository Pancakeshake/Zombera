using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldSurfaceLayerMapping
    {
        public string SemanticName = "GrassGreen";
        [Range(0, 31)] public int LayerIndex;
        public bool IsInfrastructure;
    }

    /// <summary>Semantic terrain surface names mapped to MicroSplat / TerrainLayer indices.</summary>
    [CreateAssetMenu(
        fileName = "WorldSurfacePalette",
        menuName = "Zombera/World/World Surface Palette")]
    public sealed class WorldSurfacePalette : ScriptableObject
    {
        public const int MaxLayerIndex = 31;
        public const int InfrastructureMinIndex = 13;
        public const int InfrastructureMaxIndex = 16;
        public const int ExtendedNaturalMinIndex = 17;
        public const int ExtendedNaturalMaxIndex = 31;

        [SerializeField] private UnityEngine.Object microSplatWorldAsset;
        [SerializeField] private UnityEngine.Object microSplatTemplateMaterial;
        [SerializeField] private UnityEngine.Object microSplatPropData;
        [SerializeField] private TerrainLayer[] terrainLayers;

        [SerializeField] private List<WorldSurfaceLayerMapping> mappings = new()
        {
            new() { SemanticName = "GrassGreen", LayerIndex = 0 },
            new() { SemanticName = "GrassYellow", LayerIndex = 1 },
            new() { SemanticName = "Sand01", LayerIndex = 2 },
            new() { SemanticName = "Grass", LayerIndex = 3 },
            new() { SemanticName = "SparseGrass", LayerIndex = 4 },
            new() { SemanticName = "CliffBright", LayerIndex = 5 },
            new() { SemanticName = "Sand", LayerIndex = 6 },
            new() { SemanticName = "CliffDark", LayerIndex = 7 },
            new() { SemanticName = "CliffRed", LayerIndex = 8 },
            new() { SemanticName = "CliffPink", LayerIndex = 9 },
            new() { SemanticName = "Snow", LayerIndex = 10 },
            new() { SemanticName = "SandCracks", LayerIndex = 11 },
            new() { SemanticName = "Dirt", LayerIndex = 12 },
            new() { SemanticName = "Asphalt", LayerIndex = 13, IsInfrastructure = true },
            new() { SemanticName = "RoughConcrete", LayerIndex = 14, IsInfrastructure = true },
            new() { SemanticName = "ConcreteTiles", LayerIndex = 15, IsInfrastructure = true },
            new() { SemanticName = "CleanAsphalt", LayerIndex = 16, IsInfrastructure = true },
            new() { SemanticName = "DesertSand", LayerIndex = 17 },
            new() { SemanticName = "LavaRock", LayerIndex = 18 },
            new() { SemanticName = "LavaGround", LayerIndex = 19 },
            new() { SemanticName = "BlackDirt", LayerIndex = 20 },
            new() { SemanticName = "BlackSand", LayerIndex = 21 },
            new() { SemanticName = "FrozenLake", LayerIndex = 22 },
            new() { SemanticName = "JungleFloor", LayerIndex = 23 },
            new() { SemanticName = "SnowRock", LayerIndex = 24 },
            new() { SemanticName = "SwampMud", LayerIndex = 25 },
            new() { SemanticName = "DryForestFloor", LayerIndex = 26 },
            new() { SemanticName = "MeadowGrass", LayerIndex = 27 },
            new() { SemanticName = "WetRock", LayerIndex = 28 },
            new() { SemanticName = "RiverSand", LayerIndex = 29 },
            new() { SemanticName = "Ice", LayerIndex = 30 },
            new() { SemanticName = "WetSand", LayerIndex = 31 }
        };

        public UnityEngine.Object MicroSplatWorldAsset => microSplatWorldAsset;
        public UnityEngine.Object MicroSplatTemplateMaterial => microSplatTemplateMaterial;
        public UnityEngine.Object MicroSplatPropData => microSplatPropData;
        public TerrainLayer[] TerrainLayers => terrainLayers;
        public IReadOnlyList<WorldSurfaceLayerMapping> Mappings => mappings;

        private Dictionary<string, int> _layerIndexCache;

        public bool TryGetLayerIndex(string semanticName, out int layerIndex)
        {
            layerIndex = -1;
            if (string.IsNullOrEmpty(semanticName) || mappings == null) return false;

            _layerIndexCache ??= BuildLayerIndexCache();
            return _layerIndexCache.TryGetValue(semanticName, out layerIndex);
        }

        private Dictionary<string, int> BuildLayerIndexCache()
        {
            var cache = new Dictionary<string, int>(mappings.Count, StringComparer.Ordinal);
            for (var i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.SemanticName))
                    continue;
                cache[mapping.SemanticName] = mapping.LayerIndex;
            }

            return cache;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (mappings == null || mappings.Count == 0)
            {
                error = "WorldSurfacePalette has no mappings.";
                return false;
            }

            var seenSemantics = new HashSet<string>(StringComparer.Ordinal);
            var seenIndices = new HashSet<int>();

            for (var i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.SemanticName))
                {
                    error = $"Mapping {i} is missing a semantic name.";
                    return false;
                }

                if (mapping.LayerIndex < 0 || mapping.LayerIndex > MaxLayerIndex)
                {
                    error = $"Semantic '{mapping.SemanticName}' layer index {mapping.LayerIndex} is outside 0–{MaxLayerIndex}.";
                    return false;
                }

                if (!seenSemantics.Add(mapping.SemanticName))
                {
                    error = $"Duplicate semantic '{mapping.SemanticName}'.";
                    return false;
                }

                if (!seenIndices.Add(mapping.LayerIndex))
                {
                    error = $"Duplicate layer index {mapping.LayerIndex}.";
                    return false;
                }

                var isInfraRange = mapping.LayerIndex >= InfrastructureMinIndex &&
                                   mapping.LayerIndex <= InfrastructureMaxIndex;
                if (mapping.IsInfrastructure != isInfraRange)
                {
                    error =
                        $"Semantic '{mapping.SemanticName}' infrastructure flag must match reserved indices {InfrastructureMinIndex}–{InfrastructureMaxIndex}.";
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            _layerIndexCache = null;
            if (!Validate(out var error))
                Debug.LogWarning($"[WorldSurfacePalette] {name}: {error}", this);
        }
    }
}
