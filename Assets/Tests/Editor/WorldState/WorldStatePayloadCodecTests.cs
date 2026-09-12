#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStatePayloadCodecTests
    {
        [Test]
        public void EncodeDecode_RoundTrip_PreservesHash()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f, worldSeed: 13579);

            var payload = WorldStatePayloadCodec.Encode(state);
            var decoded = WorldStatePayloadCodec.Decode(
                payload.payloadBase64,
                expectedHashSha256: payload.hashSha256);

            Assert.IsTrue(decoded.IsValid, string.Join("; ", decoded.report.Errors));
            Assert.That(decoded.hashSha256, Is.EqualTo(payload.hashSha256).IgnoreCase);
            Assert.AreEqual(payload.schemaVersion, decoded.state.header.schemaVersion);
            Assert.AreEqual(state.header.worldSeed, decoded.state.header.worldSeed);
            Assert.AreEqual(state.tiles.Count, decoded.state.tiles.Count);
        }

        [Test]
        public void EncodeToBase64_DecodeWithExpectedHash_IsValid()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f, worldSeed: 86420);
            var payload = WorldStatePayloadCodec.Encode(state);
            var encoded = WorldStatePayloadCodec.EncodeToBase64(state);

            var decoded = WorldStatePayloadCodec.Decode(encoded, expectedHashSha256: payload.hashSha256);

            Assert.IsTrue(decoded.IsValid, string.Join("; ", decoded.report.Errors));
            Assert.That(decoded.hashSha256, Is.EqualTo(payload.hashSha256).IgnoreCase);
        }

        private static WorldState CreateFreshState(int tilesPerSide, float tileSizeMeters, int worldSeed)
        {
            var header = new WorldStateHeader
            {
                worldSeed = worldSeed,
                tilesPerSide = tilesPerSide,
                tileSizeMeters = tileSizeMeters,
                worldOriginXZ = Vector2.zero,
                worldBoundsXZ = new Rect(0f, 0f, tilesPerSide * tileSizeMeters, tilesPerSide * tileSizeMeters)
            };

            var managerObject = new GameObject("WorldStatePayloadCodecTests");
            var manager = managerObject.AddComponent<WorldStateManager>();
            try
            {
                Assert.IsTrue(manager.TryCreateFresh(header, out _));
                return manager.CaptureCanonicalCopy();
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }
    }
}
#endif
