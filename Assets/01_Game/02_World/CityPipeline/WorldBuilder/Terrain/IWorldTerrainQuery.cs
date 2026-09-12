using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldTerrainQuery
    {
        bool TrySample(Vector2 worldXZ, out WorldTerrainSample sample);
        bool TrySampleHeight(Vector2 worldXZ, out float worldY);
        bool TrySampleNormal(Vector2 worldXZ, out Vector3 normal);
        bool TrySampleSlope(Vector2 worldXZ, out float degrees);
        bool TrySampleBiome(Vector2 worldXZ, out WorldBiomeSample sample);
        bool TrySampleWater(Vector2 worldXZ, out WorldWaterSample sample);
        float SampleBuildability(Vector2 worldXZ);
        float SampleNoBuildMask(Vector2 worldXZ);
        bool TryBuildCostField(Rect boundsXZ, WorldCostFieldOptions options, out IWorldCostField field);
    }
}
