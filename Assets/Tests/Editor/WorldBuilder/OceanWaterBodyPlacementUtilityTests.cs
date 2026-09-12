#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class OceanWaterBodyPlacementUtilityTests
    {
        [Test]
        public void CollectEdgePlacements_OnlyCreatesOceanSides()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains);

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>();
            OceanWaterBodyPlacementUtility.CollectEdgePlacements(bounds, 0f, layout, 650f, placements);

            Assert.AreEqual(2, placements.Count);
            Assert.AreEqual(WorldMapEdgeSide.East, placements[0].Side);
            Assert.AreEqual(WorldMapEdgeSide.South, placements[1].Side);
        }

        [Test]
        public void CollectPlacements_AllOceanLayout_CreatesOneFullSquare()
        {
            var bounds = new Rect(-3000f, -3000f, 16000f, 16000f);
            var layout = WorldMapBoundaryLayout.AllOcean;

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>();
            OceanWaterBodyPlacementUtility.CollectPlacements(bounds, 0f, layout, 650f, placements);

            Assert.AreEqual(1, placements.Count);
            Assert.AreEqual(bounds.center.x, placements[0].Center.x, 0.01f);
            Assert.AreEqual(bounds.center.y, placements[0].Center.z, 0.01f);
            Assert.AreEqual(bounds.width, placements[0].Scale.x, 0.01f);
            Assert.AreEqual(bounds.height, placements[0].Scale.z, 0.01f);
        }

        [Test]
        public void CollectEdgePlacements_AllOceanLayout_StillCreatesFourStrips()
        {
            var bounds = new Rect(0f, 0f, 8000f, 8000f);
            var layout = WorldMapBoundaryLayout.AllOcean;

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>();
            OceanWaterBodyPlacementUtility.CollectEdgePlacements(bounds, 0f, layout, 650f, placements);

            Assert.AreEqual(4, placements.Count);
        }

        [Test]
        public void ResolveOuterExtension_ZeroWhenOceanRingPresent()
        {
            Assert.AreEqual(0f, OceanWaterBodyPlacementUtility.ResolveOuterExtensionMeters(3, 3000f));
            Assert.AreEqual(3000f, OceanWaterBodyPlacementUtility.ResolveOuterExtensionMeters(0, 3000f));
        }

        [Test]
        public void CollectEdgePlacements_ExtendsWestOceanBeyondMapEdge()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Mountains,
                WorldMapBoundaryKind.Mountains);

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>();
            OceanWaterBodyPlacementUtility.CollectEdgePlacements(bounds, 0f, layout, 650f, placements);

            Assert.AreEqual(1, placements.Count);
            Assert.Less(placements[0].Center.x, bounds.xMin);
            var outerLeft = placements[0].Center.x - placements[0].Scale.x * 0.5f;
            Assert.AreEqual(-OceanWaterBodyPlacementUtility.DefaultOuterExtensionMeters, outerLeft, 0.01f);
        }

        [Test]
        public void CollectEdgePlacements_NorthSouthSpanSharesOuterCorners()
        {
            var bounds = new Rect(0f, 0f, 2000f, 2000f);
            var layout = new WorldMapBoundaryLayout(
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean,
                WorldMapBoundaryKind.Ocean);
            const float outer = 3000f;

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>();
            OceanWaterBodyPlacementUtility.CollectEdgePlacements(
                bounds, 0f, layout, 650f, placements, outer);

            Assert.AreEqual(4, placements.Count);

            OceanWaterBodyPlacementUtility.EdgePlacement west = default;
            OceanWaterBodyPlacementUtility.EdgePlacement south = default;
            for (var i = 0; i < placements.Count; i++)
            {
                if (placements[i].Side == WorldMapEdgeSide.West)
                    west = placements[i];
                if (placements[i].Side == WorldMapEdgeSide.South)
                    south = placements[i];
            }

            Assert.AreEqual(west.Scale.z, south.Scale.x, 0.01f);

            var westOuter = west.Center.x - west.Scale.x * 0.5f;
            var southLeft = south.Center.x - south.Scale.x * 0.5f;
            Assert.AreEqual(westOuter, southLeft, 0.01f);
            Assert.AreEqual(-outer, westOuter, 0.01f);
        }

        [Test]
        public void CreateSession_LargeRingExpandsToSixteenTiles()
        {
            var mapSize = ScriptableObject.CreateInstance<WorldMapSizeSettings>();
            try
            {
                var session = mapSize.CreateSession(
                    WorldMapSizeTier.Large,
                    seed: 1,
                    profileVersion: 1);
                Assert.AreEqual(16, session.TilesPerSide);
                Assert.AreEqual(3, session.OceanRingTiles);
                Assert.AreEqual(new Vector2(-3000f, -3000f), session.WorldOriginXZ);
                Assert.AreEqual(new Rect(-3000f, -3000f, 16000f, 16000f), session.WorldBoundsXZ);
                Assert.AreEqual(new Rect(0f, 0f, 10000f, 10000f), session.CoreWorldBoundsXZ);
            }
            finally
            {
                Object.DestroyImmediate(mapSize);
            }
        }
    }
}
#endif
