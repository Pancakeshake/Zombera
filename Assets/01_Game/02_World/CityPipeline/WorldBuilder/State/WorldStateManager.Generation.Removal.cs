using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        private static void RemoveIds<T>(
            List<T> records,
            Func<T, WorldEntityId> idSelector,
            List<WorldEntityId> removedIds)
        {
            if (records == null)
                return;

            for (var i = records.Count - 1; i >= 0; i--)
            {
                AddUnique(removedIds, idSelector(records[i]));
                records.RemoveAt(i);
            }
        }

        private static void RemoveRoads(
            WorldTilePartitionState partition,
            HashSet<RoadSourceKind> sourceKinds,
            WorldStateChangeSet changes)
        {
            if (partition?.roads == null)
                return;

            for (var i = partition.roads.Count - 1; i >= 0; i--)
            {
                var road = partition.roads[i];
                if (road == null || !sourceKinds.Contains(road.sourceKind))
                    continue;

                AddUnique(changes.RemovedEntityIds, road.id);
                partition.roads.RemoveAt(i);
            }
        }

        private List<WorldEntityId> RemoveLotsByDistrict(
            WorldState candidate,
            List<WorldEntityId> districtIds,
            WorldStateChangeSet changes)
        {
            var removedLotIds = new List<WorldEntityId>();
            if (districtIds == null || districtIds.Count == 0 || candidate?.tiles == null)
                return removedLotIds;

            for (var i = 0; i < candidate.tiles.Count; i++)
                RemoveLotsByDistrict(candidate.tiles[i], districtIds, removedLotIds, changes);
            return removedLotIds;
        }

        private static void RemoveLotsByDistrict(
            WorldTilePartitionState partition,
            List<WorldEntityId> districtIds,
            List<WorldEntityId> removedLotIds,
            WorldStateChangeSet changes)
        {
            if (partition?.lots == null)
                return;

            for (var i = partition.lots.Count - 1; i >= 0; i--)
            {
                var lot = partition.lots[i];
                if (lot == null || !ContainsId(districtIds, lot.districtId))
                    continue;

                AddUnique(removedLotIds, lot.id);
                AddUnique(changes.RemovedEntityIds, lot.id);
                partition.lots.RemoveAt(i);
            }
        }

        private void RemoveBuildingsByParents(
            WorldState candidate,
            List<WorldEntityId> districtIds,
            List<WorldEntityId> lotIds,
            WorldStateChangeSet changes)
        {
            if (candidate?.tiles == null)
                return;

            for (var i = 0; i < candidate.tiles.Count; i++)
                RemoveBuildingsByParents(candidate.tiles[i], districtIds, lotIds, changes);
        }

        private static void RemoveBuildingsByParents(
            WorldTilePartitionState partition,
            List<WorldEntityId> districtIds,
            List<WorldEntityId> lotIds,
            WorldStateChangeSet changes)
        {
            if (partition?.buildings == null)
                return;

            for (var i = partition.buildings.Count - 1; i >= 0; i--)
            {
                var building = partition.buildings[i];
                if (!ShouldRemoveBuilding(building, districtIds, lotIds))
                    continue;

                AddUnique(changes.RemovedEntityIds, building.id);
                partition.buildings.RemoveAt(i);
            }
        }

        private static bool ShouldRemoveBuilding(
            BuildingState building,
            List<WorldEntityId> districtIds,
            List<WorldEntityId> lotIds)
        {
            if (building == null)
                return false;

            return ContainsId(districtIds, building.districtId) ||
                ContainsId(lotIds, building.lotId);
        }

        private static void RemoveTerrainModificationIds(
            WorldTilePartitionState partition,
            WorldStateChangeSet changes)
        {
            var modifications = partition?.terrain?.modifications;
            if (modifications == null)
                return;

            AddTerrainModificationIds(modifications, changes.RemovedEntityIds);
            modifications.Clear();
        }

        private static void AddTerrainModificationIds(
            List<TerrainModificationState> modifications,
            List<WorldEntityId> ids)
        {
            if (modifications == null)
                return;

            for (var i = 0; i < modifications.Count; i++)
                AddUnique(ids, modifications[i]?.id ?? default);
        }

        private static HashSet<RoadSourceKind> CollectRoadSources(List<RoadState> roads)
        {
            var sourceKinds = new HashSet<RoadSourceKind>();
            for (var i = 0; i < roads.Count; i++)
                sourceKinds.Add(roads[i]?.sourceKind ?? RoadSourceKind.WorldPlanned);
            return sourceKinds;
        }

        private static void CopyIds(List<WorldEntityId> source, List<WorldEntityId> destination)
        {
            if (source == null)
                return;

            for (var i = 0; i < source.Count; i++)
                AddUnique(destination, source[i]);
        }

        private static bool ContainsId(List<WorldEntityId> ids, WorldEntityId id)
        {
            if (ids == null)
                return false;

            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }

        private static void AddUnique(List<WorldTileKey> items, WorldTileKey item)
        {
            var index = items.BinarySearch(item);
            if (index < 0)
                items.Insert(~index, item);
        }

        private static void AddUnique(List<WorldEntityId> items, WorldEntityId item)
        {
            if (item.kind == WorldEntityKind.None && string.IsNullOrEmpty(item.value))
                return;

            var index = items.BinarySearch(item);
            if (index < 0)
                items.Insert(~index, item);
        }

        private static Vector2 RoadAnchor(RoadState road)
        {
            if (road?.pointsXZ != null && road.pointsXZ.Count > 0)
                return road.pointsXZ[0];
            return default;
        }

        private static bool IsTileInHeader(WorldStateHeader header, WorldTileKey tile)
        {
            if (header == null || header.tilesPerSide <= 0)
                return false;

            return tile.x >= 0 &&
                tile.z >= 0 &&
                tile.x < header.tilesPerSide &&
                tile.z < header.tilesPerSide;
        }
    }
}
