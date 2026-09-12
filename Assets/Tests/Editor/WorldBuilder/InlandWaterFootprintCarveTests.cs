#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>Carve-side footprint contract: wet-bed clearance, shoulder falloff, highland skip.</summary>
    public sealed class InlandWaterFootprintCarveTests
    {
        private const int Size = 8;
        private const float Cell = 16f;
        private const float LandY = 20f;
        private const float CorridorZ = 72f;
        private const float HalfWidthMeters = 16f;

        [Test]
        public void Carve_RiverCorridor_KeepsWetFootprintClearAndShoulderUntouched()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(Size, Size, Cell, LandY);
                // Control points 8 m apart: the carver stamps one footprint disc per control, so a
                // sparse polyline would leave the channel centre shallower than the wet edge.
                var river = CreateCorridorRiver(51, new Vector2(24f, CorridorZ), new Vector2(56f, CorridorZ), 5);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(Size, Size, Cell, new[] { river });
                MarkCorridorCells(plan);

                var options = new InlandWaterFootprintOptions(0f, 8f, 64f, 8f, 0.25f, 0.25f, 0.5f, 32f, 2);
                HydrologyCarver.Carve(field, plan, profile, options);

                var footprint = plan.FootprintPlan;
                Assert.IsNotNull(footprint);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.AreEqual(LandY, feature.Points[i].SurfaceWorldY, 0.001f,
                        "The preserved free surface must come from the wet raster, not sea level.");
                    Assert.AreEqual(HalfWidthMeters, feature.Points[i].TargetWetHalfWidthMeters, 0.001f,
                        "Carving must not rewrite the planned width past the lateral tolerance.");
                }

                var report = footprint.ValidateCarvedField(field, options);
                Assert.AreEqual(0, report.CountOf(InlandWaterFootprintFailure.BedClearance),
                    report.Describe());

                AssertWetFootprintIsDug(field, feature);
                AssertBeyondShoulderIsUntouched(field, feature);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Carve_HighlandRiver_IsSkippedWithoutWideningThePlan()
        {
            const float mountainY = 200f;
            var options = InlandWaterFootprintOptions.Default;
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(Size, Size, Cell, mountainY);
                var river = CreateCorridorRiver(
                    53, new Vector2(24f, CorridorZ), new Vector2(120f, CorridorZ), 2);

                var capped = WorldBuilderTestFixtures.CreateWaterPlan(Size, Size, Cell, new[] { river });
                var cappedFootprint = capped.EnsureFootprintPlan(profile, options);
                cappedFootprint.CarveCeilingWorldY = 56f;

                var cappedReport = cappedFootprint.ValidateCarvedField(field, options);
                Assert.AreEqual(0, cappedReport.CountOf(InlandWaterFootprintFailure.BedClearance),
                    cappedReport.Describe());

                cappedFootprint.FitToCarvedField(field, options);
                Assert.IsTrue(cappedFootprint.TryGetFeature(river.StableId, out var cappedFeature));
                for (var i = 0; i < cappedFeature.Points.Count; i++)
                {
                    Assert.AreEqual(32f, cappedFeature.Points[i].TargetWetWidthMeters, 0.001f,
                        "A skipped highland point must keep its planned width.");
                }

                var uncapped = WorldBuilderTestFixtures.CreateWaterPlan(Size, Size, Cell, new[] { river });
                var uncappedReport = uncapped.EnsureFootprintPlan(profile, options)
                    .ValidateCarvedField(field, options);
                Assert.Greater(
                    uncappedReport.CountOf(InlandWaterFootprintFailure.BedClearance), 0,
                    "Without the carve ceiling the undug mountain channel is a clearance failure.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static RiverPolyline CreateCorridorRiver(
            ulong stableId,
            Vector2 from,
            Vector2 to,
            int pointCount)
        {
            var points = new Vector2[pointCount];
            var widths = new float[pointCount];
            var depths = new float[pointCount];
            for (var i = 0; i < pointCount; i++)
            {
                points[i] = Vector2.Lerp(from, to, i / (float)(pointCount - 1));
                widths[i] = 32f;
                depths[i] = 2f;
            }

            var river = WorldBuilderTestFixtures.CreateRiver(stableId, points, widths, depths);
            river.HasOceanMouth = false;
            return river;
        }

        private static void MarkCorridorCells(HydrologyPlan plan)
        {
            var z = Mathf.FloorToInt(CorridorZ / plan.CellSizeMeters);
            for (var x = 1; x <= 3; x++)
                WorldBuilderTestFixtures.MarkWetCell(plan, x, z, LandY);
        }

        private static void AssertWetFootprintIsDug(
            LandformField field,
            InlandWaterFootprintPlan.Feature feature)
        {
            var start = feature.Points[0].CenterXZ;
            var end = feature.Points[feature.Points.Count - 1].CenterXZ;
            var maxHeight = LandY - 2f + 0.05f;
            var checkedCells = 0;

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var centre = field.CellCenterXZ(x, z);
                    if (DistanceToSegment(centre, start, end) > HalfWidthMeters)
                        continue;
                    checkedCells++;
                    Assert.LessOrEqual(
                        field.WorldHeights[field.Index(x, z)], maxHeight,
                        $"Cell ({x},{z}) inside the wet footprint was not dug to clearance.");
                }
            }

            Assert.Greater(checkedCells, 0, "The test corridor must cover at least one cell centre.");
        }

        private static void AssertBeyondShoulderIsUntouched(
            LandformField field,
            InlandWaterFootprintPlan.Feature feature)
        {
            var reach = HalfWidthMeters + feature.Points[0].BankShoulderMeters;
            Assert.Greater(reach, HalfWidthMeters);

            var start = feature.Points[0].CenterXZ;
            var end = feature.Points[feature.Points.Count - 1].CenterXZ;
            var untouched = 0;
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var centre = field.CellCenterXZ(x, z);
                    if (DistanceToSegment(centre, start, end) <= reach)
                        continue;
                    Assert.AreEqual(LandY, field.WorldHeights[field.Index(x, z)], 0.05f,
                        $"Cell ({x},{z}) beyond the shoulder was carved.");
                    untouched++;
                }
            }

            Assert.Greater(untouched, 0, "The test field must contain a cell beyond the shoulder.");
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var span = end - start;
            var lengthSq = span.sqrMagnitude;
            if (lengthSq <= 1e-6f)
                return Vector2.Distance(point, start);
            var t = Mathf.Clamp01(Vector2.Dot(point - start, span) / lengthSq);
            return Vector2.Distance(point, start + span * t);
        }
    }
}
#endif
