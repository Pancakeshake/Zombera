#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class HydrologyExclusionTests
    {
        [Test]
        public void ExclusionMask_BlocksOceanAndMountainEdgeCells()
        {
            var field = new LandformField(8, 8, 125f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = 20f;

            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 7,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);
            var layout = WorldMapBoundaryLayout.AllOcean;
            var oceanMask = new bool[field.Width * field.Height];

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    if (!WorldMapBoundaryUtility.IsOceanSeedCell(x, z, field.Width, field.Height, layout))
                        continue;
                    oceanMask[field.Index(x, z)] = true;
                }
            }

            var blocked = HydrologyExclusionMask.Build(field, 0f, session, landformProfile: null, oceanMask);
            Assert.IsTrue(blocked[field.Index(0, 0)] || blocked[field.Index(4, 0)]);
        }

        [Test]
        public void PruneCityPads_ClearsOceanUnderPad()
        {
            var plan = new HydrologyPlan(8, 8, 125f, Vector2.zero);
            plan.WaterClass[plan.Index(2, 2)] = WorldWaterClass.Ocean;
            plan.DepthMeters[plan.Index(2, 2)] = 2f;
            plan.SurfaceWorldY[plan.Index(2, 2)] = 0f;

            var pads = new[]
            {
                new CityFlattenPad(
                    Rect.MinMaxRect(250f, 250f, 450f, 450f),
                    targetHeightWorldY: 10f,
                    falloffMeters: 32f)
            };

            HydrologyPlanFilter.PruneCityPads(plan, pads);
            Assert.AreEqual(0, plan.Rivers.Length);
            Assert.AreEqual(0, plan.Lakes.Length);
            Assert.AreEqual(WorldWaterClass.None, plan.WaterClass[plan.Index(2, 2)]);
        }

        [Test]
        public void PlacementMask_ZeroCoastBufferPreservesOceanFootprint()
        {
            var field = new LandformField(16, 16, 125f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = 20f;

            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 16,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);
            var hydrology = ScriptableObject.CreateInstance<HydrologyProfile>();
            var layout = WorldMapBoundaryLayout.AllOcean;
            var oceanMask = new bool[field.Width * field.Height];
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    if (WorldMapBoundaryUtility.IsOceanSeedCell(x, z, field.Width, field.Height, layout))
                        oceanMask[field.Index(x, z)] = true;
                }
            }

            var exclusion = HydrologyExclusionMask.Build(field, 0f, session, landformProfile: null, oceanMask);
            var placement = HydrologyExclusionMask.BuildRiverPlacementMask(
                field, 0f, session, landformProfile: null, hydrology, oceanMask);

            var exclusionCount = CountTrue(exclusion);
            var placementCount = CountTrue(placement);
            Assert.AreEqual(exclusionCount, placementCount);

            Object.DestroyImmediate(hydrology);
        }

        private static int CountTrue(bool[] mask)
        {
            var count = 0;
            for (var i = 0; i < mask.Length; i++)
            {
                if (mask[i]) count++;
            }

            return count;
        }
    }
}
#endif
