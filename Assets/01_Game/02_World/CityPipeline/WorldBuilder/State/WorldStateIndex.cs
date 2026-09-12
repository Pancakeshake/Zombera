using System;
using System.Collections.Generic;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed class WorldStateIndex
    {
        [NonSerialized] private readonly Dictionary<WorldTileKey, int> _partitionByTile = new();
        [NonSerialized] private readonly Dictionary<WorldEntityId, WorldStateEntityIndexEntry> _entityById = new();
        [NonSerialized] private readonly Dictionary<WorldTileKey, List<WorldEntityId>> _entityIdsByCoveredTile = new();
        [NonSerialized] private readonly Dictionary<WorldEntityId, List<WorldEntityId>> _childIdsByParent = new();

        public void Clear()
        {
            _partitionByTile.Clear();
            _entityById.Clear();
            _entityIdsByCoveredTile.Clear();
            _childIdsByParent.Clear();
        }

        public void Rebuild(WorldState state)
        {
            Clear();
            if (state == null)
                return;

            AddPartitions(state.tiles);
            AddEvents(state.pendingEvents);
            AddEvents(state.eventHistory);
        }

        public bool TryGetPartitionIndex(WorldTileCoord coord, out int partitionIndex) =>
            TryGetPartitionIndex(WorldTileKey.FromCoord(coord), out partitionIndex);

        public bool TryGetPartitionIndex(WorldTileKey key, out int partitionIndex) =>
            _partitionByTile.TryGetValue(key, out partitionIndex);

        public bool TryGetEntity(
            WorldEntityId id,
            out WorldStateEntityIndexEntry entry) =>
            _entityById.TryGetValue(id, out entry);

        public bool TryGetCoveredEntityIds(
            WorldTileCoord coord,
            out IReadOnlyList<WorldEntityId> entityIds) =>
            TryGetCoveredEntityIds(WorldTileKey.FromCoord(coord), out entityIds);

        public bool TryGetCoveredEntityIds(
            WorldTileKey key,
            out IReadOnlyList<WorldEntityId> entityIds)
        {
            if (_entityIdsByCoveredTile.TryGetValue(key, out var ids))
            {
                entityIds = ids;
                return true;
            }

            entityIds = Array.Empty<WorldEntityId>();
            return false;
        }

        public bool TryGetChildIds(
            WorldEntityId parentId,
            out IReadOnlyList<WorldEntityId> childIds)
        {
            if (_childIdsByParent.TryGetValue(parentId, out var ids))
            {
                childIds = ids;
                return true;
            }

            childIds = Array.Empty<WorldEntityId>();
            return false;
        }

        public void AddCoverage(WorldTileCoord coord, WorldEntityId entityId) =>
            AddCoverage(WorldTileKey.FromCoord(coord), entityId);

        public void AddCoverage(WorldTileKey key, WorldEntityId entityId)
        {
            if (IsEmpty(entityId))
                return;

            if (!_entityIdsByCoveredTile.TryGetValue(key, out var ids))
            {
                ids = new List<WorldEntityId>(4);
                _entityIdsByCoveredTile.Add(key, ids);
            }

            InsertSortedUnique(ids, entityId);
        }

        private void AddPartitions(List<WorldTilePartitionState> partitions)
        {
            if (partitions == null)
                return;

            for (var i = 0; i < partitions.Count; i++)
            {
                var partition = partitions[i];
                if (partition == null)
                    continue;

                _partitionByTile[partition.key] = i;
                AddPartitionEntities(partition);
            }
        }

        private void AddPartitionEntities(WorldTilePartitionState partition)
        {
            AddRegions(partition.regions, partition.key);
            AddSettlements(partition.settlements, partition.key);
            AddRoads(partition.roads, partition.key);
            AddDistricts(partition.districts, partition.key);
            AddLots(partition.lots, partition.key);
            AddBuildings(partition.buildings, partition.key);
            AddPois(partition.pois, partition.key);
            AddTerrainModifications(partition.terrain?.modifications, partition.key);
        }

        private void AddRegions(List<RegionState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Region, ownerTile, i, default);
        }

        private void AddSettlements(List<SettlementState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Settlement, ownerTile, i, records[i]?.regionId ?? default);
        }

        private void AddRoads(List<RoadState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Road, ownerTile, i, ChooseParent(records[i]?.settlementId ?? default, records[i]?.regionId ?? default));
        }

        private void AddDistricts(List<DistrictState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.District, ownerTile, i, records[i]?.settlementId ?? default);
        }

        private void AddLots(List<LotState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Lot, ownerTile, i, records[i]?.districtId ?? default);
        }

        private void AddBuildings(List<BuildingState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Building, ownerTile, i, BuildingParent(records[i]));
        }

        private void AddPois(List<PoiState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Poi, ownerTile, i, records[i]?.regionId ?? default);
        }

        private void AddTerrainModifications(List<TerrainModificationState> records, WorldTileKey ownerTile)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.TerrainModification, ownerTile, i, records[i]?.sourceEntityId ?? default);
        }

        private void AddEvents(List<WorldEventState> records)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddEntity(records[i]?.id ?? default, WorldEntityKind.Event, default, i, records[i]?.targetId ?? default);
        }

        private void AddEntity(
            WorldEntityId id,
            WorldEntityKind kind,
            WorldTileKey ownerTile,
            int recordIndex,
            WorldEntityId parentId)
        {
            if (IsEmpty(id))
                return;

            _entityById[id] = new WorldStateEntityIndexEntry(kind, ownerTile, recordIndex);
            AddCoverage(ownerTile, id);
            AddParentChild(parentId, id);
        }

        private void AddParentChild(WorldEntityId parentId, WorldEntityId childId)
        {
            if (IsEmpty(parentId))
                return;

            if (!_childIdsByParent.TryGetValue(parentId, out var childIds))
            {
                childIds = new List<WorldEntityId>(4);
                _childIdsByParent.Add(parentId, childIds);
            }

            InsertSortedUnique(childIds, childId);
        }

        private static WorldEntityId BuildingParent(BuildingState record)
        {
            if (record == null)
                return default;

            return ChooseParent(record.lotId, ChooseParent(record.districtId, record.settlementId));
        }

        private static WorldEntityId ChooseParent(WorldEntityId preferred, WorldEntityId fallback) =>
            IsEmpty(preferred) ? fallback : preferred;

        private static bool IsEmpty(WorldEntityId id) =>
            id.kind == WorldEntityKind.None && string.IsNullOrEmpty(id.value);

        private static void InsertSortedUnique(List<WorldEntityId> ids, WorldEntityId id)
        {
            var index = ids.BinarySearch(id);
            if (index >= 0)
                return;

            ids.Insert(~index, id);
        }
    }

    public readonly struct WorldStateEntityIndexEntry
    {
        public readonly WorldEntityKind Kind;
        public readonly WorldTileKey OwnerTile;
        public readonly int RecordIndex;

        public WorldStateEntityIndexEntry(
            WorldEntityKind kind,
            WorldTileKey ownerTile,
            int recordIndex)
        {
            Kind = kind;
            OwnerTile = ownerTile;
            RecordIndex = recordIndex;
        }
    }
}
