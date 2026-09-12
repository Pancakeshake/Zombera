#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class SparseHydrologyTests
    {
        [Test]
        public void Profile_DefaultsToSparseNetworkBudget()
        {
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            Assert.AreEqual(2, profile.MaxRiverSystems);
            Assert.AreEqual(6, profile.MaxVisibleRiverBranches);
            Assert.AreEqual(3, profile.MaxVisibleLakes);
            Assert.AreEqual(64f, profile.CityWaterSetbackMeters, 0.001f);
            Assert.AreEqual(72f, profile.MaxRiverWidthMeters, 0.001f);
            Assert.AreEqual(40000f, profile.LakeMinAreaMetersSq, 0.001f);
            Assert.AreEqual(0.005f, profile.LakeMaximumAreaFraction, 0.0001f);
            Assert.AreEqual(0.01f, profile.LakeMaximumTotalAreaFraction, 0.0001f);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void FlowAccumulation_RoutesEqualHeightCellsTowardOcean()
        {
            const int size = 8;
            var filled = new float[size * size];
            var ocean = new bool[size * size];
            var accumulation = new float[size * size];
            var flowTo = new int[size * size];
            for (var z = 0; z < size; z++)
            {
                for (var x = 0; x < size; x++)
                {
                    ocean[z * size + x] = z == 0;
                    filled[z * size + x] = 10f;
                }
            }

            FlowAccumulationSolver.Compute(filled, size, size, accumulation, flowTo, ocean);
            var cursor = size * (size - 1) + size / 2;
            var seen = new HashSet<int>();
            for (var guard = 0; cursor >= 0 && guard++ < size * size; guard++)
            {
                Assert.IsTrue(seen.Add(cursor), "Flat routing contains a cycle.");
                if (ocean[cursor]) break;
                cursor = flowTo[cursor];
            }
            Assert.IsTrue(cursor >= 0 && ocean[cursor]);
        }

        [Test]
        public void LakeExtractor_PreservesCellBoundsAndOutlet()
        {
            var field = new LandformField(128, 128, 16f, Vector2.zero);
            var filled = new float[field.WorldHeights.Length];
            for (var i = 0; i < field.WorldHeights.Length; i++)
            {
                field.WorldHeights[i] = 20f;
                filled[i] = 20f;
            }

            for (var z = 32; z < 48; z++)
            {
                for (var x = 32; x < 48; x++)
                {
                    field.SetHeight(x, z, -3f);
                    filled[field.Index(x, z)] = 20f;
                }
            }

            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var serialized = new UnityEditor.SerializedObject(profile);
            serialized.FindProperty("lakeMaximumAreaFraction").floatValue = 0.03f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var lakes = LakeExtractor.Extract(field, filled, profile);
            Assert.AreEqual(1, lakes.Length);
            Assert.That(lakes[0].BoundsXZ.width, Is.EqualTo(256f).Within(0.01f));
            Assert.That(lakes[0].BoundsXZ.height, Is.EqualTo(256f).Within(0.01f));
            Assert.GreaterOrEqual(lakes[0].OutlineXZ.Length, 4);
            Assert.IsTrue(lakes[0].HasOutlet);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void RiverNetworkBuilder_SelectsOceanOutletsAndMonotonicWidths()
        {
            const int size = 128;
            var field = new LandformField(size, size, 16f, Vector2.zero);
            var flowTo = new int[size * size];
            var accumulation = new float[size * size];
            var ocean = new bool[size * size];
            for (var z = 0; z < size; z++)
            {
                for (var x = 0; x < size; x++)
                {
                    var index = field.Index(x, z);
                    field.SetHeight(x, z, z);
                    ocean[index] = z == 0;
                    flowTo[index] = z == 0 ? -1 : field.Index(x, z - 1);
                    accumulation[index] = Mathf.Max(64f, (size - z) * 64f);
                }
            }

            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var rivers = RiverNetworkBuilder.Build(field, accumulation, flowTo, null, ocean, null, profile);
            Assert.That(rivers.Length, Is.InRange(1, profile.MaxRiverSystems));
            for (var r = 0; r < rivers.Length; r++)
            {
                Assert.IsTrue(rivers[r].HasOceanMouth);
                for (var i = 1; i < rivers[r].WidthMeters.Length; i++)
                    Assert.GreaterOrEqual(rivers[r].WidthMeters[i], rivers[r].WidthMeters[i - 1]);
            }
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void RiverFilter_RejectsBlockedBranchInsteadOfSplittingIt()
        {
            var field = new LandformField(8, 8, 16f, Vector2.zero);
            var mask = new bool[64];
            mask[field.Index(4, 4)] = true;
            var river = new RiverPolyline
            {
                StableId = 7,
                PointsXZ = new[]
                {
                    field.CellCenterXZ(2, 4), field.CellCenterXZ(3, 4),
                    field.CellCenterXZ(4, 4), field.CellCenterXZ(5, 4)
                },
                WidthMeters = new[] { 4f, 4f, 4f, 4f },
                DepthMeters = new[] { 1f, 1f, 1f, 1f }
            };
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var result = HydrologyRiverFilter.Filter(field, new[] { river }, mask, profile);
            Assert.IsEmpty(result);
            Object.DestroyImmediate(profile);
        }
    }
}
#endif
