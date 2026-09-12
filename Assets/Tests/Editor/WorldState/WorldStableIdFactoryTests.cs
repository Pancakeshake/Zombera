#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStableIdFactoryTests
    {
        private const int WorldSeed = 424242;

        [Test]
        public void CreateRegionId_SameInputs_ProducesDeterministicId()
        {
            var bounds = new Rect(100f, 200f, 300f, 400f);

            var first = WorldStableIdFactory.CreateRegionId(WorldSeed, "region-a", 0, bounds);
            var second = WorldStableIdFactory.CreateRegionId(WorldSeed, "region-a", 0, bounds);

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(default, first.value);
            Assert.AreEqual(WorldEntityKind.Region, first.kind);
        }

        [Test]
        public void CreateRoadId_ReversedPolyline_ProducesSameId()
        {
            var parentId = WorldStableIdFactory.CreateRegionId(
                WorldSeed,
                "region-road",
                0,
                new Rect(0f, 0f, 500f, 500f));

            var forward = new List<Vector2>
            {
                new(10f, 10f),
                new(110f, 10f),
                new(110f, 110f)
            };
            var reversed = new List<Vector2>
            {
                new(110f, 110f),
                new(110f, 10f),
                new(10f, 10f)
            };

            var forwardId = WorldStableIdFactory.CreateRoadId(
                WorldSeed,
                WorldEntityKind.Region,
                parentId,
                "arterial",
                1,
                forward);
            var reversedId = WorldStableIdFactory.CreateRoadId(
                WorldSeed,
                WorldEntityKind.Region,
                parentId,
                "arterial",
                1,
                reversed);

            Assert.AreEqual(forwardId, reversedId);
        }

        [Test]
        public void CreateDistrictId_RotatedAndReversedPolygon_ProducesSameId()
        {
            var settlementId = WorldStableIdFactory.CreateSettlementId(
                WorldSeed,
                WorldStableIdFactory.CreateRegionId(WorldSeed, "region", 0, new Rect(0f, 0f, 1000f, 1000f)),
                "settlement",
                0,
                new Vector2(500f, 500f),
                new Vector2(200f, 200f),
                0f);

            var canonical = new List<Vector2>
            {
                new(400f, 400f),
                new(600f, 400f),
                new(600f, 600f),
                new(400f, 600f)
            };
            var rotatedStart = new List<Vector2>
            {
                new(600f, 400f),
                new(600f, 600f),
                new(400f, 600f),
                new(400f, 400f)
            };
            var reversedWinding = new List<Vector2>
            {
                new(400f, 600f),
                new(400f, 400f),
                new(600f, 400f),
                new(600f, 600f)
            };
            var closedDuplicate = new List<Vector2>
            {
                new(400f, 400f),
                new(600f, 400f),
                new(600f, 600f),
                new(400f, 600f),
                new(400f, 400f)
            };

            var baseline = WorldStableIdFactory.CreateDistrictId(
                WorldSeed,
                settlementId,
                "district",
                0,
                canonical);
            var rotated = WorldStableIdFactory.CreateDistrictId(
                WorldSeed,
                settlementId,
                "district",
                0,
                rotatedStart);
            var reversed = WorldStableIdFactory.CreateDistrictId(
                WorldSeed,
                settlementId,
                "district",
                0,
                reversedWinding);
            var closed = WorldStableIdFactory.CreateDistrictId(
                WorldSeed,
                settlementId,
                "district",
                0,
                closedDuplicate);

            Assert.AreEqual(baseline, rotated);
            Assert.AreEqual(baseline, reversed);
            Assert.AreEqual(baseline, closed);
        }
    }
}
#endif
