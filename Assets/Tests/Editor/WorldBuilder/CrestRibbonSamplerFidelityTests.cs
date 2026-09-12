#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Crest.Spline;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Cross-checks <see cref="CrestRibbonSampler"/> against Crest's own spline interpolation.
    /// This is the fidelity contract: footprint validation only means something if the sampler
    /// reproduces the ribbon Crest will actually render.
    /// </summary>
    public sealed class CrestRibbonSamplerFidelityTests
    {
        private const float SplineRadius = 25f;
        private const int Subdivisions = 1;

        [Test]
        public void SampledCenters_MatchCrestCubicInterpolation()
        {
            AssertCentersMatchCrest(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(40f, 0f, 30f),
                new Vector3(90f, 0f, 60f),
                new Vector3(140f, 0f, 10f)
            });
        }

        [Test]
        public void SampledCenters_MatchCrestForThreeControlsWithVerticalChange()
        {
            AssertCentersMatchCrest(new[]
            {
                new Vector3(0f, 60f, 0f),
                new Vector3(50f, 40f, 40f),
                new Vector3(120f, 5f, 10f)
            });
        }

        [Test]
        public void SampleCount_MatchesCrestSpacingRule()
        {
            var controls = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(300f, 0f, 0f),
                new Vector3(300f, 0f, 400f)
            };

            var sampler = new CrestRibbonSampler();
            Assert.IsTrue(sampler.TrySample(controls, null, SplineRadius, Subdivisions, closed: false));

            // Crest: spacing = 16 / 2^(subdivisions + 1); pointCount = ceil(sumOfSegmentLengths / spacing).
            var lengthEstimate = 300f + 400f;
            var spacing = 16f / Mathf.Pow(2f, Subdivisions + 1);
            var expected = Mathf.Max(1, Mathf.CeilToInt(lengthEstimate / spacing));
            Assert.AreEqual(expected, sampler.Count);
            Assert.AreEqual(spacing, CrestRibbonSampler.ResolveSpacingMeters(Subdivisions), 1e-5f);
        }

        /// <summary>
        /// Builds real <see cref="SplinePoint"/> components, runs Crest's own hull + cubic
        /// interpolation, and asserts the sampler's centres agree exactly at Crest's sampling t.
        /// </summary>
        private static void AssertCentersMatchCrest(IReadOnlyList<Vector3> positions)
        {
            var parent = new GameObject("CrestFidelitySpline");
            try
            {
                var count = positions.Count;
                var splinePoints = new SplinePoint[count];
                for (var i = 0; i < count; i++)
                {
                    var pointObject = new GameObject($"SplinePoint_{i}");
                    pointObject.transform.SetParent(parent.transform, false);
                    pointObject.transform.position = positions[i];
                    splinePoints[i] = pointObject.AddComponent<SplinePoint>();
                }

                var hull = new Vector3[(count - 1) * 3 + 1];
                Assert.IsTrue(
                    SplineInterpolation.GenerateCubicSplineHull(splinePoints, hull, closed: false),
                    "Crest must build a cubic hull for this control set.");

                var controls = new Vector3[count];
                for (var i = 0; i < count; i++)
                    controls[i] = positions[i];

                var sampler = new CrestRibbonSampler();
                Assert.IsTrue(sampler.TrySample(controls, null, SplineRadius, Subdivisions, closed: false));
                Assert.Greater(sampler.Count, 2);

                for (var i = 0; i < sampler.Count; i++)
                {
                    var t = i / (float)(sampler.Count - 1);
                    SplineInterpolation.InterpolateCubicPosition(count, hull, t, out var expected);
                    var actual = sampler.Center(i);
                    Assert.AreEqual(expected.x, actual.x, 1e-4f, $"Sample {i} x diverged from Crest.");
                    Assert.AreEqual(expected.y, actual.y, 1e-4f, $"Sample {i} y diverged from Crest.");
                    Assert.AreEqual(expected.z, actual.z, 1e-4f, $"Sample {i} z diverged from Crest.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
#endif
