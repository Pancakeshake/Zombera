#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class LandformProfileCanonicalDefaultsTests
    {
        private const float CanonicalInteriorMountainPeakMeters = 1700f;
        private const string SharedProfilePath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/LandformProfile.asset";

        [Test]
        public void NewProfile_UsesCanonicalInteriorMountainPeak()
        {
            var profile = ScriptableObject.CreateInstance<LandformProfile>();

            try
            {
                Assert.AreEqual(
                    CanonicalInteriorMountainPeakMeters,
                    profile.InteriorMountainPeakMeters);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SharedProfile_UsesCanonicalInteriorMountainPeak()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LandformProfile>(SharedProfilePath);

            Assert.That(profile, Is.Not.Null, $"Missing shared LandformProfile at '{SharedProfilePath}'.");
            Assert.AreEqual(
                CanonicalInteriorMountainPeakMeters,
                profile.InteriorMountainPeakMeters);
        }
    }
}
#endif
