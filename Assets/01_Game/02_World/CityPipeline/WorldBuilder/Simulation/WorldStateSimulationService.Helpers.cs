using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Simulation
{
    public sealed partial class WorldStateSimulationService
    {
        private static WorldEventState CreateEvent(
            WorldStateHeader header,
            BuildingState building,
            WorldEventType type,
            long scheduledHour,
            float magnitude,
            long sequence)
        {
            var positionXZ = new Vector2(building.position.x, building.position.z);
            var sourceId = $"{type}:{building.id.value}:{scheduledHour}:{sequence}";
            return new WorldEventState
            {
                id = WorldStableIdFactory.CreateEventId(
                    header?.worldSeed ?? 0,
                    WorldEntityKind.Building,
                    building.id,
                    sourceId,
                    ToStableOrdinal(sequence),
                    positionXZ),
                sequence = sequence,
                type = type,
                targetId = building.id,
                scheduledHour = scheduledHour,
                magnitude = magnitude,
                status = WorldEventStatus.Pending
            };
        }

        private static TerrainModificationState CreateBurnArea(
            WorldStateHeader header,
            BuildingState building,
            float intensity,
            long currentHour)
        {
            var bounds = Expand(building.footprintXZ, BurnAreaExpansionMeters);
            var outline = CreateRectOutline(bounds);
            return new TerrainModificationState
            {
                id = WorldStableIdFactory.CreateTerrainModificationId(
                    header?.worldSeed ?? 0,
                    WorldEntityKind.Building,
                    building.id,
                    $"BurnArea:{building.id.value}",
                    (int)TerrainModificationKind.BurnArea,
                    outline),
                kind = TerrainModificationKind.BurnArea,
                sourceEntityId = building.id,
                createdAtHour = currentHour,
                boundsXZ = bounds,
                outlineXZ = outline,
                intensity01 = intensity,
                active = true
            };
        }

        private static bool TryUpsertTerrainModification(
            WorldState state,
            TerrainModificationState modification,
            WorldStateChangeSet changes)
        {
            if (TryFindTerrainModification(state, modification.id, out var existing))
            {
                existing.boundsXZ = modification.boundsXZ;
                existing.outlineXZ = new List<Vector2>(modification.outlineXZ);
                existing.intensity01 = Mathf.Max(existing.intensity01, modification.intensity01);
                existing.active |= modification.active;
                AddUnique(changes.UpdatedEntityIds, existing.id);
                return true;
            }

            return TryAppendTerrainModification(state, modification, changes);
        }

        private static bool TryAppendTerrainModification(
            WorldState state,
            TerrainModificationState modification,
            WorldStateChangeSet changes)
        {
            if (!WorldTileOwnership.TryResolveOwnerTile(state.header, modification.boundsXZ.center, out var tile, true))
                return false;

            var partition = GetOrCreatePartition(state, tile);
            if (partition == null)
                return false;

            partition.terrain ??= new TerrainChunkState();
            partition.terrain.modifications ??= new List<TerrainModificationState>();
            partition.terrain.modifications.Add(WorldStateCloner.Clone(modification));
            AddUnique(changes.AddedEntityIds, modification.id);
            return true;
        }

        private static WorldTilePartitionState GetOrCreatePartition(WorldState state, WorldTileKey tile)
        {
            state.tiles ??= new List<WorldTilePartitionState>();
            for (var i = 0; i < state.tiles.Count; i++)
            {
                if (state.tiles[i] != null && state.tiles[i].key == tile)
                    return state.tiles[i];
            }

            if (!IsTileInHeader(state.header, tile))
                return null;

            var partition = new WorldTilePartitionState { key = tile };
            state.tiles.Add(partition);
            return partition;
        }

        private static bool TryFindBuilding(WorldState state, WorldEntityId id, out BuildingState building)
        {
            building = null;
            if (state.tiles == null)
                return false;

            for (var i = 0; i < state.tiles.Count; i++)
            {
                if (TryFindBuilding(state.tiles[i]?.buildings, id, out building))
                    return true;
            }

            return false;
        }

        private static bool TryFindBuilding(List<BuildingState> buildings, WorldEntityId id, out BuildingState building)
        {
            building = null;
            if (buildings == null)
                return false;

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].id == id)
                {
                    building = buildings[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindTerrainModification(
            WorldState state,
            WorldEntityId id,
            out TerrainModificationState modification)
        {
            modification = null;
            if (state.tiles == null)
                return false;

            for (var i = 0; i < state.tiles.Count; i++)
            {
                var records = state.tiles[i]?.terrain?.modifications;
                if (TryFindTerrainModification(records, id, out modification))
                    return true;
            }

            return false;
        }

        private static bool TryFindTerrainModification(
            List<TerrainModificationState> records,
            WorldEntityId id,
            out TerrainModificationState modification)
        {
            modification = null;
            if (records == null)
                return false;

            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] != null && records[i].id == id)
                {
                    modification = records[i];
                    return true;
                }
            }

            return false;
        }

        private static void MarkBuildingAbandoned(BuildingState building, long currentHour)
        {
            building.abandoned = true;
            building.abandonedAtHour = currentHour;
            building.occupancy.occupied = false;
            building.occupancy.currentCount = 0;
        }

        private static void ClearOwnership(BuildingState building)
        {
            building.ownership.ownerTypeId = string.Empty;
            building.ownership.ownerId = string.Empty;
        }

        private static void EnsureBuildingFields(BuildingState building)
        {
            building.occupancy ??= new BuildingOccupancyState();
            building.ownership ??= new BuildingOwnershipState();
            building.utilities ??= new BuildingUtilityState();
            building.damage ??= new BuildingDamageState();
            building.fire ??= new BuildingFireState();
        }

        private static void Resolve(WorldEventState worldEvent, long currentHour, string resultCode)
        {
            worldEvent.status = resultCode == "Applied" ? WorldEventStatus.Applied : WorldEventStatus.Rejected;
            worldEvent.resolvedHour = currentHour;
            worldEvent.resultCode = resultCode;
        }

        private static void Reject(WorldEventState worldEvent, long currentHour, string resultCode)
        {
            worldEvent.status = WorldEventStatus.Rejected;
            worldEvent.resolvedHour = currentHour;
            worldEvent.resultCode = resultCode;
        }

        private bool TryCaptureState(out WorldState state, out WorldValidationReport report)
        {
            state = _stateManager != null && _stateManager.HasState
                ? _stateManager.CaptureCanonicalCopy()
                : null;
            report = state != null ? ValidReport() : ErrorReport("WorldState is not available.");
            return state != null;
        }

        private bool TryReadCurrentHour(out long currentHour, out WorldValidationReport report)
        {
            currentHour = 0L;
            if (!TryCaptureState(out var state, out report))
                return false;

            currentHour = Math.Max(0L, state.clock?.currentHour ?? 0L);
            return true;
        }

        private bool TryCreateNoOpResult(
            string reason,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = new WorldStateChangeSet
            {
                Revision = _stateManager != null ? _stateManager.Revision : 0L,
                Reason = reason
            };
            report = ValidReport();
            return true;
        }

        private static void SortEvents(WorldState state)
        {
            state.pendingEvents?.Sort(CompareEvents);
            state.eventHistory?.Sort(CompareEvents);
        }

        private static int CompareEvents(WorldEventState left, WorldEventState right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            var hour = left.scheduledHour.CompareTo(right.scheduledHour);
            if (hour != 0) return hour;
            var sequence = left.sequence.CompareTo(right.sequence);
            return sequence != 0 ? sequence : left.id.CompareTo(right.id);
        }

        private static List<Vector2> CreateRectOutline(Rect rect) =>
            new(4)
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };

        private static Rect Expand(Rect rect, float amount) =>
            new(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);

        private static float ClampIntensity(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value <= 0f ? 1f : value);

        private static float Clamp01(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

        private static int ToStableOrdinal(long value) =>
            unchecked((int)(value & 0x7fffffff));

        private static bool IsTileInHeader(WorldStateHeader header, WorldTileKey tile) =>
            header != null &&
            tile.x >= 0 &&
            tile.z >= 0 &&
            tile.x < header.tilesPerSide &&
            tile.z < header.tilesPerSide;

        private static void AddUnique(List<WorldEntityId> ids, WorldEntityId id)
        {
            if (id.kind == WorldEntityKind.None && string.IsNullOrEmpty(id.value))
                return;

            var index = ids.BinarySearch(id);
            if (index < 0)
                ids.Insert(~index, id);
        }

        private static WorldValidationReport ValidReport() => new() { IsValid = true };

        private static WorldValidationReport ErrorReport(string message)
        {
            var report = new WorldValidationReport { IsValid = false };
            report.Errors.Add(message ?? "WorldState simulation failed.");
            return report;
        }
    }
}
