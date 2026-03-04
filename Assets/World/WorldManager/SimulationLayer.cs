using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Simulation layer classification by distance from player.
    /// </summary>
    public enum WorldSimulationLayer
    {
        Full,
        Reduced,
        Abstract
    }

    /// <summary>
    /// Shared utility for converting player distance into simulation layers.
    /// </summary>
    public static class WorldSimulationLayerUtility
    {
        public static WorldSimulationLayer GetLayer(float distance, float fullRange, float reducedRange)
        {
            if (distance <= Mathf.Max(0f, fullRange))
            {
                return WorldSimulationLayer.Full;
            }

            if (distance <= Mathf.Max(fullRange, reducedRange))
            {
                return WorldSimulationLayer.Reduced;
            }

            return WorldSimulationLayer.Abstract;
        }
    }
}