#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldMapBoundaryUtilityTests
    {
        [Test]
        public void TryGetSideAwareOceanEdgeDistance_ExcludesNorthMountainBand()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains);

            var hasOcean = WorldMapBoundaryUtility.TryGetSideAwareOceanEdgeDistance(
                1000f, 1990f, bounds, layout, out _);
            Assert.IsFalse(hasOcean);
        }

        [Test]
        public void DistanceToRectEdge_IsZeroOnBoundary()
        {
            var bounds = new Rect(0f, 0f, 1000f, 1000f);
            Assert.AreEqual(0f, WorldMapBoundaryUtility.DistanceToRectEdge(0f, 500f, bounds), 0.001f);
            Assert.AreEqual(0f, WorldMapBoundaryUtility.DistanceToRectEdge(500f, 1000f, bounds), 0.001f);
        }

        [Test]
        public void TryGetNearestEdgeSide_ReportsWestOnLeftEdge()
        {
            var bounds = new Rect(0f, 0f, 1000f, 1000f);
            WorldMapBoundaryUtility.TryGetNearestEdgeSide(10f, 500f, bounds, out var side, out var dist);
            Assert.AreEqual(WorldMapEdgeSide.West, side);
            Assert.AreEqual(10f, dist, 0.001f);
        }

        [Test]
        public void EvaluateEdgeBarrierBlend_IsOneInsideCore()
        {
            var blend = WorldMapBoundaryUtility.EvaluateEdgeBarrierBlend(100f, 500f, 200f);
            Assert.AreEqual(1f, blend, 0.001f);
        }

        [Test]
        public void IsOceanSeedCell_OnlySeedsOceanSides()
        {
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains);

            Assert.IsFalse(WorldMapBoundaryUtility.IsOceanSeedCell(0, 5, 10, 10, layout));
            Assert.IsFalse(WorldMapBoundaryUtility.IsOceanSeedCell(5, 9, 10, 10, layout));
            Assert.IsTrue(WorldMapBoundaryUtility.IsOceanSeedCell(5, 0, 10, 10, layout));
            Assert.IsTrue(WorldMapBoundaryUtility.IsOceanSeedCell(9, 5, 10, 10, layout));
            Assert.IsTrue(WorldMapBoundaryUtility.IsOceanSeedCell(0, 0, 10, 10, layout));
        }

        [Test]
        public void EvaluateOceanTrenchHeight_DropsBelowSeaLevelNearEdge()
        {
            var noise = new DeterministicNoise2D(42);
            var height = WorldMapBoundaryUtility.EvaluateOceanTrenchHeight(
                landHeight: 40f,
                seaLevel: 0f,
                edgeDistanceMeters: 20f,
                stripDepthMeters: 110f,
                steepness: 2.8f,
                trenchDepthMeters: 95f,
                noiseAmplitudeMeters: 14f,
                alongEdgeMeters: 500f,
                noise);

            Assert.Less(height, -6f);
        }

        [Test]
        public void EvaluateOceanCoastHeight_DeclinesFromInlandThroughBeachToSea()
        {
            var noise = new DeterministicNoise2D(42);
            var midBeach = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                landHeight: 40f,
                seaLevel: 0f,
                edgeDistanceMeters: 280f,
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
            var nearWater = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                landHeight: 40f,
                seaLevel: 0f,
                edgeDistanceMeters: 190f,
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
            // shoreEnd = 110+70 = 180 → inner shelf depth (default 1 m under sea).
            var shelfToe = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                landHeight: 40f,
                seaLevel: 0f,
                edgeDistanceMeters: 180f,
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

            Assert.Greater(midBeach, nearWater);
            Assert.Greater(midBeach, 0.5f);
            Assert.LessOrEqual(midBeach, 3.6f);
            Assert.AreEqual(-1f, shelfToe, 0.05f);
        }

        [Test]
        public void EvaluateOceanCoastHeight_AmpZero_JoinsAreContinuous()
        {
            var noise = new DeterministicNoise2D(42);
            const float offshore = 160f;
            const float shelf = 90f;
            const float beach = 80f;
            const float strip = 650f;
            const float outerDepth = 8f;
            const float innerDepth = 1f;

            float Sample(float edge) => WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                landHeight: 12f,
                seaLevel: 0f,
                edgeDistanceMeters: edge,
                stripDepthMeters: strip,
                offshoreWidthMeters: offshore,
                shoreShelfWidthMeters: shelf,
                beachWidthMeters: beach,
                beachMaxElevationMeters: 3.5f,
                trenchDepthMeters: 80f,
                trenchSteepness: 1.55f,
                noiseAmplitudeMeters: 0f,
                alongEdgeMeters: 500f,
                noise,
                shelfOuterDepthMeters: outerDepth,
                shelfInnerDepthMeters: innerDepth,
                shelfReefNoiseAmplitudeMeters: 0f);

            var justSeawardOfJoin = Sample(offshore - 0.01f);
            var justLandwardOfJoin = Sample(offshore + 0.01f);
            Assert.AreEqual(justSeawardOfJoin, justLandwardOfJoin, 0.05f);

            var shelfToe = Sample(offshore + shelf);
            Assert.AreEqual(-innerDepth, shelfToe, 0.05f);
            Assert.AreEqual(-outerDepth, Sample(offshore), 0.05f);
        }

        [Test]
        public void EvaluateOceanCoastHeight_ReefOnlyLowersAndNeverEmerges()
        {
            var noise = new DeterministicNoise2D(7);
            const float offshore = 160f;
            const float shelf = 90f;
            var midShelf = offshore + shelf * 0.5f;
            var sawDeepen = false;

            for (var i = 0; i < 80; i++)
            {
                var wx = i * 37f;
                var wz = i * 53f;
                var baseH = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                    landHeight: 12f,
                    seaLevel: 0f,
                    edgeDistanceMeters: midShelf,
                    stripDepthMeters: 650f,
                    offshoreWidthMeters: offshore,
                    shoreShelfWidthMeters: shelf,
                    beachWidthMeters: 80f,
                    beachMaxElevationMeters: 3.5f,
                    trenchDepthMeters: 80f,
                    trenchSteepness: 1.55f,
                    noiseAmplitudeMeters: 0f,
                    alongEdgeMeters: 500f,
                    noise,
                    worldX: wx,
                    worldZ: wz,
                    shelfOuterDepthMeters: 8f,
                    shelfInnerDepthMeters: 1f,
                    shelfReefNoiseAmplitudeMeters: 0f);
                var reefH = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                    landHeight: 12f,
                    seaLevel: 0f,
                    edgeDistanceMeters: midShelf,
                    stripDepthMeters: 650f,
                    offshoreWidthMeters: offshore,
                    shoreShelfWidthMeters: shelf,
                    beachWidthMeters: 80f,
                    beachMaxElevationMeters: 3.5f,
                    trenchDepthMeters: 80f,
                    trenchSteepness: 1.55f,
                    noiseAmplitudeMeters: 0f,
                    alongEdgeMeters: 500f,
                    noise,
                    worldX: wx,
                    worldZ: wz,
                    shelfOuterDepthMeters: 8f,
                    shelfInnerDepthMeters: 1f,
                    shelfReefNoiseScaleMeters: 180f,
                    shelfReefNoiseAmplitudeMeters: 6f,
                    shelfReefCoverage: 0.32f);

                Assert.LessOrEqual(reefH, baseH + 0.001f);
                Assert.LessOrEqual(reefH, -CoastalShelfReefUtility.MinWaterCoverMeters + 0.001f);
                if (reefH < baseH - 0.5f)
                    sawDeepen = true;
            }

            Assert.IsTrue(sawDeepen, "Expected at least one reef deepen sample.");
        }

        [Test]
        public void EvaluateOceanCoastHeight_AmpZero_SlopesDownPersistentlyTowardWater()
        {
            var noise = new DeterministicNoise2D(42);
            const float stripDepth = 650f;
            const float offshore = 160f;
            const float shelf = 90f;
            const float beach = 80f;
            var previous = float.MaxValue;

            for (var dist = stripDepth; dist >= 0f; dist -= 5f)
            {
                var height = WorldMapBoundaryUtility.EvaluateOceanCoastHeight(
                    landHeight: 12f,
                    seaLevel: 0f,
                    edgeDistanceMeters: dist,
                    stripDepthMeters: stripDepth,
                    offshoreWidthMeters: offshore,
                    shoreShelfWidthMeters: shelf,
                    beachWidthMeters: beach,
                    beachMaxElevationMeters: 3.5f,
                    trenchDepthMeters: 80f,
                    trenchSteepness: 1.55f,
                    noiseAmplitudeMeters: 0f,
                    alongEdgeMeters: 500f,
                    noise,
                    shelfOuterDepthMeters: 8f,
                    shelfInnerDepthMeters: 1f,
                    shelfReefNoiseAmplitudeMeters: 0f);

                Assert.LessOrEqual(height, previous + 0.001f,
                    $"Coast height rose toward water at distance {dist} (h={height}, prev={previous}).");
                previous = height;
            }
        }

    }
}
#endif
