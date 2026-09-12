using UnityEngine;

namespace Zombera.World.Roads
{
    public static class ProceduralRoadGenerator
    {
        public static RoadNetworkRuntime Generate(int seed, RoadNetworkSettings settings)
        {
            // MapMagic spline extraction lives in Legacy MapMagicRoadPlanSource /
            // ProceduralRoadSystem per-tile path. Global network generation is math-layout only.
            if (settings != null && settings.UsesMapMagicSplineLayout)
            {
                Debug.LogWarning(
                    "[ProceduralRoadGenerator] MapMagic spline global layout requested; " +
                    "falling back to mathematical world-map network. Per-tile splines use IWorldRoadPlanSource.");
            }

            return WorldMapRoadNetworkGenerator.Generate(seed, settings);
        }

        public static void RefinePlanForTerrain(RoadNetworkRuntime network, RoadNetworkSettings settings)
        {
            WorldMapRoadNetworkGenerator.RefinePlanForTerrain(network, settings);
        }
    }
}
