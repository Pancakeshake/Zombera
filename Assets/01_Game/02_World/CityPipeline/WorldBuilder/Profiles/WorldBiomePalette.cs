using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldBiomeSurfaceWeight
    {
        public string SurfaceSemantic = "GrassGreen";
        [Range(0f, 1f)] public float Weight = 1f;
    }

    [Serializable]
    public sealed class WorldBiomeRecord
    {
        public string StableId = "Plains";
        public AnimationCurve TemperatureCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve MoistureCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve ElevationCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve SlopeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        [Min(0f)] public float Priority = 1f;
        [Range(0f, 1f)] public float BuildabilityMultiplier = 1f;
        [Min(0.01f)] public float RoadCostMultiplier = 1f;
        public List<WorldBiomeSurfaceWeight> SurfaceWeights = new();
        public bool IsCityAreaOverlay;
    }

    /// <summary>Biome classification rules and natural surface weight tables.</summary>
    [CreateAssetMenu(
        fileName = "WorldBiomePalette",
        menuName = "Zombera/World/World Biome Palette")]
    public sealed partial class WorldBiomePalette : ScriptableObject
    {
        [SerializeField] private List<WorldBiomeRecord> biomes = new();

        public IReadOnlyList<WorldBiomeRecord> Biomes => biomes;

        public bool TryGetBiome(string stableId, out WorldBiomeRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(stableId) || biomes == null) return false;

            for (var i = 0; i < biomes.Count; i++)
            {
                var candidate = biomes[i];
                if (candidate == null || candidate.StableId != stableId) continue;
                record = candidate;
                return true;
            }

            return false;
        }
    }
}
