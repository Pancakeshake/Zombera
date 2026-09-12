using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.World.City
{
    /// <summary>
    ///     Runtime entry point for placing buildings inside generated city named areas.
    /// </summary>
    public static class RuntimeCityAreaBuildingPlacer
    {
        public static int PlaceBuildingsInAreas(
            Transform areasRoot,
            StreamedCityCatalog catalog,
            CityNamedAreaBuildingLayoutSettings layoutSettings,
            bool useProxyPrefabs,
            int layoutSeed,
            float minimumStreetSetbackMeters,
            RuntimePlacedStructureFixer structureFixer,
            out string summary)
        {
            var entries = CityNamedAreaBuildingSpawnUtility.BuildCatalogFromStreamed(catalog);
            return CityNamedAreaBuildingSpawnUtility.SpawnPlacements(
                areasRoot,
                entries,
                layoutSettings,
                useProxyPrefabs,
                layoutSeed,
                minimumStreetSetbackMeters,
                structureFixer,
                out summary);
        }
    }
}
