using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Root generation profile composing all World Builder sub-profiles.</summary>
    [CreateAssetMenu(
        fileName = "WorldGenerationProfile",
        menuName = "Zombera/World/World Generation Profile")]
    public sealed class WorldGenerationProfile : ScriptableObject
    {
        [SerializeField] private int profileVersion = 3;
        [Tooltip("Bump when natural alphamap paint algorithm / band / soft policy changes identity.")]
        [SerializeField] private int surfacePaintAlgorithmVersion = 6;

        [Header("Sub-profiles")]
        [SerializeField] private WorldMapSizeSettings mapSizeSettings;
        [SerializeField] private TerrainGridProfile terrainGrid;
        [SerializeField] private LandformProfile landforms;
        [SerializeField] private HydrologyProfile hydrology;
        [SerializeField] private WorldWaterProfile water;
        [SerializeField] private WorldBiomePalette biomes;
        [SerializeField] private WorldSurfacePalette surfaces;
        [SerializeField] private WorldNatureProfile nature;
        [SerializeField] private WorldPoiProfile pois;
        [SerializeField] private WorldEnvironmentProfile environment;

        [Header("Existing City / Road Assets")]
        [SerializeField] private RoadNetworkSettings roadNetworkSettings;
        [SerializeField] private CityRegionAsset cityRegion;

        public int ProfileVersion => profileVersion;
        public int SurfacePaintAlgorithmVersion => surfacePaintAlgorithmVersion;
        public WorldMapSizeSettings MapSizeSettings => mapSizeSettings;
        public TerrainGridProfile TerrainGrid => terrainGrid;
        public LandformProfile Landforms => landforms;
        public HydrologyProfile Hydrology => hydrology;
        public WorldWaterProfile Water => water;
        public WorldBiomePalette Biomes => biomes;
        public WorldSurfacePalette Surfaces => surfaces;
        public WorldNatureProfile Nature => nature;
        public WorldPoiProfile Pois => pois;
        public WorldEnvironmentProfile Environment => environment;
        public RoadNetworkSettings RoadNetworkSettings => roadNetworkSettings;
        public CityRegionAsset CityRegion => cityRegion;

        /// <summary>Stable fingerprint of profile version + key sub-profile fields.</summary>
        public ulong ComputeFingerprint() => WorldProfileFingerprints.Compute(this);

        private void OnValidate()
        {
            LogMissing(mapSizeSettings, nameof(mapSizeSettings));
            LogMissing(terrainGrid, nameof(terrainGrid));
            LogMissing(landforms, nameof(landforms));
            LogMissing(hydrology, nameof(hydrology));
            LogMissing(water, nameof(water));
            LogMissing(biomes, nameof(biomes));
            LogMissing(surfaces, nameof(surfaces));
            LogMissing(nature, nameof(nature));
            LogMissing(pois, nameof(pois));
            LogMissing(environment, nameof(environment));
            LogMissing(roadNetworkSettings, nameof(roadNetworkSettings));
            LogMissing(cityRegion, nameof(cityRegion));

            if (surfaces != null && !surfaces.Validate(out var surfaceError))
                Debug.LogWarning($"[WorldGenerationProfile] {name}: {surfaceError}", this);
        }

        private void LogMissing(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null) return;
            Debug.LogWarning($"[WorldGenerationProfile] {name}: missing reference '{fieldName}'.", this);
        }
    }
}
