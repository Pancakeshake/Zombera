using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldPoiEntry
    {
        public string StableId = "Poi_Cabin";
        public GameObject Prefab;
        public string[] AllowedBiomeIds = { "Plains", "Badlands" };
        [Min(0f)] public float DensityPerKm2 = 0.35f;
        [Min(0f)] public float Weight = 1f;
        public Vector2 ScaleRange = new(1f, 1f);
        [Range(0f, 90f)] public float MaxSlopeDegrees = 12f;
        public Vector2 ElevationRangeMeters = new(0f, 450f);
        [Min(0f)] public float MinDistanceToWaterMeters = 8f;
        [Min(0f)] public float ExclusionRadiusMeters = 18f;
        public Vector2 FootprintMeters = new(12f, 12f);
        public Vector2 RoadDistanceRangeMeters = new(40f, 420f);
        [Min(0f)] public float MinInterPoiDistanceMeters = 180f;
        public string MapMarkerId;
        [Min(0)] public int MaxInstancesPerMap = 24;
    }

    /// <summary>Wilderness POI catalog with placement constraints.</summary>
    [CreateAssetMenu(
        fileName = "WorldPoiProfile",
        menuName = "Zombera/World/World POI Profile")]
    public sealed class WorldPoiProfile : ScriptableObject
    {
        [SerializeField] private List<WorldPoiEntry> entries = new();

        public IReadOnlyList<WorldPoiEntry> Entries => entries;
    }
}
