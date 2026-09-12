using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    /// <summary>
    ///     City-visible runtime city-area builder contract. Concrete MapMagic
    ///     implementation lives in Legacy; World serializes this base type.
    /// </summary>
    public abstract class WorldRuntimeCityAreaBuilder : MonoBehaviour
    {
        public abstract bool SpawnCityRoadMeshes { get; }

        public abstract bool HasCompletedRoadsForFocusTile();
        public abstract bool HasCompletedCityBuildForFocusTile();

        public abstract void Configure(WorldTileStreamSource streamBridge, CityAreaRuntimeConfig runtimeConfig);
    }
}
