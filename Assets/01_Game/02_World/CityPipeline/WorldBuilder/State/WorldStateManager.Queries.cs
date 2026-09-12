using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        public bool TryCopyBuilding(WorldEntityId id, out BuildingState building)
        {
            building = null;
            if (!TryFindBuilding(id, out var source))
                return false;

            building = WorldStateCloner.Clone(source);
            return true;
        }

        public void CopyBuildingsOwnedByTile(WorldTileKey tile, List<BuildingState> results)
        {
            if (results == null)
                return;

            results.Clear();
            var partition = GetPartition(tile);
            if (partition?.buildings == null)
                return;

            for (var i = 0; i < partition.buildings.Count; i++)
                results.Add(WorldStateCloner.Clone(partition.buildings[i]));
        }

        public void CopyEntityIdsCoveringTile(
            WorldTileKey tile,
            WorldEntityKind kind,
            List<WorldEntityId> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (!_index.TryGetCoveredEntityIds(tile, out var ids))
                return;

            for (var i = 0; i < ids.Count; i++)
            {
                if (ShouldCopyCoveredId(ids[i], kind))
                    results.Add(ids[i]);
            }
        }

        private bool TryFindBuilding(WorldEntityId id, out BuildingState building)
        {
            building = null;
            if (!TryGetEntityEntry(id, WorldEntityKind.Building, out var entry))
                return false;

            var partition = GetPartition(entry.OwnerTile);
            if (partition?.buildings == null)
                return false;

            if (entry.RecordIndex < 0 || entry.RecordIndex >= partition.buildings.Count)
                return false;

            building = partition.buildings[entry.RecordIndex];
            return building != null;
        }

        private bool TryGetEntityEntry(
            WorldEntityId id,
            WorldEntityKind expectedKind,
            out WorldStateEntityIndexEntry entry)
        {
            if (!_index.TryGetEntity(id, out entry))
                return false;

            return entry.Kind == expectedKind;
        }

        private WorldTilePartitionState GetPartition(WorldTileKey tile)
        {
            if (_state?.tiles == null)
                return null;

            if (!_index.TryGetPartitionIndex(tile, out var index))
                return null;

            return index >= 0 && index < _state.tiles.Count ? _state.tiles[index] : null;
        }

        private bool ShouldCopyCoveredId(WorldEntityId id, WorldEntityKind kind)
        {
            if (kind == WorldEntityKind.None)
                return true;

            return _index.TryGetEntity(id, out var entry) && entry.Kind == kind;
        }

        private void AddDerivedCoverage()
        {
            if (_state?.tiles == null || _state.header == null)
                return;

            for (var i = 0; i < _state.tiles.Count; i++)
                AddPartitionDerivedCoverage(_state.tiles[i]);
        }

        private void AddPartitionDerivedCoverage(WorldTilePartitionState partition)
        {
            if (partition == null)
                return;

            AddRectCoverage(partition.districts, record => record?.id ?? default, record => record?.boundsXZ ?? default);
            AddRectCoverage(partition.lots, record => record?.id ?? default, record => record?.boundsXZ ?? default);
            AddRectCoverage(partition.buildings, record => record?.id ?? default, record => record?.footprintXZ ?? default);
            AddRoadCoverage(partition.roads);
            AddPoiCoverage(partition.pois);
            AddTerrainModificationCoverage(partition.terrain?.modifications);
        }

        private void AddRectCoverage<T>(
            List<T> records,
            System.Func<T, WorldEntityId> idSelector,
            System.Func<T, Rect> boundsSelector)
        {
            if (records == null)
                return;

            var covered = new List<WorldTileKey>(4);
            for (var i = 0; i < records.Count; i++)
            {
                WorldTileOwnership.CopyCoveredTiles(_state.header, boundsSelector(records[i]), covered, true);
                AddCoverage(covered, idSelector(records[i]));
            }
        }

        private void AddRoadCoverage(List<RoadState> roads)
        {
            if (roads == null)
                return;

            for (var i = 0; i < roads.Count; i++)
                AddRoadCoverage(roads[i]);
        }

        private void AddRoadCoverage(RoadState road)
        {
            if (road?.pointsXZ == null)
                return;

            for (var i = 0; i < road.pointsXZ.Count; i++)
            {
                if (WorldTileOwnership.TryResolveOwnerTile(_state.header, road.pointsXZ[i], out var tile, true))
                    _index.AddCoverage(tile, road.id);
            }
        }

        private void AddPoiCoverage(List<PoiState> pois)
        {
            if (pois == null)
                return;

            for (var i = 0; i < pois.Count; i++)
            {
                var poi = pois[i];
                if (poi == null)
                    continue;

                if (WorldTileOwnership.TryResolveOwnerTile(_state.header, poi.positionXZ, out var tile, true))
                    _index.AddCoverage(tile, poi.id);
            }
        }

        private void AddTerrainModificationCoverage(List<TerrainModificationState> modifications)
        {
            AddRectCoverage(
                modifications,
                record => record?.id ?? default,
                record => record?.boundsXZ ?? default);
        }

        private void AddCoverage(List<WorldTileKey> tiles, WorldEntityId id)
        {
            for (var i = 0; i < tiles.Count; i++)
                _index.AddCoverage(tiles[i], id);
        }
    }
}
