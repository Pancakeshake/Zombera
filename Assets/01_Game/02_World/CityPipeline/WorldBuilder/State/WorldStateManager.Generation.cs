using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        internal bool TryApplyGeneratedBatch(
            WorldGeneratedStateBatch batch,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(batch?.Reason);
            if (_state == null)
            {
                report = ErrorReport("WorldState is not available.");
                return false;
            }

            if (!HasGeneratedContent(batch))
            {
                changes.Revision = Revision;
                report = ValidReport();
                return true;
            }

            var candidate = WorldStateCloner.Clone(_state);
            var scope = BuildBatchScope(candidate, batch);
            ApplyTerrainRecords(candidate, batch.TerrainRecords, changes);
            ReplaceGeneratedDomains(candidate, batch, scope, changes);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.GeneratedBatchApplied,
                out report);
        }

        private void ReplaceGeneratedDomains(
            WorldState candidate,
            WorldGeneratedStateBatch batch,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            ReplaceRegions(candidate, batch.Regions, scope, changes);
            ReplaceSettlements(candidate, batch.Settlements, scope, changes);
            ReplaceRoads(candidate, batch.Roads, scope, changes);
            ReplaceDistricts(candidate, batch.Districts, scope, changes);
            ReplaceLots(candidate, batch.Lots, scope, changes);
            ReplaceBuildings(candidate, batch.Buildings, scope, changes);
            ReplacePois(candidate, batch.Pois, scope, changes);
            ReplaceTerrainModifications(candidate, batch.TerrainModifications, scope, changes);
            AppendEvents(candidate, batch.Events, changes);
        }

        private void ReplaceRegions(
            WorldState candidate,
            List<RegionState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.regions, record => record?.id ?? default, changes.RemovedEntityIds));
            AddRecords(candidate, records, record => record?.id ?? default, record => record?.boundsXZ.center ?? default, (partition, record) => partition.regions.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceSettlements(
            WorldState candidate,
            List<SettlementState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.settlements, record => record?.id ?? default, changes.RemovedEntityIds));
            AddRecords(candidate, records, record => record?.id ?? default, record => record?.centerXZ ?? default, (partition, record) => partition.settlements.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceRoads(
            WorldState candidate,
            List<RoadState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            var sourceKinds = CollectRoadSources(records);
            ForEachScopedPartition(candidate, scope, partition => RemoveRoads(partition, sourceKinds, changes));
            AddRecords(candidate, records, record => record?.id ?? default, RoadAnchor, (partition, record) => partition.roads.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceDistricts(
            WorldState candidate,
            List<DistrictState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            var removedDistrictIds = new List<WorldEntityId>();
            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.districts, record => record?.id ?? default, removedDistrictIds));
            var removedLotIds = RemoveLotsByDistrict(candidate, removedDistrictIds, changes);
            RemoveBuildingsByParents(candidate, removedDistrictIds, removedLotIds, changes);
            CopyIds(removedDistrictIds, changes.RemovedEntityIds);
            AddRecords(candidate, records, record => record?.id ?? default, record => record?.centerXZ ?? default, (partition, record) => partition.districts.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceLots(
            WorldState candidate,
            List<LotState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            var removedLotIds = new List<WorldEntityId>();
            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.lots, record => record?.id ?? default, removedLotIds));
            RemoveBuildingsByParents(candidate, null, removedLotIds, changes);
            CopyIds(removedLotIds, changes.RemovedEntityIds);
            AddRecords(candidate, records, record => record?.id ?? default, record => record?.boundsXZ.center ?? default, (partition, record) => partition.lots.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceBuildings(
            WorldState candidate,
            List<BuildingState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.buildings, record => record?.id ?? default, changes.RemovedEntityIds));
            AddRecords(candidate, records, record => record?.id ?? default, record => record != null ? new Vector2(record.position.x, record.position.z) : default, (partition, record) => partition.buildings.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplacePois(
            WorldState candidate,
            List<PoiState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            ForEachScopedPartition(candidate, scope, partition => RemoveIds(partition.pois, record => record?.id ?? default, changes.RemovedEntityIds));
            AddRecords(candidate, records, record => record?.id ?? default, record => record?.positionXZ ?? default, (partition, record) => partition.pois.Add(WorldStateCloner.Clone(record)), changes);
        }

        private void ReplaceTerrainModifications(
            WorldState candidate,
            List<TerrainModificationState> records,
            List<WorldTileKey> scope,
            WorldStateChangeSet changes)
        {
            if (records == null || records.Count == 0)
                return;

            ForEachScopedPartition(candidate, scope, partition => RemoveTerrainModificationIds(partition, changes));
            AddTerrainModificationRecords(candidate, records, changes);
        }
    }
}
