using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    /// Read-only accessors used by WorldState capture and the development hub.
    /// Kept as a thin partial so district/building files do not grow further.
    /// </summary>
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        /// <summary>Region seed from the most recent successful road publish; 0 if none.</summary>
        public int LastBuiltRegionSeed => lastBuiltRegionSeed;

        /// <summary>
        /// Copies the cached city road polylines into <paramref name="destination"/>.
        /// Rebuilds the runtime network cache first when needed.
        /// </summary>
        public void CopyGeneratedRoadPolylines(List<RoadPolyline> destination)
        {
            if (destination == null)
                throw new System.ArgumentNullException(nameof(destination));

            destination.Clear();
            EnsureRoadCache();
            if (_cachedRoadPolylines == null || _cachedRoadPolylines.Count == 0)
                return;

            for (var i = 0; i < _cachedRoadPolylines.Count; i++)
                destination.Add(_cachedRoadPolylines[i]);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Loads the assembled/proxy building catalog using this builder's configured folders.
        /// </summary>
        public List<CityAssembledBuildingCatalogEntry> LoadWorldStateBuildingCatalog()
        {
            return CityAssembledBuildingCatalogLoader.Load(ResolvedAssembledFolder, ResolvedProxyFolder);
        }
#endif
    }
}
