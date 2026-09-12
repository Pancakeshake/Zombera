using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public readonly struct WorldRiverSample
    {
        public readonly ulong RiverId;
        public readonly Vector2 ClosestPointXZ;
        public readonly float DistanceMeters;
        public readonly float WidthMeters;
        public readonly float DepthMeters;

        public WorldRiverSample(
            ulong riverId,
            Vector2 closestPointXZ,
            float distanceMeters,
            float widthMeters,
            float depthMeters)
        {
            RiverId = riverId;
            ClosestPointXZ = closestPointXZ;
            DistanceMeters = distanceMeters;
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
        }
    }
}
