using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Mesh-based intersection markings stub. EasyRoads junction walks were removed;
    ///     procedural city hubs use first-party junction markings instead.
    /// </summary>
    public static partial class CityIntersectionMarkingBuilder
    {
        /// <summary>
        ///     No-op after EasyRoads purge. Kept so City Prefab menus still compile.
        /// </summary>
        public static int BuildForHub(ProceduralRoadSystem roadSystem, RoadNetworkSettings settings)
        {
            _ = roadSystem;
            _ = settings;
            return 0;
        }

        /// <summary>
        ///     Always false — EasyRoads connection markings are no longer produced.
        /// </summary>
        public static bool HasMarkings(Object connection)
        {
            _ = connection;
            return false;
        }

        /// <summary>
        ///     Always false — EasyRoads connection markings are no longer produced.
        /// </summary>
        public static bool HasMarking(Object connection, int portIndex)
        {
            _ = connection;
            _ = portIndex;
            return false;
        }

        public static void Clear()
        {
            // Matched by name: the container may sit under the legacy EasyRoads root or
            // the procedural root, and nothing creates the legacy root any more.
            RoadLegacyContainerCleanup.DestroyContainer("Road Markings");
        }
    }
}
