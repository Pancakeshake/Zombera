#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldMapBoundaryOceanCoastTests
    {
        [Test]
        public void CoastalShelfReefUtility_FullAmplitudeAtNoisePeak()
        {
            // Force peak by using a seed scan; assert max deepen within 10% of amplitude.
            var noise = new DeterministicNoise2D(99);
            const float amp = 6f;
            const float baseH = -4f;
            var maxDeepen = 0f;
            for (var i = 0; i < 200; i++)
            {
                var h = CoastalShelfReefUtility.ApplyDeepen(
                    baseH, 0f, i * 17f, i * 29f, 180f, amp, 0.99f, noise);
                maxDeepen = Mathf.Max(maxDeepen, baseH - h);
            }

            Assert.Greater(maxDeepen, amp * 0.5f);
            Assert.LessOrEqual(maxDeepen, amp + 0.01f);
        }

        [Test]
        public void EvaluateOceanCoastHeight_DoesNotBumpAboveLowInlandLand()
        {
            var noise = new DeterministicNoise2D(42);
            var height = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                landHeight: 2f,
                seaLevel: 0f,
                edgeDistanceMeters: 300f,
                stripDepthMeters: 650f,
                offshoreWidthMeters: 110f,
                shoreShelfWidthMeters: 70f,
                beachWidthMeters: 180f,
                beachMaxElevationMeters: 3.5f,
                trenchDepthMeters: 95f,
                trenchSteepness: 2.8f,
                noiseAmplitudeMeters: 14f,
                alongEdgeMeters: 500f,
                noise);

            Assert.LessOrEqual(height, 2.05f);
        }

        [Test]
        public void EvaluateOceanCoastBlend_IsFullThroughBeachCore()
        {
            // Beach core ends at 110+70+180=360m; strip is 650m.
            Assert.AreEqual(1f, WorldMapBoundaryUtility.EvaluateOceanCoastBlend(100f, 650f, 360f), 0.001f);
            Assert.AreEqual(1f, WorldMapBoundaryUtility.EvaluateOceanCoastBlend(360f, 650f, 360f), 0.001f);
            Assert.Less(WorldMapBoundaryUtility.EvaluateOceanCoastBlend(500f, 650f, 360f), 1f);
            Assert.AreEqual(0f, WorldMapBoundaryUtility.EvaluateOceanCoastBlend(650f, 650f, 360f), 0.001f);
        }

        [Test]
        public void TryGetOceanEdgeDistance_UsesOceanSidesOnly()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains);

            Assert.IsTrue(WorldMapBoundaryUtility.TryGetOceanEdgeDistance(
                50f, 1000f, bounds, layout, out var westCellOceanDist));
            // West is mountains; nearest ocean is south (1000) or east (1950).
            Assert.AreEqual(1000f, westCellOceanDist, 0.001f);

            Assert.IsTrue(WorldMapBoundaryUtility.TryGetOceanEdgeDistance(
                1950f, 1000f, bounds, layout, out var eastDist));
            Assert.AreEqual(50f, eastDist, 0.001f);
        }

        [Test]
        public void EvaluateOceanCoastFalloff_DoesNotRaiseLows()
        {
            var height = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: -4f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 450f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);

            Assert.AreEqual(-4f, height, 0.001f);
        }

        [Test]
        public void EvaluateOceanCoastFalloff_PullsHighsTowardDryShoulder()
        {
            var nearCore = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 380f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);
            var mid = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 500f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);
            var inland = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 650f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);

            Assert.Less(nearCore, mid);
            Assert.Less(mid, inland);
            Assert.AreEqual(40f, inland, 0.001f);
            Assert.Greater(nearCore, 3.5f);
            Assert.Less(nearCore, 40f);
        }

        [Test]
        public void EvaluateOceanCoastFalloff_LeavesBeachCoreUnchanged()
        {
            var height = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 200f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);

            Assert.AreEqual(40f, height, 0.001f);
        }

        [Test]
        public void EvaluateOceanCoastFalloff_ClampsWidthToBarrierDepth()
        {
            // 900 requested but barrier 650 → no soft effect past 650.
            var height = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                rawHeight: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 700f,
                coastCoreDepthMeters: 360f,
                falloffWidthMeters: 900f,
                barrierDepthMeters: 650f,
                dryShoulderElevationMeters: 3.5f);

            Assert.AreEqual(40f, height, 0.001f);
        }

        [Test]
        public void WarpOceanEdgeDistance_KeepsGeometricEdgeWet()
        {
            var noise = new DeterministicNoise2D(42);
            var warped = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                0f, 100f, 200f, noise, 280f, 480f);
            Assert.AreEqual(0f, warped, 0.001f);
        }

        [Test]
        public void WarpOceanEdgeDistance_OnlyErodesInlandNeverExpands()
        {
            var noise = new DeterministicNoise2D(42);
            const float straight = 400f;
            var minWarped = straight;
            var sawErosion = false;
            for (var i = 0; i < 40; i++)
            {
                var warped = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                    straight, i * 137f, i * 91f, noise, 280f, 480f);
                Assert.LessOrEqual(warped, straight + 0.001f);
                if (warped < straight - 1f)
                    sawErosion = true;
                minWarped = Mathf.Min(minWarped, warped);
            }

            Assert.IsTrue(sawErosion);
            Assert.GreaterOrEqual(minWarped, 0f);
        }

        [Test]
        public void WarpOceanEdgeDistance_VariesAlongEdge()
        {
            var noise = new DeterministicNoise2D(99);
            var a = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                350f, 0f, 100f, noise, 280f, 480f);
            var b = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                350f, 0f, 900f, noise, 280f, 480f);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void TryGetOceanCoastDistance_OnlyOnOceanSides()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains);

            Assert.IsFalse(WorldMapBoundaryUtility.TryGetOceanCoastDistance(
                50f, 1000f, bounds, 650f, layout, out _));
            Assert.IsTrue(WorldMapBoundaryUtility.TryGetOceanCoastDistance(
                1950f, 1000f, bounds, 650f, layout, out var eastDist));
            Assert.AreEqual(50f, eastDist, 0.001f);
        }

        [Test]
        public void TryGetEdgeBarrier_ReturnsMountainSideInsideStrip()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean);

            Assert.IsTrue(WorldMapBoundaryUtility.TryGetEdgeBarrier(
                50f, 1000f, bounds, 400f, layout, out var kind));
            Assert.AreEqual(WorldMapBoundaryKind.Mountains, kind);
        }

        [Test]
        public void ApplyInlandDryFloor_LeavesOceanCoreUnchanged()
        {
            var lifted = WorldMapBoundaryUtility.ApplyInlandDryFloor(
                height: -40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 50f,
                coastCoreDepthMeters: 360f,
                dryFloorMetersAboveSea: 6f,
                blendMeters: 120f);

            Assert.AreEqual(-40f, lifted, 0.001f);
        }

        [Test]
        public void ApplyInlandDryFloor_RaisesDeepInlandLowsToFloor()
        {
            var lifted = WorldMapBoundaryUtility.ApplyInlandDryFloor(
                height: -20f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 800f,
                coastCoreDepthMeters: 360f,
                dryFloorMetersAboveSea: 6f,
                blendMeters: 120f);

            Assert.AreEqual(6f, lifted, 0.001f);
        }

        [Test]
        public void ApplyInlandDryFloor_DoesNotLowerExistingHighs()
        {
            var kept = WorldMapBoundaryUtility.ApplyInlandDryFloor(
                height: 40f,
                seaLevel: 0f,
                oceanEdgeDistanceMeters: 800f,
                coastCoreDepthMeters: 360f,
                dryFloorMetersAboveSea: 6f,
                blendMeters: 120f);

            Assert.AreEqual(40f, kept, 0.001f);
        }
    }
}
#endif
