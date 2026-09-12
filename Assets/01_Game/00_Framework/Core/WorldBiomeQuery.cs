using System;
using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     Cross-assembly biome lookup. World registers a resolver (typically RegionSystem);
    ///     City placement rules consume tokens without referencing RegionSystem.
    /// </summary>
    public static class WorldBiomeQuery
    {
        /// <summary>
        ///     Returns (regionId, biomeName) for a world position. Null/empty when unknown.
        /// </summary>
        public static Func<Vector3, (string regionId, string biomeName)> Resolve { get; set; }

        public static bool TryGetAt(Vector3 worldPosition, out string regionId, out string biomeName)
        {
            regionId = null;
            biomeName = null;
            if (Resolve == null)
                return false;

            var result = Resolve(worldPosition);
            regionId = result.regionId;
            biomeName = result.biomeName;
            return !string.IsNullOrEmpty(regionId) || !string.IsNullOrEmpty(biomeName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload() => Resolve = null;
    }
}
