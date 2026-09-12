#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Simulation;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStateSimulationTests
    {
        private const float FireDamageScale = 0.65f;
        private const float FireConditionLossPerHour = 0.01f;

        [Test]
        public void FireBuildingNow_FullIntensity_AppliesExactConditionMath()
        {
            using var scope = CreateLoadedBuildingScope(new Vector2(50f, 50f));
            var buildingId = scope.BuildingId;

            Assert.IsTrue(scope.Simulation.FireBuildingNow(buildingId, intensity01: 1f));
            Assert.IsTrue(scope.Manager.TryCopyBuilding(buildingId, out var building));

            var expectedFireDamage = FireDamageScale * 1f;
            Assert.AreEqual(expectedFireDamage, building.damage.fireDamage01, 0.0001f);
            Assert.AreEqual(1f - expectedFireDamage, building.condition01, 0.0001f);
            Assert.IsTrue(building.fire.active);
            Assert.AreEqual(0L, building.fire.startedAtHour);
            Assert.AreEqual(1f, building.fire.intensity01, 0.0001f);
            Assert.IsTrue(building.abandoned);
            Assert.AreEqual(WorldUtilityStatus.Failed, building.utilities.power);
        }

        [Test]
        public void AdvanceHours_ActiveFire_AppliesHourlyConditionLoss()
        {
            using var scope = CreateLoadedBuildingScope(new Vector2(50f, 50f));
            var buildingId = scope.BuildingId;

            Assert.IsTrue(scope.Simulation.FireBuildingNow(buildingId, intensity01: 0.5f));
            Assert.IsTrue(scope.Simulation.AdvanceHours(2));

            Assert.IsTrue(scope.Manager.TryCopyBuilding(buildingId, out var building));
            var expectedCondition = (1f - FireDamageScale * 0.5f) - FireConditionLossPerHour * 0.5f * 2f;
            Assert.AreEqual(expectedCondition, building.condition01, 0.0001f);
            Assert.IsTrue(building.fire.active);
        }

        [Test]
        public void AdvanceHours_TwentyFourSingleHourSteps_MatchesSingleTwentyFourHourAdvanceForScheduledEvent()
        {
            using var steppedScope = CreateLoadedBuildingScope(new Vector2(50f, 50f));
            using var bulkScope = CreateLoadedBuildingScope(new Vector2(50f, 50f));
            const long scheduledHour = 12L;

            Assert.IsTrue(steppedScope.Simulation.TryQueueBuildingEvent(
                WorldEventType.AbandonBuilding,
                steppedScope.BuildingId,
                scheduledHour,
                magnitude: 1f,
                out _,
                out _));
            Assert.IsTrue(bulkScope.Simulation.TryQueueBuildingEvent(
                WorldEventType.AbandonBuilding,
                bulkScope.BuildingId,
                scheduledHour,
                magnitude: 1f,
                out _,
                out _));

            for (var hour = 0; hour < 24; hour++)
                Assert.IsTrue(steppedScope.Simulation.AdvanceHours(1));
            Assert.IsTrue(bulkScope.Simulation.AdvanceHours(24));

            Assert.AreEqual(24L, steppedScope.Manager.CaptureCanonicalCopy().clock.currentHour);
            Assert.AreEqual(24L, bulkScope.Manager.CaptureCanonicalCopy().clock.currentHour);

            Assert.IsTrue(steppedScope.Manager.TryCopyBuilding(steppedScope.BuildingId, out var steppedBuilding));
            Assert.IsTrue(bulkScope.Manager.TryCopyBuilding(bulkScope.BuildingId, out var bulkBuilding));
            Assert.AreEqual(steppedBuilding.abandoned, bulkBuilding.abandoned);
            Assert.AreEqual(steppedBuilding.abandonedAtHour, bulkBuilding.abandonedAtHour);
            Assert.AreEqual(steppedScope.Manager.Revision, bulkScope.Manager.Revision);
            Assert.AreEqual(
                steppedScope.Manager.CaptureCanonicalCopy().eventHistory.Count,
                bulkScope.Manager.CaptureCanonicalCopy().eventHistory.Count);
        }

        private static SimulationScope CreateLoadedBuildingScope(Vector2 positionXZ)
        {
            var header = new WorldStateHeader
            {
                worldSeed = 8080,
                tilesPerSide = 2,
                tileSizeMeters = 100f,
                worldOriginXZ = Vector2.zero,
                worldBoundsXZ = new Rect(0f, 0f, 200f, 200f)
            };
            var building = CreateBuilding(header, ordinal: 0, positionXZ);

            var gameObject = new GameObject("WorldStateSimulationTests");
            var manager = gameObject.AddComponent<WorldStateManager>();
            Assert.IsTrue(manager.TryCreateFresh(header, out _));

            var state = manager.CaptureCanonicalCopy();
            var partition = FindPartition(state, WorldTileOwnership.ResolveOwnerTile(header, positionXZ));
            partition.buildings.Add(building);
            Assert.IsTrue(manager.TryLoad(state, out _));

            return new SimulationScope(gameObject, manager, new WorldStateSimulationService(manager), building.id);
        }

        private static BuildingState CreateBuilding(WorldStateHeader header, int ordinal, Vector2 positionXZ)
        {
            var id = WorldStableIdFactory.CreateBuildingId(
                header.worldSeed,
                WorldEntityKind.None,
                default,
                "sim-building",
                ordinal,
                positionXZ,
                new Vector2(4f, 4f),
                0f,
                Vector3.one);

            return new BuildingState
            {
                id = id,
                sourceId = "sim-building",
                archetypeId = "sim-building-archetype",
                typeId = "sim-building",
                position = new Vector3(positionXZ.x, 0f, positionXZ.y),
                footprintXZ = new Rect(positionXZ.x - 2f, positionXZ.y - 2f, 4f, 4f),
                scale = Vector3.one,
                condition01 = 1f
            };
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

        private sealed class SimulationScope : System.IDisposable
        {
            private readonly GameObject _gameObject;

            public WorldStateManager Manager { get; }
            public WorldStateSimulationService Simulation { get; }
            public WorldEntityId BuildingId { get; }

            public SimulationScope(
                GameObject gameObject,
                WorldStateManager manager,
                WorldStateSimulationService simulation,
                WorldEntityId buildingId)
            {
                _gameObject = gameObject;
                Manager = manager;
                Simulation = simulation;
                BuildingId = buildingId;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_gameObject);
            }
        }
    }
}
#endif
