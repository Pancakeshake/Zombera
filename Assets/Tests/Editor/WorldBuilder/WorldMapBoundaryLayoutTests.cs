#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldMapBoundaryLayoutTests
    {
        [Test]
        public void Resolve_ForceOceanOnAllEdges_ReturnsAllOcean()
        {
            var session = CreateSession(seed: 1);
            var landforms = WorldBuilderTestFixtures.CreateLandformProfile(edgeDepth: 1000f);

            var layout = WorldMapBoundaryLayout.Resolve(session, landforms);

            Assert.AreEqual(WorldMapBoundaryKind.Ocean, layout.West);
            Assert.AreEqual(WorldMapBoundaryKind.Ocean, layout.East);
            Assert.AreEqual(WorldMapBoundaryKind.Ocean, layout.South);
            Assert.AreEqual(WorldMapBoundaryKind.Ocean, layout.North);
            Object.DestroyImmediate(landforms);
        }

        [Test]
        public void Resolve_WithoutLandlocked_ForcesAtLeastOneOceanEdge()
        {
            var session = CreateSession(seed: 42);
            var landforms = ScriptableObject.CreateInstance<LandformProfile>();
            var so = new SerializedObject(landforms);
            so.FindProperty("forceOceanOnAllEdges").boolValue = false;
            so.FindProperty("oceanEdgeChance").floatValue = 0f;
            so.FindProperty("allowLandlockedMaps").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var layout = WorldMapBoundaryLayout.Resolve(session, landforms);

            Assert.GreaterOrEqual(layout.OceanEdgeCount, 1);
            Object.DestroyImmediate(landforms);
        }

        [Test]
        public void Resolve_AllowLandlocked_WithZeroChance_CanBeAllMountains()
        {
            var session = CreateSession(seed: 7);
            var landforms = ScriptableObject.CreateInstance<LandformProfile>();
            var so = new SerializedObject(landforms);
            so.FindProperty("forceOceanOnAllEdges").boolValue = false;
            so.FindProperty("oceanEdgeChance").floatValue = 0f;
            so.FindProperty("allowLandlockedMaps").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var layout = WorldMapBoundaryLayout.Resolve(session, landforms);

            Assert.AreEqual(0, layout.OceanEdgeCount);
            Object.DestroyImmediate(landforms);
        }

        [Test]
        public void Resolve_SameSeed_IsDeterministic()
        {
            var session = CreateSession(seed: 99);
            var landforms = ScriptableObject.CreateInstance<LandformProfile>();
            var so = new SerializedObject(landforms);
            so.FindProperty("forceOceanOnAllEdges").boolValue = false;
            so.FindProperty("oceanEdgeChance").floatValue = 0.55f;
            so.FindProperty("allowLandlockedMaps").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var a = WorldMapBoundaryLayout.Resolve(session, landforms);
            var b = WorldMapBoundaryLayout.Resolve(session, landforms);

            Assert.AreEqual(a.West, b.West);
            Assert.AreEqual(a.East, b.East);
            Assert.AreEqual(a.South, b.South);
            Assert.AreEqual(a.North, b.North);
            Object.DestroyImmediate(landforms);
        }

        [Test]
        public void AllOcean_MatchesStaticInstance()
        {
            Assert.AreEqual(WorldMapBoundaryLayout.AllOcean, WorldMapBoundaryLayout.AllOcean);
            Assert.AreEqual(WorldMapBoundaryKind.Ocean, WorldMapBoundaryLayout.AllOcean.North);
        }

        private static WorldMapSession CreateSession(int seed) =>
            WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: seed,
                profileVersion: 2,
                originXZ: Vector2.zero,
                tilesPerSide: 8,
                tileSizeMeters: 1000f);
    }
}
#endif
