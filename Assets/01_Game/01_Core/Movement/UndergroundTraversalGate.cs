using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    ///     Optional world hook so terrain-authority grounding can yield to underground
    ///     NavMesh (enterable tunnel bores) without Core→World assembly cycles.
    /// </summary>
    public static class UndergroundTraversalGate
    {
        public static System.Func<Vector3, bool> IsInsideUndergroundVolume;

        public static bool ShouldPreferTerrainHeight(bool terrainAuthorityConfigured, Vector3 worldPosition)
        {
            if (!terrainAuthorityConfigured)
                return false;

            var fn = IsInsideUndergroundVolume;
            if (fn != null && fn(worldPosition))
                return false;

            return true;
        }
    }
}
