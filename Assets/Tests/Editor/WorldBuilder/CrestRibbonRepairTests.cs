#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>Ribbon-side repair contract: uphill overshoot flattening and drift insertion.</summary>
    public sealed class CrestRibbonRepairTests
    {
        [Test]
        public void RepairCrestRibbon_FlattensUphillOvershootRun()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(16, 16, 16f, 0f);
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    61, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, 8f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });
                var footprint = plan.EnsureFootprintPlan(profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.IsTrue(footprint.InsertPoint(river.StableId, 1, new Vector2(50f, 0f), 4f, 0f));
                Assert.AreEqual(3, feature.Points.Count);
                feature.Points[0].SurfaceWorldY = 0f;
                feature.Points[1].SurfaceWorldY = 10f;
                feature.Points[2].SurfaceWorldY = 0f;

                var options = new InlandWaterFootprintOptions(0f, 1000f, 8f, 8f, 0.25f, 0.9f, 0.5f, 32f, 2);
                var crest = CrestRibbonValidationSettings.Default;

                var before = footprint.ValidateCrestRibbon(field, options, crest);
                var overshootBefore = before.CountOf(InlandWaterFootprintFailure.UphillOvershoot);
                Assert.Greater(overshootBefore, 0, before.Describe());

                var peakBefore = PeakSurface(feature);
                var repaired = footprint.RepairCrestRibbon(field, options, crest);

                // RepairCrestRibbon reports flattened height runs through FlattenedRuns; ExtendedBanks
                // belongs to the carve-side fit (FitToCarvedField) and is always 0 here.
                Assert.Greater(repaired.FlattenedRuns, 0, repaired.Describe());
                Assert.AreEqual(
                    0,
                    repaired.CountOf(InlandWaterFootprintFailure.UphillOvershoot),
                    "One repair call must leave no uphill overshoot behind. " + repaired.Describe());
                Assert.Less(PeakSurface(feature), peakBefore,
                    "The uphill run must be flattened down to its downstream height.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RepairCrestRibbon_LongSteppedRiver_ConvergesInOneCallWithoutRaisingWater()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            // Ground far below every water surface, so the width-drift pass stays inert and this
            // exercises the uphill-overshoot fixpoint on its own.
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(96, 8, 16f, -120f);
            try
            {
                // Long, sparsely controlled river whose free surface steps down every few controls. A
                // step makes the cubic ribbon dip and climb back out by a few tenths of a metre — the
                // small overshoots that used to survive every bounded stage pass. The spacing bound
                // keeps the adaptive pass from collapsing the straight test river to two controls.
                const int controlCount = 40;
                const int controlsPerStep = 8;
                const float spacingMeters = 32f;
                const float stepMeters = 12f;
                var points = new Vector2[controlCount];
                for (var i = 0; i < controlCount; i++)
                    points[i] = new Vector2(32f + spacingMeters * i, 64f);

                var river = WorldBuilderTestFixtures.CreateRiver(82, points, 8f, 2f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(96, 8, 16f, new[] { river });
                var options = new InlandWaterFootprintOptions(
                    minPointSpacingMeters: 0f,
                    maxPointSpacingMeters: spacingMeters,
                    lateralToleranceMeters: 8f,
                    widthToleranceMeters: 8f,
                    heightToleranceMeters: 0.25f,
                    bankHeightToleranceMeters: 0.25f,
                    minimumBedClearanceMeters: 0.5f,
                    maximumBankExtensionMeters: 32f,
                    repairPassLimit: 2);
                var crest = CrestRibbonValidationSettings.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                Assert.Greater(feature.Points.Count, 40, "The stepped river must keep many controls.");

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = -5f - stepMeters * (i / controlsPerStep);
                    feature.Points[i].BankShoulderMeters = 0f;
                }

                var controlsBefore = new List<InlandWaterFootprintPlan.Point>(feature.Points);
                var heightsBefore = new float[controlsBefore.Count];
                for (var i = 0; i < controlsBefore.Count; i++)
                    heightsBefore[i] = controlsBefore[i].SurfaceWorldY;

                var before = footprint.ValidateCrestRibbon(field, options, crest);
                Assert.Greater(
                    before.CountOf(InlandWaterFootprintFailure.UphillOvershoot), 0, before.Describe());
                for (var i = 0; i < before.Issues.Count; i++)
                {
                    var issue = before.Issues[i];
                    if (issue.Failure != InlandWaterFootprintFailure.UphillOvershoot)
                        continue;
                    Assert.Greater(
                        issue.MeasuredMeters, options.BankHeightToleranceMeters, issue.Describe());
                    Assert.Less(
                        issue.MeasuredMeters, 1.5f,
                        "The regression must reproduce SMALL overshoots. " + issue.Describe());
                }

                var repaired = footprint.RepairCrestRibbon(field, options, crest, before);

                Assert.AreEqual(
                    0,
                    repaired.CountOf(InlandWaterFootprintFailure.UphillOvershoot),
                    "One repair call must converge. " + repaired.Describe());
                for (var i = 0; i < controlsBefore.Count; i++)
                {
                    Assert.LessOrEqual(
                        controlsBefore[i].SurfaceWorldY,
                        heightsBefore[i] + 0.0001f,
                        $"Repair must never raise the water surface (control {i}).");
                }

                Assert.LessOrEqual(
                    repaired.InsertedPoints, 256, "Repair must stay inside its insert bound.");
                Assert.LessOrEqual(
                    feature.Points.Count - controlsBefore.Count,
                    256,
                    "Repair must stay inside its control-insertion bound.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RepairCrestRibbon_InsertsControlsWhereCarvedWaterlineIsWider()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            var field = CreateTrenchField(centreZ: 128f, trenchHalfWidth: 40f, bedY: -5f, landY: 20f);
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    63, new[] { new Vector2(30f, 128f), new Vector2(130f, 128f) }, 32f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(16, 16, 16f, new[] { river });
                var footprint = plan.EnsureFootprintPlan(profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                var controlsBefore = feature.Points.Count;
                Assert.AreEqual(2, controlsBefore);

                var options = InlandWaterFootprintOptions.Default;
                var crest = CrestRibbonValidationSettings.Default;

                var before = footprint.ValidateCrestRibbon(field, options, crest);
                Assert.Greater(
                    before.CountOf(InlandWaterFootprintFailure.MissingBank) +
                    before.CountOf(InlandWaterFootprintFailure.EdgeMismatch),
                    0,
                    before.Describe());

                var repaired = footprint.RepairCrestRibbon(field, options, crest);
                Assert.Greater(repaired.InsertedPoints + repaired.ExtendedBanks, 0, repaired.Describe());
                Assert.Greater(feature.Points.Count, controlsBefore,
                    "Drift repair must add control points to the feature.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static float PeakSurface(InlandWaterFootprintPlan.Feature feature)
        {
            var peak = float.NegativeInfinity;
            for (var i = 0; i < feature.Points.Count; i++)
                peak = Mathf.Max(peak, feature.Points[i].SurfaceWorldY);
            return peak;
        }

        private static LandformField CreateTrenchField(
            float centreZ,
            float trenchHalfWidth,
            float bedY,
            float landY)
        {
            var field = new LandformField(16, 16, 16f, Vector2.zero);
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var cellZ = field.CellCenterXZ(x, z).y;
                    field.WorldHeights[field.Index(x, z)] =
                        Mathf.Abs(cellZ - centreZ) <= trenchHalfWidth ? bedY : landY;
                }
            }

            return field;
        }
    }
}
#endif
