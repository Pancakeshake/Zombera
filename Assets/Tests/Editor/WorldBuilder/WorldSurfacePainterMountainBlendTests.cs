#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldSurfacePainterMountainBlendTests
    {
        // Matches WorldSurfacePainter defaults for ~1200–1400 m realized peaks.
        private const float RockMin = 350f;
        private const float RockFull = 550f;
        private const float SnowMin = 500f;
        private const float SnowFull = 750f;

        private GameObject _gameObject;
        private WorldSurfacePainter _painter;
        private MethodInfo _evaluateMethod;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("MountainBlendTestPainter");
            _painter = _gameObject.AddComponent<WorldSurfacePainter>();
            _evaluateMethod = typeof(WorldSurfacePainter).GetMethod(
                "EvaluateMountainSurfaceWeights",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(_evaluateMethod);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void BelowRockBand_GentleTerrainKeepsMountainLayersEmpty()
        {
            var weights = Evaluate(worldX: 120f, worldZ: 340f, slope: 10f, elevation: 300f);

            Assert.That(weights, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RockBand_BeforeSnowlineIsExposedDarkRock()
        {
            var weights = Evaluate(worldX: 120f, worldZ: 340f, slope: 18f, elevation: 450f);

            Assert.That(weights.x, Is.GreaterThan(0.99f), "CliffDark should own the exposed-rock band.");
            Assert.That(weights.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(weights.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SnowTransition_UsesSnowRockBetweenRockAndDepositedSnow()
        {
            var weights = Evaluate(worldX: 120f, worldZ: 340f, slope: 15f, elevation: 620f);

            Assert.That(weights.y, Is.GreaterThan(weights.x), "SnowRock should overtake exposed rock mid-transition.");
            Assert.That(weights.z, Is.GreaterThan(0f), "Patchy deposited snow should begin above the snowline.");
        }

        [Test]
        public void GentleSummit_IsSnowDominant()
        {
            var weights = Evaluate(worldX: 120f, worldZ: 340f, slope: 8f, elevation: 900f);

            Assert.That(weights.z, Is.GreaterThan(weights.y));
            Assert.That(weights.z, Is.GreaterThan(weights.x));
        }

        [Test]
        public void SteepSummit_KeepsReadableSnowWithRockAccents()
        {
            var weights = Evaluate(worldX: 120f, worldZ: 340f, slope: 50f, elevation: 900f);

            Assert.That(weights.x, Is.GreaterThan(0.03f), "Steep faces must retain some CliffDark accent.");
            Assert.That(weights.z, Is.GreaterThan(0.4f), "Deposited snow must stay readable on steep caps.");
            Assert.That(weights.z, Is.GreaterThan(weights.x), "Snow should outrank bare dark rock on summit faces.");
        }

        [Test]
        public void BelowSnowline_NoNoiseSampleCanIntroduceSnow()
        {
            for (var z = -2; z <= 2; z++)
            {
                for (var x = -2; x <= 2; x++)
                {
                    var weights = Evaluate(x * 173f, z * 211f, slope: 24f, elevation: SnowMin - 0.1f);
                    Assert.That(weights.z, Is.EqualTo(0f).Within(0.0001f));
                }
            }
        }

        [Test]
        public void NoiseAndBandBoundaries_AreDeterministicAndContinuous()
        {
            var first = Evaluate(worldX: 407.25f, worldZ: -91.5f, slope: 22f, elevation: 620f);
            var repeated = Evaluate(worldX: 407.25f, worldZ: -91.5f, slope: 22f, elevation: 620f);
            var beforeSnowline = Evaluate(407.25f, -91.5f, slope: 22f, elevation: SnowMin - 0.1f);
            var afterSnowline = Evaluate(407.25f, -91.5f, slope: 22f, elevation: SnowMin + 0.1f);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(Vector3.Distance(beforeSnowline, afterSnowline), Is.LessThan(0.01f));
        }

        [Test]
        public void SnowLineOffset_IsDeterministicAndWithinConfiguredAmplitude()
        {
            var method = typeof(WorldSurfacePainter).GetMethod(
                "SampleSnowLineOffsetMeters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var first = (float)method.Invoke(_painter, new object[] { 234f, -765f });
            var repeated = (float)method.Invoke(_painter, new object[] { 234f, -765f });

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(Mathf.Abs(first), Is.LessThanOrEqualTo(50.001f));
        }

        private Vector3 Evaluate(float worldX, float worldZ, float slope, float elevation)
        {
            return (Vector3)_evaluateMethod.Invoke(
                _painter,
                new object[]
                {
                    worldX,
                    worldZ,
                    slope,
                    elevation,
                    RockMin,
                    RockFull,
                    SnowMin,
                    SnowFull
                });
        }
    }
}
#endif
