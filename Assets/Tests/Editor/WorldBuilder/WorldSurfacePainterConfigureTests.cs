#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldSurfacePainterConfigureTests
    {
        private GameObject _go;
        private WorldSurfacePainter _painter;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ConfigurePaintTestPainter");
            _painter = _go.AddComponent<WorldSurfacePainter>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void FastMode_UsesBlockPathContract()
        {
            _painter.ConfigurePaintIteration(SurfacePaintQualityMode.Fast);

            Assert.That(_painter.ConfiguredPaintMode, Is.EqualTo(SurfacePaintQualityMode.Fast));
            Assert.That(_painter.UseCellResolutionPaint, Is.True);
            Assert.That(_painter.CoarsePaintResolution, Is.EqualTo(32));
            Assert.That(_painter.SkipAlphamapSoften, Is.True);
            Assert.That(_painter.ConfiguredFastPaintSampling, Is.True);
        }

        [Test]
        public void BalancedMode_UsesBilinearSoftContract()
        {
            _painter.ConfigurePaintIteration(SurfacePaintQualityMode.Balanced);

            Assert.That(_painter.ConfiguredPaintMode, Is.EqualTo(SurfacePaintQualityMode.Balanced));
            Assert.That(_painter.UseCellResolutionPaint, Is.True);
            Assert.That(_painter.CoarsePaintResolution, Is.EqualTo(128));
            Assert.That(_painter.SkipAlphamapSoften, Is.False);
            Assert.That(_painter.ConfiguredFastPaintSampling, Is.False);
            Assert.That(
                WorldSurfacePainter.SoftRadiusForGrid(128, 512, _painter.EffectiveCoarseSoftRadius),
                Is.EqualTo(1));
        }

        [Test]
        public void QualityMode_UsesCell256SoftBeforeUpscaleContract()
        {
            _painter.ConfigurePaintIteration(SurfacePaintQualityMode.Quality);

            Assert.That(_painter.ConfiguredPaintMode, Is.EqualTo(SurfacePaintQualityMode.Quality));
            Assert.That(_painter.UseCellResolutionPaint, Is.True);
            Assert.That(_painter.CoarsePaintResolution, Is.EqualTo(256));
            Assert.That(_painter.SkipAlphamapSoften, Is.False);
            Assert.That(_painter.ConfiguredFastPaintSampling, Is.False);
            Assert.That(_painter.PaintTexelStride, Is.EqualTo(1));
            Assert.That(_painter.EffectivePostSoftRadius, Is.GreaterThanOrEqualTo(3));
            Assert.That(
                Mathf.Max(2, WorldSurfacePainter.SoftRadiusForGrid(256, 512, _painter.EffectivePostSoftRadius)),
                Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void SoftRadiusForGrid_ScalesCoarseBlurDown()
        {
            Assert.That(WorldSurfacePainter.SoftRadiusForGrid(128, 512, 4), Is.EqualTo(1));
            Assert.That(WorldSurfacePainter.SoftRadiusForGrid(256, 512, 2), Is.EqualTo(1));
            Assert.That(WorldSurfacePainter.SoftRadiusForGrid(512, 512, 3), Is.EqualTo(3));
        }

        [Test]
        public void BudgetGate_AcceptsBalancedWhenUnderFractionOfQuality()
        {
            Assert.That(
                SurfacePaintBudgetGate.ShouldPreferBalanced(
                    balancedMedianMs: 40_000,
                    qualityMedianMs: 90_000,
                    qualityIterationCapMs: 60_000),
                Is.True);
            Assert.That(
                SurfacePaintBudgetGate.ShouldPreferBalanced(
                    balancedMedianMs: 80_000,
                    qualityMedianMs: 90_000,
                    qualityIterationCapMs: 60_000),
                Is.False);
        }

        [Test]
        public void FastSurfacePaintBoolBridge_MapsToFastOrQuality()
        {
            var options = new WorldBuildRunOptions { FastSurfacePaint = true };
            Assert.That(options.SurfacePaintQuality, Is.EqualTo(SurfacePaintQualityMode.Fast));
            options.FastSurfacePaint = false;
            Assert.That(options.SurfacePaintQuality, Is.EqualTo(SurfacePaintQualityMode.Quality));
        }
    }
}
#endif
