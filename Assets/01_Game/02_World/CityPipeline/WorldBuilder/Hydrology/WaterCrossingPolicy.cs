using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public enum WaterCrossingPolicy : byte
    {
        Avoid = 0,
        Ford = 1,
        Causeway = 2,
        Bridge = 3
    }

    public static class WaterCrossingPolicyResolver
    {
        public static WaterCrossingPolicy Resolve(
            HydrologyProfile profile,
            RoadClass roadClass,
            float depthMeters,
            float widthMeters,
            float bankSlopeDegrees)
        {
            if (profile == null) return WaterCrossingPolicy.Avoid;

            if (depthMeters <= profile.FordMaxDepthMeters &&
                widthMeters <= profile.FordMaxWidthMeters &&
                bankSlopeDegrees <= profile.FordMaxBankSlopeDegrees)
            {
                return WaterCrossingPolicy.Ford;
            }

            var arterialOrHighway = roadClass == RoadClass.Highway || roadClass == RoadClass.Arterial;
            if (arterialOrHighway &&
                depthMeters <= profile.CausewayMaxDepthMeters &&
                widthMeters <= profile.CausewayMaxWidthMeters)
            {
                return WaterCrossingPolicy.Causeway;
            }

            if (depthMeters > 0.01f || widthMeters > 0.01f)
                return WaterCrossingPolicy.Bridge;

            return WaterCrossingPolicy.Avoid;
        }
    }
}
