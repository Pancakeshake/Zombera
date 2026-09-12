using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Deepen-only shelf reef blotches for Crest depth colour (no dry berms).
    /// </summary>
    public static class CoastalShelfReefUtility
    {
        public const float MinWaterCoverMeters = 0.25f;
        public const float MaxShelfFloorDepthMeters = 14f;

        /// <summary>
        /// Lowers underwater shelf cells where noise exceeds coverage threshold.
        /// Never raises height; never emerges above <c>seaLevel - MinWaterCoverMeters</c>.
        /// </summary>
        public static float ApplyDeepen(
            float height,
            float seaLevel,
            float worldX,
            float worldZ,
            float scaleMeters,
            float amplitudeMeters,
            float coverage,
            DeterministicNoise2D noise)
        {
            if (noise == null || amplitudeMeters <= 0.01f || scaleMeters < 1f || coverage <= 0f)
                return ClampUnderwater(height, seaLevel);

            var n = noise.Fbm(worldX / scaleMeters, worldZ / scaleMeters);
            var threshold = 1f - Mathf.Clamp01(coverage);
            if (n <= threshold)
                return ClampUnderwater(height, seaLevel);

            var t = Mathf.InverseLerp(threshold, 1f, n);
            height -= t * amplitudeMeters;
            return ClampUnderwater(height, seaLevel);
        }

        public static float ClampUnderwater(float height, float seaLevel)
        {
            height = Mathf.Min(height, seaLevel - MinWaterCoverMeters);
            height = Mathf.Max(height, seaLevel - MaxShelfFloorDepthMeters);
            return height;
        }
    }
}
