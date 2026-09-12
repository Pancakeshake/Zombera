using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Gameplay and geometric checks for generated landforms / orogens.</summary>
    public static class LandformValidationUtility
    {
        public readonly struct Report
        {
            public readonly bool Passed;
            public readonly float LowlandSlope12Fraction;
            public readonly float OrogenCoverageFraction;
            public readonly float ClipFlatFraction;
            public readonly int PassCount;
            public readonly int ExpectedMinPasses;
            public readonly string Message;

            public Report(
                bool passed,
                float lowlandSlope12Fraction,
                float orogenCoverageFraction,
                float clipFlatFraction,
                int passCount,
                int expectedMinPasses,
                string message)
            {
                Passed = passed;
                LowlandSlope12Fraction = lowlandSlope12Fraction;
                OrogenCoverageFraction = orogenCoverageFraction;
                ClipFlatFraction = clipFlatFraction;
                PassCount = passCount;
                ExpectedMinPasses = expectedMinPasses;
                Message = message ?? string.Empty;
            }
        }

        public static Report Validate(
            LandformField field,
            LandformProfile profile,
            OrogenPlan orogen,
            float baseY,
            float maxY,
            float seaLevel)
        {
            if (field == null || profile == null)
                return new Report(false, 0f, 0f, 0f, 0, 0, "Missing landform field or profile.");

            var landCells = 0;
            var lowlandCells = 0;
            var orogenCells = 0;
            var clipCells = 0;
            var clipLimit = maxY - 0.51f;

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var h = field.WorldHeights[field.Index(x, z)];
                    if (h <= seaLevel + 0.5f)
                        continue;

                    landCells++;
                    var slope = LandformFieldSampling.EstimateSlopeDegrees(field, x, z);
                    if (slope <= 12f)
                        lowlandCells++;

                    if (h >= clipLimit)
                        clipCells++;

                    if (orogen != null)
                    {
                        var center = field.CellCenterXZ(x, z);
                        if (orogen.SampleOrogenCoreMask(center.x, center.y) >= 0.35f)
                            orogenCells++;
                    }
                }
            }

            var lowlandFrac = landCells > 0 ? lowlandCells / (float)landCells : 1f;
            var orogenFrac = landCells > 0 ? orogenCells / (float)landCells : 0f;
            var clipFrac = landCells > 0 ? clipCells / (float)landCells : 0f;

            var rangeCount = orogen?.Ranges != null ? orogen.Ranges.Length : 0;
            var passCount = orogen?.PassCentersXZ != null ? orogen.PassCentersXZ.Length : 0;
            var expectedPasses = rangeCount * Mathf.Max(0, profile.MinPassCountPerRange);

            var passOk = expectedPasses == 0 || passCount >= expectedPasses;
            if (passOk && orogen != null && passCount > 0)
                passOk = PassesHaveCorridorBudget(field, orogen, profile.HighwayPassMaxSlopeDegrees);

            var lowlandOk = lowlandFrac + 1e-4f >= profile.MinLowlandSlope12Fraction;
            var orogenOk = orogenFrac <= profile.MaxOrogenCoverageFraction + 1e-4f;
            var clipOk = clipFrac < 0.005f;
            var passed = lowlandOk && orogenOk && clipOk && passOk;

            var message = passed
                ? $"Landform OK lowland={lowlandFrac:P0} orogen={orogenFrac:P0} passes={passCount}"
                : $"Landform gate fail lowland={lowlandFrac:P0}/{profile.MinLowlandSlope12Fraction:P0} " +
                  $"orogen={orogenFrac:P0}/{profile.MaxOrogenCoverageFraction:P0} " +
                  $"clip={clipFrac:P2} passes={passCount}/{expectedPasses}";

            _ = baseY;
            return new Report(passed, lowlandFrac, orogenFrac, clipFrac, passCount, expectedPasses, message);
        }

        public static ulong HashHeights(LandformField field)
        {
            var hasher = new StableHash64();
            if (field?.WorldHeights == null)
                return hasher.Finalize();

            hasher.Append(field.Width);
            hasher.Append(field.Height);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                hasher.Append(field.WorldHeights[i]);
            return hasher.Finalize();
        }

        private static bool PassesHaveCorridorBudget(
            LandformField field,
            OrogenPlan orogen,
            float maxSlopeDegrees)
        {
            for (var i = 0; i < orogen.PassCentersXZ.Length; i++)
            {
                var p = orogen.PassCentersXZ[i];
                var passH = LandformFieldSampling.SampleBilinear(field, p.x, p.y);
                var slope = LandformFieldSampling.EstimateSlopeDegreesBilinear(field, p.x, p.y);

                // Prefer a local saddle: pass should sit below nearby spine samples.
                var range = orogen.Ranges[Mathf.Min(i, orogen.Ranges.Length - 1)];
                var before = EvaluateBezier(range.Start, range.Mid, range.End, Mathf.Clamp01(range.PassAlong01 - 0.12f));
                var after = EvaluateBezier(range.Start, range.Mid, range.End, Mathf.Clamp01(range.PassAlong01 + 0.12f));
                var beforeH = LandformFieldSampling.SampleBilinear(field, before.x, before.y);
                var afterH = LandformFieldSampling.SampleBilinear(field, after.x, after.y);
                var isSaddle = passH <= beforeH + 1.5f && passH <= afterH + 1.5f;
                if (!isSaddle && slope > maxSlopeDegrees + 3f)
                    return false;
            }

            return true;
        }

        private static Vector2 EvaluateBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
