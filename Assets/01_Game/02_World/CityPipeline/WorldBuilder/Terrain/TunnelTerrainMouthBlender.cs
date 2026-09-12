using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Intentionally empty: height blending around tunnels was creating road-grade
    ///     scars over the bore. Mouth seating is holes-only + scanner surface Y.
    /// </summary>
    public static class TunnelTerrainMouthBlender
    {
        // Kept so older call sites compile if any remain; no terrain writes.
        public static void BlendMouthApproaches(in MountainTunnel tunnel, RoadNetworkSettings settings)
        {
        }
    }
}
