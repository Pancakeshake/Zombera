using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    /// <summary>
    ///     Biome gating configuration for city generation.
    ///     Controls whether a tile qualifies as a city area based on biome dominance.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/World/New SOs/City Biome Config", fileName = "CityBiomeConfig")]
    public sealed class CityBiomeConfig : ScriptableObject
    {
        [Tooltip("Minimum fraction of tile area that must be City_Area biome to trigger a city build.")]
        [Range(0f, 1f)] public float biomeDominanceThreshold = WorldTileInfoUtility.DefaultCityAreaDominanceThreshold;

        [Tooltip("When enabled, the single-tile stress scene always builds a city on the pinned tile.")]
        public bool forceBuildOnSingleTileStressSession = true;

        public static CityBiomeConfig CreateDefault()
        {
            var config = CreateInstance<CityBiomeConfig>();
            config.biomeDominanceThreshold = WorldTileInfoUtility.DefaultCityAreaDominanceThreshold;
            config.forceBuildOnSingleTileStressSession = true;
            return config;
        }
    }
}
