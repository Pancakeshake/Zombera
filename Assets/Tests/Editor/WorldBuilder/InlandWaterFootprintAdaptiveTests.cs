#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>Adaptive control-point density: pinned survival, spacing refinement, merging.</summary>
    public sealed class InlandWaterFootprintAdaptiveTests
    {
        [Test]
        public void ApplyAdaptiveControlPoints_KeepsPinnedJunctionAndEndpoints()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var points = new[]
                {
                    new Vector2(0f, 0f), new Vector2(30f, 0f), new Vector2(60f, 0f),
                    new Vector2(90f, 0f), new Vector2(120f, 0f)
                };
                var river = WorldBuilderTestFixtures.CreateRiver(31, points, 16f);
                river.HasConfluence = true;
                river.ConfluenceXZ = new Vector2(61f, 0f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                footprint.ApplyAdaptiveControlPoints(InlandWaterFootprintOptions.Default);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                var junctionIndex = -1;
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    if (feature.Points[i].IsJunction)
                        junctionIndex = i;
                }

                Assert.GreaterOrEqual(junctionIndex, 0, "Pinned junction was simplified away.");
                Assert.AreEqual(new Vector2(60f, 0f), feature.Points[junctionIndex].CenterXZ);
                Assert.IsTrue((feature.Points[0].Role & InlandWaterFootprintPlan.PointRole.Source) != 0);
                Assert.IsTrue(
                    (feature.Points[feature.Points.Count - 1].Role &
                     InlandWaterFootprintPlan.PointRole.Mouth) != 0);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyAdaptiveControlPoints_RefinesSegmentsToMaxSpacing()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    33, new[] { new Vector2(0f, 0f), new Vector2(300f, 0f) }, 16f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });

                var options = new InlandWaterFootprintOptions(0f, 32f, 8f, 8f, 0.25f, 0.25f, 0.5f, 32f, 2);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile, options);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.Greater(feature.Points.Count, 2, "Long segments must be refined.");
                for (var i = 1; i < feature.Points.Count; i++)
                {
                    Assert.LessOrEqual(
                        Vector2.Distance(feature.Points[i - 1].CenterXZ, feature.Points[i].CenterXZ),
                        options.MaxPointSpacingMeters + 0.01f,
                        $"Segment {i - 1}->{i} exceeds the authored maximum spacing.");
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyAdaptiveControlPoints_SimplifiesDenseStraightTaperedRiver()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                const int sampleCount = 64;
                var points = new Vector2[sampleCount];
                var widths = new float[sampleCount];
                for (var i = 0; i < sampleCount; i++)
                {
                    points[i] = new Vector2(i * 10f, 0f);
                    widths[i] = 4f + i * 0.5f;
                }

                var river = WorldBuilderTestFixtures.CreateRiver(35, points, widths);
                river.HasOceanMouth = true;
                river.OceanMouthXZ = points[sampleCount - 1];
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.GreaterOrEqual(feature.Points.Count, 2);
                Assert.Less(feature.Points.Count, sampleCount,
                    "A straight, evenly tapered river must lose redundant controls.");
                for (var i = 1; i < feature.Points.Count; i++)
                {
                    Assert.LessOrEqual(
                        Vector2.Distance(feature.Points[i - 1].CenterXZ, feature.Points[i].CenterXZ),
                        InlandWaterFootprintOptions.Default.MaxPointSpacingMeters + 0.01f);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyAdaptiveControlPoints_MergesSegmentsBelowMinSpacing()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var points = new[]
                {
                    new Vector2(0f, 0f), new Vector2(34f, 0f),
                    new Vector2(35f, 0f), new Vector2(100f, 0f)
                };
                var widths = new[] { 10f, 10.4f, 10.8f, 12f };
                var river = WorldBuilderTestFixtures.CreateRiver(37, points, widths);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });

                var keepOptions = new InlandWaterFootprintOptions(0f, 1000f, 8f, 0.05f, 0.25f, 0.25f, 0.5f, 32f, 2);
                var mergeOptions = new InlandWaterFootprintOptions(16f, 1000f, 8f, 0.05f, 0.25f, 0.25f, 0.5f, 32f, 2);

                var kept = InlandWaterFootprintPlan.Build(plan, profile, keepOptions);
                Assert.IsTrue(kept.TryGetFeature(river.StableId, out var keptFeature));
                Assert.AreEqual(4, keptFeature.Points.Count,
                    "Tight width tolerance must preserve every authored cross-section.");

                plan.ReplaceWaterFeatures(new[] { river }, null);
                var merged = InlandWaterFootprintPlan.Build(plan, profile, mergeOptions);
                Assert.IsTrue(merged.TryGetFeature(river.StableId, out var mergedFeature));
                Assert.AreEqual(3, mergedFeature.Points.Count,
                    "A 1 m segment must be merged away at a 16 m minimum spacing.");
                for (var i = 1; i < mergedFeature.Points.Count; i++)
                {
                    Assert.GreaterOrEqual(
                        Vector2.Distance(mergedFeature.Points[i - 1].CenterXZ, mergedFeature.Points[i].CenterXZ),
                        mergeOptions.MinPointSpacingMeters - 0.001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InsertPoint_InsertsAtRequestedIndexWithInsertedRole()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    39, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, 16f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));
                Assert.AreEqual(2, feature.Points.Count);

                Assert.IsTrue(footprint.InsertPoint(
                    river.StableId, 1, new Vector2(50f, 0f), 5f, 3f));

                Assert.AreEqual(3, feature.Points.Count);
                var inserted = feature.Points[1];
                Assert.AreEqual(new Vector2(50f, 0f), inserted.CenterXZ);
                Assert.AreEqual(5f, inserted.TargetWetHalfWidthMeters, 0.001f);
                Assert.AreEqual(10f, inserted.TargetWetWidthMeters, 0.001f);
                Assert.AreEqual(3f, inserted.SurfaceWorldY, 0.001f);
                Assert.AreEqual(InlandWaterFootprintPlan.PointRole.Inserted, inserted.Role);
                Assert.IsFalse(inserted.IsPinned);

                Assert.IsFalse(footprint.InsertPoint(999UL, 1, Vector2.zero, 5f, 0f));

                Assert.IsTrue(footprint.InsertPoint(river.StableId, 0, new Vector2(-10f, 0f), 5f, 0f));
                Assert.AreEqual(4, feature.Points.Count);
                Assert.AreEqual(InlandWaterFootprintPlan.PointRole.Inserted, feature.Points[0].Role);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TryGetRiverPointWidth_ReportsAuthoredFullWidth()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    41, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, new[] { 32f, 48f });
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 16f, new[] { river });
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);

                Assert.IsTrue(footprint.TryGetRiverPointWidth(river.StableId, 0, out var first));
                Assert.AreEqual(32f, first, 0.001f);
                Assert.IsTrue(footprint.TryGetRiverPointWidth(river.StableId, 1, out var second));
                Assert.AreEqual(48f, second, 0.001f);
                Assert.IsFalse(footprint.TryGetRiverPointWidth(river.StableId, 5, out _));
                Assert.IsFalse(footprint.TryGetRiverPointWidth(999UL, 0, out _));
                Assert.IsFalse(footprint.TryGetFeature(999UL, out _));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif
