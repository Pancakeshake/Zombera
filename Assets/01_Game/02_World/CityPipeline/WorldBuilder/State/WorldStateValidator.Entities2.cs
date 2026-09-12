using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        private static void ValidateLots(List<LotState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireReference(record.id, record.districtId, WorldEntityKind.District, ids, recordPath + ".districtId", report);
                RequireFinite(record.boundsXZ, recordPath + ".boundsXZ", record.id, report);
                RequireFinite(record.groundWorldY, recordPath + ".groundWorldY", record.id, report);
                RequireOwnerTile(header, record.boundsXZ.center, tile, record.id, recordPath + ".boundsXZ", report);
                ValidateVectorList(record.outlineXZ, record.id, recordPath + ".outlineXZ", 3, report);
            }
        }

        private static void ValidateBuildings(List<BuildingState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationContext context, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireOptionalReference(record.id, record.settlementId, WorldEntityKind.Settlement, ids, recordPath + ".settlementId", report);
                RequireOptionalReference(record.id, record.districtId, WorldEntityKind.District, ids, recordPath + ".districtId", report);
                RequireOptionalReference(record.id, record.lotId, WorldEntityKind.Lot, ids, recordPath + ".lotId", report);
                RequireKnownArchetype(record.id, record.archetypeId, context, true, recordPath + ".archetypeId", report);
                ValidateBuildingGeometry(record, header, tile, recordPath, report);
                ValidateBuildingHealth(record, recordPath, report);
                ValidateBuildingNested(record, recordPath, report);
            }
        }

        private static void ValidatePois(List<PoiState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldStateHeader header, WorldTileKey tile, WorldValidationContext context, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireReference(record.id, record.regionId, WorldEntityKind.Region, ids, recordPath + ".regionId", report);
                RequireKnownArchetype(record.id, record.archetypeId, context, false, recordPath + ".archetypeId", report);
                RequireFinite(record.positionXZ, recordPath + ".positionXZ", record.id, report);
                RequireFinite(record.yawDegrees, recordPath + ".yawDegrees", record.id, report);
                RequireFinite(record.footprintMeters, recordPath + ".footprintMeters", record.id, report);
                RequireOwnerTile(header, record.positionXZ, tile, record.id, recordPath + ".positionXZ", report);
                RequirePositive(record.footprintMeters.x, record.id, recordPath + ".footprintMeters.x", report);
                RequirePositive(record.footprintMeters.y, record.id, recordPath + ".footprintMeters.y", report);
            }
        }

        private static void ValidateTerrain(TerrainChunkState terrain, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (terrain == null)
                return;

            RequireFinite(terrain.seaLevelWorldY, path + ".seaLevelWorldY", default, report);
            RequireFinite(terrain.terrainBaseWorldY, path + ".terrainBaseWorldY", default, report);
            RequirePositive(terrain.verticalSizeMeters, default, path + ".verticalSizeMeters", report);
            ValidateTerrainModifications(terrain.modifications, path + ".modifications", ids, report);
        }

        private static void ValidateTerrainModifications(List<TerrainModificationState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationReport report)
        {
            if (records == null) return;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null) continue;
                RequireOptionalReference(record.id, record.sourceEntityId, WorldEntityKind.None, ids, recordPath + ".sourceEntityId", report);
                RequireFinite(record.boundsXZ, recordPath + ".boundsXZ", record.id, report);
                ValidateVectorList(record.outlineXZ, record.id, recordPath + ".outlineXZ", 3, report);
                RequireRange01(record.intensity01, record.id, recordPath + ".intensity01", report);
            }
        }

        private static void ValidateBuildingGeometry(BuildingState record, WorldStateHeader header, WorldTileKey tile, string path, WorldValidationReport report)
        {
            RequireFinite(record.position, path + ".position", record.id, report);
            RequireFinite(record.rotation, path + ".rotation", record.id, report);
            RequireFinite(record.scale, path + ".scale", record.id, report);
            RequireFinite(record.footprintXZ, path + ".footprintXZ", record.id, report);
            RequireOwnerTile(header, record.position, tile, record.id, path + ".position", report);
            RequirePositive(record.scale.x, record.id, path + ".scale.x", report);
            RequirePositive(record.scale.y, record.id, path + ".scale.y", report);
            RequirePositive(record.scale.z, record.id, path + ".scale.z", report);
        }

        private static void ValidateBuildingHealth(BuildingState record, string path, WorldValidationReport report)
        {
            RequireRange01(record.condition01, record.id, path + ".condition01", report);
            RequireRange01(record.damage?.structuralDamage01 ?? 0f, record.id, path + ".damage.structuralDamage01", report);
            RequireRange01(record.damage?.fireDamage01 ?? 0f, record.id, path + ".damage.fireDamage01", report);
            RequireRange01(record.fire?.intensity01 ?? 0f, record.id, path + ".fire.intensity01", report);
            if (record.occupancy != null && (record.occupancy.capacity < 0 || record.occupancy.currentCount < 0 || record.occupancy.currentCount > record.occupancy.capacity))
                Add(report, HealthInvalid, record.id, path + ".occupancy", "Occupancy counts must be non-negative and currentCount must not exceed capacity.");
        }

        private static void ValidateBuildingNested(BuildingState record, string path, WorldValidationReport report)
        {
            RequireFinite(record.doorAnchorWorld, path + ".doorAnchorWorld", record.id, report);
            ValidateLoot(record.loot, record.id, path + ".loot", report);
            ValidateBuildingModifications(record.modifications, record.id, path + ".modifications", report);
            ValidateBuildingModules(record.modules, record.id, path + ".modules", report);
        }
    }
}
