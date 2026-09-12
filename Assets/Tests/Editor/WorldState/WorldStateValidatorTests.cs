#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStateValidatorTests
    {
        [Test]
        public void Validate_UnsupportedSchemaVersion_EmitsWsSchemaUnsupported()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            state.header.schemaVersion = WorldStateSchema.CurrentVersion + 99;

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.SchemaUnsupported));
        }

        [Test]
        public void Validate_DuplicateEntityId_EmitsWsIdDuplicate()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            var sharedId = WorldStableIdFactory.CreateBuildingId(
                state.header.worldSeed,
                WorldEntityKind.None,
                default,
                "duplicate-building",
                0,
                new Vector2(50f, 50f),
                new Vector2(4f, 4f),
                0f,
                Vector3.one);

            AddBuilding(state, sharedId, new Vector2(50f, 50f));
            AddBuilding(state, sharedId, new Vector2(150f, 150f));

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.IdDuplicate));
        }

        [Test]
        public void Validate_DuplicateTilePartition_EmitsWsTileDuplicate()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            state.tiles.Add(new WorldTilePartitionState { key = new WorldTileKey(0, 0) });

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.TileDuplicate));
        }

        [Test]
        public void Validate_InvalidEntityId_EmitsWsIdInvalid()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            AddBuilding(state, default, new Vector2(50f, 50f));

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.IdInvalid));
        }

        [Test]
        public void Validate_OwnerTileMismatch_EmitsWsOwnerTileMismatch()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            var id = WorldStableIdFactory.CreateBuildingId(
                state.header.worldSeed,
                WorldEntityKind.None,
                default,
                "misplaced-building",
                0,
                new Vector2(150f, 150f),
                new Vector2(4f, 4f),
                0f,
                Vector3.one);

            var wrongPartition = FindPartition(state, new WorldTileKey(0, 0));
            wrongPartition.buildings.Add(new BuildingState
            {
                id = id,
                sourceId = "misplaced-building",
                position = new Vector3(150f, 0f, 150f),
                footprintXZ = new Rect(148f, 148f, 4f, 4f),
                scale = Vector3.one,
                condition01 = 1f
            });

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.OwnerTileMismatch));
        }

        [Test]
        public void Validate_TileOutOfRange_EmitsWsTileOutOfRange()
        {
            var state = CreateFreshState(tilesPerSide: 2, tileSizeMeters: 100f);
            state.tiles[0].key = new WorldTileKey(9, 9);

            var report = WorldStateValidator.Validate(state);

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(HasCode(report, WorldStateValidator.TileOutOfRange));
        }

        private static WorldState CreateFreshState(int tilesPerSide, float tileSizeMeters)
        {
            var header = new WorldStateHeader
            {
                worldSeed = 24680,
                tilesPerSide = tilesPerSide,
                tileSizeMeters = tileSizeMeters,
                worldOriginXZ = Vector2.zero,
                worldBoundsXZ = new Rect(0f, 0f, tilesPerSide * tileSizeMeters, tilesPerSide * tileSizeMeters)
            };

            var managerObject = new GameObject("WorldStateValidatorTests");
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

        private static void AddBuilding(WorldState state, WorldEntityId id, Vector2 positionXZ)
        {
            var partition = FindPartition(state, WorldTileOwnership.ResolveOwnerTile(state.header, positionXZ));
            partition.buildings.Add(new BuildingState
            {
                id = id,
                sourceId = "validator-building",
                position = new Vector3(positionXZ.x, 0f, positionXZ.y),
                footprintXZ = new Rect(positionXZ.x - 2f, positionXZ.y - 2f, 4f, 4f),
                scale = Vector3.one,
                condition01 = 1f
            });
        }

        private static WorldTilePartitionState FindPartition(WorldState state, WorldTileKey tile)
        {
            for (var i = 0; i < state.tiles.Count; i++)
            {
                if (state.tiles[i].key == tile)
                    return state.tiles[i];
            }

            Assert.Fail($"Tile partition {tile} was not found.");
            return null;
        }

        private static bool HasCode(WorldValidationReport report, string code)
        {
            for (var i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return true;
            }

            return false;
        }
    }
}
#endif
