#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Open-floodplain contract: flat lowland that never rises within bank reach must not
    /// fail carve validation as MissingBank (widening cannot invent a bank).
    /// </summary>
    public sealed class InlandWaterOpenFloodplainTests
    {
        [Test]
        public void FitToCarvedField_OpenFloodplain_KeepsPlannedWidthWithoutMissingBank()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // Entire field below the bed crossing target → FindBankCrossing never rises.
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(16, 16, 16f, -2f);
                var river = WorldBuilderTestFixtures.CreateRiver(
                    71,
                    new[] { new Vector2(32f, 128f), new Vector2(160f, 128f) },
                    24f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(16, 16, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = 0f;
                    feature.Points[i].TargetWetHalfWidthMeters = 12f;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                }

                var widthBefore = feature.Points[0].TargetWetHalfWidthMeters;
                footprint.FitToCarvedField(field, options);

                Assert.AreEqual(0, footprint.Report.ExtendedBanks,
                    "Open floodplain must not widen the plan.");
                Assert.AreEqual(0, footprint.Report.CountOf(InlandWaterFootprintFailure.MissingBank),
                    footprint.Report.Describe());

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.IsFalse(feature.Points[i].HasMissingBank, $"point {i}");
                    Assert.AreEqual(widthBefore, feature.Points[i].TargetWetHalfWidthMeters, 0.001f);
                }

                var validated = footprint.ValidateCarvedField(field, options);
                Assert.AreEqual(0, validated.CountOf(InlandWaterFootprintFailure.MissingBank),
                    validated.Describe());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ValidateCrestRibbon_OpenFloodplain_DoesNotReportMissingBank()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(16, 16, 16f, -2f);
                var river = WorldBuilderTestFixtures.CreateRiver(
                    72,
                    new[] { new Vector2(32f, 128f), new Vector2(160f, 128f) },
                    24f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(16, 16, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                    feature.Points[i].SurfaceWorldY = 0f;

                var report = footprint.ValidateCrestRibbon(
                    field, options, CrestRibbonValidationSettings.Default);
                Assert.AreEqual(0, report.CountOf(InlandWaterFootprintFailure.MissingBank),
                    report.Describe());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FitToCarvedField_SoftBank_NarrowsOversizedPlanToDugTrench()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // High free-surface target with a narrow dug trench: surface-based banks never fire,
                // soft banks relative to the dug centre must shrink the plan.
                const float landY = 20f;
                const float bedY = -2f;
                const float trenchHalf = 12f;
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(32, 32, 16f, landY);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var c = field.CellCenterXZ(x, z);
                        if (Mathf.Abs(c.y - 128f) <= trenchHalf)
                            field.WorldHeights[field.Index(x, z)] = bedY;
                    }
                }

                var river = WorldBuilderTestFixtures.CreateRiver(
                    73,
                    new[] { new Vector2(48f, 128f), new Vector2(200f, 128f) },
                    180f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(32, 32, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = landY;
                    feature.Points[i].TargetWetHalfWidthMeters = 90f;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                }

                footprint.FitToCarvedField(field, options);

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.Less(
                        feature.Points[i].TargetWetHalfWidthMeters, 40f,
                        $"point {i} should adopt the dug trench, not keep 90m. " + footprint.Report.Describe());
                }

                var ribbon = footprint.ValidateCrestRibbon(
                    field, options, CrestRibbonValidationSettings.Default);
                Assert.AreEqual(0, ribbon.CountOf(InlandWaterFootprintFailure.EdgeMismatch),
                    ribbon.Describe());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FitToCarvedField_PrefersSoftTrenchOverFarDigShoulder()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // Wide dig shoulder (surface bank at ~90m) plus a nearer soft rise at ~12m —
                // Fit must shrink to the soft trench, not lock the shoulder (ribbon EdgeMismatch).
                const float landY = 20f;
                const float bedY = -2f;
                const float softHalf = 12f;
                const float digShoulderHalf = 90f;
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(48, 48, 16f, landY);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var c = field.CellCenterXZ(x, z);
                        var dist = Mathf.Abs(c.y - 384f);
                        if (dist <= softHalf)
                            field.WorldHeights[field.Index(x, z)] = bedY;
                        else if (dist <= digShoulderHalf)
                            // Still below free surface so surface banks only fire at the dig shoulder,
                            // but high enough for soft banks (rise ≥ 1m from bed) at softHalf.
                            field.WorldHeights[field.Index(x, z)] = bedY + 3f;
                    }
                }

                var river = WorldBuilderTestFixtures.CreateRiver(
                    75,
                    new[] { new Vector2(64f, 384f), new Vector2(400f, 384f) },
                    180f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(48, 48, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = landY;
                    feature.Points[i].TargetWetHalfWidthMeters = digShoulderHalf;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                    feature.Points[i].BankShoulderMeters = 0f;
                }

                footprint.FitToCarvedField(field, options);

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.Less(
                        feature.Points[i].TargetWetHalfWidthMeters, 40f,
                        $"point {i} should prefer soft trench (~12m) over dig shoulder (~90m). " +
                        footprint.Report.Describe());
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FitToCarvedField_DoesNotRewidenFromLeftoverOuterTrench()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // Plan already narrowed; leftover outer trench wall must not widen Fit again.
                const float landY = 20f;
                const float bedY = -2f;
                const float outerHalf = 90f;
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(48, 48, 16f, landY);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var c = field.CellCenterXZ(x, z);
                        if (Mathf.Abs(c.y - 384f) <= outerHalf)
                            field.WorldHeights[field.Index(x, z)] = bedY;
                    }
                }

                var river = WorldBuilderTestFixtures.CreateRiver(
                    76,
                    new[] { new Vector2(64f, 384f), new Vector2(400f, 384f) },
                    180f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(48, 48, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                const float plannedHalf = 8f;
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = landY;
                    feature.Points[i].TargetWetHalfWidthMeters = plannedHalf;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                }

                footprint.FitToCarvedField(field, options);

                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.AreEqual(
                        plannedHalf, feature.Points[i].TargetWetHalfWidthMeters, 0.01f,
                        $"point {i} must not re-widen from leftover outer trench");
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AdoptEdgeMismatchWidths_ShrinksOversizedPlanFromPriorReport()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(16, 16, 16f, -2f);
                var river = WorldBuilderTestFixtures.CreateRiver(
                    77,
                    new[] { new Vector2(32f, 128f), new Vector2(160f, 128f) },
                    180f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(16, 16, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                    feature.Points[i].TargetWetHalfWidthMeters = 98.5f;

                var prior = new InlandWaterFootprintReport();
                prior.Add(
                    river.StableId,
                    InlandWaterFootprintPlan.FeatureKind.River,
                    0,
                    feature.Points[0].CenterXZ,
                    InlandWaterFootprintFailure.EdgeMismatch,
                    measuredMeters: 22f,
                    limitMeters: 98.5f);

                Assert.Greater(footprint.AdoptEdgeMismatchWidths(prior), 0);
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.LessOrEqual(
                        feature.Points[i].TargetWetHalfWidthMeters, 22.01f,
                        $"point {i} must adopt EdgeMismatch measured half-width");
                }

                Assert.LessOrEqual(footprint.MaxRiverHalfWidthMeters(), 22.01f);

                // Repair must Cap from the prior report even when Flatten would kill soft dig-depth.
                for (var i = 0; i < feature.Points.Count; i++)
                    feature.Points[i].TargetWetHalfWidthMeters = 98.5f;
                footprint.RepairCrestRibbon(
                    field, options, CrestRibbonValidationSettings.Default, prior);
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    Assert.LessOrEqual(
                        feature.Points[i].TargetWetHalfWidthMeters, 22.01f,
                        $"point {i} after RepairCrestRibbon");
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FitToCarvedField_MidTrenchPlan_DoesNotInventFullShoulderOffset()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // Narrow plan inside a leftover wide trench: soft banks ~12m, planned half 12m.
                // Shoulder offset must not invent MaxRiverWidth-scale predicted edges.
                const float landY = 20f;
                const float bedY = -2f;
                const float softHalf = 12f;
                const float outerHalf = 90f;
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(48, 48, 16f, landY);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var c = field.CellCenterXZ(x, z);
                        var dist = Mathf.Abs(c.y - 384f);
                        if (dist <= softHalf)
                            field.WorldHeights[field.Index(x, z)] = bedY;
                        else if (dist <= outerHalf)
                            field.WorldHeights[field.Index(x, z)] = bedY + 3f;
                    }
                }

                var river = WorldBuilderTestFixtures.CreateRiver(
                    78,
                    new[] { new Vector2(64f, 384f), new Vector2(400f, 384f) },
                    24f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(48, 48, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = landY;
                    feature.Points[i].TargetWetHalfWidthMeters = softHalf;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                    feature.Points[i].BankShoulderMeters = 36f;
                }

                var ribbon = footprint.ValidateCrestRibbon(
                    field, options, CrestRibbonValidationSettings.Default);
                Assert.AreEqual(0, ribbon.CountOf(InlandWaterFootprintFailure.EdgeMismatch),
                    ribbon.Describe());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ValidateCrestRibbon_CentreNoise_DoesNotReportEdgeMismatch()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                // Dug bed with sub-metre height wobble at the centre: soft banks must not treat
                // that noise as a waterline against a wide planned half-width.
                const float bedY = -2f;
                var field = WorldBuilderTestFixtures.CreateFlatLandforms(16, 16, 16f, bedY);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var c = field.CellCenterXZ(x, z);
                        if (Mathf.Abs(c.x - 96f) < 1f && Mathf.Abs(c.y - 128f) < 1f)
                            field.WorldHeights[field.Index(x, z)] = bedY + 0.35f;
                    }
                }

                var river = WorldBuilderTestFixtures.CreateRiver(
                    74,
                    new[] { new Vector2(32f, 128f), new Vector2(160f, 128f) },
                    180f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(16, 16, 16f, new[] { river });
                var options = InlandWaterFootprintOptions.Default;
                var footprint = plan.EnsureFootprintPlan(profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    feature.Points[i].SurfaceWorldY = 20f;
                    feature.Points[i].TargetWetHalfWidthMeters = 90f;
                    feature.Points[i].RequestedBedClearanceMeters = 0.5f;
                }

                var ribbon = footprint.ValidateCrestRibbon(
                    field, options, CrestRibbonValidationSettings.Default);
                Assert.AreEqual(0, ribbon.CountOf(InlandWaterFootprintFailure.EdgeMismatch),
                    ribbon.Describe());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif
