#if UNITY_EDITOR
using Crest;
using Crest.Spline;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Crest;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Lake Crest eligibility, footprint bounds and scope, plus the generated
    /// <c>Lake_&lt;id&gt;</c> authored spline stack driven by the medial spine.
    /// </summary>
    public sealed class LakeCrestPlacementUtilityTests
    {
        [Test]
        public void IsCrestEligible_RequiresSurfaceWithinTolerance()
        {
            var lake = new LakeRecord { SurfaceWorldY = 0.5f };

            Assert.IsTrue(LakeCrestPlacementUtility.IsCrestEligible(lake, 0f, 2f));
            Assert.IsTrue(LakeCrestPlacementUtility.IsCrestEligible(lake, 0f, 0.5f));
            Assert.IsFalse(LakeCrestPlacementUtility.IsCrestEligible(lake, 0f, 0.49f));
            Assert.IsFalse(LakeCrestPlacementUtility.IsCrestEligible(lake, 20f, 2f));
        }

        [Test]
        public void IsCrestEligible_NullLakeIsIneligible()
        {
            Assert.IsFalse(LakeCrestPlacementUtility.IsCrestEligible(null, 0f, 100f));
        }

        [Test]
        public void IsCrestEligible_ClampsNegativeToleranceToExactMatch()
        {
            var lake = new LakeRecord { SurfaceWorldY = 3f };

            Assert.IsTrue(LakeCrestPlacementUtility.IsCrestEligible(lake, 3f, -5f));
            Assert.IsFalse(LakeCrestPlacementUtility.IsCrestEligible(lake, 3.01f, -5f));
        }

        [Test]
        public void TryComputeBoundsXZ_PrefersStoredBounds()
        {
            var lake = new LakeRecord
            {
                BoundsXZ = new Rect(20f, 20f, 40f, 12f),
                BasinCellCentersXZ = new[] { new Vector2(0f, 0f), new Vector2(500f, 500f) }
            };

            Assert.IsTrue(LakeCrestPlacementUtility.TryComputeBoundsXZ(lake, out var bounds));
            Assert.AreEqual(new Rect(20f, 20f, 40f, 12f), bounds);
        }

        [Test]
        public void TryComputeBoundsXZ_FallsBackToBasinCells()
        {
            var lake = new LakeRecord
            {
                BasinCellCentersXZ = new[]
                {
                    new Vector2(90f, 190f), new Vector2(110f, 190f),
                    new Vector2(110f, 210f), new Vector2(90f, 210f)
                }
            };

            Assert.IsTrue(LakeCrestPlacementUtility.TryComputeBoundsXZ(lake, out var bounds));
            Assert.AreEqual(90f, bounds.xMin, 0.001f);
            Assert.AreEqual(190f, bounds.yMin, 0.001f);
            Assert.AreEqual(20f, bounds.width, 0.001f);
            Assert.AreEqual(20f, bounds.height, 0.001f);
        }

        [Test]
        public void TryComputeBoundsXZ_EnforcesMinimumExtent()
        {
            var lake = new LakeRecord
            {
                BasinCellCentersXZ = new[] { new Vector2(100f, 200f) }
            };

            Assert.IsTrue(LakeCrestPlacementUtility.TryComputeBoundsXZ(lake, out var bounds));
            Assert.AreEqual(LakeCrestPlacementUtility.MinExtentMeters, bounds.width, 0.001f);
            Assert.AreEqual(LakeCrestPlacementUtility.MinExtentMeters, bounds.height, 0.001f);
            Assert.AreEqual(100f, bounds.center.x, 0.001f);
            Assert.AreEqual(200f, bounds.center.y, 0.001f);
        }

        [Test]
        public void TryComputeBoundsXZ_RejectsLakeWithoutGeometry()
        {
            Assert.IsFalse(LakeCrestPlacementUtility.TryComputeBoundsXZ(null, out _));
            Assert.IsFalse(LakeCrestPlacementUtility.TryComputeBoundsXZ(new LakeRecord(), out _));
            Assert.IsFalse(LakeCrestPlacementUtility.TryComputeBoundsXZ(
                new LakeRecord { BasinCellCentersXZ = System.Array.Empty<Vector2>() }, out _));
        }

        [Test]
        public void IsInScope_RequiresLakeInsideRadiusExpandedBounds()
        {
            var scope = WorldBuildScope.Bounds(new Rect(0f, 0f, 100f, 100f));
            Assert.IsFalse(LakeCrestPlacementUtility.IsInScope(scope, null));
            Assert.IsTrue(LakeCrestPlacementUtility.IsInScope(
                scope, new LakeRecord { CenterXZ = new Vector2(50f, 50f) }));
            Assert.IsFalse(LakeCrestPlacementUtility.IsInScope(
                scope, new LakeRecord { CenterXZ = new Vector2(500f, 500f) }));

            // A large lake expands the scope by its equivalent radius.
            var bigLake = new LakeRecord
            {
                CenterXZ = new Vector2(150f, 50f),
                AreaMetersSq = Mathf.PI * 100f * 100f
            };
            Assert.IsTrue(LakeCrestPlacementUtility.IsInScope(scope, bigLake));
            Assert.IsTrue(LakeCrestPlacementUtility.IsInScope(
                WorldBuildScope.FullMap(new Rect(0f, 0f, 100f, 100f)),
                new LakeRecord { CenterXZ = new Vector2(9000f, 9000f) }));
        }

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(-0.05f)]
        public void TryBuildPlacement_SnapsSeaLevelLakeToSeaLevel(float lakeHeight)
        {
            var lake = CreateRectangleLake(91, lakeHeight);

            Assert.IsTrue(TryBuild(lake, 0f, 0.05f, out var placement));
            Assert.AreEqual(lake.StableId, placement.StableId);
            Assert.IsFalse(placement.NeedsHeightSpline);
            Assert.AreEqual(0f, placement.SurfaceWorldY, 0.0001f);
        }

        [TestCase(0.051f)]
        [TestCase(-0.051f)]
        [TestCase(4f)]
        public void TryBuildPlacement_KeepsElevatedLakeSurface(float lakeHeight)
        {
            var lake = CreateRectangleLake(91, lakeHeight);

            Assert.IsTrue(TryBuild(lake, 0f, 0.05f, out var placement));
            Assert.IsTrue(placement.NeedsHeightSpline);
            Assert.AreEqual(lakeHeight, placement.SurfaceWorldY, 0.0001f);
        }

        [Test]
        public void TryBuildPlacement_FloorsToleranceAtElevationEpsilon()
        {
            Assert.IsTrue(TryBuild(CreateRectangleLake(1, 0.04f), 0f, 0f, out var near));
            Assert.IsFalse(near.NeedsHeightSpline);

            Assert.IsTrue(TryBuild(CreateRectangleLake(1, 0.06f), 0f, 0f, out var far));
            Assert.IsTrue(far.NeedsHeightSpline);
            Assert.AreEqual(0.06f, far.SurfaceWorldY, 0.0001f);
        }

        [Test]
        public void TryBuildPlacement_UsesAuthoredToleranceOverride()
        {
            var lake = CreateRectangleLake(91, 5f);

            Assert.IsFalse(TryBuild(lake, 0f, 2f, out var tight));
            Assert.IsTrue(tight.NeedsHeightSpline);

            Assert.IsTrue(TryBuild(lake, 0f, 8f, out var loose));
            Assert.IsFalse(loose.NeedsHeightSpline);
            Assert.AreEqual(0f, loose.SurfaceWorldY, 0.0001f);
        }

        [Test]
        public void TryBuildPlacement_RejectsOutOfScopeLake()
        {
            var lake = CreateRectangleLake(91, 4f);
            var scope = WorldBuildScope.Bounds(new Rect(2000f, 2000f, 100f, 100f));

            Assert.IsFalse(CrestLakeWaterBodyPlacementUtility.TryBuildPlacement(
                lake, 0f, 0.05f, scope, out _));
        }

        [Test]
        public void SpawnLakeSystem_CreatesDirectAuthoredStackAtLakeSurface()
        {
            var parent = new GameObject("Lakes");
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var lake = CreateRectangleLake(91, 22f);
                var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 4, 16f, null, new[] { lake });
                var footprint = plan.EnsureFootprintPlan(profile);
                Assert.IsTrue(footprint.TryGetFeature(lake.StableId, out var feature));
                Assert.GreaterOrEqual(feature.Points.Count, 2);

                Assert.IsTrue(TryBuild(lake, 0f, 0.05f, out var placement));
                Assert.IsTrue(placement.NeedsHeightSpline);

                var root = CrestLakeSplineBuilder.SpawnLakeSystem(
                    parent.transform,
                    feature,
                    placement,
                    WorldWaterProfile.CrestSplinePreset.LakeDefault,
                    null);

                Assert.IsNotNull(root);
                Assert.AreEqual($"Lake_{lake.StableId}", root.name);
                Assert.AreEqual(parent.transform, root.transform.parent);
                Assert.AreEqual(1, parent.transform.childCount);
                Assert.IsNull(root.transform.Find("Centerlines"));
                Assert.IsNull(root.transform.Find("ClipMesh"));
                Assert.IsNull(root.GetComponentInChildren<WaterBody>(true));

                var spline = root.GetComponent<Spline>();
                Assert.IsNotNull(spline);
                Assert.IsFalse(spline._closed);
                Assert.AreEqual(Spline.Offset.Center, spline._offset);
                Assert.AreEqual(20f, spline.Radius, 0.001f);
                Assert.AreEqual(1, spline.Subdivisions);

                var height = root.GetComponent<RegisterHeightInput>();
                Assert.IsNotNull(height);
                Assert.IsTrue(height.OverrideSplineSettings);
                Assert.AreEqual(25f, height.Radius, 0.001f);
                Assert.AreEqual(4, height.Subdivisions);
                Assert.IsNotNull(root.GetComponent<RegisterFlowInput>());
                Assert.AreEqual(128, root.GetComponent<ShapeFFT>()._resolution);

                var points = root.GetComponentsInChildren<SplinePoint>();
                Assert.AreEqual(feature.Points.Count, points.Length);
                for (var i = 0; i < points.Length; i++)
                {
                    Assert.AreEqual(22f, points[i].transform.position.y, 0.0001f,
                        "Lakes render one constant surface height.");
                    Assert.AreEqual(
                        InlandWaterFootprintPlan.ResolveRadiusMultiplier(
                            feature.Points[i].TargetWetWidthMeters, 20f),
                        points[i].GetComponent<SplinePointData>().RadiusMultiplier,
                        1e-4f);
                    Assert.AreEqual(1f, points[i].GetComponent<SplinePointDataWaves>().Weight, 0.001f);
                    Assert.AreEqual(2f, points[i].GetComponent<SplinePointDataFlow>().FlowVelocity, 0.001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(profile);
            }
        }

        private static bool TryBuild(
            LakeRecord lake,
            float seaLevelWorldY,
            float toleranceMeters,
            out CrestLakeWaterBodyPlacementUtility.LakePlacement placement) =>
            CrestLakeWaterBodyPlacementUtility.TryBuildPlacement(
                lake,
                seaLevelWorldY,
                toleranceMeters,
                WorldBuildScope.FullMap(new Rect(-500f, -500f, 1000f, 1000f)),
                out placement);

        private static LakeRecord CreateRectangleLake(ulong stableId, float surfaceWorldY) => new()
        {
            StableId = stableId,
            CenterXZ = new Vector2(100f, 30f),
            SurfaceWorldY = surfaceWorldY,
            MaxDepthMeters = 6f,
            AreaMetersSq = 12000f,
            BoundsXZ = new Rect(0f, 0f, 200f, 60f),
            OutlineXZ = new[]
            {
                new Vector2(0f, 0f), new Vector2(200f, 0f),
                new Vector2(200f, 60f), new Vector2(0f, 60f)
            },
            BasinCellCentersXZ = new[]
            {
                new Vector2(20f, 20f), new Vector2(100f, 20f), new Vector2(180f, 20f),
                new Vector2(20f, 40f), new Vector2(100f, 40f), new Vector2(180f, 40f)
            }
        };
    }
}
#endif
