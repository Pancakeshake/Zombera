#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>Shared EditMode fixtures for WorldBuilder coastal / biome tests.</summary>
    internal static class WorldBuilderTestFixtures
    {
        public static WorldBiomePalette CreatePalette()
        {
            var palette = ScriptableObject.CreateInstance<WorldBiomePalette>();
            palette.ApplyProgrammaticDefaults();
            return palette;
        }

        public static LandformProfile CreateLandformProfile(
            float edgeDepth,
            float beachWidth = 180f,
            float barrierPeak = 220f,
            float offshore = 110f,
            float shelf = 70f)
        {
            var profile = ScriptableObject.CreateInstance<LandformProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("edgeBarrierDepthMeters").floatValue = edgeDepth;
            so.FindProperty("edgeBarrierPeakMeters").floatValue = barrierPeak;
            so.FindProperty("edgeBarrierFalloffMeters").floatValue = 200f;
            so.FindProperty("oceanBeachWidthMeters").floatValue = beachWidth;
            so.FindProperty("oceanOffshoreWidthMeters").floatValue = offshore;
            so.FindProperty("oceanShoreShelfWidthMeters").floatValue = shelf;
            so.FindProperty("forceOceanOnAllEdges").boolValue = true;
            so.FindProperty("interiorMountainPaintMinMask").floatValue = 0.55f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>Mirrors LandformProfile.asset coastal / barrier values used in production.</summary>
        public static LandformProfile CreateAssetLikeLandformProfile() =>
            CreateLandformProfile(
                edgeDepth: 1100f,
                beachWidth: 90f,
                barrierPeak: 540f,
                offshore: 110f,
                shelf: 70f);

        public static LandformField CreateFlatLandforms(
            int width,
            int height,
            float cellSize,
            float heightMeters,
            Vector2? origin = null)
        {
            var field = new LandformField(width, height, cellSize, origin ?? Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = heightMeters;
            return field;
        }

        public static BiomeField CreateMinimalBiomeField(int width, int height)
        {
            var biome = new BiomeField(
                width,
                height,
                new[] { "Ocean", "Shore", "Plains" });
            for (var i = 0; i < width * height; i++)
            {
                biome.DominantBiomeIndex[i] = 2;
                biome.Weights[biome.WeightIndex(i, 0)] = 0f;
                biome.Weights[biome.WeightIndex(i, 1)] = 0.2f;
                biome.Weights[biome.WeightIndex(i, 2)] = 0.8f;
                biome.Moisture[i] = 0.4f;
                biome.Slope[i] = 5f;
            }

            return biome;
        }

        public static void FillHydrologyDefaults(HydrologyPlan hydrology)
        {
            for (var i = 0; i < hydrology.WaterClass.Length; i++)
            {
                hydrology.WaterClass[i] = WorldWaterClass.None;
                hydrology.DistanceToWaterMeters[i] = 9999f;
                hydrology.FlowAccumulation[i] = 1f;
            }
        }

        public static void FillMoistHydrology(HydrologyPlan hydrology, int centerX, int centerZ)
        {
            FillHydrologyDefaults(hydrology);
            for (var z = 0; z < hydrology.Height; z++)
            {
                for (var x = 0; x < hydrology.Width; x++)
                {
                    var dx = x - centerX;
                    var dz = z - centerZ;
                    var dist = Mathf.Sqrt(dx * dx + dz * dz);
                    var cell = hydrology.Index(x, z);
                    hydrology.DistanceToWaterMeters[cell] = dist * 12f;
                    hydrology.FlowAccumulation[cell] = dist < 2.5f ? 800f : 1f;
                }
            }
        }

        /// <summary>Hydrology profile matching the inland-water carve defaults the tests assert on.</summary>
        public static HydrologyProfile CreateInlandWaterHydrologyProfile(
            float seaLevel = 0f,
            float minRiverBedDepthBelowSea = 2f,
            float minLakeBedDepthBelowSea = 4f,
            float maxRiverLandElevationAboveSeaMeters = 56f,
            float carveShoulderWidthMeters = 36f,
            float maxRiverDepthMeters = 8f)
        {
            var profile = ScriptableObject.CreateInstance<HydrologyProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("seaLevelWorldY").floatValue = seaLevel;
            so.FindProperty("minRiverBedDepthBelowSea").floatValue = minRiverBedDepthBelowSea;
            so.FindProperty("minLakeBedDepthBelowSea").floatValue = minLakeBedDepthBelowSea;
            so.FindProperty("maxRiverLandElevationAboveSeaMeters").floatValue =
                maxRiverLandElevationAboveSeaMeters;
            so.FindProperty("carveShoulderWidthMeters").floatValue = carveShoulderWidthMeters;
            so.FindProperty("maxRiverDepthMeters").floatValue = maxRiverDepthMeters;
            so.FindProperty("lakeMinimumDepthMeters").floatValue = 2f;
            so.FindProperty("hydrologyAlgorithmVersion").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        public static HydrologyPlan CreateWaterPlan(
            int width,
            int height,
            float cellSize,
            RiverPolyline[] rivers = null,
            LakeRecord[] lakes = null)
        {
            var plan = new HydrologyPlan(width, height, cellSize, Vector2.zero, rivers, lakes);
            FillHydrologyDefaults(plan);
            return plan;
        }

        /// <summary>Marks a single cell as river water with a preserved free surface.</summary>
        public static void MarkWetCell(HydrologyPlan plan, int x, int z, float surfaceWorldY)
        {
            var index = plan.Index(x, z);
            plan.WaterClass[index] = WorldWaterClass.River;
            plan.SurfaceWorldY[index] = surfaceWorldY;
            plan.FlowAccumulation[index] = 100f;
        }

        public static RiverPolyline CreateRiver(
            ulong stableId,
            Vector2[] pointsXZ,
            float widthMeters,
            float depthMeters = 2f)
        {
            var widths = new float[pointsXZ.Length];
            var depths = new float[pointsXZ.Length];
            for (var i = 0; i < pointsXZ.Length; i++)
            {
                widths[i] = widthMeters;
                depths[i] = depthMeters;
            }

            return CreateRiver(stableId, pointsXZ, widths, depths);
        }

        public static RiverPolyline CreateRiver(
            ulong stableId,
            Vector2[] pointsXZ,
            float[] widthMeters,
            float[] depthMeters = null) =>
            new()
            {
                StableId = stableId,
                RiverSystemStableId = stableId,
                PointsXZ = pointsXZ,
                WidthMeters = widthMeters,
                DepthMeters = depthMeters ?? System.Array.Empty<float>(),
                FlowAccumulation = 100f
            };

        public static LandformField CreateRidgeLandforms(
            int width,
            int height,
            float cellSize,
            float baseHeight,
            float ridgeHeight)
        {
            var field = new LandformField(width, height, cellSize, Vector2.zero);
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var ridge = (x + z) % 3 == 0;
                    field.WorldHeights[z * width + x] = ridge ? ridgeHeight : baseHeight;
                }
            }

            return field;
        }
    }
}
#endif
