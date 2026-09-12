using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    /// <summary>
    ///     City-visible streamed city builder contract. Concrete MapMagic
    ///     implementation lives in Legacy; World serializes this base type.
    /// </summary>
    public abstract class WorldStreamedCityBuilder : MonoBehaviour
    {
        public abstract bool HasAnyValidBuildingEntries { get; }
        public abstract bool HasProcessedCityTiles { get; protected set; }
        public abstract int ProcessedCityTileCount { get; protected set; }
        public abstract int PendingCityTileRequestCount { get; }

        public abstract void Configure(WorldTileStreamSource streamBridge, StreamedCityCatalog catalog);

        /// <summary>Called after road meshes are ready; Legacy implementations spawn city buildings.</summary>
        public abstract void GenerateBuildingsForFinishedRoads();
    }
}
