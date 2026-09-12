using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     City-visible road-network system contract. MapMagic tile-road runtime
    ///     implementation lives in Legacy.
    /// </summary>
    public abstract class WorldRoadNetworkSystem : MonoBehaviour
    {
        public abstract bool UsesLegacyRoadRuntime { get; }
        public abstract bool HasProcessedTileRoads { get; protected set; }
        public abstract int PendingTileRoadRequestCount { get; }
    }
}
