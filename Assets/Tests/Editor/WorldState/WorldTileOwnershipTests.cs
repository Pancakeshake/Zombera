#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldTileOwnershipTests
    {
        [Test]
        public void TryResolveOwnerTile_WithOriginOffset_MapsWorldPositionToLocalTileIndex()
        {
            var header = CreateHeader(
                tilesPerSide: 4,
                tileSizeMeters: 100f,
                worldOriginXZ: new Vector2(500f, 500f));

            Assert.IsTrue(WorldTileOwnership.TryResolveOwnerTile(
                header,
                new Vector2(550f, 550f),
                out var originCorner));
            Assert.AreEqual(new WorldTileKey(0, 0), originCorner);

            Assert.IsTrue(WorldTileOwnership.TryResolveOwnerTile(
                header,
                new Vector2(649f, 749f),
                out var innerTile));
            Assert.AreEqual(new WorldTileKey(1, 2), innerTile);
        }

        [Test]
        public void TryResolveOwnerTile_OutsideBoundsWithoutClamp_ReturnsFalse()
        {
            var header = CreateHeader(
                tilesPerSide: 2,
                tileSizeMeters: 100f,
                worldOriginXZ: new Vector2(1000f, 1000f));

            Assert.IsFalse(WorldTileOwnership.TryResolveOwnerTile(
                header,
                new Vector2(999f, 1000f),
                out _,
                clamp: false));
        }

        [Test]
        public void CopyCoveredTiles_WithOriginOffset_IncludesExpectedTileKeys()
        {
            var header = CreateHeader(
                tilesPerSide: 3,
                tileSizeMeters: 50f,
                worldOriginXZ: new Vector2(200f, 300f));
            var results = new List<WorldTileKey>();

            WorldTileOwnership.CopyCoveredTiles(
                header,
                new Rect(225f, 325f, 60f, 60f),
                results,
                clamp: true);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new WorldTileKey(0, 0),
                    new WorldTileKey(1, 0),
                    new WorldTileKey(0, 1),
                    new WorldTileKey(1, 1)
                },
                results);
        }

        private static WorldStateHeader CreateHeader(
            int tilesPerSide,
            float tileSizeMeters,
            Vector2 worldOriginXZ)
        {
            var sideMeters = tilesPerSide * tileSizeMeters;
            return new WorldStateHeader
            {
                tilesPerSide = tilesPerSide,
                tileSizeMeters = tileSizeMeters,
                worldOriginXZ = worldOriginXZ,
                worldBoundsXZ = new Rect(worldOriginXZ.x, worldOriginXZ.y, sideMeters, sideMeters)
            };
        }
    }
}
#endif
