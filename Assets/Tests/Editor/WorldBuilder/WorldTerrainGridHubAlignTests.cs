using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldTerrainGridHubAlignTests
    {
        [Test]
        public void ResnapWorldTerrainGridToSession_RestoresTilesAfterHubMove()
        {
            var hub = new GameObject("Hub_AlignTest");
            var stack = new GameObject("WorldBuilderStack");
            stack.transform.SetParent(hub.transform, false);
            var grid = new GameObject("WorldTerrainGrid");
            grid.transform.SetParent(stack.transform, false);

            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 42,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 2,
                tileSizeMeters: 1000f);

            var tile = new GameObject("Terrain_1_0");
            tile.transform.SetParent(grid.transform, false);
            tile.transform.position = new Vector3(1000f, -50f, 0f);

            var builder = hub.AddComponent<CityPrefabRoadNetworkBuilder>();
            hub.transform.position = new Vector3(2500f, 0f, 1800f);

            // Simulate parenting drag: local stays, world shifts with hub.
            Assert.AreNotEqual(1000f, tile.transform.position.x);

            var corrected = builder.ResnapWorldTerrainGridToSession(session);
            Assert.AreEqual(1, corrected);
            Assert.AreEqual(1000f, tile.transform.position.x, 0.01f);
            Assert.AreEqual(0f, tile.transform.position.z, 0.01f);
            Assert.AreEqual(-50f, tile.transform.position.y, 0.01f);

            Object.DestroyImmediate(hub);
        }
    }
}
