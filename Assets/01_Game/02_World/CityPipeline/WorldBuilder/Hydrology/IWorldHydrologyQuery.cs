using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldHydrologyQuery
    {
        bool TryGetNearestRiver(Vector2 worldXZ, out WorldRiverSample sample);
        float SampleWaterDepth(Vector2 worldXZ);
        float SampleDistanceToWater(Vector2 worldXZ);
        bool TrySampleWater(Vector2 worldXZ, out WorldWaterSample sample);
        bool TryFindCrossing(Vector2 from, Vector2 to, out WaterCrossing crossing);
    }
}
