using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Bilinear sampling helpers for <see cref="LandformField"/>.</summary>
    public static class LandformFieldSampling
    {
        public static float SampleBilinear(LandformField field, float worldX, float worldZ)
        {
            if (field == null || field.WorldHeights == null || field.WorldHeights.Length == 0)
                return 0f;

            var invCell = 1f / field.CellSize;
            var fx = (worldX - field.OriginXZ.x) * invCell - 0.5f;
            var fz = (worldZ - field.OriginXZ.y) * invCell - 0.5f;
            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var tx = fx - x0;
            var tz = fz - z0;

            var h00 = SampleClamped(field, x0, z0);
            var h10 = SampleClamped(field, x0 + 1, z0);
            var h01 = SampleClamped(field, x0, z0 + 1);
            var h11 = SampleClamped(field, x0 + 1, z0 + 1);

            var hx0 = Mathf.Lerp(h00, h10, tx);
            var hx1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(hx0, hx1, tz);
        }

        public static float SampleClamped(LandformField field, int x, int z)
        {
            x = Mathf.Clamp(x, 0, field.Width - 1);
            z = Mathf.Clamp(z, 0, field.Height - 1);
            return field.WorldHeights[field.Index(x, z)];
        }

        public static float EstimateSlopeDegrees(LandformField field, int x, int z)
        {
            if (field == null) return 0f;
            var hL = SampleClamped(field, x - 1, z);
            var hR = SampleClamped(field, x + 1, z);
            var hD = SampleClamped(field, x, z - 1);
            var hU = SampleClamped(field, x, z + 1);
            return SlopeFromHeightDeltas(field.CellSize, hL, hR, hD, hU);
        }

        public static float EstimateSlopeDegreesBilinear(LandformField field, float worldX, float worldZ)
        {
            if (field == null) return 0f;

            var cellSize = field.CellSize;
            var hL = SampleBilinear(field, worldX - cellSize, worldZ);
            var hR = SampleBilinear(field, worldX + cellSize, worldZ);
            var hD = SampleBilinear(field, worldX, worldZ - cellSize);
            var hU = SampleBilinear(field, worldX, worldZ + cellSize);
            return SlopeFromHeightDeltas(cellSize * 2f, hL, hR, hD, hU);
        }

        public static void SampleElevAndSlopeBilinear(
            LandformField field,
            float worldX,
            float worldZ,
            out float elev,
            out float slope)
        {
            elev = 0f;
            slope = 0f;
            if (field == null || field.WorldHeights == null || field.WorldHeights.Length == 0)
                return;

            var invCell = 1f / field.CellSize;
            var fx = (worldX - field.OriginXZ.x) * invCell - 0.5f;
            var fz = (worldZ - field.OriginXZ.y) * invCell - 0.5f;
            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var tx = fx - x0;
            var tz = fz - z0;

            var h00 = SampleClamped(field, x0, z0);
            var h10 = SampleClamped(field, x0 + 1, z0);
            var h01 = SampleClamped(field, x0, z0 + 1);
            var h11 = SampleClamped(field, x0 + 1, z0 + 1);

            var hx0 = Mathf.Lerp(h00, h10, tx);
            var hx1 = Mathf.Lerp(h01, h11, tx);
            elev = Mathf.Lerp(hx0, hx1, tz);

            var hL = SampleClamped(field, x0 - 1, z0);
            var hR = SampleClamped(field, x0 + 2, z0);
            var hD = SampleClamped(field, x0, z0 - 1);
            var hU = SampleClamped(field, x0, z0 + 2);
            slope = SlopeFromHeightDeltas(field.CellSize * 2f, hL, hR, hD, hU);
        }

        private static float SlopeFromHeightDeltas(
            float sampleSpanMeters,
            float hL,
            float hR,
            float hD,
            float hU)
        {
            var span = Mathf.Max(0.5f, sampleSpanMeters);
            var dx = (hR - hL) / span;
            var dz = (hU - hD) / span;
            // Equivalent to Angle(Normalize(-dx,1,-dz), up) without Vector3 allocs/calls.
            var invLen = 1f / Mathf.Sqrt(dx * dx + 1f + dz * dz);
            return Mathf.Acos(Mathf.Clamp(invLen, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
