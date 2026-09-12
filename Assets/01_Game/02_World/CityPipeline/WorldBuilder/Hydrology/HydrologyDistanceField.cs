using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Two-pass chamfer distance-to-water field for <see cref="HydrologyPlan"/>.</summary>
    public static class HydrologyDistanceField
    {
        public static void Compute(HydrologyPlan plan)
        {
            if (plan == null) return;

            var count = plan.Width * plan.Height;
            var width = plan.Width;
            var orthStep = plan.CellSizeMeters;
            var diagStep = orthStep * 1.41421356f;
            var waterClass = plan.WaterClass;
            var distances = plan.DistanceToWaterMeters;

            for (var i = 0; i < count; i++)
                distances[i] = waterClass[i] == WorldWaterClass.None ? float.PositiveInfinity : 0f;

            for (var z = 0; z < plan.Height; z++)
            {
                var row = z * width;
                for (var x = 0; x < width; x++)
                {
                    var i = row + x;
                    if (x > 0)
                        distances[i] = Mathf.Min(distances[i], distances[i - 1] + orthStep);
                    if (z > 0)
                        distances[i] = Mathf.Min(distances[i], distances[i - width] + orthStep);
                    if (x > 0 && z > 0)
                        distances[i] = Mathf.Min(distances[i], distances[i - width - 1] + diagStep);
                    if (x < width - 1 && z > 0)
                        distances[i] = Mathf.Min(distances[i], distances[i - width + 1] + diagStep);
                }
            }

            for (var z = plan.Height - 1; z >= 0; z--)
            {
                var row = z * width;
                for (var x = width - 1; x >= 0; x--)
                {
                    var i = row + x;
                    if (x < width - 1)
                        distances[i] = Mathf.Min(distances[i], distances[i + 1] + orthStep);
                    if (z < plan.Height - 1)
                        distances[i] = Mathf.Min(distances[i], distances[i + width] + orthStep);
                    if (x < width - 1 && z < plan.Height - 1)
                        distances[i] = Mathf.Min(distances[i], distances[i + width + 1] + diagStep);
                    if (x > 0 && z < plan.Height - 1)
                        distances[i] = Mathf.Min(distances[i], distances[i + width - 1] + diagStep);
                }
            }

            var fallback = orthStep * (width + plan.Height);
            for (var i = 0; i < count; i++)
            {
                if (float.IsPositiveInfinity(distances[i]))
                    distances[i] = fallback;
            }
        }
    }
}
