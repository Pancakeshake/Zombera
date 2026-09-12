#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class AlphamapSoftBlendUtilityTests
    {
        [Test]
        public void FillStrideGapsBilinear_InterpolatesMidpointBetweenPaintedSamples()
        {
            const int width = 5;
            const int height = 5;
            const int layers = 2;
            const int stride = 4;
            var map = new float[height, width, layers];
            map[0, 0, 0] = 1f;
            map[0, 4, 0] = 0f;
            map[0, 0, 1] = 0f;
            map[0, 4, 1] = 1f;
            map[4, 0, 0] = 1f;
            map[4, 4, 0] = 0f;
            map[4, 0, 1] = 0f;
            map[4, 4, 1] = 1f;

            AlphamapSoftBlendUtility.FillStrideGapsBilinear(map, width, height, layers, stride);

            Assert.AreEqual(0.5f, map[0, 2, 0], 0.05f);
            Assert.AreEqual(0.5f, map[0, 2, 1], 0.05f);
        }

        [Test]
        public void SoftenNaturalEdges_MixesHardLayerBoundary()
        {
            const int width = 8;
            const int height = 1;
            const int layers = 2;
            var map = new float[height, width, layers];
            var scratch = new float[height, width, layers];
            for (var x = 0; x < width; x++)
            {
                var left = x < 4;
                map[0, x, 0] = left ? 1f : 0f;
                map[0, x, 1] = left ? 0f : 1f;
            }

            AlphamapSoftBlendUtility.SoftenNaturalEdges(
                map, scratch, width, height, layers, radius: 2, strength: 1f);

            Assert.Greater(map[0, 3, 1], 0.05f);
            Assert.Greater(map[0, 4, 0], 0.05f);
            Assert.AreEqual(1f, map[0, 3, 0] + map[0, 3, 1], 0.001f);
            Assert.AreEqual(1f, map[0, 4, 0] + map[0, 4, 1], 0.001f);
        }
        [Test]
        public void UpscaleBilinear_InterpolatesBetweenCoarseSamples()
        {
            const int srcW = 2;
            const int srcH = 2;
            const int dstW = 4;
            const int dstH = 4;
            const int layers = 2;
            var src = new float[srcH, srcW, layers];
            var dst = new float[dstH, dstW, layers];
            src[0, 0, 0] = 1f;
            src[0, 1, 0] = 0f;
            src[1, 0, 0] = 0f;
            src[1, 1, 0] = 1f;
            src[0, 0, 1] = 0f;
            src[0, 1, 1] = 1f;
            src[1, 0, 1] = 1f;
            src[1, 1, 1] = 0f;

            AlphamapSoftBlendUtility.UpscaleBilinear(src, srcW, srcH, dst, dstW, dstH, layers);

            // Center-sample bilinear at dst(1,1) → srcF=0.25 → ~0.625 / ~0.375.
            Assert.AreEqual(0.625f, dst[1, 1, 0], 0.08f);
            Assert.AreEqual(0.375f, dst[1, 1, 1], 0.08f);
            Assert.AreEqual(1f, dst[1, 1, 0] + dst[1, 1, 1], 0.001f);
        }

        [Test]
        public void ComputeCoarsePaintDimensions_MatchesLandformCellSize()
        {
            WorldSurfacePainter.ComputeCoarsePaintDimensions(1000f, 1000f, 16f, 0, out var w, out var h);
            Assert.AreEqual(63, w);
            Assert.AreEqual(63, h);
        }

        [Test]
        public void ComputeCoarsePaintDimensions_UsesMaxResolutionWhenSet()
        {
            WorldSurfacePainter.ComputeCoarsePaintDimensions(1000f, 1000f, 16f, 32, out var w, out var h);
            Assert.AreEqual(32, w);
            Assert.AreEqual(32, h);
        }

        [Test]
        public void SoftRadiusForGrid_ScalesDownOnCoarseResolution()
        {
            Assert.AreEqual(1, WorldSurfacePainter.SoftRadiusForGrid(128, 512, 2));
            Assert.AreEqual(1, WorldSurfacePainter.SoftRadiusForGrid(256, 512, 2));
            Assert.AreEqual(2, WorldSurfacePainter.SoftRadiusForGrid(512, 512, 2));
            Assert.AreEqual(1, WorldSurfacePainter.SoftRadiusForGrid(32, 512, 2));
        }

        [Test]
        public void UpscaleBilinear_SkipRenormalize_PreservesNormalizedBilinearSum()
        {
            const int srcW = 2;
            const int srcH = 2;
            const int dstW = 4;
            const int dstH = 4;
            const int layers = 2;
            var src = new float[srcH, srcW, layers];
            var dst = new float[dstH, dstW, layers];
            src[0, 0, 0] = 1f;
            src[0, 1, 0] = 0f;
            src[1, 0, 0] = 0f;
            src[1, 1, 0] = 1f;
            src[0, 0, 1] = 0f;
            src[0, 1, 1] = 1f;
            src[1, 0, 1] = 1f;
            src[1, 1, 1] = 0f;

            AlphamapSoftBlendUtility.UpscaleBilinearPreserveWeights(
                src, srcW, srcH, dst, dstW, dstH, layers);

            Assert.AreEqual(1f, dst[1, 1, 0] + dst[1, 1, 1], 0.001f);
        }

        [Test]
        public void UpscaleBlockReplicate_FillsDestinationBlocks()
        {
            const int srcW = 2;
            const int srcH = 2;
            const int dstW = 4;
            const int dstH = 4;
            const int layers = 2;
            var src = new float[srcH, srcW, layers];
            var dst = new float[dstH, dstW, layers];
            src[0, 0, 0] = 1f;
            src[0, 1, 0] = 0f;
            src[1, 0, 0] = 0f;
            src[1, 1, 0] = 1f;
            src[0, 0, 1] = 0f;
            src[0, 1, 1] = 1f;
            src[1, 0, 1] = 1f;
            src[1, 1, 1] = 0f;

            AlphamapSoftBlendUtility.UpscaleBlockReplicate(src, srcW, srcH, dst, dstW, dstH, layers);

            Assert.AreEqual(1f, dst[0, 0, 0], 0.001f);
            // src[1,1] → dst block [2..4): layer0=1, layer1=0
            Assert.AreEqual(1f, dst[2, 2, 0], 0.001f);
            Assert.AreEqual(0f, dst[2, 2, 1], 0.001f);
        }

        [Test]
        public void SoftenNaturalEdgesPasses_TwoPasses_KeepsNormalizedWeights()
        {
            const int width = 16;
            const int height = 16;
            const int layers = 2;
            var map = new float[height, width, layers];
            var scratch = new float[height, width, layers];
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var left = x < width / 2;
                    map[z, x, 0] = left ? 1f : 0f;
                    map[z, x, 1] = left ? 0f : 1f;
                }
            }

            AlphamapSoftBlendUtility.SoftenNaturalEdgesPasses(
                new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                    map, scratch, width, height, layers, radius: 3, strength: 0.92f),
                passes: 2);

            Assert.AreEqual(1f, map[8, 7, 0] + map[8, 7, 1], 0.002f);
            Assert.AreEqual(1f, map[8, 8, 0] + map[8, 8, 1], 0.002f);
            Assert.Greater(map[8, 7, 1], 0.02f);
            Assert.Greater(map[8, 8, 0], 0.02f);
        }

        [Test]
        public void SoftenNaturalEdgesPassesHalfRes_MixesHardBoundary()
        {
            const int width = 32;
            const int height = 32;
            const int layers = 2;
            var map = new float[height, width, layers];
            var scratch = new float[height, width, layers];
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var left = x < width / 2;
                    map[z, x, 0] = left ? 1f : 0f;
                    map[z, x, 1] = left ? 0f : 1f;
                }
            }

            AlphamapSoftBlendUtility.SoftenNaturalEdgesPassesHalfRes(
                new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                    map, scratch, width, height, layers, radius: 3, strength: 0.92f),
                passes: 2);

            Assert.AreEqual(1f, map[16, 15, 0] + map[16, 15, 1], 0.01f);
            Assert.Greater(map[16, 15, 1], 0.02f);
            Assert.Greater(map[16, 16, 0], 0.02f);
        }

        [Test]
        public void SoftenNaturalEdgesPassesDownsampled_QuarterRes_MixesHardBoundary()
        {
            const int width = 64;
            const int height = 64;
            const int layers = 2;
            var map = new float[height, width, layers];
            var scratch = new float[height, width, layers];
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var left = x < width / 2;
                    map[z, x, 0] = left ? 1f : 0f;
                    map[z, x, 1] = left ? 0f : 1f;
                }
            }

            AlphamapSoftBlendUtility.SoftenNaturalEdgesPassesDownsampled(
                new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                    map, scratch, width, height, layers, radius: 3, strength: 0.92f),
                passes: 2,
                divisor: 4);

            Assert.AreEqual(1f, map[32, 31, 0] + map[32, 31, 1], 0.02f);
            Assert.Greater(map[32, 31, 1], 0.02f);
            Assert.Greater(map[32, 32, 0], 0.02f);
        }
    }
}
#endif
