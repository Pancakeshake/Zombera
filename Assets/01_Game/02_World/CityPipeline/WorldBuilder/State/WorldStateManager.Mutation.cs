using System;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        public bool TryUpdateHeader(
            string reason,
            Action<WorldStateHeader> mutation,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(reason);
            if (_state == null)
            {
                report = ErrorReport("WorldState is not available.");
                return false;
            }

            if (mutation == null)
            {
                report = ErrorReport("Header mutation delegate is null.");
                return false;
            }

            var candidate = WorldStateCloner.Clone(_state);
            candidate.header ??= new WorldStateHeader();
            mutation(candidate.header);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.Mutated,
                out report);
        }

        public bool TryMutateBuilding(
            WorldEntityId id,
            long expectedRevision,
            string reason,
            Action<BuildingState> mutation,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(reason);
            if (!CanMutate(expectedRevision, out report))
                return false;

            if (mutation == null)
            {
                report = ErrorReport("Building mutation delegate is null.");
                return false;
            }

            var candidate = WorldStateCloner.Clone(_state);
            if (!TryMoveMutatedBuilding(candidate, id, mutation, out report))
                return false;

            changes.UpdatedEntityIds.Add(id);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.Mutated,
                out report);
        }

        public bool TryAddTerrainModification(
            TerrainModificationState modification,
            long expectedRevision,
            string reason,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(reason);
            if (!CanMutate(expectedRevision, out report))
                return false;

            var candidate = WorldStateCloner.Clone(_state);
            if (!TryAddTerrainModification(candidate, modification, out var added, out report))
                return false;

            if (added)
                changes.AddedEntityIds.Add(modification.id);
            else
                changes.UpdatedEntityIds.Add(modification.id);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.Mutated,
                out report);
        }

        public bool TryQueueEvent(
            WorldEventState worldEvent,
            long expectedRevision,
            string reason,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(reason);
            if (!CanMutate(expectedRevision, out report))
                return false;

            if (worldEvent == null)
            {
                report = ErrorReport("World event is null.");
                return false;
            }

            var candidate = WorldStateCloner.Clone(_state);
            candidate.clock ??= new WorldSimulationClockState();
            candidate.pendingEvents ??= new();
            var queued = WorldStateCloner.Clone(worldEvent);
            if (queued.sequence <= 0)
                queued.sequence = candidate.clock.nextEventSequence;
            queued.status = WorldEventStatus.Pending;
            queued.resolvedHour = -1L;
            candidate.clock.nextEventSequence = Math.Max(candidate.clock.nextEventSequence, queued.sequence + 1L);
            candidate.pendingEvents.Add(queued);
            changes.AddedEntityIds.Add(worldEvent.id);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.Mutated,
                out report);
        }

        private bool CanMutate(long expectedRevision, out WorldValidationReport report)
        {
            if (_state == null)
            {
                report = ErrorReport("WorldState is not available.");
                return false;
            }

            if (expectedRevision != _state.revision)
            {
                report = ErrorReport($"WorldState revision mismatch. Expected {expectedRevision}, current {_state.revision}.");
                return false;
            }

            report = ValidReport();
            return true;
        }

        private bool TryMoveMutatedBuilding(
            WorldState candidate,
            WorldEntityId id,
            Action<BuildingState> mutation,
            out WorldValidationReport report)
        {
            report = ValidReport();
            if (!TryFindBuildingForMutation(candidate, id, out var ownerPartition, out var buildingIndex))
            {
                report = ErrorReport($"Building {id} was not found.");
                return false;
            }

            var building = ownerPartition.buildings[buildingIndex];
            mutation(building);
            if (building.id != id)
            {
                report = ErrorReport("Building mutation cannot change the entity id.");
                return false;
            }

            return TryMoveBuildingIfOwnerChanged(candidate, ownerPartition, buildingIndex, building, out report);
        }

        private bool TryFindBuildingForMutation(
            WorldState candidate,
            WorldEntityId id,
            out WorldTilePartitionState ownerPartition,
            out int buildingIndex)
        {
            ownerPartition = null;
            buildingIndex = -1;
            if (!TryGetEntityEntry(id, WorldEntityKind.Building, out var entry))
                return false;

            ownerPartition = GetCandidatePartition(candidate, entry.OwnerTile);
            buildingIndex = entry.RecordIndex;
            return ownerPartition?.buildings != null &&
                buildingIndex >= 0 &&
                buildingIndex < ownerPartition.buildings.Count;
        }

        private bool TryMoveBuildingIfOwnerChanged(
            WorldState candidate,
            WorldTilePartitionState ownerPartition,
            int buildingIndex,
            BuildingState building,
            out WorldValidationReport report)
        {
            report = ValidReport();
            if (!WorldTileOwnership.TryResolveOwnerTile(candidate.header, building.position, out var ownerTile, true))
            {
                report = ErrorReport($"Could not resolve owner tile for building {building.id}.");
                return false;
            }

            if (ownerPartition.key == ownerTile)
                return true;

            if (!TryGetOrCreateCandidatePartition(candidate, ownerTile, out var targetPartition))
            {
                report = ErrorReport($"Could not find owner tile partition {ownerTile}.");
                return false;
            }

            ownerPartition.buildings.RemoveAt(buildingIndex);
            targetPartition.buildings ??= new();
            targetPartition.buildings.Add(building);
            return true;
        }

        private bool TryAddTerrainModification(
            WorldState candidate,
            TerrainModificationState modification,
            out WorldValidationReport report) =>
            TryAddTerrainModification(candidate, modification, out _, out report);

        private bool TryAddTerrainModification(
            WorldState candidate,
            TerrainModificationState modification,
            out bool added,
            out WorldValidationReport report)
        {
            added = false;
            report = ValidReport();
            if (modification == null)
            {
                report = ErrorReport("Terrain modification is null.");
                return false;
            }

            var center = modification.boundsXZ.center;
            if (!WorldTileOwnership.TryResolveOwnerTile(candidate.header, center, out var tile, true))
            {
                report = ErrorReport($"Could not resolve owner tile for terrain modification {modification.id}.");
                return false;
            }

            if (!TryGetOrCreateCandidatePartition(candidate, tile, out var partition))
            {
                report = ErrorReport($"Could not find owner tile partition {tile}.");
                return false;
            }

            partition.terrain ??= new TerrainChunkState();
            partition.terrain.modifications ??= new();
            if (TryUpdateExistingBurnModification(partition.terrain.modifications, modification))
                return true;

            partition.terrain.modifications.Add(WorldStateCloner.Clone(modification));
            added = true;
            return true;
        }

        private static bool TryUpdateExistingBurnModification(
            System.Collections.Generic.List<TerrainModificationState> records,
            TerrainModificationState modification)
        {
            if (modification.kind != TerrainModificationKind.BurnArea)
                return false;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null || record.id != modification.id)
                    continue;

                record.boundsXZ = modification.boundsXZ;
                record.outlineXZ = WorldStateCloner.Clone(modification).outlineXZ;
                record.intensity01 = Math.Max(record.intensity01, modification.intensity01);
                record.active |= modification.active;
                return true;
            }

            return false;
        }

        private bool TryCommitCandidate(
            WorldState candidate,
            WorldStateChangeSet changes,
            WorldStateLifecycleReason lifecycleReason,
            out WorldValidationReport report)
        {
            candidate.revision = _state.revision + 1L;
            report = WorldStateValidator.Validate(candidate);
            if (!report.IsValid)
            {
                RaiseValidationFailed(changes?.Reason ?? "WorldState mutation validation failed.");
                return false;
            }

            changes.Revision = candidate.revision;
            ReplaceState(candidate, _availability);
            RaiseCommitted(lifecycleReason, changes);
            return true;
        }

        private static WorldStateChangeSet CreateChangeSet(string reason) =>
            new() { Reason = reason ?? string.Empty };
    }
}
