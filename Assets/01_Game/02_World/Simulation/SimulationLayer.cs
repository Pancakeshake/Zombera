namespace Zombera.World.Simulation
{
    public enum WorldSimulationLayer
    {
        Full,
        Reduced,
        Abstract
    }

    public static class WorldSimulationLayerUtility
    {
        public static WorldSimulationLayer GetLayer(float distance, float fullRange, float reducedRange)
        {
            if (distance <= fullRange) return WorldSimulationLayer.Full;
            if (distance <= reducedRange) return WorldSimulationLayer.Reduced;
            return WorldSimulationLayer.Abstract;
        }
    }
}
