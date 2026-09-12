#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class BiomeClassifierTests
    {
        [Test]
        public void Classify_AllOceanEdges_ForcesOceanOnWestEdgeCell()
        {
            var palette = WorldBuilderTestFixtures.CreatePalette();
            var hydrologyProfile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var landformProfile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 600f);
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 42,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);

            var landforms = WorldBuilderTestFixtures.CreateFlatLandforms(8, 8, 125f, 50f);
            var hydrology = new HydrologyPlan(8, 8, 125f, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(hydrology);

            var field = BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(landforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, 7));

            field.TryGetDominantStableId(0, out var edgeBiome);
            Assert.AreEqual("Ocean", edgeBiome);
            Assert.IsTrue(field.NoBuild[0]);
            Assert.AreEqual(0f, field.Buildability[0], 0.001f);
        }

        [Test]
        public void Classify_OceanCell_ForcesOceanAndNoBuild()
        {
            var palette = WorldBuilderTestFixtures.CreatePalette();
            var hydrologyProfile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var landformProfile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 200f);
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 42,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);

            var landforms = WorldBuilderTestFixtures.CreateFlatLandforms(8, 8, 125f, 50f);
            var hydrology = new HydrologyPlan(8, 8, 125f, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(hydrology);
            hydrology.WaterClass[hydrology.Index(4, 0)] = WorldWaterClass.Ocean;

            var field = BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(landforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, 7));

            var cell = hydrology.Index(4, 0);
            field.TryGetDominantStableId(cell, out var oceanBiome);
            Assert.AreEqual("Ocean", oceanBiome);
            Assert.IsTrue(field.NoBuild[cell]);
        }

        [Test]
        public void ApplyProgrammaticDefaults_ContainsAllNaturalBiomes()
        {
            var palette = WorldBuilderTestFixtures.CreatePalette();
            var expected = new[]
            {
                "Ocean", "Shore", "Plains", "Hills", "Mountains", "Desert", "Snow", "Forest", "Wetland",
                "Badlands", "Dunes", "AlpineSnow", "Savanna", "Taiga", "Swamp", "Scrubland", "Ashlands", "CityArea"
            };

            Assert.AreEqual(expected.Length, palette.Biomes.Count);
            for (var i = 0; i < expected.Length; i++)
                Assert.IsTrue(palette.TryGetBiome(expected[i], out _), $"Missing biome '{expected[i]}'.");
        }

        [Test]
        public void Classify_SeedSweep_ProducesExpandedAridAndColdBiomes()
        {
            var palette = WorldBuilderTestFixtures.CreatePalette();
            var hydrologyProfile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var landformProfile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 200f);
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 11,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);

            var foundDesert = false;
            var foundDunes = false;
            var foundBadlands = false;
            var foundAlpineSnow = false;
            var foundSavanna = false;
            var foundTaiga = false;
            var foundSwamp = false;
            var foundScrubland = false;

            for (var biomeSeed = 0; biomeSeed < 96; biomeSeed++)
            {
                var lowLandforms = WorldBuilderTestFixtures.CreateFlatLandforms(8, 8, 125f, 40f);
                var highLandforms = WorldBuilderTestFixtures.CreateFlatLandforms(8, 8, 125f, 520f);
                var duneLandforms = WorldBuilderTestFixtures.CreateRidgeLandforms(
                    8, 8, 125f, baseHeight: 35f, ridgeHeight: 55f);
                var badlandLandforms = WorldBuilderTestFixtures.CreateRidgeLandforms(
                    8, 8, 125f, baseHeight: 120f, ridgeHeight: 200f);

                var hydrology = new HydrologyPlan(8, 8, 125f, Vector2.zero);
                WorldBuilderTestFixtures.FillHydrologyDefaults(hydrology);
                var wetHydrology = new HydrologyPlan(8, 8, 125f, Vector2.zero);
                WorldBuilderTestFixtures.FillMoistHydrology(wetHydrology, centerX: 4, centerZ: 4);

                ScanField(BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(lowLandforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed)),
                    ref foundDesert, ref foundDunes, ref foundBadlands, ref foundAlpineSnow,
                    ref foundSavanna, ref foundTaiga, ref foundSwamp, ref foundScrubland);

                ScanField(BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(lowLandforms, wetHydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed)),
                    ref foundDesert, ref foundDunes, ref foundBadlands, ref foundAlpineSnow,
                    ref foundSavanna, ref foundTaiga, ref foundSwamp, ref foundScrubland);

                ScanField(BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(duneLandforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed)),
                    ref foundDesert, ref foundDunes, ref foundBadlands, ref foundAlpineSnow,
                    ref foundSavanna, ref foundTaiga, ref foundSwamp, ref foundScrubland);

                ScanField(BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(badlandLandforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed)),
                    ref foundDesert, ref foundDunes, ref foundBadlands, ref foundAlpineSnow,
                    ref foundSavanna, ref foundTaiga, ref foundSwamp, ref foundScrubland);

                ScanField(BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(highLandforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed)),
                    ref foundDesert, ref foundDunes, ref foundBadlands, ref foundAlpineSnow,
                    ref foundSavanna, ref foundTaiga, ref foundSwamp, ref foundScrubland);

                if (foundDesert && foundDunes && foundBadlands && foundAlpineSnow &&
                    foundSavanna && foundTaiga && foundSwamp && foundScrubland)
                    break;
            }

            Assert.IsTrue(foundDesert, "Expected at least one Desert cell in seed sweep.");
            Assert.IsTrue(foundDunes, "Expected at least one Dunes cell in seed sweep.");
            Assert.IsTrue(foundBadlands, "Expected at least one Badlands cell in seed sweep.");
            Assert.IsTrue(foundAlpineSnow, "Expected at least one AlpineSnow cell in seed sweep.");
            Assert.IsTrue(foundSavanna, "Expected at least one Savanna cell in seed sweep.");
            Assert.IsTrue(foundTaiga, "Expected at least one Taiga cell in seed sweep.");
            Assert.IsTrue(foundSwamp, "Expected at least one Swamp cell in seed sweep.");
            Assert.IsTrue(foundScrubland, "Expected at least one Scrubland cell in seed sweep.");
        }

        [Test]
        public void Classify_SeedSweep_AshlandsStaysRare()
        {
            var palette = WorldBuilderTestFixtures.CreatePalette();
            var hydrologyProfile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var landformProfile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 200f);
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 19,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 16,
                tileSizeMeters: 1000f);

            var landforms = WorldBuilderTestFixtures.CreateRidgeLandforms(
                16, 16, 125f, baseHeight: 180f, ridgeHeight: 240f);
            var hydrology = new HydrologyPlan(16, 16, 125f, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(hydrology);

            var ashlandsCount = 0;
            var landCells = 0;
            for (var biomeSeed = 0; biomeSeed < 64; biomeSeed++)
            {
                var field = BiomeClassifier.Classify(new BiomeClassifier.ClassifyArgs(landforms, hydrology, new BiomeClassifier.ClassifyProfiles(palette, hydrologyProfile, landformProfile, null), session, biomeSeed));

                ashlandsCount = 0;
                landCells = 0;
                for (var i = 0; i < field.DominantBiomeIndex.Length; i++)
                {
                    if (hydrology.WaterClass[i] == WorldWaterClass.Ocean)
                        continue;

                    landCells++;
                    field.TryGetDominantStableId(i, out var stableId);
                    if (stableId == "Ashlands")
                        ashlandsCount++;
                }

                if (ashlandsCount > 0)
                    break;
            }

            Assert.Greater(landCells, 0);
            Assert.Greater(ashlandsCount, 0, "Expected Ashlands to appear at least once.");
            Assert.Less((float)ashlandsCount / landCells, 0.05f, "Ashlands should remain rare on land cells.");
        }

        private static void ScanField(
            BiomeField field,
            ref bool foundDesert,
            ref bool foundDunes,
            ref bool foundBadlands,
            ref bool foundAlpineSnow,
            ref bool foundSavanna,
            ref bool foundTaiga,
            ref bool foundSwamp,
            ref bool foundScrubland)
        {
            for (var i = 0; i < field.DominantBiomeIndex.Length; i++)
            {
                field.TryGetDominantStableId(i, out var stableId);
                switch (stableId)
                {
                    case "Desert": foundDesert = true; break;
                    case "Dunes": foundDunes = true; break;
                    case "Badlands": foundBadlands = true; break;
                    case "AlpineSnow": foundAlpineSnow = true; break;
                    case "Savanna": foundSavanna = true; break;
                    case "Taiga": foundTaiga = true; break;
                    case "Swamp": foundSwamp = true; break;
                    case "Scrubland": foundScrubland = true; break;
                }
            }
        }
    }
}
#endif
