using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        private static void ValidateTileContents(
            WorldState state,
            Dictionary<WorldEntityId, WorldEntityKind> ids,
            WorldValidationMode mode,
            WorldValidationContext context,
            WorldValidationReport report)
        {
            if (state.tiles == null)
                return;

            for (var i = 0; i < state.tiles.Count; i++)
            {
                var tile = state.tiles[i];
                if (tile == null)
                    continue;

                var path = TilePath(tile);
                if (mode == WorldValidationMode.Canonical)
                    ValidateCanonicalLists(tile, path, report);
                ValidateTerrain(tile.terrain, path + ".terrain", ids, report);
                ValidateRegions(tile.regions, path + ".regions", state.header, tile.key, report);
                ValidateSettlements(tile.settlements, path + ".settlements", ids, state.header, tile.key, report);
                ValidateRoads(tile.roads, path + ".roads", ids, state.header, tile.key, report);
                ValidateDistricts(tile.districts, path + ".districts", ids, state.header, tile.key, report);
                ValidateLots(tile.lots, path + ".lots", ids, state.header, tile.key, report);
                ValidateBuildings(tile.buildings, path + ".buildings", ids, state.header, tile.key, context, report);
                ValidatePois(tile.pois, path + ".pois", ids, state.header, tile.key, context, report);
            }
        }

        private static void ValidateCanonicalLists(WorldTilePartitionState tile, string path, WorldValidationReport report)
        {
            RequireSorted(tile.regions, record => record?.id ?? default, path + ".regions", report);
            RequireSorted(tile.settlements, record => record?.id ?? default, path + ".settlements", report);
            RequireSorted(tile.roads, record => record?.id ?? default, path + ".roads", report);
            RequireSorted(tile.districts, record => record?.id ?? default, path + ".districts", report);
            RequireSorted(tile.lots, record => record?.id ?? default, path + ".lots", report);
            RequireSorted(tile.buildings, record => record?.id ?? default, path + ".buildings", report);
            RequireSorted(tile.pois, record => record?.id ?? default, path + ".pois", report);
            RequireSorted(tile.terrain?.modifications, record => record?.id ?? default, path + ".terrain.modifications", report);
        }

        private static void ValidateRegions(List<RegionState> records, string path, WorldStateHeader header, WorldTileKey tile, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null)
                {
                    Add(report, HeaderInvalid, default, recordPath, "Region record is null.");
                    continue;
                }

                RequireFinite(record.boundsXZ, recordPath + ".boundsXZ", record.id, report);
                RequireOwnerTile(header, record.boundsXZ.center, tile, record.id, recordPath + ".boundsXZ", report);
            }
        }

        private static void ValidateSettlements(List<SettlementState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireReference(record.id, record.regionId, WorldEntityKind.Region, ids, recordPath + ".regionId", report);
                RequireFinite(record.centerXZ, recordPath + ".centerXZ", record.id, report);
                RequireFinite(record.halfExtentsMeters, recordPath + ".halfExtentsMeters", record.id, report);
                RequireFinite(record.padHeightWorldY, recordPath + ".padHeightWorldY", record.id, report);
                RequireOwnerTile(header, record.centerXZ, tile, record.id, recordPath + ".centerXZ", report);
                RequireRange01(record.buildabilityScore, record.id, recordPath + ".buildabilityScore", report);
                RequirePositive(record.halfExtentsMeters.x, record.id, recordPath + ".halfExtentsMeters.x", report);
                RequirePositive(record.halfExtentsMeters.y, record.id, recordPath + ".halfExtentsMeters.y", report);
            }
        }

        private static void ValidateRoads(List<RoadState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireOptionalReference(record.id, record.regionId, WorldEntityKind.Region, ids, recordPath + ".regionId", report);
                RequireOptionalReference(record.id, record.settlementId, WorldEntityKind.Settlement, ids, recordPath + ".settlementId", report);
                RequirePositive(record.widthMeters, record.id, recordPath + ".widthMeters", report);
                ValidateVectorList(record.pointsXZ, record.id, recordPath + ".pointsXZ", 2, report);
                if (record.pointsXZ != null && record.pointsXZ.Count > 0)
                    RequireOwnerTile(header, record.pointsXZ[0], tile, record.id, recordPath + ".pointsXZ[0]", report);
            }
        }

        private static void ValidateDistricts(List<DistrictState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireReference(record.id, record.settlementId, WorldEntityKind.Settlement, ids, recordPath + ".settlementId", report);
                RequireFinite(record.boundsXZ, recordPath + ".boundsXZ", record.id, report);
                RequireFinite(record.centerXZ, recordPath + ".centerXZ", record.id, report);
                RequireFinite(record.groundWorldY, recordPath + ".groundWorldY", record.id, report);
                RequireFinite(record.arterialCornerRadiusMeters, recordPath + ".arterialCornerRadiusMeters", record.id, report);
                RequireOwnerTile(header, record.centerXZ, tile, record.id, recordPath + ".centerXZ", report);
                RequirePositive(record.areaSquareMeters, record.id, recordPath + ".areaSquareMeters", report);
                ValidateVectorList(record.outlineXZ, record.id, recordPath + ".outlineXZ", 3, report);
            }
        }
    }
}
