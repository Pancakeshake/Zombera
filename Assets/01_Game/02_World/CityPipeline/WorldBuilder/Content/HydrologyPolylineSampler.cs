using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Samples hydrology presentation data along river polylines.</summary>
    public static class HydrologyPolylineSampler
    {
        public const float DefaultWidthMeters = 6f;

        public static float SampleSurfaceY(HydrologyPlan plan, Vector2 worldXZ)
        {
            if (TrySampleSurfaceY(plan, worldXZ, out var value))
                return value;
            return 0f;
        }

        /// <summary>
        /// Preserved free surface at a point: the wet cell itself, else the nearest wet cell.
        /// Returns false when the plan has no wet data at all, so callers can pick a
        /// non-zero fallback instead of silently producing sea level.
        /// </summary>
        public static bool TrySampleSurfaceY(HydrologyPlan plan, Vector2 worldXZ, out float value)
        {
            if (TrySampleWetSurface(plan, worldXZ, out value))
                return true;
            return TryFindNearestSurface(plan, worldXZ, out value);
        }

        public static float ResolveWidthMeters(float[] widths, int index)
        {
            if (widths != null && index >= 0 && index < widths.Length && widths[index] > 0f)
                return widths[index];
            return DefaultWidthMeters;
        }

        public static float[] BuildFreeSurfaceYs(HydrologyPlan plan, Vector2[] pointsXZ)
        {
            if (pointsXZ == null || pointsXZ.Length == 0)
                return System.Array.Empty<float>();

            var ys = new float[pointsXZ.Length];
            for (var i = 0; i < pointsXZ.Length; i++)
                ys[i] = SampleSurfaceY(plan, pointsXZ[i]);

            var smoothed = (float[])ys.Clone();
            for (var i = 1; i < ys.Length - 1; i++)
                ys[i] = (smoothed[i - 1] + smoothed[i] + smoothed[i + 1]) / 3f;

            for (var i = 1; i < ys.Length; i++)
                ys[i] = Mathf.Min(ys[i], ys[i - 1]);

            return ys;
        }

        public static float ResolveMaxWidthMeters(RiverPolyline river)
        {
            if (river?.WidthMeters == null || river.WidthMeters.Length == 0)
                return DefaultWidthMeters;

            var max = 0f;
            for (var i = 0; i < river.WidthMeters.Length; i++)
                max = Mathf.Max(max, river.WidthMeters[i]);
            return max > 0f ? max : DefaultWidthMeters;
        }

        public static float ResolveFlowVelocity(RiverPolyline river, float minVelocity, float maxVelocity)
        {
            var accumulation = river != null ? river.FlowAccumulation : 0f;
            return ResolveFlowVelocity(accumulation, minVelocity, maxVelocity);
        }

        public static float[] BuildFlowVelocities(
            HydrologyPlan plan,
            RiverPolyline river,
            IReadOnlyList<Vector2> pointsXZ,
            float minVelocity,
            float maxVelocity)
        {
            if (pointsXZ == null || pointsXZ.Count == 0)
                return System.Array.Empty<float>();

            var velocities = new float[pointsXZ.Count];
            var fallbackAccumulation = river != null ? river.FlowAccumulation : 0f;
            for (var i = 0; i < pointsXZ.Count; i++)
            {
                var accumulation = TrySamplePlanField(
                    plan,
                    plan != null ? plan.FlowAccumulation : null,
                    pointsXZ[i],
                    out var sampled)
                    ? sampled
                    : fallbackAccumulation;
                var velocity = ResolveFlowVelocity(accumulation, minVelocity, maxVelocity);
                velocities[i] = i > 0 ? Mathf.Max(velocities[i - 1], velocity) : velocity;
            }

            return velocities;
        }

        private static float ResolveFlowVelocity(float accumulation, float minVelocity, float maxVelocity)
        {
            var minimum = Mathf.Min(minVelocity, maxVelocity);
            var maximum = Mathf.Max(minVelocity, maxVelocity);
            return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(accumulation * 0.0001f));
        }

        private static bool TrySamplePlanField(
            HydrologyPlan plan,
            float[] values,
            Vector2 worldXZ,
            out float value)
        {
            value = 0f;
            if (plan == null || values == null || values.Length != plan.Width * plan.Height)
                return false;

            var x = Mathf.FloorToInt((worldXZ.x - plan.OriginXZ.x) / plan.CellSizeMeters);
            var z = Mathf.FloorToInt((worldXZ.y - plan.OriginXZ.y) / plan.CellSizeMeters);
            if (x < 0 || z < 0 || x >= plan.Width || z >= plan.Height)
                return false;

            value = values[plan.Index(x, z)];
            return true;
        }

        private static bool TrySampleWetSurface(HydrologyPlan plan, Vector2 worldXZ, out float value)
        {
            value = 0f;
            if (!TryGetCell(plan, worldXZ, out var x, out var z))
                return false;
            var cell = plan.Index(x, z);
            if (plan.WaterClass[cell] == WorldWaterClass.None)
                return false;
            value = plan.SurfaceWorldY[cell];
            return true;
        }

        private static bool TryFindNearestSurface(HydrologyPlan plan, Vector2 worldXZ, out float value)
        {
            value = 0f;
            if (!TryGetCell(plan, worldXZ, out var originX, out var originZ))
                return false;
            var bestDistance = int.MaxValue;
            var found = false;
            var radius = 1;
            while (radius <= 8 && !found)
            {
                var minX = Mathf.Max(0, originX - radius);
                var maxX = Mathf.Min(plan.Width - 1, originX + radius);
                var minZ = Mathf.Max(0, originZ - radius);
                var maxZ = Mathf.Min(plan.Height - 1, originZ + radius);
                for (var z = minZ; z <= maxZ; z++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var index = plan.Index(x, z);
                        if (plan.WaterClass[index] == WorldWaterClass.None)
                            continue;
                        var distance = Mathf.Abs(x - originX) + Mathf.Abs(z - originZ);
                        if (distance >= bestDistance)
                            continue;
                        bestDistance = distance;
                        value = plan.SurfaceWorldY[index];
                        found = true;
                    }
                }
                radius++;
            }
            return found;
        }

        private static bool TryGetCell(HydrologyPlan plan, Vector2 worldXZ, out int x, out int z)
        {
            x = z = 0;
            if (plan == null || plan.SurfaceWorldY == null ||
                plan.SurfaceWorldY.Length != plan.Width * plan.Height)
                return false;
            x = Mathf.FloorToInt((worldXZ.x - plan.OriginXZ.x) / plan.CellSizeMeters);
            z = Mathf.FloorToInt((worldXZ.y - plan.OriginXZ.y) / plan.CellSizeMeters);
            return x >= 0 && z >= 0 && x < plan.Width && z < plan.Height;
        }
    }
}
