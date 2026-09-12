#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class HydrologyCarverBathymetryTests
    {
        [Test]
        public void Carve_OceanCells_LowerBedBelowSea()
        {
            const int size = 4;
            const float cell = 16f;
            const float landY = 0.1f;
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(size, size, cell, landY);
            var plan = new HydrologyPlan(size, size, cell, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(plan);

            for (var i = 0; i < plan.WaterClass.Length; i++)
            {
                plan.WaterClass[i] = WorldWaterClass.Ocean;
                plan.SurfaceWorldY[i] = 17f;
            }

            var profile = CreateHydrologyProfile(seaLevel: 0f);
            HydrologyCarver.Carve(field, plan, profile);

            for (var i = 0; i < field.WorldHeights.Length; i++)
            {
                Assert.AreEqual(-0.25f, field.WorldHeights[i], 0.05f);
                Assert.AreEqual(WorldWaterClass.Ocean, plan.WaterClass[i]);
                Assert.AreEqual(0f, plan.SurfaceWorldY[i], 0.01f);
            }

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Carve_LowLandRiver_PreservesSurfaceAndCarvesClearance()
        {
            const int size = 8;
            const float cell = 16f;
            const float landY = 20f;
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(size, size, cell, landY);
            var plan = new HydrologyPlan(size, size, cell, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(plan);

            var points = new[]
            {
                new Vector2(cell * 2f, cell * 2f),
                new Vector2(cell * 5f, cell * 2f),
                new Vector2(cell * 5f, cell * 5f)
            };
            // A 32 m wide channel is needed: the carver only stamps WorldWaterClass.River inside
            // the target wet footprint, so a narrower river never covers a planning-grid cell centre.
            plan.ReplaceWaterFeatures(new[]
            {
                new RiverPolyline
                {
                    StableId = 1,
                    RiverSystemStableId = 1,
                    PointsXZ = points,
                    WidthMeters = new[] { 32f, 32f, 32f },
                    DepthMeters = new[] { 2f, 2f, 2f },
                    FlowAccumulation = 100f,
                    HasOceanMouth = true,
                    OceanMouthXZ = points[points.Length - 1]
                }
            }, null);
            var preservedCell = plan.Index(2, 2);
            plan.WaterClass[preservedCell] = WorldWaterClass.River;
            plan.SurfaceWorldY[preservedCell] = 12f;

            var profile = CreateHydrologyProfile(seaLevel: 0f);
            HydrologyCarver.Carve(field, plan, profile);

            var foundRiver = false;
            for (var i = 0; i < plan.WaterClass.Length; i++)
            {
                if (plan.WaterClass[i] != WorldWaterClass.River)
                    continue;
                foundRiver = true;
                Assert.LessOrEqual(field.WorldHeights[i], plan.SurfaceWorldY[i] - 2f + 0.05f);
                Assert.Greater(plan.DepthMeters[i], 0f);
                Assert.LessOrEqual(plan.DepthMeters[i], 8f + 0.01f);
            }

            Assert.IsTrue(foundRiver, "Expected stamped river cells.");
            Assert.AreEqual(12f, plan.SurfaceWorldY[preservedCell], 0.01f);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Carve_HighLandMarkedRiver_DoesNotSeaClampThroughMountain()
        {
            const int size = 4;
            const float cell = 16f;
            const float mountainY = 220f;
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(size, size, cell, mountainY);
            var plan = new HydrologyPlan(size, size, cell, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(plan);

            for (var i = 0; i < plan.WaterClass.Length; i++)
            {
                plan.WaterClass[i] = WorldWaterClass.River;
                plan.DepthMeters[i] = 3f;
                plan.SurfaceWorldY[i] = mountainY;
            }

            var profile = CreateHydrologyProfile(seaLevel: 0f);
            HydrologyCarver.Carve(field, plan, profile);

            for (var i = 0; i < field.WorldHeights.Length; i++)
            {
                Assert.Greater(field.WorldHeights[i], 100f, "Mountain cells must not be quarried to sea floor.");
                Assert.That(Mathf.Abs(plan.SurfaceWorldY[i]) > 0.01f);
            }

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Carve_LakeCells_PreserveSurfaceAndCarveClearance()
        {
            const int size = 6;
            const float cell = 16f;
            const float landY = 25f;
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(size, size, cell, landY);
            var plan = new HydrologyPlan(size, size, cell, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(plan);

            var centers = new[]
            {
                new Vector2(cell * 2.5f, cell * 2.5f),
                new Vector2(cell * 3.5f, cell * 2.5f),
                new Vector2(cell * 2.5f, cell * 3.5f),
                new Vector2(cell * 3.5f, cell * 3.5f)
            };
            var lake = new LakeRecord
            {
                StableId = 9,
                CenterXZ = centers[0],
                SurfaceWorldY = 22f,
                MaxDepthMeters = 4f,
                BasinCellCentersXZ = centers,
                OutlineXZ = new[]
                {
                    centers[0], centers[1], centers[3], centers[2], centers[0]
                },
                ConnectedRiverSystemStableId = 1
            };
            plan.ReplaceWaterFeatures(null, new[] { lake });

            var profile = CreateHydrologyProfile(seaLevel: 0f);
            HydrologyCarver.Carve(field, plan, profile);

            Assert.AreEqual(22f, lake.SurfaceWorldY, 0.01f);
            var foundLake = false;
            for (var i = 0; i < plan.WaterClass.Length; i++)
            {
                if (plan.WaterClass[i] != WorldWaterClass.Lake)
                    continue;
                foundLake = true;
                Assert.LessOrEqual(field.WorldHeights[i], lake.SurfaceWorldY - 4f + 0.05f);
                Assert.AreEqual(lake.SurfaceWorldY, plan.SurfaceWorldY[i], 0.01f);
                Assert.LessOrEqual(plan.DepthMeters[i], 8f + 0.01f);
            }

            Assert.IsTrue(foundLake, "Expected stamped lake cells.");
            Object.DestroyImmediate(profile);
        }

        private static HydrologyProfile CreateHydrologyProfile(float seaLevel)
        {
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("seaLevelWorldY").floatValue = seaLevel;
            so.FindProperty("minRiverBedDepthBelowSea").floatValue = 2f;
            so.FindProperty("minLakeBedDepthBelowSea").floatValue = 4f;
            so.FindProperty("maxRiverLandElevationAboveSeaMeters").floatValue = 56f;
            so.FindProperty("carveShoulderWidthMeters").floatValue = 36f;
            so.FindProperty("maxRiverDepthMeters").floatValue = 8f;
            so.FindProperty("lakeMinimumDepthMeters").floatValue = 2f;
            so.FindProperty("hydrologyAlgorithmVersion").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }
    }
}
#endif
