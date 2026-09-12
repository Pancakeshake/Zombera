using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Simulation
{
    public sealed partial class WorldStateSimulationService
    {
        private const float FireDamageScale = 0.65f;
        private const float FireConditionLossPerHour = 0.01f;
        private const float BurnAreaExpansionMeters = 2f;

        private readonly List<WorldEventState> _dueEvents = new(8);
        private readonly List<BuildingState> _activeFires = new(32);
        private readonly WorldStateManager _stateManager;

        public WorldStateSimulationService(WorldStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public WorldStateManager StateManager => _stateManager;

        public bool AdvanceHours(int hours) => AdvanceHours(hours, out _, out _);

        public bool AdvanceHours(
            int hours,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = null;
            if (hours <= 0)
                return TryCreateNoOpResult("WorldState simulation no-op.", out changes, out report);

            return TryReadCurrentHour(out var currentHour, out report) &&
                AdvanceToHour(currentHour + hours, out changes, out report);
        }

        public bool AdvanceToHour(long targetHour) => AdvanceToHour(targetHour, out _, out _);

        public bool AdvanceToHour(
            long targetHour,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = null;
            if (!TryReadCurrentHour(out var currentHour, out report))
                return false;

            if (targetHour <= currentHour)
                return TryCreateNoOpResult("WorldState simulation target already reached.", out changes, out report);

            while (currentHour < targetHour)
            {
                currentHour++;
                if (!TryProcessHour(currentHour, true, out changes, out report))
                    return false;
            }

            return true;
        }

        public bool FireBuildingNow(WorldEntityId buildingId, float intensity01 = 1f) =>
            FireBuildingNow(buildingId, intensity01, out _, out _);

        public bool FireBuildingNow(
            WorldEntityId buildingId,
            float intensity01,
            out WorldStateChangeSet changes,
            out WorldValidationReport report) =>
            ApplyEventNow(WorldEventType.FireBuilding, buildingId, intensity01, out changes, out report);

        public bool AbandonBuildingNow(WorldEntityId buildingId) =>
            AbandonBuildingNow(buildingId, out _, out _);

        public bool AbandonBuildingNow(
            WorldEntityId buildingId,
            out WorldStateChangeSet changes,
            out WorldValidationReport report) =>
            ApplyEventNow(WorldEventType.AbandonBuilding, buildingId, 1f, out changes, out report);

        public bool ApplyEventNow(
            WorldEventType type,
            WorldEntityId buildingId,
            float magnitude,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = null;
            if (!TryReadCurrentHour(out var currentHour, out report))
                return false;

            if (!TryQueueBuildingEvent(type, buildingId, currentHour, magnitude, out _, out report))
                return false;

            return TryProcessHour(currentHour, false, out changes, out report);
        }

        public bool TryQueueBuildingEvent(
            WorldEventType type,
            WorldEntityId buildingId,
            long scheduledHour,
            float magnitude,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = null;
            if (!TryBuildEvent(type, buildingId, scheduledHour, magnitude, out var worldEvent, out report))
                return false;

            return _stateManager.TryQueueEvent(
                worldEvent,
                _stateManager.Revision,
                $"Queue {type} for {buildingId}.",
                out changes,
                out report);
        }

        private bool TryBuildEvent(
            WorldEventType type,
            WorldEntityId buildingId,
            long scheduledHour,
            float magnitude,
            out WorldEventState worldEvent,
            out WorldValidationReport report)
        {
            worldEvent = null;
            if (!TryCaptureState(out var state, out report))
                return false;

            if (!_stateManager.TryCopyBuilding(buildingId, out var building))
            {
                report = ErrorReport($"Building {buildingId} was not found.");
                return false;
            }

            var sequence = Math.Max(1L, state.clock?.nextEventSequence ?? 1L);
            worldEvent = CreateEvent(state.header, building, type, Math.Max(0L, scheduledHour), magnitude, sequence);
            report = ValidReport();
            return true;
        }

        private bool TryProcessHour(
            long hour,
            bool updateClock,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            return _stateManager.TryApplySimulationTransaction(
                $"WorldState simulation hour {hour}.",
                (state, changeSet) => ProcessHour(state, changeSet, hour, updateClock),
                out changes,
                out report);
        }

        private void ProcessHour(
            WorldState state,
            WorldStateChangeSet changes,
            long hour,
            bool updateClock)
        {
            state.clock ??= new WorldSimulationClockState();
            if (updateClock)
                state.clock.currentHour = hour;

            ApplyDueEvents(state, changes, state.clock.currentHour);
            TickActiveFires(state, changes, state.clock.currentHour);
            SortEvents(state);
        }

        private void ApplyDueEvents(WorldState state, WorldStateChangeSet changes, long currentHour)
        {
            _dueEvents.Clear();
            state.pendingEvents ??= new List<WorldEventState>();
            state.eventHistory ??= new List<WorldEventState>();
            MoveDueEvents(state.pendingEvents, currentHour);
            _dueEvents.Sort(CompareEvents);

            for (var i = 0; i < _dueEvents.Count; i++)
            {
                ApplyEvent(state, _dueEvents[i], changes, currentHour);
                state.eventHistory.Add(_dueEvents[i]);
                AddUnique(changes.UpdatedEntityIds, _dueEvents[i].id);
            }
        }

        private void MoveDueEvents(List<WorldEventState> pendingEvents, long currentHour)
        {
            for (var i = pendingEvents.Count - 1; i >= 0; i--)
            {
                var worldEvent = pendingEvents[i];
                if (worldEvent == null || worldEvent.status != WorldEventStatus.Pending || worldEvent.scheduledHour > currentHour)
                    continue;

                pendingEvents.RemoveAt(i);
                _dueEvents.Add(worldEvent);
            }
        }

        private void ApplyEvent(
            WorldState state,
            WorldEventState worldEvent,
            WorldStateChangeSet changes,
            long currentHour)
        {
            if (!TryFindBuilding(state, worldEvent.targetId, out var building))
            {
                Reject(worldEvent, currentHour, "TargetMissing");
                return;
            }

            var resultCode = worldEvent.type switch
            {
                WorldEventType.FireBuilding => ApplyFireEvent(state, building, worldEvent, changes, currentHour),
                WorldEventType.AbandonBuilding => ApplyAbandonEvent(building, changes, currentHour),
                _ => "UnsupportedEvent"
            };

            Resolve(worldEvent, currentHour, resultCode);
        }

        private string ApplyFireEvent(
            WorldState state,
            BuildingState building,
            WorldEventState worldEvent,
            WorldStateChangeSet changes,
            long currentHour)
        {
            var intensity = ClampIntensity(worldEvent.magnitude);
            var burn = CreateBurnArea(state.header, building, intensity, currentHour);
            if (!TryUpsertTerrainModification(state, burn, changes))
                return "BurnAreaRejected";

            EnsureBuildingFields(building);
            building.fire.active = true;
            building.fire.startedAtHour = currentHour;
            building.fire.intensity01 = intensity;
            building.damage.fireDamage01 = Mathf.Max(building.damage.fireDamage01, FireDamageScale * intensity);
            building.condition01 = Mathf.Min(Clamp01(building.condition01), 1f - building.damage.fireDamage01);
            MarkBuildingAbandoned(building, currentHour);
            building.utilities.power = WorldUtilityStatus.Failed;
            AddUnique(changes.UpdatedEntityIds, building.id);
            return "Applied";
        }

        private string ApplyAbandonEvent(
            BuildingState building,
            WorldStateChangeSet changes,
            long currentHour)
        {
            EnsureBuildingFields(building);
            MarkBuildingAbandoned(building, currentHour);
            ClearOwnership(building);
            AddUnique(changes.UpdatedEntityIds, building.id);
            return "Applied";
        }

        private void TickActiveFires(WorldState state, WorldStateChangeSet changes, long currentHour)
        {
            CollectActiveFires(state, currentHour);
            _activeFires.Sort((left, right) => left.id.CompareTo(right.id));
            for (var i = 0; i < _activeFires.Count; i++)
                TickFire(_activeFires[i], changes);
        }

        private void CollectActiveFires(WorldState state, long currentHour)
        {
            _activeFires.Clear();
            if (state.tiles == null)
                return;

            for (var i = 0; i < state.tiles.Count; i++)
                CollectActiveFires(state.tiles[i]?.buildings, currentHour);
        }

        private void CollectActiveFires(List<BuildingState> buildings, long currentHour)
        {
            if (buildings == null)
                return;

            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building?.fire != null && building.fire.active && building.fire.startedAtHour < currentHour)
                    _activeFires.Add(building);
            }
        }

        private void TickFire(BuildingState building, WorldStateChangeSet changes)
        {
            EnsureBuildingFields(building);
            var intensity = Clamp01(building.fire.intensity01);
            building.condition01 = Mathf.Max(0f, Clamp01(building.condition01) - FireConditionLossPerHour * intensity);
            if (building.condition01 <= 0f)
            {
                building.condition01 = 0f;
                building.damage.destroyed = true;
                building.fire.active = false;
                building.fire.intensity01 = 0f;
            }

            AddUnique(changes.UpdatedEntityIds, building.id);
        }
    }
}
