#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using Crest;
using Crest.Spline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Crest;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>
    /// Guards the shared inland water footprint contract at its two consumers:
    /// <list type="bullet">
    /// <item>the city road builder, which must never refill a carved channel (public protection API only),</item>
    /// <item>the Crest inland spline builders, which must emit single, prefixed, unwrapped and
    /// deterministically named roots.</item>
    /// </list>
    /// The prefix contract asserted here is what <c>CrestOceanWaterBackend.IsOwnedGeneratedChild</c>
    /// matches on when it clears the <c>WaterBodies</c> folder (<c>River_</c> / <c>Lake_</c>).
    /// </summary>
    public sealed class InlandWaterRoadProtectionTests
    {
        private const float SeaLevelWorldY = 5f;
        private const float LakeSurfaceWorldY = 22f;
        private const float RoadBedClearanceMeters = 2f;
        private const ulong RiverStableId = 1UL;
        private const ulong LakeStableId = 9UL;
        private const string RiverRootPrefix = "River_";
        private const string LakeRootPrefix = "Lake_";

        private static readonly string[] ForbiddenWrapperNames =
        {
            "Centerlines", "ClipMesh", "RiverSystem", "Lakes"
        };

        // ---------------------------------------------------------------- A. road / city refinement

        /// <summary>
        /// A1-A6: the footprint instance round-trips, inside points are protected, outside points are
        /// untouched, the bed floor clamps a filling candidate and never raises a lowering one.
        /// </summary>
        [Test]
        public void RoadBuilder_ClampsRefillInsideFootprint()
        {
            var profile = CreateHydrologyProfile();
            var roadRoot = new GameObject("RoadBuilder");
            try
            {
                var plan = CreatePlan(CreateRiver(), null, profile);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                var feature = RequireFeature(footprint, RiverStableId);

                var builder = roadRoot.AddComponent<CityPrefabRoadNetworkBuilder>();
                builder.SetWaterFootprintPlan(footprint, minimumBedClearanceMeters: RoadBedClearanceMeters);

                Assert.AreSame(footprint, builder.InlandWaterFootprintPlan);

                var centerline = feature.Points[0].CenterXZ;
                var offFootprint = new Vector2(centerline.x, centerline.y + 200f);
                Assert.Greater(
                    Vector2.Distance(centerline, offFootprint),
                    feature.Points[0].TargetWetHalfWidthMeters + feature.Points[0].BankShoulderMeters,
                    "The outside probe must clear the wet span plus the bank shoulder.");

                Assert.IsTrue(builder.IsWaterProtected(centerline), "River centreline must be protected.");
                Assert.IsFalse(builder.IsWaterProtected(offFootprint), "Ground beyond the footprint is unprotected.");

                var bedFloorY = feature.Points[0].SurfaceWorldY - RoadBedClearanceMeters;
                Assert.AreEqual(bedFloorY, builder.ResolveProtectedGroundHeight(centerline, 100f), 0.01f);
                Assert.AreEqual(-500f, builder.ResolveProtectedGroundHeight(centerline, -500f), 0.01f);
                Assert.AreEqual(100f, builder.ResolveProtectedGroundHeight(offFootprint, 100f), 0.01f);
            }
            finally
            {
                DestroyAll(profile, roadRoot);
            }
        }

        /// <summary>A7: clearing the plan (null) removes every protection.</summary>
        [Test]
        public void RoadBuilder_NullFootprintClearsProtection()
        {
            var profile = CreateHydrologyProfile();
            var roadRoot = new GameObject("RoadBuilder");
            try
            {
                var plan = CreatePlan(CreateRiver(), null, profile);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                var centerline = RequireFeature(footprint, RiverStableId).Points[0].CenterXZ;

                var builder = roadRoot.AddComponent<CityPrefabRoadNetworkBuilder>();
                builder.SetWaterFootprintPlan(footprint, RoadBedClearanceMeters);
                Assert.IsTrue(builder.IsWaterProtected(centerline));

                builder.SetWaterFootprintPlan(null);

                Assert.IsNull(builder.InlandWaterFootprintPlan);
                Assert.IsFalse(builder.IsWaterProtected(centerline));
                Assert.AreEqual(100f, builder.ResolveProtectedGroundHeight(centerline, 100f), 0.01f);
            }
            finally
            {
                DestroyAll(profile, roadRoot);
            }
        }

        /// <summary>A8: a river feature and a lake feature both protect their own centreline.</summary>
        [Test]
        public void RoadBuilder_BothRiverAndLakeProtect()
        {
            var profile = CreateHydrologyProfile();
            var roadRoot = new GameObject("RoadBuilder");
            try
            {
                var plan = CreatePlan(CreateRiver(), CreateLake(), profile);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                var river = RequireFeature(footprint, RiverStableId);
                var lake = RequireFeature(footprint, LakeStableId);

                Assert.AreEqual(InlandWaterFootprintPlan.FeatureKind.River, river.Kind);
                Assert.AreEqual(InlandWaterFootprintPlan.FeatureKind.Lake, lake.Kind);
                Assert.GreaterOrEqual(lake.Points.Count, 2, "The lake medial spine must be non-degenerate.");
                Assert.IsTrue(lake.Points[0].TargetWetHalfWidthMeters > 1f, "Lake spine needs a real half width.");

                var builder = roadRoot.AddComponent<CityPrefabRoadNetworkBuilder>();
                builder.SetWaterFootprintPlan(footprint, RoadBedClearanceMeters);

                Assert.IsTrue(builder.IsWaterProtected(river.Points[0].CenterXZ), "River centreline.");
                Assert.IsTrue(builder.IsWaterProtected(lake.Points[0].CenterXZ), "Lake spine endpoint.");
                Assert.IsTrue(builder.IsWaterProtected(Midpoint(lake)), "Lake spine midpoint.");
            }
            finally
            {
                DestroyAll(profile, roadRoot);
            }
        }

        // ------------------------------------------------- B. Crest naming / topology determinism

        /// <summary>
        /// 9-12 + 14: one river spawn yields exactly one prefixed root, no wrapper objects and no
        /// Crest WaterBody, and a second spawn into a separate folder rebuilds identical point data.
        /// </summary>
        [Test]
        public void RiverBuilder_SpawnRivers_IsSinglePrefixedAndDeterministic()
        {
            var profile = CreateHydrologyProfile();
            var water = ScriptableObject.CreateInstance<WorldWaterProfile>();
            var firstFolder = new GameObject("WaterBodies_First");
            var secondFolder = new GameObject("WaterBodies_Second");
            try
            {
                var plan = CreatePlan(CreateRiver(), null, profile);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                var feature = RequireFeature(footprint, RiverStableId);
                var scope = CreateScope();

                var firstRoots = new List<GameObject>();
                var firstSpawned = CrestRiverSplineBuilder.SpawnRivers(
                    firstFolder.transform, plan, footprint, scope, water, null, SeaLevelWorldY, firstRoots);
                var secondRoots = new List<GameObject>();
                var secondSpawned = CrestRiverSplineBuilder.SpawnRivers(
                    secondFolder.transform, plan, footprint, scope, water, null, SeaLevelWorldY, secondRoots);

                Assert.AreEqual(1, firstSpawned);
                Assert.AreEqual(1, secondSpawned);

                var expectedName = RiverRootPrefix + RiverStableId.ToString(CultureInfo.InvariantCulture);
                Assert.AreEqual(1, firstRoots.Count);
                Assert.AreEqual(expectedName, firstRoots[0].name);
                Assert.AreEqual(1, firstFolder.transform.childCount, "No RiverSystem / Centerlines wrapper folder.");
                Assert.AreEqual(expectedName, firstFolder.transform.GetChild(0).name);
                Assert.AreEqual(
                    CountDistinctRiverIds(plan),
                    CountChildrenWithPrefix(firstFolder, RiverRootPrefix),
                    "Exactly one root per distinct river StableId.");

                AssertNoInlandWrappers(firstRoots[0], RiverRootPrefix);
                AssertSplineDataMatches(firstRoots[0], secondRoots[0], feature.Points.Count);

                var point = firstRoots[0].GetComponentsInChildren<SplinePoint>(true)[0];
                Assert.AreEqual(1.6f, point.GetComponent<SplinePointData>().RadiusMultiplier, 0.001f);
            }
            finally
            {
                DestroyAll(profile, water, firstFolder, secondFolder);
            }
        }

        /// <summary>13 + 14: the lake builder mirrors the river naming / determinism contract.</summary>
        [Test]
        public void LakeBuilder_SpawnLakes_IsSinglePrefixedAndDeterministic()
        {
            var profile = CreateHydrologyProfile();
            var water = ScriptableObject.CreateInstance<WorldWaterProfile>();
            var firstFolder = new GameObject("WaterBodies_First");
            var secondFolder = new GameObject("WaterBodies_Second");
            try
            {
                var plan = CreatePlan(null, CreateLake(), profile);
                var footprint = InlandWaterFootprintPlan.Build(plan, profile);
                var feature = RequireFeature(footprint, LakeStableId);
                var scope = CreateScope();

                var firstRoots = new List<GameObject>();
                var firstSpawned = CrestLakeSplineBuilder.SpawnLakes(
                    firstFolder.transform, plan, footprint, scope, water, null, 0f, firstRoots);
                var secondRoots = new List<GameObject>();
                var secondSpawned = CrestLakeSplineBuilder.SpawnLakes(
                    secondFolder.transform, plan, footprint, scope, water, null, 0f, secondRoots);

                Assert.AreEqual(1, firstSpawned);
                Assert.AreEqual(1, secondSpawned);

                var expectedName = LakeRootPrefix + LakeStableId.ToString(CultureInfo.InvariantCulture);
                Assert.AreEqual(1, firstRoots.Count);
                Assert.AreEqual(expectedName, firstRoots[0].name);
                Assert.AreEqual(1, firstFolder.transform.childCount, "No LakeSystem / clip-mesh wrapper folder.");

                AssertNoInlandWrappers(firstRoots[0], LakeRootPrefix);
                AssertSplineDataMatches(firstRoots[0], secondRoots[0], feature.Points.Count);

                var points = firstRoots[0].GetComponentsInChildren<SplinePoint>(true);
                for (var i = 0; i < points.Length; i++)
                {
                    Assert.AreEqual(
                        LakeSurfaceWorldY,
                        points[i].transform.position.y,
                        0.01f,
                        "Every lake control point shares one placement surface height.");
                }
            }
            finally
            {
                DestroyAll(profile, water, firstFolder, secondFolder);
            }
        }

        // ------------------------------------------------------------------------------- helpers

        private static WorldBuildScope CreateScope() =>
            WorldBuildScope.FullMap(new Rect(-500f, -500f, 2000f, 2000f));

        private static Vector2 Midpoint(InlandWaterFootprintPlan.Feature feature)
        {
            var first = feature.Points[0].CenterXZ;
            var last = feature.Points[feature.Points.Count - 1].CenterXZ;
            return (first + last) * 0.5f;
        }

        private static InlandWaterFootprintPlan.Feature RequireFeature(
            InlandWaterFootprintPlan footprint,
            ulong stableId)
        {
            Assert.IsTrue(
                footprint.TryGetFeature(stableId, out var feature),
                $"Footprint is missing feature {stableId}.");
            Assert.IsNotNull(feature);
            return feature;
        }

        private static RiverPolyline CreateRiver() => new()
        {
            StableId = RiverStableId,
            RiverSystemStableId = RiverStableId,
            Kind = RiverKind.MainStem,
            PointsXZ = new[] { new Vector2(32f, 32f), new Vector2(112f, 32f) },
            WidthMeters = new[] { 40f, 40f },
            DepthMeters = new[] { 2f, 2f },
            FlowAccumulation = 100f,
            HasOceanMouth = false
        };

        private static LakeRecord CreateLake() => new()
        {
            StableId = LakeStableId,
            CenterXZ = new Vector2(350f, 20f),
            SurfaceWorldY = LakeSurfaceWorldY,
            AreaMetersSq = 10000f,
            MaxDepthMeters = 4f,
            BoundsXZ = new Rect(300f, 0f, 100f, 40f),
            OutlineXZ = new[]
            {
                new Vector2(300f, 0f),
                new Vector2(400f, 0f),
                new Vector2(400f, 40f),
                new Vector2(300f, 40f)
            },
            BasinCellCentersXZ = new[]
            {
                new Vector2(310f, 6f), new Vector2(330f, 6f), new Vector2(350f, 6f), new Vector2(370f, 6f),
                new Vector2(310f, 20f), new Vector2(330f, 20f), new Vector2(350f, 20f), new Vector2(370f, 20f),
                new Vector2(310f, 34f), new Vector2(330f, 34f), new Vector2(350f, 34f), new Vector2(370f, 34f)
            },
            ConnectedRiverSystemStableId = RiverStableId
        };

        /// <summary>Row-major hydrology with a wet surface everywhere, so river surfaces are sampled, not guessed.</summary>
        private static HydrologyPlan CreatePlan(
            RiverPolyline river,
            LakeRecord lake,
            HydrologyProfile profile)
        {
            var plan = new HydrologyPlan(32, 16, 16f, Vector2.zero);
            WorldBuilderTestFixtures.FillHydrologyDefaults(plan);
            for (var i = 0; i < plan.WaterClass.Length; i++)
            {
                plan.WaterClass[i] = WorldWaterClass.River;
                plan.SurfaceWorldY[i] = profile.SeaLevelWorldY;
            }

            plan.ReplaceWaterFeatures(
                river != null ? new[] { river } : System.Array.Empty<RiverPolyline>(),
                lake != null ? new[] { lake } : System.Array.Empty<LakeRecord>());
            return plan;
        }

        private static HydrologyProfile CreateHydrologyProfile()
        {
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("seaLevelWorldY").floatValue = SeaLevelWorldY;
            so.FindProperty("minRiverBedDepthBelowSea").floatValue = 2f;
            so.FindProperty("minLakeBedDepthBelowSea").floatValue = 4f;
            so.FindProperty("maxRiverLandElevationAboveSeaMeters").floatValue = 56f;
            so.FindProperty("carveShoulderWidthMeters").floatValue = 36f;
            so.FindProperty("maxRiverDepthMeters").floatValue = 8f;
            so.FindProperty("lakeMinimumDepthMeters").floatValue = 2f;
            so.FindProperty("minRiverWidthMeters").floatValue = 4f;
            so.FindProperty("hydrologyAlgorithmVersion").intValue = 9;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static int CountDistinctRiverIds(HydrologyPlan plan)
        {
            var ids = new HashSet<ulong>();
            for (var i = 0; i < plan.Rivers.Length; i++)
                ids.Add(plan.Rivers[i].StableId);
            return ids.Count;
        }

        private static int CountChildrenWithPrefix(GameObject folder, string prefix)
        {
            var count = 0;
            var parent = folder.transform;
            for (var i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name.StartsWith(prefix, System.StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static void AssertNoInlandWrappers(GameObject root, string expectedPrefix)
        {
            Assert.IsTrue(
                root.name.StartsWith(expectedPrefix, System.StringComparison.Ordinal),
                $"Generated root '{root.name}' must keep the '{expectedPrefix}' cleanup prefix.");
            AssertNoForbiddenNames(root);
            AssertNoWaterBodyComponent(root);
        }

        private static void AssertNoForbiddenNames(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var name = transforms[i].name;
                Assert.IsFalse(
                    IsForbiddenWrapperName(name),
                    $"Unexpected wrapper '{name}' under {root.name}.");
            }
        }

        private static bool IsForbiddenWrapperName(string name)
        {
            for (var i = 0; i < ForbiddenWrapperNames.Length; i++)
            {
                if (ForbiddenWrapperNames[i] == name)
                    return true;
            }

            return false;
        }

        private static void AssertNoWaterBodyComponent(GameObject root)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;
                Assert.AreNotEqual(
                    "Crest.WaterBody",
                    component.GetType().FullName,
                    $"Inland spline roots must not carry a Crest WaterBody wrapper ({root.name}).");
            }
        }

        /// <summary>Deterministic-rebuild assertion: identical root name and identical per-point data.</summary>
        private static void AssertSplineDataMatches(GameObject first, GameObject second, int expectedPoints)
        {
            Assert.AreEqual(first.name, second.name);

            var firstPoints = first.GetComponentsInChildren<SplinePoint>(true);
            var secondPoints = second.GetComponentsInChildren<SplinePoint>(true);
            Assert.AreEqual(expectedPoints, firstPoints.Length, "One SplinePoint per footprint control point.");
            Assert.AreEqual(firstPoints.Length, secondPoints.Length);

            for (var i = 0; i < firstPoints.Length; i++)
                AssertSplinePointMatches(firstPoints[i], secondPoints[i], i);
        }

        private static void AssertSplinePointMatches(SplinePoint first, SplinePoint second, int index)
        {
            Assert.AreEqual("SplinePoint", first.name);
            Assert.LessOrEqual(
                (first.transform.position - second.transform.position).magnitude,
                1e-4f,
                $"Spline point {index} position is not deterministic across rebuilds.");
            Assert.AreEqual(
                first.GetComponent<SplinePointData>().RadiusMultiplier,
                second.GetComponent<SplinePointData>().RadiusMultiplier,
                1e-4f,
                $"Spline point {index} radius multiplier drifted.");
            Assert.AreEqual(
                first.GetComponent<SplinePointDataFlow>().FlowVelocity,
                second.GetComponent<SplinePointDataFlow>().FlowVelocity,
                1e-4f,
                $"Spline point {index} flow velocity drifted.");
            Assert.AreEqual(
                first.GetComponent<SplinePointDataWaves>().Weight,
                second.GetComponent<SplinePointDataWaves>().Weight,
                1e-4f,
                $"Spline point {index} wave weight drifted.");
        }

        private static void DestroyAll(params Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                    continue;
                Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
#endif
