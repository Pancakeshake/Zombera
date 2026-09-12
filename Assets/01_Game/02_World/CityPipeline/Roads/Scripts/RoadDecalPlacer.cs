using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Junction decal placer stub. EasyRoads ERConnection walks were removed;
    ///     procedural hubs use first-party marking paths instead.
    /// </summary>
    public static partial class RoadDecalPlacer
    {
        private const string DecalsContainerName = "Road Decals";

        /// <summary>
        ///     No-op after EasyRoads purge. Kept so City Prefab menus still compile.
        /// </summary>
        public static int GenerateForHub(ProceduralRoadSystem roadSystem, RoadNetworkSettings settings)
        {
            _ = roadSystem;
            _ = settings;
            return 0;
        }

        /// <summary>
        ///     Remove the Road Decals container (and all placed decals) from the scene.
        ///     The container may sit under the legacy EasyRoads root OR the procedural
        ///     root, so it is matched by name rather than by parent.
        /// </summary>
        public static void Clear()
        {
            RoadLegacyContainerCleanup.DestroyContainer(DecalsContainerName);
        }
    }
}
