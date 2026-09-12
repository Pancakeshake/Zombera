#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Crest ribbon sampling contract: spacing table, geometry and the
    /// <c>full ribbon width == Spline.Radius * RadiusMultiplier</c> equation.
    /// </summary>
    public sealed class CrestRibbonSamplerTests
    {
        [Test]
        public void ResolveSpacingMeters_MatchesCrestSubdivisionTable()
        {
            Assert.AreEqual(8f, CrestRibbonSampler.ResolveSpacingMeters(-1), 1e-4f);
            Assert.AreEqual(8f, CrestRibbonSampler.ResolveSpacingMeters(0), 1e-4f);
            Assert.AreEqual(4f, CrestRibbonSampler.ResolveSpacingMeters(1), 1e-4f);
            Assert.AreEqual(2f, CrestRibbonSampler.ResolveSpacingMeters(2), 1e-4f);
            Assert.AreEqual(1f, CrestRibbonSampler.ResolveSpacingMeters(3), 1e-4f);
        }

        [Test]
        public void ResolveSampleCount_RoundsUpAndNeverReturnsZero()
        {
            Assert.AreEqual(25, CrestRibbonSampler.ResolveSampleCount(100f, 1));
            Assert.AreEqual(50, CrestRibbonSampler.ResolveSampleCount(100f, 2));
            Assert.AreEqual(1, CrestRibbonSampler.ResolveSampleCount(0.1f, 1));
            Assert.AreEqual(1, CrestRibbonSampler.ResolveSampleCount(0f, 3));
        }

        [Test]
        public void EstimateLengthMeters_SumsControlSpans()
        {
            Assert.AreEqual(0f, CrestRibbonSampler.EstimateLengthMeters(null), 1e-4f);
            Assert.AreEqual(0f, CrestRibbonSampler.EstimateLengthMeters(new[] { Vector3.zero }), 1e-4f);
            Assert.AreEqual(
                30f,
                CrestRibbonSampler.EstimateLengthMeters(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 20f)
                }),
                1e-4f);
        }

        [Test]
        public void TrySample_RejectsDegenerateControlSets()
        {
            var sampler = new CrestRibbonSampler();
            Assert.IsFalse(sampler.TrySample(Array.Empty<Vector3>(), null, 25f, 1, false));
            Assert.IsFalse(sampler.TrySample(new[] { Vector3.zero }, null, 25f, 1, false));
            Assert.IsFalse(sampler.TrySample(
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f) }, null, 25f, 1, closed: true));
            Assert.IsTrue(sampler.TrySample(
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f) },
                null,
                25f,
                1,
                closed: true));
            Assert.AreEqual(3, sampler.ControlCount);
        }

        [Test]
        public void StraightSpline_KeepsCentersCollinearAndHalfWidthExact()
        {
            var controls = new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f) };
            var multipliers = new[] { 1f, 0.4f };
            var sampler = new CrestRibbonSampler();

            Assert.IsTrue(sampler.TrySample(controls, multipliers, 25f, 1, false));

            const float lengthEstimate = 100f;
            Assert.AreEqual(CrestRibbonSampler.ResolveSampleCount(lengthEstimate, 1), sampler.Count);
            Assert.AreEqual(2, sampler.ControlCount);
            Assert.AreEqual(25f, sampler.SplineRadius, 1e-4f);

            var direction = (controls[1] - controls[0]).normalized;
            for (var i = 0; i < sampler.Count; i++)
            {
                var offset = sampler.Center(i) - controls[0];
                Assert.Less(Vector3.Cross(direction, offset).magnitude, 0.01f,
                    $"Sample {i} left the straight control line.");
                Assert.AreEqual(
                    sampler.SplineRadius * sampler.RadiusMultiplier(i) * 0.5f,
                    sampler.HalfWidthMeters(i),
                    0f,
                    $"Sample {i} half-width must be exactly Radius * Multiplier / 2.");
            }

            Assert.AreEqual(0f, sampler.Center(0).x, 1e-3f);
            Assert.AreEqual(100f, sampler.Center(sampler.Count - 1).x, 1e-3f);
        }

        [Test]
        public void RenderedRibbonWidth_MatchesRadiusTimesMultiplier()
        {
            var controls = new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f) };
            var sampler = new CrestRibbonSampler();
            Assert.IsTrue(sampler.TrySample(controls, new[] { 1f, 1f }, 25f, 1, false));

            for (var i = 0; i < sampler.Count; i++)
            {
                Assert.AreEqual(
                    sampler.SplineRadius * sampler.RadiusMultiplier(i),
                    (sampler.Right(i) - sampler.Left(i)).magnitude,
                    1e-3f,
                    $"Sample {i} breaks the full-ribbon width equation.");
            }
        }

        [Test]
        public void RenderedRibbonWidth_TapersWithTheAuthoredMultiplier()
        {
            var controls = new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f) };
            var sampler = new CrestRibbonSampler();
            Assert.IsTrue(sampler.TrySample(controls, new[] { 1f, 0.4f }, 25f, 1, false));

            var last = sampler.Count - 1;
            Assert.AreEqual(
                25f * sampler.RadiusMultiplier(0),
                (sampler.Right(0) - sampler.Left(0)).magnitude,
                1e-3f);
            Assert.AreEqual(
                25f * sampler.RadiusMultiplier(last),
                (sampler.Right(last) - sampler.Left(last)).magnitude,
                1e-3f);

            // Crest runs five edge-smoothing passes over the interior samples, so interior widths
            // track Radius * Multiplier within a fraction of a metre rather than exactly.
            for (var i = 0; i < sampler.Count; i++)
            {
                Assert.AreEqual(
                    25f * sampler.RadiusMultiplier(i),
                    (sampler.Right(i) - sampler.Left(i)).magnitude,
                    0.5f,
                    $"Sample {i} drifted too far from Radius * Multiplier.");
            }

            Assert.Less(
                (sampler.Right(last) - sampler.Left(last)).magnitude,
                (sampler.Right(0) - sampler.Left(0)).magnitude);
        }

        [Test]
        public void ControlPerpendicular_PointsAcrossTheControlPolyline()
        {
            IReadOnlyList<Vector3> controls = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 5f)
            };

            Assert.AreEqual(new Vector2(0f, -1f), CrestRibbonSampler.ControlPerpendicular(controls, 0));
            Assert.AreEqual(new Vector2(5f, -20f).normalized, CrestRibbonSampler.ControlPerpendicular(controls, 1));
            Assert.AreEqual(new Vector2(5f, -10f).normalized, CrestRibbonSampler.ControlPerpendicular(controls, 2));
            Assert.AreEqual(Vector2.right, CrestRibbonSampler.ControlPerpendicular(null, 0));
        }
    }
}
#endif
