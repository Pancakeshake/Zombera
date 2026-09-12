using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateCanonicalizer
    {
        public static WorldValidationReport Canonicalize(WorldState state)
        {
            var report = new WorldValidationReport();
            if (state == null)
            {
                report.AddIssue(WorldValidationSeverity.Error, WorldStateValidator.HeaderInvalid, default, "$", "WorldState is null.");
                return report;
            }

            EnsureRoot(state);
            EnsureTileRecords(state);
            NormalizeQuaternions(state);
            SortState(state);
            report.Merge(WorldStateValidator.Validate(state, WorldValidationMode.Canonical));
            return report;
        }

        public static WorldState CanonicalizeCopy(WorldState state, out WorldValidationReport report)
        {
            var copy = WorldStateCloner.Clone(state);
            report = Canonicalize(copy);
            return copy;
        }

        private static void EnsureRoot(WorldState state)
        {
            state.header ??= new WorldStateHeader();
            state.clock ??= new WorldSimulationClockState();
            state.tiles ??= new List<WorldTilePartitionState>();
            state.pendingEvents ??= new List<WorldEventState>();
            state.eventHistory ??= new List<WorldEventState>();
            EnsureHeaderStrings(state.header);
        }

        private static void EnsureHeaderStrings(WorldStateHeader header)
        {
            header.generatorId ??= string.Empty;
            header.profileFingerprint ??= string.Empty;
            header.planFingerprint ??= string.Empty;
        }

        private static void EnsureTileRecords(WorldState state)
        {
            for (var i = 0; i < state.tiles.Count; i++)
            {
                var tile = state.tiles[i];
                if (tile == null)
                    continue;

                tile.terrain ??= new TerrainChunkState();
                tile.regions ??= new List<RegionState>();
                tile.settlements ??= new List<SettlementState>();
                tile.roads ??= new List<RoadState>();
                tile.districts ??= new List<DistrictState>();
                tile.lots ??= new List<LotState>();
                tile.buildings ??= new List<BuildingState>();
                tile.pois ??= new List<PoiState>();
                EnsureTerrain(tile.terrain);
                EnsureStrings(tile);
            }

            EnsureEvents(state.pendingEvents);
            EnsureEvents(state.eventHistory);
        }

        private static void EnsureTerrain(TerrainChunkState terrain)
        {
            if (terrain == null)
                return;

            terrain.baseGeneratorId ??= string.Empty;
            terrain.baseGenerationFingerprint ??= string.Empty;
            terrain.modifications ??= new List<TerrainModificationState>();
            for (var i = 0; i < terrain.modifications.Count; i++)
            {
                if (terrain.modifications[i] != null)
                    terrain.modifications[i].outlineXZ ??= new List<Vector2>();
            }
        }

        private static void EnsureStrings(WorldTilePartitionState tile)
        {
            EnsureRegionStrings(tile.regions);
            EnsureSettlementStrings(tile.settlements);
            EnsureRoadPoints(tile.roads);
            EnsureDistrictStrings(tile.districts);
            EnsureLotOutlines(tile.lots);
            EnsureBuildingStrings(tile.buildings);
            EnsurePoiStrings(tile.pois);
        }

        private static void EnsureRegionStrings(List<RegionState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].displayName ??= string.Empty;
            }
        }

        private static void EnsureSettlementStrings(List<SettlementState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].displayName ??= string.Empty;
            }
        }

        private static void EnsureRoadPoints(List<RoadState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].pointsXZ ??= new List<Vector2>();
            }
        }

        private static void EnsureDistrictStrings(List<DistrictState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].displayName ??= string.Empty;
                records[i].clusterName ??= string.Empty;
                records[i].outlineXZ ??= new List<Vector2>();
            }
        }

        private static void EnsureLotOutlines(List<LotState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].outlineXZ ??= new List<Vector2>();
            }
        }

        private static void EnsurePoiStrings(List<PoiState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] == null) continue;
                records[i].sourceId ??= string.Empty;
                records[i].archetypeId ??= string.Empty;
                records[i].mapMarkerId ??= string.Empty;
            }
        }

        private static void EnsureEvents(List<WorldEventState> records)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] != null)
                    records[i].resultCode ??= string.Empty;
            }
        }

        private static void EnsureBuildingStrings(List<BuildingState> records)
        {
            for (var i = 0; i < records.Count; i++)
                EnsureBuilding(records[i]);
        }

        private static void EnsureBuilding(BuildingState record)
        {
            if (record == null)
                return;

            record.sourceId ??= string.Empty;
            record.archetypeId ??= string.Empty;
            record.typeId ??= string.Empty;
            record.occupancy ??= new BuildingOccupancyState();
            record.ownership ??= new BuildingOwnershipState();
            record.utilities ??= new BuildingUtilityState();
            record.damage ??= new BuildingDamageState();
            record.fire ??= new BuildingFireState();
            record.loot ??= new BuildingLootState();
            record.modifications ??= new List<BuildingModificationState>();
            record.modules ??= new List<BuildingModuleState>();
            EnsureBuildingNested(record);
        }

        private static void EnsureBuildingNested(BuildingState record)
        {
            record.ownership.ownerTypeId ??= string.Empty;
            record.ownership.ownerId ??= string.Empty;
            record.loot.items ??= new List<WorldItemStackState>();
            for (var i = 0; i < record.loot.items.Count; i++)
            {
                if (record.loot.items[i] != null)
                    record.loot.items[i].itemId ??= string.Empty;
            }

            for (var i = 0; i < record.modifications.Count; i++)
                EnsureBuildingModification(record.modifications[i]);
            for (var i = 0; i < record.modules.Count; i++)
                EnsureBuildingModule(record.modules[i]);
        }

        private static void EnsureBuildingModification(BuildingModificationState record)
        {
            if (record == null) return;
            record.modificationId ??= string.Empty;
            record.typeId ??= string.Empty;
            record.slotId ??= string.Empty;
        }

        private static void EnsureBuildingModule(BuildingModuleState record)
        {
            if (record == null) return;
            record.moduleId ??= string.Empty;
            record.typeId ??= string.Empty;
        }

        private static void NormalizeQuaternions(WorldState state)
        {
            for (var i = 0; i < state.tiles.Count; i++)
            {
                var tile = state.tiles[i];
                if (tile?.buildings == null)
                    continue;

                for (var j = 0; j < tile.buildings.Count; j++)
                    NormalizeBuilding(tile.buildings[j]);
            }
        }

        private static void NormalizeBuilding(BuildingState building)
        {
            if (building == null)
                return;

            building.rotation = NormalizeQuaternion(building.rotation);
            for (var i = 0; i < building.modifications.Count; i++)
            {
                if (building.modifications[i] != null)
                    building.modifications[i].localRotation = NormalizeQuaternion(building.modifications[i].localRotation);
            }
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            if (!IsFinite(value))
                return value;

            var magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude <= 0.000001f)
                return Quaternion.identity;

            var scale = value.w < 0f ? -1f / magnitude : 1f / magnitude;
            return new Quaternion(value.x * scale, value.y * scale, value.z * scale, value.w * scale);
        }

        private static void SortState(WorldState state)
        {
            state.tiles.Sort((left, right) => CompareTiles(left, right));
            for (var i = 0; i < state.tiles.Count; i++)
                SortTile(state.tiles[i]);
            state.pendingEvents.Sort(CompareEvents);
            state.eventHistory.Sort(CompareEvents);
        }

        private static void SortTile(WorldTilePartitionState tile)
        {
            if (tile == null)
                return;

            SortById(tile.regions, record => record?.id ?? default);
            SortById(tile.settlements, record => record?.id ?? default);
            SortById(tile.roads, record => record?.id ?? default);
            SortById(tile.districts, record => record?.id ?? default);
            SortById(tile.lots, record => record?.id ?? default);
            SortById(tile.buildings, record => record?.id ?? default);
            SortById(tile.pois, record => record?.id ?? default);
            SortById(tile.terrain?.modifications, record => record?.id ?? default);
        }

        private static void SortById<T>(List<T> records, Func<T, WorldEntityId> idSelector)
        {
            records?.Sort((left, right) => idSelector(left).CompareTo(idSelector(right)));
        }

        private static int CompareTiles(WorldTilePartitionState left, WorldTilePartitionState right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return left.key.CompareTo(right.key);
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

        private static bool IsFinite(Quaternion value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);
    }
}
