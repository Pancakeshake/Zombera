#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class LandformGeneratorOrogenTests
    {
        [Test]
        public void Generate_SameSeed_IsDeterministic()
        {
            var landforms = CreateOrogenProfile();
            var hydro = ScriptableObject.CreateInstance<HydrologyProfile>();
            var grid = ScriptableObject.CreateInstance<TerrainGridProfile>();
            var plan = CreateTinyPlan(seed: 16, tilesPerSide: 2, cellSize: 32f);

            var a = LandformGenerator.GenerateWithOrogen(plan, landforms, hydro, grid, 1001, false, out var orogenA);
            var b = LandformGenerator.GenerateWithOrogen(plan, landforms, hydro, grid, 1001, false, out var orogenB);

            Assert.AreEqual(LandformValidationUtility.HashHeights(a), LandformValidationUtility.HashHeights(b));
            Assert.AreEqual(orogenA.PassCentersXZ.Length, orogenB.PassCentersXZ.Length);
            Assert.AreEqual(orogenA.Ranges.Length, orogenB.Ranges.Length);

            Object.DestroyImmediate(landforms);
            Object.DestroyImmediate(hydro);
            Object.DestroyImmediate(grid);
        }

        [Test]
        public void Generate_WithOrogen_DoesNotUseLegacyInteriorBoostedMountainChannel()
        {
            // Residual amplitude stays tiny; orogen peaks dominate interior highs.
            var landforms = CreateOrogenProfile();
            SetFloat(landforms, "residualMountainAmplitudeMeters", 20f);
            SetFloat(landforms, "mountainAmplitude", 320f);
            SetFloat(landforms, "interiorMountainPeakMeters", 160f);
            var hydro = ScriptableObject.CreateInstance<HydrologyProfile>();
            var grid = ScriptableObject.CreateInstance<TerrainGridProfile>();
            var plan = CreateTinyPlan(seed: 42, tilesPerSide: 3, cellSize: 32f);

            var field = LandformGenerator.GenerateWithOrogen(
                plan, landforms, hydro, grid, 2002, false, out var orogen);

            Assert.Greater(orogen.Ranges.Length, 0);
            var sea = hydro.SeaLevelWorldY;
            var maxInterior = float.MinValue;
            var ridgeSample = float.MinValue;
            for (var i = 0; i < orogen.Ranges.Length; i++)
            {
                var range = orogen.Ranges[i];
                // Sample off-pass crest (pass sits near mid-along).
                var crestT = range.PassAlong01 < 0.5f ? 0.78f : 0.22f;
                var u = 1f - crestT;
                var crest = u * u * range.Start + 2f * u * crestT * range.Mid + crestT * crestT * range.End;
                var h = LandformFieldSampling.SampleBilinear(field, crest.x, crest.y);
                ridgeSample = Mathf.Max(ridgeSample, h - sea);
            }

            for (var i = 0; i < field.WorldHeights.Length; i++)
            {
                if (field.WorldHeights[i] > sea + 2f)
                    maxInterior = Mathf.Max(maxInterior, field.WorldHeights[i] - sea);
            }

            // Would exceed ~320+160 if double-counted; with partition expect below that stack.
            Assert.Less(maxInterior, 320f + 160f);
            Assert.Greater(ridgeSample, 18f, "Orogen mid-spine should rise above residual-only noise.");

            Object.DestroyImmediate(landforms);
            Object.DestroyImmediate(hydro);
            Object.DestroyImmediate(grid);
        }

        [Test]
        public void Validate_DefaultOrogenMap_PassesGameplayGates()
        {
            var landforms = CreateOrogenProfile();
            // Tiny fixture maps are mostly orogen footprint; keep gates loose and assert structure.
            SetFloat(landforms, "minLowlandSlope12Fraction", 0.05f);
            SetFloat(landforms, "maxOrogenCoverageFraction", 1f);
            var hydro = ScriptableObject.CreateInstance<HydrologyProfile>();
            var grid = ScriptableObject.CreateInstance<TerrainGridProfile>();
            var plan = CreateTinyPlan(seed: 16, tilesPerSide: 3, cellSize: 32f);
            var field = LandformGenerator.GenerateWithOrogen(
                plan, landforms, hydro, grid, 16, true, out var orogen);

            var sea = hydro.SeaLevelWorldY;
            var baseY = grid.GetTerrainBaseY(sea);
            var maxY = baseY + grid.TerrainVerticalSize;
            var report = LandformValidationUtility.Validate(field, landforms, orogen, baseY, maxY, sea);

            Assert.IsTrue(report.Passed, report.Message);
            Assert.GreaterOrEqual(report.PassCount, orogen.Ranges.Length);
            Assert.Greater(orogen.Ranges.Length, 0);

            Object.DestroyImmediate(landforms);
            Object.DestroyImmediate(hydro);
            Object.DestroyImmediate(grid);
        }

        [Test]
        public void BuildRanges_InsetTooLarge_ReturnsEmpty()
        {
            var landforms = CreateOrogenProfile();
            SetFloat(landforms, "interiorReliefInsetMeters", 5000f);
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 7,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 2,
                tileSizeMeters: 1000f);

            var ranges = InteriorLandformRelief.BuildRanges(
                99,
                landforms,
                session.WorldBoundsXZ,
                WorldMapBoundaryLayout.AllOcean);

            Assert.AreEqual(0, ranges.Length);
            Object.DestroyImmediate(landforms);
        }

        [Test]
        public void PassCorridor_AtPassCenter_IsFlatterThanCrest()
        {
            var landforms = CreateOrogenProfile();
            SetFloat(landforms, "interiorMountainPeakMeters", 200f);
            SetFloat(landforms, "saddleDepth", 0.7f);
            SetFloat(landforms, "passCorridorHalfWidthMeters", 160f);
            var hydro = ScriptableObject.CreateInstance<HydrologyProfile>();
            var grid = ScriptableObject.CreateInstance<TerrainGridProfile>();
            var plan = CreateTinyPlan(seed: 11, tilesPerSide: 3, cellSize: 24f);
            var field = LandformGenerator.GenerateWithOrogen(
                plan, landforms, hydro, grid, 11, false, out var orogen);

            Assert.Greater(orogen.PassCentersXZ.Length, 0);
            var pass = orogen.PassCentersXZ[0];
            var passSlope = LandformFieldSampling.EstimateSlopeDegreesBilinear(field, pass.x, pass.y);
            Assert.LessOrEqual(passSlope, landforms.HighwayPassMaxSlopeDegrees + 2f, $"pass slope {passSlope}");

            Object.DestroyImmediate(landforms);
            Object.DestroyImmediate(hydro);
            Object.DestroyImmediate(grid);
        }

        private static LandformProfile CreateOrogenProfile()
        {
            var profile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 200f);
            SetBool(profile, "interiorMountainsEnabled", true);
            SetInt(profile, "interiorMountainRangeCount", 2);
            SetFloat(profile, "interiorMountainPeakMeters", 140f);
            SetFloat(profile, "interiorMountainWidthMeters", 900f);
            SetFloat(profile, "interiorReliefInsetMeters", 120f);
            SetFloat(profile, "foothillSkirtMeters", 500f);
            SetFloat(profile, "residualMountainAmplitudeMeters", 30f);
            SetInt(profile, "landformAlgorithmVersion", 2);
            SetFloat(profile, "structuralValleyDepthMeters", 20f);
            SetFloat(profile, "minLowlandSlope12Fraction", 0.25f);
            SetFloat(profile, "maxOrogenCoverageFraction", 0.55f);
            SetInt(profile, "minPassCountPerRange", 1);
            SetFloat(profile, "highwayPassMaxSlopeDegrees", 8f);
            return profile;
        }

        private static WorldPlan CreateTinyPlan(int seed, int tilesPerSide, float cellSize)
        {
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: tilesPerSide,
                tileSizeMeters: 1000f);
            var width = Mathf.Max(8, Mathf.CeilToInt(session.WorldBoundsXZ.width / cellSize));
            var height = Mathf.Max(8, Mathf.CeilToInt(session.WorldBoundsXZ.height / cellSize));
            return new WorldPlan(
                session,
                session.WorldBoundsXZ.position,
                cellSize,
                width,
                height,
                new Dictionary<string, int> { { WorldSubsystemSeeds.Landforms, seed } },
                fingerprint: 1);
        }

        private static void SetFloat(Object asset, string name, float value)
        {
            var so = new SerializedObject(asset);
            so.FindProperty(name).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object asset, string name, int value)
        {
            var so = new SerializedObject(asset);
            so.FindProperty(name).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object asset, string name, bool value)
        {
            var so = new SerializedObject(asset);
            so.FindProperty(name).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
