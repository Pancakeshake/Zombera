#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class CoastalSurfaceBlendUtilityTests
    {
        [Test]
        public void ComputeBeachStrength_ReturnsZeroWhenNotNearOceanCoast()
        {
            var strength = CoastalSurfaceBlendUtility.ComputeBeachStrength(
                new CoastalSurfaceBlendUtility.BeachStrengthArgs
                {
                    ShoreWeight = 1f,
                    Elev = 2f,
                    SeaLevel = 0f,
                    WaterDist = 4f,
                    WaterClass = WorldWaterClass.None,
                    DominantId = "Shore",
                    NearOceanCoast = false,
                    OceanBeachMaxWaterDistanceMeters = 14f,
                    BeachMaxElevationAboveSea = 5.5f,
                    BeachInlandBlendMeters = 28f,
                    EdgeDistMeters = 9999f,
                    OceanCoastStripWidthMeters = 230f
                });
            Assert.AreEqual(0f, strength, 0.001f);
        }
        [Test]
        public void ComputeBeachStrength_BoostsShoreBiomeNearCoast()
        {
            var strength = CoastalSurfaceBlendUtility.ComputeBeachStrength(
                new CoastalSurfaceBlendUtility.BeachStrengthArgs
                {
                    ShoreWeight = 0.1f,
                    Elev = 2f,
                    SeaLevel = 0f,
                    WaterDist = 2f,
                    WaterClass = WorldWaterClass.None,
                    DominantId = "Shore",
                    NearOceanCoast = true,
                    OceanBeachMaxWaterDistanceMeters = 14f,
                    BeachMaxElevationAboveSea = 5.5f,
                    BeachInlandBlendMeters = 28f,
                    EdgeDistMeters = 20f,
                    OceanCoastStripWidthMeters = 230f
                });
            Assert.Greater(strength, 0.3f);
        }
        [Test]
        public void ComputeBeachStrength_UsesMapEdgeWhenWaterDistIsFar()
        {
            var strength = CoastalSurfaceBlendUtility.ComputeBeachStrength(
                new CoastalSurfaceBlendUtility.BeachStrengthArgs
                {
                    ShoreWeight = 0f,
                    Elev = 3f,
                    SeaLevel = 0f,
                    WaterDist = 400f,
                    WaterClass = WorldWaterClass.None,
                    DominantId = "Plains",
                    NearOceanCoast = true,
                    OceanBeachMaxWaterDistanceMeters = 14f,
                    BeachMaxElevationAboveSea = 8f,
                    BeachInlandBlendMeters = 40f,
                    EdgeDistMeters = 40f,
                    OceanCoastStripWidthMeters = 230f
                });
            Assert.Greater(strength, 0.2f);
        }
        [Test]
        public void CoastalBlendFactor_IsSoftGateAroundThreshold()
        {
            Assert.AreEqual(0f, CoastalSurfaceBlendUtility.CoastalBlendFactor(0.15f), 0.001f);
            Assert.Greater(CoastalSurfaceBlendUtility.CoastalBlendFactor(0.5f), 0.2f);
            Assert.AreEqual(1f, CoastalSurfaceBlendUtility.CoastalBlendFactor(0.9f), 0.001f);
        }
        [Test]
        public void AccumulateCoastalLayers_AtWaterline_PrefersWetSandOverPebbles()
        {
            var map = new float[1, 1, 32];
            var indices = new CoastalSurfaceBlendUtility.CoastalLayerIndices
            {
                Sand = 6,
                Sand01 = 2,
                WetSand = 31,
                WetRock = 28,
                BlackSand = 21,
                CliffBright = 5,
                CliffDark = 7,
                CliffPink = 9,
                CliffRed = 8,
                Dirt = 12,
                GrassGreen = 0,
                GrassYellow = 1,
                Grass = 3,
                SparseGrass = 4,
                MeadowGrass = 27,
                DryForestFloor = 26
            };
            // Simulate prior pebble contamination that suppress should clear.
            map[0, 0, 2] = 0.8f;
            map[0, 0, 0] = 0.5f;
            CoastalSurfaceBlendUtility.SuppressCompetingLayers(map, 0, 0, indices, 0.85f);
            var blend = new CoastalSurfaceBlendUtility.CoastalBlendParams
            {
                CoastalT = 1f,
                Slope = 4f,
                WaterDist = 1f,
                EdgeDist = 20f,
                ElevAbove = 1f,
                Moisture = 0.5f,
                MacroNoise = 0.5f,
                DetailNoise = 0.5f,
                PreferWet = true,
                PreferBlackSand = false,
                PreferRedCliff = false,
                PreferDryDesert = false
            };
            CoastalSurfaceBlendUtility.AccumulateCoastalLayers(map, 0, 0, 32, indices, blend);
            Assert.Less(map[0, 0, 2], 0.2f, "Sand01 pebbles should be suppressed on ocean coast.");
            Assert.Greater(map[0, 0, 31], map[0, 0, 6], "WetSand should dominate dry Sand at waterline.");
            Assert.Greater(map[0, 0, 31], 0.05f);
        }
        [Test]
        public void AccumulateCoastalLayers_OnSteepCoast_PaintsCliffLayers()
        {
            var map = new float[1, 1, 32];
            var indices = new CoastalSurfaceBlendUtility.CoastalLayerIndices
            {
                Sand = 6,
                Sand01 = 2,
                WetSand = 31,
                WetRock = 28,
                BlackSand = 21,
                CliffBright = 5,
                CliffDark = 7,
                CliffPink = 9,
                CliffRed = 8,
                Dirt = 12,
                GrassGreen = 0,
                GrassYellow = 1,
                Grass = 3,
                SparseGrass = 4,
                MeadowGrass = 27,
                DryForestFloor = 26
            };
            var blend = new CoastalSurfaceBlendUtility.CoastalBlendParams
            {
                CoastalT = 0.9f,
                Slope = 40f,
                WaterDist = 8f,
                EdgeDist = 80f,
                ElevAbove = 40f,
                Moisture = 0.4f,
                MacroNoise = 0.6f,
                DetailNoise = 0.4f,
                PreferWet = false,
                PreferBlackSand = true,
                PreferRedCliff = false,
                PreferDryDesert = false
            };
            CoastalSurfaceBlendUtility.AccumulateCoastalLayers(map, 0, 0, 32, indices, blend);
            Assert.AreEqual(0f, map[0, 0, 2], 0.001f);
            Assert.Greater(map[0, 0, 5] + map[0, 0, 7] + map[0, 0, 9], 0.05f);
            Assert.Less(map[0, 0, 6], 0.08f, "Dry sand should not carpet steep coastal cliffs.");
        }

        [Test]
        public void ComputeBeachStrength_UsesWarpedEdgeEvenWhenGeometricWouldBeFar()
        {
            // EdgeDistMeters is warped coast distance in the painter path.
            var strength = CoastalSurfaceBlendUtility.ComputeBeachStrength(
                new CoastalSurfaceBlendUtility.BeachStrengthArgs
                {
                    ShoreWeight = 0.2f,
                    Elev = 4f,
                    SeaLevel = 0f,
                    WaterDist = 500f,
                    WaterClass = WorldWaterClass.None,
                    DominantId = "Shore",
                    NearOceanCoast = true,
                    OceanBeachMaxWaterDistanceMeters = 14f,
                    BeachMaxElevationAboveSea = 8f,
                    BeachInlandBlendMeters = 40f,
                    EdgeDistMeters = 60f,
                    OceanCoastStripWidthMeters = 330f
                });

            Assert.Greater(strength, 0.2f);
        }
    }
}
#endif
