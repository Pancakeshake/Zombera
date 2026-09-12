using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public readonly struct WorldTerrainSample
    {
        public readonly Vector2 WorldXZ;
        public readonly float HeightWorldY;
        public readonly Vector3 Normal;
        public readonly float SlopeDegrees;
        public readonly WorldBiomeSample Biome;
        public readonly WorldWaterSample Water;
        public readonly float Buildability;
        public readonly float NoBuildMask;

        public WorldTerrainSample(
            Vector2 worldXZ,
            float heightWorldY,
            Vector3 normal,
            float slopeDegrees,
            WorldBiomeSample biome,
            WorldWaterSample water,
            float buildability,
            float noBuildMask)
        {
            WorldXZ = worldXZ;
            HeightWorldY = heightWorldY;
            Normal = normal;
            SlopeDegrees = slopeDegrees;
            Biome = biome;
            Water = water;
            Buildability = buildability;
            NoBuildMask = noBuildMask;
        }
    }
}
