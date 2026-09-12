#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStateManagerTests
    {
        [Test]
        public void TryCreateFresh_TwoByTwoGrid_CreatesFourTilePartitions()
        {
            using var scope = new WorldStateManagerScope();
            var header = CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f);

            Assert.IsTrue(scope.Manager.TryCreateFresh(header, out var report));
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(4, scope.Manager.CaptureCanonicalCopy().tiles.Count);
            Assert.AreEqual(0L, scope.Manager.Revision);
        }

        [Test]
        public void CaptureCanonicalCopy_ReturnsIndependentClone()
        {
            using var scope = new WorldStateManagerScope();
            scope.Manager.TryCreateFresh(CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f), out _);

            var copy = scope.Manager.CaptureCanonicalCopy();
            copy.revision = 99L;
            copy.header.worldSeed = 777;

            Assert.AreEqual(0L, scope.Manager.Revision);
            Assert.AreNotEqual(777, scope.Manager.Header.worldSeed);
        }

        [Test]
        public void Clear_RemovesStateAndResetsAvailability()
        {
            using var scope = new WorldStateManagerScope();
            scope.Manager.TryCreateFresh(CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f), out _);

            scope.Manager.Clear(WorldStateClearReason.Manual);

            Assert.IsFalse(scope.Manager.HasState);
            Assert.AreEqual(WorldStateAvailability.None, scope.Manager.Availability);
            Assert.AreEqual(0L, scope.Manager.Revision);
        }

        [Test]
        public void TryMutateBuilding_ExpectedRevisionMismatch_FailsWithoutCommitting()
        {
            using var scope = new WorldStateManagerScope();
            var header = CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f);
            var building = CreateBuilding(header, ordinal: 0, positionXZ: new Vector2(50f, 50f));
            var state = CreateStateWithBuilding(header, building);

            Assert.IsTrue(scope.Manager.TryLoad(state, out var loadReport));
            Assert.IsTrue(loadReport.IsValid);
            Assert.IsTrue(scope.Manager.TryMutateBuilding(
                building.id,
                expectedRevision: 0L,
                reason: "initial mutation",
                mutation: record => record.condition01 = 0.75f,
                out _,
                out _));

            Assert.IsFalse(scope.Manager.TryMutateBuilding(
                building.id,
                expectedRevision: 0L,
                reason: "stale revision",
                mutation: record => record.condition01 = 0.5f,
                out _,
                out var mismatchReport));
            Assert.IsFalse(mismatchReport.IsValid);
            Assert.AreEqual(1L, scope.Manager.Revision);
            Assert.IsTrue(scope.Manager.TryCopyBuilding(building.id, out var unchanged));
            Assert.AreEqual(0.75f, unchanged.condition01, 0.0001f);
        }

        [Test]
        public void TryMutateBuilding_ValidRevision_IncrementsRevisionAndAppliesMutation()
        {
            using var scope = new WorldStateManagerScope();
            var header = CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f);
            var building = CreateBuilding(header, ordinal: 0, positionXZ: new Vector2(50f, 50f));
            var state = CreateStateWithBuilding(header, building);

            Assert.IsTrue(scope.Manager.TryLoad(state, out _));

            Assert.IsTrue(scope.Manager.TryMutateBuilding(
                building.id,
                expectedRevision: 0L,
                reason: "damage building",
                mutation: record => record.condition01 = 0.42f,
                out var changes,
                out var report));
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(1L, scope.Manager.Revision);
            Assert.AreEqual(1L, changes.Revision);
            Assert.Contains(building.id, changes.UpdatedEntityIds);
            Assert.IsTrue(scope.Manager.TryCopyBuilding(building.id, out var updated));
            Assert.AreEqual(0.42f, updated.condition01, 0.0001f);
        }

        private static WorldStateHeader CreateHeader(int tilesPerSide, float tileSizeMeters)
        {
            var sideMeters = tilesPerSide * tileSizeMeters;
            return new WorldStateHeader
            {
                worldSeed = 12345,
                tilesPerSide = tilesPerSide,
                tileSizeMeters = tileSizeMeters,
                worldOriginXZ = Vector2.zero,
                worldBoundsXZ = new Rect(0f, 0f, sideMeters, sideMeters)
            };
        }

        private static BuildingState CreateBuilding(WorldStateHeader header, int ordinal, Vector2 positionXZ)
        {
            var id = WorldStableIdFactory.CreateBuildingId(
                header.worldSeed,
                WorldEntityKind.None,
                default,
                "test-building",
                ordinal,
                positionXZ,
                new Vector2(4f, 4f),
                0f,
                Vector3.one);

            return new BuildingState
            {
                id = id,
                sourceId = "test-building",
                archetypeId = "test-building-archetype",
                typeId = "test-building",
                position = new Vector3(positionXZ.x, 0f, positionXZ.y),
                footprintXZ = new Rect(positionXZ.x - 2f, positionXZ.y - 2f, 4f, 4f),
                scale = Vector3.one,
                condition01 = 1f
            };
        }

        private static WorldState CreateStateWithBuilding(WorldStateHeader header, BuildingState building)
        {
            var managerObject = new GameObject("WorldStateManagerTests_BuildState");
            var manager = managerObject.AddComponent<WorldStateManager>();
            try
            {
                Assert.IsTrue(manager.TryCreateFresh(header, out _));
                var state = manager.CaptureCanonicalCopy();
                var ownerTile = WorldTileOwnership.ResolveOwnerTile(header, building.position);
                var partition = FindPartition(state, ownerTile);
                partition.buildings.Add(building);
                return state;
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
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

        private sealed class WorldStateManagerScope : System.IDisposable
        {
            private readonly GameObject _gameObject;

            public WorldStateManager Manager { get; }

            public WorldStateManagerScope()
            {
                _gameObject = new GameObject("WorldStateManagerTests");
                Manager = _gameObject.AddComponent<WorldStateManager>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_gameObject);
            }
        }
    }
}
#endif
