#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Footprint geometry contract: the width equation, upstream-to-mouth ordering, preserved
    /// surfaces and the lake medial spine.
    /// </summary>
    public sealed class InlandWaterFootprintTests
    {
        [Test]
        public void ResolveRadiusMultiplier_ReproducesRequestedWidthInMeters()
        {
            AssertWidthEquation(1f, 25f);
            AssertWidthEquation(600f, 25f);
            AssertWidthEquation(32f, 40f);
            AssertWidthEquation(12.5f, 12.5f);
            AssertWidthEquation(0.25f, 25f);
        }

        [Test]
        public void FootprintWidths_MatchAuthoredCrossSections()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var narrow = WorldBuilderTestFixtures.CreateRiver(
                    1, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) }, new[] { 1f, 1f });
                var wide = WorldBuilderTestFixtures.CreateRiver(
                    2, new[] { new Vector2(0f, 50f), new Vector2(100f, 50f) }, new[] { 600f, 600f });
                var asymmetric = WorldBuilderTestFixtures.CreateRiver(
                    3,
                    new[] { new Vector2(0f, 100f), new Vector2(40f, 100f), new Vector2(80f, 100f) },
                    new[] { 1f, 600f, 1f });
                var tapered = WorldBuilderTestFixtures.CreateRiver(
                    4,
                    new[]
                    {
                        new Vector2(0f, 150f), new Vector2(30f, 150f),
                        new Vector2(60f, 150f), new Vector2(90f, 150f)
                    },
                    new[] { 4f, 16f, 36f, 64f });
                var junction = WorldBuilderTestFixtures.CreateRiver(
                    5,
                    new[] { new Vector2(0f, 200f), new Vector2(50f, 200f), new Vector2(100f, 200f) },
                    new[] { 20f, 20f, 20f });
                junction.HasConfluence = true;
                junction.ConfluenceXZ = new Vector2(50f, 200f);

                var plan = WorldBuilderTestFixtures.CreateWaterPlan(
                    8, 8, 16f, new[] { narrow, wide, asymmetric, tapered, junction });
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);

                Assert.AreEqual(5, footprint.Features.Count);
                AssertWidthInvariant(footprint, 1);
                AssertWidthInvariant(footprint, 2);
                AssertWidthInvariant(footprint, 3);
                AssertWidthInvariant(footprint, 4);
                AssertWidthInvariant(footprint, 5);

                AssertHasHalfWidth(footprint, 1, 0.5f);
                AssertHasHalfWidth(footprint, 2, 300f);
                AssertHasHalfWidth(footprint, 3, 300f);
                AssertHasHalfWidth(footprint, 3, 0.5f);
                AssertHasHalfWidth(footprint, 5, 10f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_OrdersMouthFirstInputUpstreamToMouthWithMonotonicHeights()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = CreateMouthFirstRiver(7);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(8, 1, 10f, new[] { river });
                MarkSurfaceRow(plan);

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.AreEqual(InlandWaterFootprintPlan.FeatureKind.River, feature.Kind);
                Assert.IsFalse(feature.Closed);
                Assert.AreEqual(new Vector2(75f, 5f), feature.Points[0].CenterXZ);
                Assert.AreEqual(new Vector2(5f, 5f), feature.Points[feature.Points.Count - 1].CenterXZ);
                Assert.AreEqual(profile.SeaLevelWorldY,
                    feature.Points[feature.Points.Count - 1].SurfaceWorldY, 0.001f);

                for (var i = 1; i < feature.Points.Count; i++)
                {
                    Assert.LessOrEqual(
                        feature.Points[i].SurfaceWorldY,
                        feature.Points[i - 1].SurfaceWorldY + 0.001f,
                        $"Height rises downstream at point {i}.");
                }

                Assert.IsTrue((feature.Points[0].Role & InlandWaterFootprintPlan.PointRole.Source) != 0);
                Assert.IsTrue((feature.Points[feature.Points.Count - 1].Role &
                               InlandWaterFootprintPlan.PointRole.Mouth) != 0);
                Assert.IsTrue(feature.Points[0].IsPinned);
                Assert.IsTrue(feature.Points[feature.Points.Count - 1].IsPinned);
                Assert.IsTrue(
                    (feature.Points[0].Role & InlandWaterFootprintPlan.PinnedRoleMask) != 0);
                Assert.IsTrue(
                    (feature.Points[feature.Points.Count - 1].Role &
                     InlandWaterFootprintPlan.PinnedRoleMask) != 0);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_MarksConfluencePointAsJunction()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = CreateMouthFirstRiver(11);
                river.HasConfluence = true;
                // Nearest control after upstream-to-mouth ordering is (45, 5).
                river.ConfluenceXZ = new Vector2(46f, 5f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(8, 1, 10f, new[] { river });
                MarkSurfaceRow(plan);

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                var junctionIndex = -1;
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    if (feature.Points[i].IsJunction)
                        junctionIndex = i;
                }

                Assert.GreaterOrEqual(junctionIndex, 0, "The confluence control must survive as a junction.");
                Assert.AreEqual(new Vector2(45f, 5f), feature.Points[junctionIndex].CenterXZ);
                Assert.IsTrue(
                    (feature.Points[junctionIndex].Role & InlandWaterFootprintPlan.PointRole.Junction) != 0);
                Assert.IsTrue(feature.Points[junctionIndex].IsPinned);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_WithoutWetCellsFallsBackToSeaLevelNotLiteralZero()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile(seaLevel: 40f);
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    21, new[] { new Vector2(0f, 0f), new Vector2(30f, 0f), new Vector2(60f, 0f) }, 12f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 4, 16f, new[] { river });

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.GreaterOrEqual(feature.Points.Count, 2);
                for (var i = 0; i < feature.Points.Count; i++)
                    Assert.AreEqual(40f, feature.Points[i].SurfaceWorldY, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_WithWetCellsUsesNearestWetSurface()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = WorldBuilderTestFixtures.CreateRiver(
                    23, new[] { new Vector2(56f, 104f), new Vector2(56f, 8f) }, 12f);
                river.HasOceanMouth = false;
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(8, 8, 16f, new[] { river });
                // Only one wet cell: the upstream control must find it instead of returning zero.
                WorldBuilderTestFixtures.MarkWetCell(plan, 3, 1, 33f);

                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                Assert.AreEqual(2, feature.Points.Count);
                Assert.AreEqual(33f, feature.Points[0].SurfaceWorldY, 0.001f,
                    "The nearest wet cell surface must be adopted, never a literal zero.");
                Assert.AreEqual(33f, feature.Points[1].SurfaceWorldY, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_LakeOutline_YieldsVaryingWidthMedialSpine()
        {
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var lake = new LakeRecord
                {
                    StableId = 9,
                    CenterXZ = new Vector2(500f, 500f),
                    SurfaceWorldY = 22f,
                    MaxDepthMeters = 6f,
                    AreaMetersSq = 90000f,
                    OutlineXZ = CreateEllipseOutline(new Vector2(500f, 500f), 200f, 30f, 32),
                    BasinCellCentersXZ = new[]
                    {
                        new Vector2(320f, 500f),
                        new Vector2(420f, 470f), new Vector2(420f, 530f),
                        new Vector2(500f, 440f), new Vector2(500f, 500f), new Vector2(500f, 560f),
                        new Vector2(580f, 470f), new Vector2(580f, 530f),
                        new Vector2(680f, 500f)
                    }
                };
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 4, 16f, null, new[] { lake });

                var options = new InlandWaterFootprintOptions(0f, 1000f, 8f, 0.5f, 0.25f, 0.25f, 0.5f, 32f, 2);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile, options);
                Assert.IsTrue(footprint.TryGetFeature(lake.StableId, out var feature));

                Assert.AreEqual(InlandWaterFootprintPlan.FeatureKind.Lake, feature.Kind);
                Assert.IsFalse(feature.IsRiver);
                Assert.GreaterOrEqual(feature.Points.Count, 2);
                Assert.AreEqual(lake.SurfaceWorldY, feature.SurfaceWorldY, 0.001f);

                var distinctWidths = new List<float>();
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    var point = feature.Points[i];
                    Assert.AreEqual(lake.SurfaceWorldY, point.SurfaceWorldY, 0.001f,
                        "Every lake control shares one constant surface height.");
                    if (!ContainsApproximately(distinctWidths, point.TargetWetHalfWidthMeters))
                        distinctWidths.Add(point.TargetWetHalfWidthMeters);
                }

                Assert.Greater(distinctWidths.Count, 1,
                    "An ordered shoreline loop must reproduce a varying cross-section.");

                var first = feature.Points[0].Role;
                var last = feature.Points[feature.Points.Count - 1].Role;
                Assert.IsTrue((first & InlandWaterFootprintPlan.PointRole.Source) != 0);
                Assert.IsTrue((first & InlandWaterFootprintPlan.PointRole.LakeConnection) != 0);
                Assert.IsTrue((last & InlandWaterFootprintPlan.PointRole.Mouth) != 0);
                Assert.IsTrue((last & InlandWaterFootprintPlan.PointRole.LakeConnection) != 0);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ResolveBankDirection_IsPerpendicularToTheControlPolyline()
        {
            var points = new List<InlandWaterFootprintPlan.Point>
            {
                new() { CenterXZ = new Vector2(0f, 0f) },
                new() { CenterXZ = new Vector2(10f, 0f) },
                new() { CenterXZ = new Vector2(20f, 0f) }
            };

            Assert.AreEqual(Vector2.up, InlandWaterFootprintPlan.ResolveBankDirection(points, 0));
            Assert.AreEqual(Vector2.up, InlandWaterFootprintPlan.ResolveBankDirection(points, 1));
            Assert.AreEqual(Vector2.up, InlandWaterFootprintPlan.ResolveBankDirection(points, 2));
        }

        private static void AssertWidthEquation(float widthMeters, float splineRadius)
        {
            var multiplier = InlandWaterFootprintPlan.ResolveRadiusMultiplier(widthMeters, splineRadius);
            Assert.AreEqual(widthMeters, multiplier * splineRadius, 0.01f,
                $"Width {widthMeters} m is not reproduced at radius {splineRadius} m.");
        }

        private static void AssertWidthInvariant(InlandWaterFootprintPlan footprint, ulong stableId)
        {
            Assert.IsTrue(footprint.TryGetFeature(stableId, out var feature));
            for (var i = 0; i < feature.Points.Count; i++)
            {
                var point = feature.Points[i];
                Assert.AreEqual(
                    2f * point.TargetWetHalfWidthMeters,
                    point.TargetWetWidthMeters,
                    0.0001f,
                    $"River {stableId} point {i} breaks the full-width contract.");
            }
        }

        private static void AssertHasHalfWidth(
            InlandWaterFootprintPlan footprint,
            ulong stableId,
            float halfWidthMeters)
        {
            Assert.IsTrue(footprint.TryGetFeature(stableId, out var feature));
            for (var i = 0; i < feature.Points.Count; i++)
            {
                if (Mathf.Abs(feature.Points[i].TargetWetHalfWidthMeters - halfWidthMeters) <= 0.001f)
                    return;
            }

            Assert.Fail($"River {stableId} has no control at half-width {halfWidthMeters} m.");
        }

        private static bool ContainsApproximately(List<float> values, float candidate)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (Mathf.Abs(values[i] - candidate) <= 0.01f)
                    return true;
            }

            return false;
        }

        private static Vector2[] CreateEllipseOutline(Vector2 center, float halfLength, float halfWidth, int segments)
        {
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                points[i] = center + new Vector2(
                    Mathf.Cos(angle) * halfLength,
                    Mathf.Sin(angle) * halfWidth);
            }

            return points;
        }

        private static RiverPolyline CreateMouthFirstRiver(ulong stableId)
        {
            var points = new[]
            {
                new Vector2(5f, 5f),
                new Vector2(25f, 5f),
                new Vector2(45f, 5f),
                new Vector2(75f, 5f)
            };
            var river = WorldBuilderTestFixtures.CreateRiver(
                stableId, points, new[] { 8f, 12f, 16f, 20f });
            river.HasOceanMouth = true;
            river.OceanMouthXZ = points[0];
            return river;
        }

        private static void MarkSurfaceRow(HydrologyPlan plan)
        {
            for (var x = 0; x < plan.Width; x++)
                WorldBuilderTestFixtures.MarkWetCell(plan, x, 0, x);
        }
    }
}
#endif
