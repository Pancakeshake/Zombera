#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class CityPadCoreFreezeTests
    {
        [Test]
        public void ThermalErode_WithPadFreeze_LeavesCoreFlat()
        {
            var field = new LandformField(48, 48, 16f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = 40f;

            for (var z = 0; z < 48; z++)
            {
                for (var x = 30; x < 48; x++)
                    field.WorldHeights[field.Index(x, z)] = 120f;
            }

            var plateau = Rect.MinMaxRect(80f, 80f, 200f, 200f);
            var pad = new CityFlattenPad(plateau, 40f, falloffMeters: 180f);
            CityPadCoreUtility.StampCores(
                field,
                new List<CityFlattenPad> { pad },
                CreateLandformProfile(),
                hydrology: null,
                seaLevelWorldY: 0f);

            var coreY = field.WorldHeights[field.Index(10, 10)];
            Assert.AreEqual(40f, coreY, 0.05f);

            var profile = CreateLandformProfile();
            ThermalErosionSolver.Apply(field, profile, new List<CityFlattenPad> { pad });

            Assert.AreEqual(coreY, field.WorldHeights[field.Index(10, 10)], 0.05f);
            Assert.AreEqual(coreY, field.WorldHeights[field.Index(8, 8)], 0.05f);
        }

        [Test]
        public void FlatMargin_DefaultsToThirtyMeters()
        {
            Assert.AreEqual(30f, CityPadCoreUtility.FlatMarginMeters(null), 0.01f);
            Assert.AreEqual(30f, CityPadCoreUtility.FlatMarginMeters(CreateLandformProfile()), 0.01f);
        }

        [Test]
        public void StampCoresWithBlend_BuildsNoisyEscarpmentNotCliff()
        {
            var field = new LandformField(64, 64, 16f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = 20f;

            var plateau = Rect.MinMaxRect(200f, 200f, 400f, 400f);
            var pad = new CityFlattenPad(plateau, 80f, falloffMeters: 180f);
            var profile = CreateLandformProfile();
            CityPadCoreUtility.StampCoresWithBlend(
                field,
                new List<CityFlattenPad> { pad },
                profile,
                roads: null,
                hydrology: null,
                seaLevelWorldY: 0f);

            Assert.AreEqual(80f, field.WorldHeights[field.Index(20, 20)], 0.5f);

            // Near rim outside plateau (~416m → cell 26): between pad and natural.
            var near = field.WorldHeights[field.Index(26, 20)];
            Assert.Greater(near, 20f);
            Assert.Less(near, 80f);

            // Far field stays near natural.
            Assert.AreEqual(20f, field.WorldHeights[field.Index(55, 55)], 2f);
        }

        [Test]
        public void TryResolveCorePlateau_UsesArterialFractionPlusMargin()
        {
            var site = new WorldCitySite
            {
                CenterXZ = new Vector2(1000f, 2000f),
                HalfWidthMeters = 200f,
                HalfDepthMeters = 150f
            };
            Assert.IsTrue(CityPadCoreUtility.TryResolveCorePlateau(site, 30f, out var plateau));
            Assert.AreEqual(1000f - 200f * 0.55f - 30f, plateau.xMin, 0.01f);
            Assert.AreEqual(2000f - 150f * 0.55f - 30f, plateau.yMin, 0.01f);
            Assert.AreEqual(1000f + 200f * 0.55f + 30f, plateau.xMax, 0.01f);
            Assert.AreEqual(2000f + 150f * 0.55f + 30f, plateau.yMax, 0.01f);
        }

        private static LandformProfile CreateLandformProfile()
        {
            var profile = ScriptableObject.CreateInstance<LandformProfile>();
            return profile;
        }
    }
}
#endif
