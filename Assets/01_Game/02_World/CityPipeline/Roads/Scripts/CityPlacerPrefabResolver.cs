using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Centralised prefab source for streetscape placers.
    ///     All prefabs are assigned in the City Prefab Catalog and resolved here
    ///     so placers don't need direct references to the catalog asset.
    /// </summary>
    internal static class CityPlacerPrefabResolver
    {
        private static CityStreetscapeConfig _catalog;
        internal static CityStreetscapeConfig Catalog
        {
            get
            {
                if (_catalog == null)
                    _catalog = Resources.Load<CityStreetscapeConfig>("World/CityStreetscapeConfig");
                return _catalog;
            }
            set => _catalog = value;
        }

        internal static GameObject TrafficSignal => Catalog != null ? Catalog.trafficSignalPrefab : null;
        internal static GameObject StreetSign => Catalog != null ? Catalog.streetSignPrefab : null;
        internal static GameObject StreetLamp => Catalog != null ? Catalog.streetLampPrefab : null;
        internal static GameObject UtilityPole => Catalog != null ? Catalog.utilityPolePrefab : null;
        internal static Material PowerLineWireMaterial => Catalog != null ? Catalog.powerLineWireMaterial : null;
        internal static GameObject Bench => Catalog != null ? Catalog.benchPrefab : null;
        internal static GameObject Mailbox => Catalog != null ? Catalog.mailboxPrefab : null;
        internal static GameObject FireHydrant => Catalog != null ? Catalog.fireHydrantPrefab : null;
        internal static GameObject TrashCan => Catalog != null ? Catalog.trashCanPrefab : null;
        internal static GameObject[] ParkedCars => Catalog != null ? Catalog.parkedCarPrefabs : null;
    }
}
