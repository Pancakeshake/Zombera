using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Building placement configuration for city generation.
    ///     Controls catalog selection, proxy usage, and building layout settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/World/New SOs/City Building Placement Config", fileName = "CityBuildingPlacementConfig")]
    public sealed class CityBuildingPlacementConfig : ScriptableObject
    {
        [Header("Catalog")]
        [Tooltip("Weighted building catalog used for lot selection.")]
        public StreamedCityCatalog buildingCatalog;

        [Tooltip("When enabled, spawns lightweight proxy prefabs first when a catalog entry provides one.")]
        public bool useProxyPrefabs = true;

        [Header("Layout")]
        public CityNamedAreaBuildingLayoutSettings buildingLayoutSettings =
            CityNamedAreaBuildingLayoutSettings.CreateDefault();

        [Header("Generation")]
        public int layoutSeed = 12345;

        [Tooltip("When enabled, uses the world seed for layout generation.")]
        public bool useWorldSeedForLayout;

        public int ResolveLayoutSeed(int worldSeed)
        {
            if (useWorldSeedForLayout && worldSeed != 0)
                return worldSeed;
            return layoutSeed != 0 ? layoutSeed : 12345;
        }

        public static CityBuildingPlacementConfig CreateDefault()
        {
            var config = CreateInstance<CityBuildingPlacementConfig>();
            config.useProxyPrefabs = true;
            config.buildingLayoutSettings = CityNamedAreaBuildingLayoutSettings.CreateDefault();
            config.layoutSeed = 12345;
            config.useWorldSeedForLayout = false;
            return config;
        }
    }
}
