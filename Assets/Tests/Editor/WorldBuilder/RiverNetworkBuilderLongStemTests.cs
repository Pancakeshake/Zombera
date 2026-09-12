#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class RiverNetworkBuilderLongStemTests
    {
        [Test]
        public void Build_CoastalLowlandStrip_ProducesMultiKmOceanMouthStem()
        {
            const int width = 256;
            const int height = 24;
            const float cell = 16f;
            var field = WorldBuilderTestFixtures.CreateFlatLandforms(width, height, cell, 8f);
            var oceanMask = new bool[width * height];
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = field.Index(x, z);
                    if (x == 0)
                    {
                        oceanMask[i] = true;
                        field.WorldHeights[i] = -2f;
                    }
                    else
                    {
                        // Gentle rise inland, stays under sea+120 low-land ceiling.
                        field.WorldHeights[i] = 4f + x * 0.12f;
                    }
                }
            }

            var accumulation = new float[width * height];
            var flowTo = new int[width * height];
            FlowAccumulationSolver.Compute(
                field.WorldHeights,
                width,
                height,
                accumulation,
                flowTo,
                oceanMask);

            var profile = CreateProfile();
            var rivers = RiverNetworkBuilder.Build(
                field,
                accumulation,
                flowTo,
                exclusionMask: null,
                oceanMask,
                lakeMask: null,
                profile);

            Assert.Greater(rivers.Length, 0, "Expected at least one coast-origin river.");
            var bestLength = 0f;
            var foundMouth = false;
            for (var i = 0; i < rivers.Length; i++)
            {
                var river = rivers[i];
                if (river == null || river.Kind != RiverKind.MainStem)
                    continue;
                if (!river.HasOceanMouth)
                    continue;
                foundMouth = true;
                var length = ApproximatePolylineLength(river.PointsXZ);
                if (length > bestLength)
                    bestLength = length;
            }

            Assert.IsTrue(foundMouth, "Expected a main stem with HasOceanMouth.");
            Assert.GreaterOrEqual(
                bestLength,
                profile.PrimaryRiverMinimumLengthMeters * 0.9f,
                $"Expected ~3km inland stem, got {bestLength:0}m.");

            Object.DestroyImmediate(profile);
        }

        private static float ApproximatePolylineLength(Vector2[] points)
        {
            if (points == null || points.Length < 2)
                return 0f;
            var length = 0f;
            for (var i = 1; i < points.Length; i++)
                length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        private static HydrologyProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("seaLevelWorldY").floatValue = 0f;
            so.FindProperty("cellSizeMeters").floatValue = 16f;
            so.FindProperty("maxRiverSystems").intValue = 2;
            so.FindProperty("primaryRiverMinimumLengthMeters").floatValue = 3000f;
            so.FindProperty("maxRiverLandElevationAboveSeaMeters").floatValue = 120f;
            so.FindProperty("valleyFlowPreference").floatValue = 0.7f;
            so.FindProperty("primaryOutletSeparationMeters").floatValue = 400f;
            so.FindProperty("outletClusterRadiusMeters").floatValue = 200f;
            so.FindProperty("hydrologyAlgorithmVersion").intValue = 9;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }
    }
}
#endif
