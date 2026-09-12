#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldSurfacePaletteContractTests
    {
        [Test]
        public void DefaultMappings_MatchCanonicalMicroSplatIndices()
        {
            var palette = ScriptableObject.CreateInstance<WorldSurfacePalette>();
            Assert.IsTrue(palette.Validate(out var error), error);

            Assert.IsTrue(palette.TryGetLayerIndex("Sand01", out var sand01));
            Assert.AreEqual(2, sand01);

            Assert.IsTrue(palette.TryGetLayerIndex("Sand", out var sand));
            Assert.AreEqual(6, sand);

            Assert.IsTrue(palette.TryGetLayerIndex("CliffPink", out var cliffPink));
            Assert.AreEqual(9, cliffPink);

            Assert.IsTrue(palette.TryGetLayerIndex("BlackSand", out var blackSand));
            Assert.AreEqual(21, blackSand);

            Assert.IsTrue(palette.TryGetLayerIndex("WetRock", out var wetRock));
            Assert.AreEqual(28, wetRock);

            Assert.IsTrue(palette.TryGetLayerIndex("RiverSand", out var riverSand));
            Assert.AreEqual(29, riverSand);

            Assert.IsTrue(palette.TryGetLayerIndex("WetSand", out var wetSand));
            Assert.AreEqual(31, wetSand);

            Assert.IsTrue(palette.TryGetLayerIndex("SnowRock", out var snowRock));
            Assert.AreEqual(24, snowRock);

            Assert.IsTrue(palette.TryGetLayerIndex("CliffBright", out var cliffBright));
            Assert.AreEqual(5, cliffBright);

            Assert.IsTrue(palette.TryGetLayerIndex("CliffDark", out var cliffDark));
            Assert.AreEqual(7, cliffDark);
        }

        [Test]
        public void OceanBiomeDefaults_DoNotUsePebbleSand01()
        {
            var palette = ScriptableObject.CreateInstance<WorldBiomePalette>();
            palette.ApplyProgrammaticDefaults();
            Assert.IsTrue(palette.TryGetBiome("Ocean", out var ocean));

            var hasSand01 = false;
            var hasSand = false;
            var hasWetSand = false;
            for (var i = 0; i < ocean.SurfaceWeights.Count; i++)
            {
                var semantic = ocean.SurfaceWeights[i].SurfaceSemantic;
                if (semantic == "Sand01") hasSand01 = true;
                if (semantic == "Sand") hasSand = true;
                if (semantic == "WetSand") hasWetSand = true;
            }

            Assert.IsFalse(hasSand01, "Ocean should not paint lake pebbles (Sand01).");
            Assert.IsTrue(hasSand);
            Assert.IsTrue(hasWetSand);
        }
    }
}
#endif
