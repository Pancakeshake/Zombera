#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class CityPadLandformFlattenerTests
    {
        [Test]
        public void Flatten_PlateauCellsMatchTarget_ForVaryingPadSizes()
        {
            var halfExtents = new[] { 40f, 120f, 239f, 240f, 241f, 280f, 600f };
            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();
            try
            {
                foreach (var half in halfExtents)
                {
                    var field = CreateSlopedField(64, 64, 16f);
                    var plateau = Rect.MinMaxRect(200f, 200f, 200f + half * 2f, 200f + half * 2f);
                    var pads = new[]
                    {
                        new CityFlattenPad(plateau, targetHeightWorldY: 40f, falloffMeters: 150f)
                    };

                    CityPadLandformFlattener.Apply(field, pads, profile, roads, hydrology: null);
                    AssertPlateauFlat(field, pads[0].PlateauBoundsXZ, pads[0].TargetHeightWorldY, 0.05f);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        [Test]
        public void Flatten_OverlappingPads_IsOrderIndependent()
        {
            var fieldA = CreateSlopedField(80, 80, 16f);
            var fieldB = CreateSlopedField(80, 80, 16f);
            System.Array.Copy(fieldA.WorldHeights, fieldB.WorldHeights, fieldA.WorldHeights.Length);

            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();

            CityPadLandformFlattener.Apply(
                fieldA,
                new[]
                {
                    new CityFlattenPad(Rect.MinMaxRect(100f, 100f, 420f, 420f), 20f, 180f),
                    new CityFlattenPad(Rect.MinMaxRect(380f, 100f, 700f, 420f), 60f, 180f)
                },
                profile,
                roads,
                null);
            CityPadLandformFlattener.Apply(
                fieldB,
                new[]
                {
                    new CityFlattenPad(Rect.MinMaxRect(380f, 100f, 700f, 420f), 60f, 180f),
                    new CityFlattenPad(Rect.MinMaxRect(100f, 100f, 420f, 420f), 20f, 180f)
                },
                profile,
                roads,
                null);

            Assert.AreEqual(fieldA.WorldHeights.Length, fieldB.WorldHeights.Length);
            for (var i = 0; i < fieldA.WorldHeights.Length; i++)
                Assert.AreEqual(fieldA.WorldHeights[i], fieldB.WorldHeights[i], 0.001f, $"cell {i}");

            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(roads);
        }

        [Test]
        public void Flatten_DoesNotRaiseDeepOceanBeyondReclaim()
        {
            var field = CreateSlopedField(40, 40, 16f);
            var hydro = new HydrologyPlan(40, 40, 16f, Vector2.zero);
            for (var z = 0; z < 40; z++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var i = hydro.Index(x, z);
                    hydro.WaterClass[i] = WorldWaterClass.Ocean;
                    hydro.DepthMeters[i] = 40f;
                }
            }

            var before = (float[])field.WorldHeights.Clone();
            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(200f, 200f, 500f, 500f), 80f, 200f)
            };

            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();
            try
            {
                CityPadLandformFlattener.Apply(field, pads, profile, roads, hydro, seaLevelWorldY: 0f);

                for (var z = 0; z < 40; z++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var i = field.Index(x, z);
                        Assert.AreEqual(before[i], field.WorldHeights[i], 0.001f);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        [Test]
        public void Flatten_RaisesShallowOceanAndFloorsAboveSea()
        {
            const float seaLevel = 10f;
            var field = new LandformField(32, 32, 16f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = seaLevel - 3f;

            var hydro = new HydrologyPlan(32, 32, 16f, Vector2.zero);
            for (var i = 0; i < hydro.WaterClass.Length; i++)
            {
                hydro.WaterClass[i] = WorldWaterClass.Ocean;
                hydro.DepthMeters[i] = 3f;
            }

            var plateau = Rect.MinMaxRect(80f, 80f, 240f, 240f);
            var pads = new[]
            {
                new CityFlattenPad(plateau, targetHeightWorldY: seaLevel - 3f, falloffMeters: 80f)
            };
            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();

            try
            {
                CityPadLandformFlattener.Apply(field, pads, profile, roads, hydro, seaLevel);
                var expectedMin = seaLevel + profile.CityPadMinClearanceAboveSeaMeters;
                Assert.GreaterOrEqual(pads[0].TargetHeightWorldY, expectedMin - 0.01f);
                AssertPlateauFlat(field, plateau, pads[0].TargetHeightWorldY, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        [Test]
        public void Prune_ClearsOceanUnderPad()
        {
            var plan = new HydrologyPlan(32, 32, 16f, Vector2.zero);
            for (var z = 5; z <= 12; z++)
            {
                for (var x = 5; x <= 12; x++)
                    plan.WaterClass[plan.Index(x, z)] = WorldWaterClass.Ocean;
            }

            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(80f, 80f, 200f, 200f), 10f, falloffMeters: 40f)
                {
                    HydrologyPruneMarginMeters = 0f
                }
            };

            HydrologyPlanFilter.PruneCityPads(plan, pads);
            Assert.AreEqual(WorldWaterClass.None, plan.WaterClass[plan.Index(8, 8)]);
        }

        [Test]
        public void Flatten_Twice_IsIdempotentWithSnapshot()
        {
            var field = CreateSlopedField(48, 48, 16f);
            var snapshot = (float[])field.WorldHeights.Clone();
            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(150f, 150f, 450f, 450f), 35f, 160f)
            };
            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();

            CityPadLandformFlattener.Apply(field, pads, profile, roads, null);
            var afterFirst = (float[])field.WorldHeights.Clone();

            CityPadLandformFlattener.RestoreFromSnapshot(field, snapshot);
            CityPadLandformFlattener.Apply(field, pads, profile, roads, null);

            for (var i = 0; i < field.WorldHeights.Length; i++)
                Assert.AreEqual(afterFirst[i], field.WorldHeights[i], 0.001f);
        }

        [Test]
        public void RoundedRectDistance_InsideIsNonPositive()
        {
            var rect = Rect.MinMaxRect(0f, 0f, 100f, 80f);
            Assert.LessOrEqual(
                CityPadLandformFlattener.RoundedRectDistanceOutside(new Vector2(50f, 40f), rect, 0.2f),
                0f);
            Assert.Greater(
                CityPadLandformFlattener.RoundedRectDistanceOutside(new Vector2(200f, 40f), rect, 0.2f),
                0f);
        }

        [Test]
        public void Prune_UsesPlateauNotFullApron()
        {
            var plan = new HydrologyPlan(32, 32, 16f, Vector2.zero);
            var riverPoint = new Vector2(300f, 100f);
            plan.WaterClass[plan.Index(18, 6)] = WorldWaterClass.River;
            plan.ReplaceWaterFeatures(
                new[]
                {
                    new RiverPolyline
                    {
                        PointsXZ = new[] { riverPoint, riverPoint + new Vector2(16f, 0f) },
                        WidthMeters = new[] { 8f, 8f },
                        DepthMeters = new[] { 1f, 1f }
                    }
                },
                System.Array.Empty<LakeRecord>());

            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(80f, 80f, 200f, 200f), 10f, falloffMeters: 240f)
                {
                    HydrologyPruneMarginMeters = 16f
                }
            };

            HydrologyPlanFilter.PruneCityPads(plan, pads);
            Assert.AreEqual(1, plan.Rivers.Length);
            Assert.AreEqual(WorldWaterClass.River, plan.WaterClass[plan.Index(18, 6)]);
        }

        [Test]
        public void MaskedTalus_DoesNotDisturbPlateau()
        {
            var field = CreateSlopedField(48, 48, 16f);
            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();
            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(150f, 150f, 450f, 450f), 35f, 160f)
            };

            try
            {
                CityPadLandformFlattener.Apply(field, pads, profile, roads, null);
                var plateauY = pads[0].TargetHeightWorldY;
                ThermalErosionSolver.ApplyMaskedForPads(field, profile, pads, roads.maxHighwayRoadSlopeDegrees);
                CityPadLandformFlattener.ReclampApronCones(field, pads, profile, roads);
                AssertPlateauFlat(field, pads[0].PlateauBoundsXZ, plateauY, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        [Test]
        public void MaskedTalus_DoesNotTouchFarField()
        {
            var field = CreateSlopedField(64, 64, 16f);
            var before = (float[])field.WorldHeights.Clone();
            var profile = CreateLandformProfile();
            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(100f, 100f, 260f, 260f), 40f, 80f)
            };

            try
            {
                ThermalErosionSolver.ApplyMaskedForPads(field, profile, pads, 8f);
                var far = field.Index(60, 60);
                Assert.AreEqual(before[far], field.WorldHeights[far], 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Continuity_FalloffMaxBind_HasNoRimCliff()
        {
            // Large height step just outside a small plateau so FalloffMax binds.
            var field = new LandformField(80, 80, 16f, Vector2.zero);
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                    field.WorldHeights[field.Index(x, z)] = 10f;
            }

            var plateau = Rect.MinMaxRect(400f, 400f, 560f, 560f);
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var c = field.CellCenterXZ(x, z);
                    if (c.x >= plateau.xMin && c.x <= plateau.xMax &&
                        c.y >= plateau.yMin && c.y <= plateau.yMax)
                        field.WorldHeights[field.Index(x, z)] = 200f;
                }
            }

            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();
            var pads = new[] { new CityFlattenPad(plateau, 200f, 150f) };

            try
            {
                CityPadLandformFlattener.Apply(field, pads, profile, roads, null);
                ThermalErosionSolver.ApplyMaskedForPads(
                    field, profile, pads, CityPadContinuity.MaxPadConeDegrees(pads, profile));
                CityPadLandformFlattener.ReclampApronCones(field, pads, profile, roads, null);

                Assert.Greater(pads[0].ConeSlopeRatio, 0.01f);
                AssertRimSlopeWithinCone(field, pads[0], profile.CityPadCornerRadiusFraction, 0.05f);
                AssertPlateauFlat(field, pads[0].PlateauBoundsXZ, pads[0].TargetHeightWorldY, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        [Test]
        public void Continuity_ExceedsMax_RejectsPredicate()
        {
            var falloffMax = 800f;
            var maxCont = 25f;
            var over = falloffMax * Mathf.Tan(maxCont * Mathf.Deg2Rad) + 20f;
            Assert.IsTrue(CityPadContinuity.ExceedsMaxContinuity(over, falloffMax, maxCont));

            var under = falloffMax * Mathf.Tan(maxCont * Mathf.Deg2Rad) - 20f;
            Assert.IsFalse(CityPadContinuity.ExceedsMaxContinuity(under, falloffMax, maxCont));
        }

        [Test]
        public void Continuity_Reclamp_DoesNotRaiseDeepOceanCells()
        {
            var field = CreateSlopedField(40, 40, 16f);
            var hydro = new HydrologyPlan(40, 40, 16f, Vector2.zero);
            for (var z = 0; z < 40; z++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var i = hydro.Index(x, z);
                    hydro.WaterClass[i] = WorldWaterClass.Ocean;
                    hydro.DepthMeters[i] = 40f;
                }
            }

            var before = (float[])field.WorldHeights.Clone();
            var profile = CreateLandformProfile();
            var roads = CreateRoadSettings();
            var pads = new[]
            {
                new CityFlattenPad(Rect.MinMaxRect(200f, 200f, 500f, 500f), 80f, 200f)
                {
                    ConeSlopeRatio = Mathf.Tan(12f * Mathf.Deg2Rad)
                }
            };

            try
            {
                CityPadLandformFlattener.ReclampApronCones(field, pads, profile, roads, hydro);
                for (var z = 0; z < 40; z++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var i = field.Index(x, z);
                        Assert.AreEqual(before[i], field.WorldHeights[i], 0.001f);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(roads);
            }
        }

        private static LandformField CreateSlopedField(int width, int height, float cellSize)
        {
            var field = new LandformField(width, height, cellSize, Vector2.zero);
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                    field.WorldHeights[field.Index(x, z)] = 10f + x * 0.35f + z * 0.2f;
            }

            return field;
        }

        private static LandformProfile CreateLandformProfile()
        {
            return ScriptableObject.CreateInstance<LandformProfile>();
        }

        private static RoadNetworkSettings CreateRoadSettings()
        {
            return ScriptableObject.CreateInstance<RoadNetworkSettings>();
        }

        private static void AssertPlateauFlat(LandformField field, Rect plateau, float expectedY, float epsilon)
        {
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (center.x < plateau.xMin || center.x > plateau.xMax ||
                        center.y < plateau.yMin || center.y > plateau.yMax)
                        continue;

                    Assert.AreEqual(
                        expectedY,
                        field.WorldHeights[field.Index(x, z)],
                        epsilon,
                        $"plateau cell ({x},{z})");
                }
            }
        }

        private static void AssertRimSlopeWithinCone(
            LandformField field,
            CityFlattenPad pad,
            float cornerFrac,
            float epsilon)
        {
            var maxAllowed = pad.ConeSlopeRatio * field.CellSize + epsilon;
            var falloff = Mathf.Max(1f, pad.FalloffMeters);
            for (var z = 1; z < field.Height - 1; z++)
            {
                for (var x = 1; x < field.Width - 1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    var d = CityPadLandformFlattener.RoundedRectDistanceOutside(
                        center, pad.PlateauBoundsXZ, cornerFrac);
                    if (d < falloff * 0.85f || d > falloff)
                        continue;

                    var h = field.WorldHeights[field.Index(x, z)];
                    var hRight = field.WorldHeights[field.Index(x + 1, z)];
                    var hUp = field.WorldHeights[field.Index(x, z + 1)];
                    Assert.LessOrEqual(Mathf.Abs(h - hRight), maxAllowed + 1f, $"rim x ({x},{z})");
                    Assert.LessOrEqual(Mathf.Abs(h - hUp), maxAllowed + 1f, $"rim z ({x},{z})");
                }
            }
        }
    }
}
#endif
