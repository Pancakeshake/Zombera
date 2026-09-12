using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        private static bool HasGeneratedContent(WorldGeneratedStateBatch batch) =>
            batch != null &&
            (HasItems(batch.TerrainRecords) ||
             HasItems(batch.Regions) ||
             HasItems(batch.Settlements) ||
             HasItems(batch.Roads) ||
             HasItems(batch.Districts) ||
             HasItems(batch.Lots) ||
             HasItems(batch.Buildings) ||
             HasItems(batch.Pois) ||
             HasItems(batch.TerrainModifications) ||
             HasItems(batch.Events));

        private static bool HasItems<T>(List<T> records) => records != null && records.Count > 0;

        private List<WorldTileKey> BuildBatchScope(WorldState candidate, WorldGeneratedStateBatch batch)
        {
            var scope = new List<WorldTileKey>(8);
            if (batch.HasScopedTiles)
                AddScopeTiles(batch, scope);
            else
                AddImplicitScopeTiles(candidate, batch, scope);
            return scope;
        }

        private static void AddScopeTiles(WorldGeneratedStateBatch batch, List<WorldTileKey> scope)
        {
            for (var i = 0; i < batch.ScopeTiles.Count; i++)
                AddUnique(scope, WorldTileKey.FromCoord(batch.ScopeTiles[i]));
        }

        private void AddImplicitScopeTiles(WorldState candidate, WorldGeneratedStateBatch batch, List<WorldTileKey> scope)
        {
            AddTerrainRecordScope(batch.TerrainRecords, scope);
            AddRecordScope(candidate, batch.Regions, record => record?.boundsXZ.center ?? default, scope);
            AddRecordScope(candidate, batch.Settlements, record => record?.centerXZ ?? default, scope);
            AddRecordScope(candidate, batch.Roads, RoadAnchor, scope);
            AddRecordScope(candidate, batch.Districts, record => record?.centerXZ ?? default, scope);
            AddRecordScope(candidate, batch.Lots, record => record?.boundsXZ.center ?? default, scope);
            AddRecordScope(candidate, batch.Buildings, record => record != null ? new Vector2(record.position.x, record.position.z) : default, scope);
            AddRecordScope(candidate, batch.Pois, record => record?.positionXZ ?? default, scope);
            AddRecordScope(candidate, batch.TerrainModifications, record => record?.boundsXZ.center ?? default, scope);
        }

        private static void AddTerrainRecordScope(List<WorldTileTerrainRecord> records, List<WorldTileKey> scope)
        {
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] != null)
                    AddUnique(scope, records[i].Tile);
            }
        }

        private static void AddRecordScope<T>(
            WorldState candidate,
            List<T> records,
            Func<T, Vector2> anchorSelector,
            List<WorldTileKey> scope)
        {
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
            {
                if (WorldTileOwnership.TryResolveOwnerTile(candidate.header, anchorSelector(records[i]), out var tile, true))
                    AddUnique(scope, tile);
            }
        }

        private void ApplyTerrainRecords(
            WorldState candidate,
            List<WorldTileTerrainRecord> records,
            WorldStateChangeSet changes)
        {
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
                ApplyTerrainRecord(candidate, records[i], changes);
        }

        private void ApplyTerrainRecord(
            WorldState candidate,
            WorldTileTerrainRecord record,
            WorldStateChangeSet changes)
        {
            if (record == null || !TryGetOrCreateCandidatePartition(candidate, record.Tile, out var partition))
                return;

            AddTerrainModificationIds(partition.terrain?.modifications, changes.RemovedEntityIds);
            partition.terrain = record.CreateTerrainCopy();
            partition.terrain ??= new TerrainChunkState();
            AddTerrainModificationIds(partition.terrain.modifications, changes.AddedEntityIds);
        }

        private void AddRecords<T>(
            WorldState candidate,
            List<T> records,
            Func<T, WorldEntityId> idSelector,
            Func<T, Vector2> anchorSelector,
            Action<WorldTilePartitionState, T> append,
            WorldStateChangeSet changes)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (!WorldTileOwnership.TryResolveOwnerTile(candidate.header, anchorSelector(records[i]), out var tile, true))
                    continue;

                if (!TryGetOrCreateCandidatePartition(candidate, tile, out var partition))
                    continue;

                append(partition, records[i]);
                AddUnique(changes.AddedEntityIds, idSelector(records[i]));
            }
        }

        private void AddTerrainModificationRecords(
            WorldState candidate,
            List<TerrainModificationState> records,
            WorldStateChangeSet changes)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (TryAddTerrainModification(candidate, records[i], out _))
                    AddUnique(changes.AddedEntityIds, records[i].id);
            }
        }

        private void AppendEvents(
            WorldState candidate,
            List<WorldEventState> events,
            WorldStateChangeSet changes)
        {
            if (events == null || events.Count == 0)
                return;

            candidate.pendingEvents ??= new List<WorldEventState>(events.Count);
            for (var i = 0; i < events.Count; i++)
            {
                candidate.pendingEvents.Add(WorldStateCloner.Clone(events[i]));
                AddUnique(changes.AddedEntityIds, events[i]?.id ?? default);
            }
        }

        private void ForEachScopedPartition(
            WorldState candidate,
            List<WorldTileKey> scope,
            Action<WorldTilePartitionState> action)
        {
            for (var i = 0; i < scope.Count; i++)
            {
                var partition = GetCandidatePartition(candidate, scope[i]);
                if (partition != null)
                    action(partition);
            }
        }

        private WorldTilePartitionState GetCandidatePartition(WorldState candidate, WorldTileKey tile)
        {
            if (candidate?.tiles == null)
                return null;

            for (var i = 0; i < candidate.tiles.Count; i++)
            {
                var partition = candidate.tiles[i];
                if (partition != null && partition.key == tile)
                    return EnsurePartitionLists(partition);
            }

            return null;
        }

        private bool TryGetOrCreateCandidatePartition(
            WorldState candidate,
            WorldTileKey tile,
            out WorldTilePartitionState partition)
        {
            partition = GetCandidatePartition(candidate, tile);
            if (partition != null)
                return true;

            if (!IsTileInHeader(candidate?.header, tile))
                return false;

            candidate.tiles ??= new List<WorldTilePartitionState>();
            partition = EnsurePartitionLists(new WorldTilePartitionState { key = tile });
            candidate.tiles.Add(partition);
            return true;
        }

        private static WorldTilePartitionState EnsurePartitionLists(WorldTilePartitionState partition)
        {
            partition.terrain ??= new TerrainChunkState();
            partition.regions ??= new List<RegionState>();
            partition.settlements ??= new List<SettlementState>();
            partition.roads ??= new List<RoadState>();
            partition.districts ??= new List<DistrictState>();
            partition.lots ??= new List<LotState>();
            partition.buildings ??= new List<BuildingState>();
            partition.pois ??= new List<PoiState>();
            return partition;
        }
    }
}
