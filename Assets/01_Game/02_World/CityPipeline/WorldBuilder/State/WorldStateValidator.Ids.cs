using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        private static Dictionary<WorldEntityId, WorldEntityKind> CollectIds(
            WorldState state,
            WorldValidationReport report)
        {
            var ids = new Dictionary<WorldEntityId, WorldEntityKind>();
            CollectTileIds(state.tiles, ids, report);
            AddEventIds(state.pendingEvents, "$.pendingEvents", ids, report);
            AddEventIds(state.eventHistory, "$.eventHistory", ids, report);
            return ids;
        }

        private static void CollectTileIds(
            List<WorldTilePartitionState> tiles,
            Dictionary<WorldEntityId, WorldEntityKind> ids,
            WorldValidationReport report)
        {
            if (tiles == null)
                return;

            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null)
                    continue;

                var path = TilePath(tile);
                AddRegionIds(tile.regions, path + ".regions", ids, report);
                AddSettlementIds(tile.settlements, path + ".settlements", ids, report);
                AddRoadIds(tile.roads, path + ".roads", ids, report);
                AddDistrictIds(tile.districts, path + ".districts", ids, report);
                AddLotIds(tile.lots, path + ".lots", ids, report);
                AddBuildingIds(tile.buildings, path + ".buildings", ids, report);
                AddPoiIds(tile.pois, path + ".pois", ids, report);
                AddTerrainModificationIds(tile.terrain?.modifications, path + ".terrain.modifications", ids, report);
            }
        }

        private static void AddRegionIds(List<RegionState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Region, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddSettlementIds(List<SettlementState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Settlement, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddRoadIds(List<RoadState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Road, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddDistrictIds(List<DistrictState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.District, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddLotIds(List<LotState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Lot, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddBuildingIds(List<BuildingState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Building, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddPoiIds(List<PoiState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Poi, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddTerrainModificationIds(List<TerrainModificationState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.TerrainModification, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddEventIds(List<WorldEventState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
                AddId(records[i]?.id ?? default, WorldEntityKind.Event, IdPath(path, records[i]?.id ?? default, i), ids, report);
        }

        private static void AddId(
            WorldEntityId id,
            WorldEntityKind expectedKind,
            string path,
            Dictionary<WorldEntityId, WorldEntityKind> ids,
            WorldValidationReport report)
        {
            if (IsEmpty(id) || string.IsNullOrWhiteSpace(id.value) || id.kind == WorldEntityKind.None)
            {
                Add(report, IdInvalid, id, path + ".id", "Entity id must include a typed kind and non-empty value.");
                return;
            }

            if (id.kind != expectedKind)
                Add(report, IdKindMismatch, id, path + ".id.kind", "Entity id kind does not match record list.", expectedKind.ToString(), id.kind.ToString());

            if (ids.ContainsKey(id))
                Add(report, IdDuplicate, id, path + ".id", "Duplicate world entity id.", "unique id", id.ToString());
            else
                ids.Add(id, expectedKind);
        }
    }
}
