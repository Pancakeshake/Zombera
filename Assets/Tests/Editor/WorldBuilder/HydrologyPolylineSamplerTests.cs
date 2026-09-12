#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class HydrologyPolylineSamplerTests
    {
        [Test]
        public void BuildFreeSurfaceYs_EnforcesNonIncreasingAlongFlow()
        {
            var plan = new HydrologyPlan(8, 8, 16f, Vector2.zero);
            plan.SurfaceWorldY[plan.Index(2, 2)] = 10f;
            plan.SurfaceWorldY[plan.Index(3, 2)] = 14f;
            plan.SurfaceWorldY[plan.Index(4, 2)] = 8f;
            var ys = HydrologyPolylineSampler.BuildFreeSurfaceYs(plan, new[]
            {
                new Vector2(40f, 40f),
                new Vector2(56f, 40f),
                new Vector2(72f, 40f)
            });

            Assert.AreEqual(3, ys.Length);
            Assert.LessOrEqual(ys[1], ys[0] + 0.001f);
            Assert.LessOrEqual(ys[2], ys[1] + 0.001f);
        }

        [Test]
        public void ResolveWidthMeters_ZeroUsesDefault()
        {
            Assert.AreEqual(HydrologyPolylineSampler.DefaultWidthMeters,
                HydrologyPolylineSampler.ResolveWidthMeters(new[] { 0f }, 0), 0.001f);
            Assert.AreEqual(12f,
                HydrologyPolylineSampler.ResolveWidthMeters(new[] { 12f }, 0), 0.001f);
        }

        [Test]
        public void BuildFlowVelocities_SamplesRasterAndNeverDecreasesDownstream()
        {
            var plan = new HydrologyPlan(4, 1, 10f, Vector2.zero);
            plan.FlowAccumulation[plan.Index(0, 0)] = 0f;
            plan.FlowAccumulation[plan.Index(1, 0)] = 5000f;
            plan.FlowAccumulation[plan.Index(2, 0)] = 1000f;
            plan.FlowAccumulation[plan.Index(3, 0)] = 100000f;
            var river = new RiverPolyline { FlowAccumulation = 2500f };
            IReadOnlyList<Vector2> points = new[]
            {
                new Vector2(5f, 5f),
                new Vector2(15f, 5f),
                new Vector2(25f, 5f),
                new Vector2(35f, 5f)
            };

            var velocities = HydrologyPolylineSampler.BuildFlowVelocities(
                plan,
                river,
                points,
                0.75f,
                4f);

            Assert.AreEqual(points.Count, velocities.Length);
            Assert.AreEqual(0.75f, velocities[0], 0.001f);
            Assert.AreEqual(2.375f, velocities[1], 0.001f);
            Assert.AreEqual(velocities[1], velocities[2], 0.001f,
                "A lower raster accumulation must not slow the flow downstream.");
            Assert.AreEqual(4f, velocities[3], 0.001f);
        }

        [Test]
        public void BuildFlowVelocities_InvalidRasterUsesRiverFallbackAndClamps()
        {
            var river = new RiverPolyline { FlowAccumulation = 100000f };
            IReadOnlyList<Vector2> points = new[]
            {
                new Vector2(-10f, -10f),
                new Vector2(500f, 500f)
            };

            var velocities = HydrologyPolylineSampler.BuildFlowVelocities(
                null,
                river,
                points,
                0.75f,
                4f);

            Assert.AreEqual(2, velocities.Length);
            Assert.AreEqual(4f, velocities[0], 0.001f);
            Assert.AreEqual(4f, velocities[1], 0.001f);
        }

        [Test]
        public void BuildFlowVelocities_EmptyPointsReturnsEmptyArray()
        {
            var velocities = HydrologyPolylineSampler.BuildFlowVelocities(
                null,
                new RiverPolyline(),
                new List<Vector2>(),
                0.75f,
                4f);

            Assert.IsEmpty(velocities);
        }
    }
}
#endif
