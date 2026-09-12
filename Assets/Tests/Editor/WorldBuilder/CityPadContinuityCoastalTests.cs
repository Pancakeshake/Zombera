#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class CityPadContinuityCoastalTests
    {
        [Test]
        public void FeatherClamp_AtOuterFalloff_ReturnsNatural()
        {
            var natural = 100f;
            var clamped = 40f;
            var result = CityPadLandformFlattener.FeatherClamp(natural, clamped, distOutside: 100f, falloff: 100f);
            Assert.AreEqual(natural, result, 0.01f);
        }

        [Test]
        public void FeatherClamp_NearPlateau_ReturnsClamped()
        {
            var natural = 100f;
            var clamped = 40f;
            var result = CityPadLandformFlattener.FeatherClamp(natural, clamped, distOutside: 10f, falloff: 100f);
            Assert.AreEqual(clamped, result, 0.01f);
        }

        [Test]
        public void CoastalFieldGate_IgnoresSeawardRelief()
        {
            var field = new LandformField(64, 64, 16f, Vector2.zero);
            for (var i = 0; i < field.WorldHeights.Length; i++)
                field.WorldHeights[i] = 20f;

            // Huge cliff only on +Z (seaward) side of pad.
            for (var z = 40; z < 64; z++)
            {
                for (var x = 0; x < 64; x++)
                    field.WorldHeights[field.Index(x, z)] = 200f;
            }

            var plateau = Rect.MinMaxRect(200f, 200f, 400f, 400f);
            var profile = ScriptableObject.CreateInstance<LandformProfile>();
            try
            {
                Assert.IsFalse(
                    CityPadContinuity.PassesFieldContinuityGate(
                        new CityPadContinuity.FieldContinuityGateArgs(
                            new CityPadContinuity.FieldContinuityCore(
                                field, plateau, 20f, profile, null, 8f)),
                        out _),
                    "inland-all annulus should reject tall seaward cliff");

                Assert.IsTrue(
                    CityPadContinuity.PassesFieldContinuityGate(
                        new CityPadContinuity.FieldContinuityGateArgs(
                            new CityPadContinuity.FieldContinuityCore(
                                field, plateau, 20f, profile, null, 8f),
                            isCoastal: true,
                            seawardNormalXZ: Vector2.up),
                        out _),
                    "coastal inland-only gate should ignore seaward cliff");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IsSeawardBearing_MatchesDotProduct()
        {
            Assert.IsTrue(CityCoastalPadUtility.IsSeawardBearing(Vector2.up, Vector2.up));
            Assert.IsFalse(CityCoastalPadUtility.IsSeawardBearing(Vector2.down, Vector2.up));
        }
    }
}
#endif
