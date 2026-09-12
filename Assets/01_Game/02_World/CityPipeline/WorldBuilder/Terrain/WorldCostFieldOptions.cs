using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public sealed class WorldCostFieldOptions
    {
        public float CellSizeMeters = 8f;
        public float MaxNoBuildMask = 0.5f;
        public float MaxTraversableWaterDepthMeters = 0.3f;
        /// <summary>Highway/arterial may enter water up to this depth (bridge/causeway corridors).</summary>
        public float MaxBridgeableWaterDepthMeters = 4f;
        /// <summary>Soft cost = waterDepth * this value.</summary>
        public float WaterSoftCostPerMeterDepth = 28f;
        public RoadNetworkSettings RoadSettings;
        public OrogenPlan Orogen;
        public float PassAttractHalfWidthMeters = 120f;
        public float OrogenCoreCostMultiplier = 2.4f;
        public float PassAttractCostMultiplier = 0.55f;
    }
}
