using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Shared bilinear biome weight sampling for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private struct BiomeBilinearCoords
        {
            public int X0;
            public int Z0;
            public float Tx;
            public float Tz;

            public static BiomeBilinearCoords FromWorld(
                float worldX,
                float worldZ,
                Vector2 origin,
                float invCellSize)
            {
                var fx = (worldX - origin.x) * invCellSize - 0.5f;
                var fz = (worldZ - origin.y) * invCellSize - 0.5f;
                var x0 = Mathf.FloorToInt(fx);
                var z0 = Mathf.FloorToInt(fz);
                return new BiomeBilinearCoords
                {
                    X0 = x0,
                    Z0 = z0,
                    Tx = fx - x0,
                    Tz = fz - z0,
                };
            }

            public float SampleWeight(BiomeField biomes, int biomeIndex)
            {
                if (biomes == null || biomeIndex < 0)
                    return 0f;

                var w00 = SampleBiomeWeightClamped(biomes, X0, Z0, biomeIndex);
                var w10 = SampleBiomeWeightClamped(biomes, X0 + 1, Z0, biomeIndex);
                var w01 = SampleBiomeWeightClamped(biomes, X0, Z0 + 1, biomeIndex);
                var w11 = SampleBiomeWeightClamped(biomes, X0 + 1, Z0 + 1, biomeIndex);

                var wx0 = Mathf.Lerp(w00, w10, Tx);
                var wx1 = Mathf.Lerp(w01, w11, Tx);
                return Mathf.Lerp(wx0, wx1, Tz);
            }
        }

        private float SampleBiomeWeightBilinearFast(
            BiomeField biomes,
            float worldX,
            float worldZ,
            int biomeIndex) =>
            BiomeBilinearCoords.FromWorld(worldX, worldZ, _paintLandformOrigin, _paintInvLandformCellSize)
                .SampleWeight(biomes, biomeIndex);

        private float SampleSnowBiomeWeight(
            BiomeField biomes,
            in BiomeBilinearCoords coords)
        {
            var snow = _snowBiomeIndex >= 0 ? coords.SampleWeight(biomes, _snowBiomeIndex) : 0f;
            var alpine = _alpineSnowBiomeIndex >= 0
                ? coords.SampleWeight(biomes, _alpineSnowBiomeIndex)
                : 0f;
            return Mathf.Max(snow, alpine);
        }
    }
}
