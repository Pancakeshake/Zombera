#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldSurfacePainterSnapshotTests
    {
        [TestCase(650f, 180f, TestName = "ClassDefaultProfile")]
        [TestCase(1100f, 90f, TestName = "AssetLikeProfile")]
        public void PreparePaintSession_DryShoulderCell_SetsNearOceanCoastTrue(
            float edgeDepth,
            float beachWidth)
        {
            var go = new GameObject("CoastSnapshotPainter");
            try
            {
                var painter = go.AddComponent<WorldSurfacePainter>();
                var profile = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth, beachWidth);
                var session = WorldMapSession.Create(
                    WorldMapSizeTier.Medium,
                    seed: 42,
                    profileVersion: 1,
                    originXZ: Vector2.zero,
                    tilesPerSide: 4,
                    tileSizeMeters: 500f);

                painter.BindPaintContext(session, profile);

                var cellSize = session.WorldBoundsXZ.width / 7f;
                var landforms = WorldBuilderTestFixtures.CreateFlatLandforms(
                    8, 8, cellSize, 8f, session.WorldBoundsXZ.min);

                var water = new HydrologyPlan(8, 8, cellSize, session.WorldBoundsXZ.min);
                for (var i = 0; i < water.WaterClass.Length; i++)
                {
                    water.WaterClass[i] = WorldWaterClass.None;
                    water.DistanceToWaterMeters[i] = 60f;
                    water.FlowAccumulation[i] = 1f;
                }

                var biomes = WorldBuilderTestFixtures.CreateMinimalBiomeField(8, 8);
                painter.PreparePaintSession(landforms, water, biomes);

                var snapshot = InvokeGetCellSnapshot(painter, landforms, water, biomes, cx: 0, cz: 4);
                var nearCoast = ReadBoolField(snapshot, "NearOceanCoast");
                var inBarrier = ReadBoolField(snapshot, "InOceanBarrierStrip");
                var edgeDist = ReadFloatField(snapshot, "EdgeDistMeters");

                Assert.IsTrue(
                    nearCoast,
                    "Dry-shoulder boundary coast must set NearOceanCoast even when waterDist >= 40.");
                Assert.IsTrue(inBarrier);
                Assert.Less(edgeDist, profile.EdgeBarrierDepthMeters);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static object InvokeGetCellSnapshot(
            WorldSurfacePainter painter,
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes,
            int cx,
            int cz)
        {
            var method = typeof(WorldSurfacePainter).GetMethod(
                "GetCellSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "GetCellSnapshot not found.");
            return method.Invoke(painter, new object[] { landforms, water, biomes, cx, cz });
        }

        private static bool ReadBoolField(object snapshot, string name)
        {
            var field = snapshot.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, name);
            return (bool)field.GetValue(snapshot);
        }

        private static float ReadFloatField(object snapshot, string name)
        {
            var field = snapshot.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, name);
            return (float)field.GetValue(snapshot);
        }
    }
}
#endif
