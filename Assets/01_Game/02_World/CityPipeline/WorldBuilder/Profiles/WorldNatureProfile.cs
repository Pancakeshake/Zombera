using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public enum WorldNaturePlacementMode
    {
        TerrainTree = 0,
        GameObject = 1
    }

    [Serializable]
    public sealed class WorldNatureEntry
    {
        public string StableId = "Tree_Oak";
        public GameObject Prefab;
        public WorldNaturePlacementMode PlacementMode = WorldNaturePlacementMode.TerrainTree;
        public string[] AllowedBiomeIds = { "Plains" };
        [Min(0f)] public float DensityPerKm2 = 40f;
        [Min(0f)] public float Weight = 1f;
        public Vector2 ScaleRange = new(0.85f, 1.15f);
        [Range(0f, 90f)] public float MaxSlopeDegrees = 28f;
        /// <summary>Meters above sea level (HeightWorldY − Hydrology.SeaLevelWorldY).</summary>
        public Vector2 ElevationRangeMeters = new(-50f, 340f);
        [Min(0f)] public float MinDistanceToWaterMeters;
        [Min(0f)] public float ExclusionRadiusMeters = 2f;
        [Min(0)] public int MaxInstancesPerTile = 64;
        [Tooltip("When true, reject candidates unless painted grass alphamap weight meets MinGrassWeight.")]
        public bool RequireGrassSurface = true;
        [Range(0f, 1f)] public float MinGrassWeight = 0.35f;
    }

    /// <summary>Vegetation / rock scatter prototypes filtered by biome.</summary>
    [CreateAssetMenu(
        fileName = "WorldNatureProfile",
        menuName = "Zombera/World/World Nature Profile")]
    public sealed class WorldNatureProfile : ScriptableObject
    {
        [SerializeField] private List<WorldNatureEntry> entries = new();

        public IReadOnlyList<WorldNatureEntry> Entries => entries;
    }
}
