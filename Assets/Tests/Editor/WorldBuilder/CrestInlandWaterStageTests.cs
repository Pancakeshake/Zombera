#if UNITY_EDITOR
using System;
using System.Reflection;
using Crest;
using Crest.Spline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Crest;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class CrestInlandWaterStageTests
    {
        [Test]
        public void BuildWaterSurfaces_IsFullMapOnlyAndDependsOnOcean()
        {
            var descriptor = WorldBuildStageRegistry.CreateDefault()
                .GetDescriptor(WorldBuildStageId.BuildWaterSurfaces);

            CollectionAssert.Contains(descriptor.SupportedScopes, WorldBuildScopeKind.FullMap);
            Assert.AreEqual(1, descriptor.SupportedScopes.Count);
            CollectionAssert.Contains(descriptor.HardPrerequisites, WorldBuildStageId.BuildOceanSurfaces);
        }

        [Test]
        public void Dilate_ZeroRadiusDoesNotExpandMask()
        {
            var field = new LandformField(3, 3, 16f, Vector2.zero);
            var source = new bool[9];
            source[field.Index(1, 1)] = true;
            var result = HydrologyExclusionMask.Dilate(field, source, 0f);

            Assert.AreEqual(1, CountTrue(result));
            Assert.IsTrue(result[field.Index(1, 1)]);
        }

        [Test]
        public void Dilate_ObstacleRadiusExpandsMask()
        {
            var field = new LandformField(3, 3, 16f, Vector2.zero);
            var source = new bool[9];
            source[field.Index(1, 1)] = true;

            var result = HydrologyExclusionMask.Dilate(field, source, 10f);

            Assert.AreEqual(9, CountTrue(result));
        }

        [Test]
        public void AppendWater_EverySerializedSettingChangesFingerprint()
        {
            var baseline = ScriptableObject.CreateInstance<WorldWaterProfile>();
            try
            {
                var baselineFingerprint = ComputeWaterFingerprint(baseline);
                var fields = typeof(WorldWaterProfile).GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic);
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field.GetCustomAttribute<SerializeField>() == null)
                        continue;

                    AssertFieldChangesFingerprint(field, baselineFingerprint);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseline);
            }
        }

        [Test]
        public void RiverBuilder_CreatesDirectAuthoredStackOrderedUpstreamToMouth()
        {
            var parent = new GameObject("Rivers");
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var river = CreateMouthFirstRiver(7);
                var plan = CreateRiverPlan(river);
                var footprint = plan.EnsureFootprintPlan(profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                var root = CrestRiverSplineBuilder.SpawnRiverSystem(
                    parent.transform,
                    feature,
                    river,
                    WorldWaterProfile.CrestSplinePreset.RiverDefault,
                    new CrestRiverSplineBuilder.RiverSplineSettings(),
                    0f);

                Assert.IsNotNull(root);
                Assert.AreEqual($"River_{river.StableId}", root.name, "Roots use the decimal stable id.");
                Assert.AreEqual(parent.transform, root.transform.parent);
                Assert.AreEqual(1, parent.transform.childCount, "No RiverSystem or Centerlines wrapper is expected.");
                Assert.IsNull(root.transform.Find("RiverSystem"));
                Assert.IsNull(root.transform.Find("Centerlines"));
                Assert.IsNull(root.transform.Find("ClipMesh"));
                Assert.IsNull(root.GetComponentInChildren<WaterBody>(true),
                    "River splines must not create a Crest WaterBody.");

                var spline = root.GetComponent<Spline>();
                Assert.IsNotNull(spline);
                Assert.IsFalse(spline._closed);
                Assert.AreEqual(Spline.Offset.Center, spline._offset);
                Assert.AreEqual(25f, spline.Radius, 0.001f);
                Assert.AreEqual(1, spline.Subdivisions);

                var height = root.GetComponent<RegisterHeightInput>();
                Assert.IsNotNull(height);
                Assert.IsTrue(height.OverrideSplineSettings);
                Assert.AreEqual(18.92f, height.Radius, 0.001f);
                Assert.AreEqual(8, height.Subdivisions);
                Assert.IsNotNull(root.GetComponent<RegisterFlowInput>());
                Assert.AreEqual(256, root.GetComponent<ShapeFFT>()._resolution);

                AssertUpstreamToMouth(feature);
                AssertAuthoredPoints(root, feature, 25f, 2f, 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RiverBuilder_HonoursNonDefaultWaterProfile()
        {
            var parent = new GameObject("Rivers");
            var water = ScriptableObject.CreateInstance<WorldWaterProfile>();
            var profile = WorldBuilderTestFixtures.CreateInlandWaterHydrologyProfile();
            try
            {
                var so = new SerializedObject(water);
                so.FindProperty("riverSplineRadius").floatValue = 40f;
                so.FindProperty("riverSplineSubdivisions").intValue = 3;
                so.FindProperty("riverHeightRadius").floatValue = 30f;
                so.FindProperty("riverHeightSubdivisions").intValue = 2;
                so.FindProperty("riverWaveResolution").intValue = 64;
                so.FindProperty("pointFlowVelocity").floatValue = 3.5f;
                so.FindProperty("pointWaveWeight").floatValue = 0.25f;
                so.FindProperty("riverMinimumFlowSpeedMetersPerSecond").floatValue = 1f;
                so.FindProperty("riverMaximumFlowSpeedMetersPerSecond").floatValue = 6f;
                so.FindProperty("lakeSplineRadius").floatValue = 55f;
                so.FindProperty("lakeHeightRadius").floatValue = 44f;
                so.FindProperty("lakeWaveResolution").intValue = 32;
                so.ApplyModifiedPropertiesWithoutUndo();

                var riverPreset = water.RiverPreset;
                Assert.AreEqual(40f, riverPreset.SplineRadius, 0.001f);
                Assert.AreEqual(3, riverPreset.SplineSubdivisions);
                Assert.AreEqual(30f, riverPreset.HeightRadius, 0.001f);
                Assert.AreEqual(2, riverPreset.HeightSubdivisions);
                Assert.AreEqual(64, riverPreset.WaveResolution);
                Assert.AreEqual(3.5f, riverPreset.PointFlowVelocity, 0.001f);
                Assert.AreEqual(55f, water.LakePreset.SplineRadius, 0.001f);
                Assert.AreEqual(44f, water.LakePreset.HeightRadius, 0.001f);
                Assert.AreEqual(32, water.LakePreset.WaveResolution);

                var river = CreateMouthFirstRiver(17);
                var plan = CreateRiverPlan(river);
                var footprint = plan.EnsureFootprintPlan(profile);
                Assert.IsTrue(footprint.TryGetFeature(river.StableId, out var feature));

                var root = CrestRiverSplineBuilder.SpawnRiverSystem(
                    parent.transform,
                    feature,
                    river,
                    riverPreset,
                    new CrestRiverSplineBuilder.RiverSplineSettings(
                        null,
                        water.RiverMinimumFlowSpeedMetersPerSecond,
                        water.RiverMaximumFlowSpeedMetersPerSecond),
                    0f);

                Assert.IsNotNull(root);
                Assert.AreEqual(40f, root.GetComponent<Spline>().Radius, 0.001f);
                Assert.AreEqual(3, root.GetComponent<Spline>().Subdivisions);
                Assert.AreEqual(30f, root.GetComponent<RegisterHeightInput>().Radius, 0.001f);
                Assert.AreEqual(2, root.GetComponent<RegisterHeightInput>().Subdivisions);
                Assert.AreEqual(64, root.GetComponent<ShapeFFT>()._resolution);

                AssertAuthoredPoints(root, feature, 40f, 3.5f, 0.25f);
                var points = root.GetComponentsInChildren<SplinePoint>();
                Assert.AreEqual(
                    10f / 40f,
                    points[points.Length - 1].GetComponent<SplinePointData>().RadiusMultiplier,
                    0.0001f,
                    "The multiplier must be targetWetWidth / authored radius.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(water);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static RiverPolyline CreateMouthFirstRiver(ulong stableId) => new()
        {
            StableId = stableId,
            RiverSystemStableId = stableId,
            Kind = RiverKind.MainStem,
            HasOceanMouth = true,
            OceanMouthXZ = new Vector2(5f, 5f),
            PointsXZ = new[]
            {
                new Vector2(5f, 5f),
                new Vector2(15f, 5f),
                new Vector2(25f, 5f),
                new Vector2(35f, 5f)
            },
            WidthMeters = new[] { 10f, 15f, 20f, 25f }
        };

        private static HydrologyPlan CreateRiverPlan(RiverPolyline river)
        {
            var plan = WorldBuilderTestFixtures.CreateWaterPlan(4, 1, 10f, new[] { river });
            for (var x = 0; x < plan.Width; x++)
                WorldBuilderTestFixtures.MarkWetCell(plan, x, 0, x);
            return plan;
        }

        private static void AssertUpstreamToMouth(InlandWaterFootprintPlan.Feature feature)
        {
            Assert.GreaterOrEqual(feature.Points.Count, 2);
            Assert.AreEqual(new Vector2(35f, 5f), feature.Points[0].CenterXZ);
            Assert.AreEqual(new Vector2(5f, 5f), feature.Points[feature.Points.Count - 1].CenterXZ);
            Assert.AreEqual(0f, feature.Points[feature.Points.Count - 1].SurfaceWorldY, 0.001f);
            for (var i = 1; i < feature.Points.Count; i++)
            {
                Assert.LessOrEqual(
                    feature.Points[i].SurfaceWorldY,
                    feature.Points[i - 1].SurfaceWorldY + 0.001f);
            }
        }

        private static void AssertAuthoredPoints(
            GameObject root,
            InlandWaterFootprintPlan.Feature feature,
            float splineRadius,
            float flowVelocity,
            float waveWeight)
        {
            var points = root.GetComponentsInChildren<SplinePoint>();
            Assert.AreEqual(feature.Points.Count, points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                var point = feature.Points[i];
                Assert.AreEqual(
                    new Vector3(point.CenterXZ.x, point.SurfaceWorldY, point.CenterXZ.y),
                    points[i].transform.position);
                Assert.IsNotNull(points[i].GetComponent<SplinePointData>());
                Assert.AreEqual(
                    InlandWaterFootprintPlan.ResolveRadiusMultiplier(point.TargetWetWidthMeters, splineRadius),
                    points[i].GetComponent<SplinePointData>().RadiusMultiplier,
                    0.0001f);
                Assert.AreEqual(flowVelocity, points[i].GetComponent<SplinePointDataFlow>().FlowVelocity, 0.001f);
                Assert.AreEqual(waveWeight, points[i].GetComponent<SplinePointDataWaves>().Weight, 0.001f);
            }
        }

        private static int CountTrue(bool[] values)
        {
            var count = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i]) count++;
            }
            return count;
        }

        private static void AssertFieldChangesFingerprint(FieldInfo field, ulong baselineFingerprint)
        {
            var profile = ScriptableObject.CreateInstance<WorldWaterProfile>();
            UnityEngine.Object assignedObject = null;
            try
            {
                assignedObject = AssignDifferentValue(profile, field);
                Assert.AreNotEqual(
                    baselineFingerprint,
                    ComputeWaterFingerprint(profile),
                    $"WorldProfileFingerprints.AppendWater does not include '{field.Name}'.");
            }
            finally
            {
                if (assignedObject != null)
                    UnityEngine.Object.DestroyImmediate(assignedObject);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static UnityEngine.Object AssignDifferentValue(WorldWaterProfile profile, FieldInfo field)
        {
            if (field.FieldType == typeof(bool))
            {
                field.SetValue(profile, !(bool)field.GetValue(profile));
                return null;
            }

            if (field.FieldType == typeof(float))
            {
                field.SetValue(profile, (float)field.GetValue(profile) + 0.123f);
                return null;
            }

            if (field.FieldType == typeof(int))
            {
                field.SetValue(profile, (int)field.GetValue(profile) + 1);
                return null;
            }

            if (field.FieldType == typeof(LayerMask))
            {
                var current = (LayerMask)field.GetValue(profile);
                field.SetValue(profile, (LayerMask)(current.value ^ (1 << 12)));
                return null;
            }

            var assignedObject = CreateReferenceValue(field);
            field.SetValue(profile, assignedObject);
            return assignedObject;
        }

        private static UnityEngine.Object CreateReferenceValue(FieldInfo field)
        {
            if (field.FieldType == typeof(GameObject))
                return new GameObject($"Fingerprint_{field.Name}");
            if (field.FieldType == typeof(Material))
            {
                var shader = Shader.Find("Hidden/InternalErrorShader");
                Assert.IsNotNull(shader, "Unity's internal error shader is required by this test.");
                return new Material(shader) { name = $"Fingerprint_{field.Name}" };
            }

            if (typeof(ScriptableObject).IsAssignableFrom(field.FieldType))
            {
                var asset = ScriptableObject.CreateInstance<WorldWaterProfile>();
                asset.name = $"Fingerprint_{field.Name}";
                return asset;
            }
            if (field.FieldType == typeof(UnityEngine.Object))
                return new Texture2D(1, 1) { name = $"Fingerprint_{field.Name}" };

            Assert.Fail($"Add a fingerprint test value for serialized field '{field.Name}' ({field.FieldType}).");
            return null;
        }

        private static ulong ComputeWaterFingerprint(WorldWaterProfile profile)
        {
            var hasher = new StableHash64(0x5741544552544553UL);
            WorldProfileFingerprints.AppendWater(ref hasher, profile);
            return hasher.Finalize();
        }
    }
}
#endif
