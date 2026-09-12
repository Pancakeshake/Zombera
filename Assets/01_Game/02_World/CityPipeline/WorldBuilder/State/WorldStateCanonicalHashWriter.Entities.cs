namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateCanonicalHashWriter
    {
        private void WriteRegion(RegionState record)
        {
            Require(record, "region");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteString(record.displayName);
            WriteInt32(record.generationSeed);
            WriteRect(record.boundsXZ);
        }

        private void WriteSettlement(SettlementState record)
        {
            Require(record, "settlement");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteEntityId(record.regionId);
            WriteString(record.displayName);
            WriteVector2(record.centerXZ);
            WriteVector2(record.halfExtentsMeters);
            WriteSingle(record.padHeightWorldY);
            WriteSingle(record.buildabilityScore);
            WriteInt32(record.layoutSeed);
        }

        private void WriteRoad(RoadState record)
        {
            Require(record, "road");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteInt32((int)record.sourceKind);
            WriteEntityId(record.regionId);
            WriteEntityId(record.settlementId);
            WriteInt32(record.sourceRoadId);
            WriteInt32((int)record.roadClass);
            WriteSingle(record.widthMeters);
            WriteBool(record.preserveWorldPath);
            WriteBool(record.curvedMarkers);
            WriteList(record.pointsXZ, WriteVector2);
        }

        private void WriteDistrict(DistrictState record)
        {
            Require(record, "district");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteEntityId(record.settlementId);
            WriteInt32(record.sourceAreaId);
            WriteString(record.displayName);
            WriteString(record.clusterName);
            WriteInt32((int)record.districtType);
            WriteInt32(record.gridX);
            WriteInt32(record.gridZ);
            WriteRect(record.boundsXZ);
            WriteVector2(record.centerXZ);
            WriteSingle(record.groundWorldY);
            WriteSingle(record.areaSquareMeters);
            WriteInt32((int)record.roundedCorners);
            WriteSingle(record.arterialCornerRadiusMeters);
            WriteList(record.outlineXZ, WriteVector2);
        }

        private void WriteLot(LotState record)
        {
            Require(record, "lot");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteEntityId(record.districtId);
            WriteInt32(record.sourceIndex);
            WriteRect(record.boundsXZ);
            WriteList(record.outlineXZ, WriteVector2);
            WriteSingle(record.groundWorldY);
            WriteInt32((int)record.streetFace);
            WriteInt32((int)record.commercialKind);
            WriteBool(record.isCornerLot);
            WriteBool(record.isCurvedLot);
        }

        private void WriteBuilding(BuildingState record)
        {
            Require(record, "building");
            WriteBuildingIdentity(record);
            WriteBuildingTransform(record);
            WriteSingle(record.condition01);
            WriteBool(record.abandoned);
            WriteInt64(record.abandonedAtHour);
            WriteBuildingOccupancy(record.occupancy);
            WriteBuildingOwnership(record.ownership);
            WriteBuildingUtilities(record.utilities);
            WriteBuildingDamage(record.damage);
            WriteBuildingFire(record.fire);
            WriteBuildingLoot(record.loot);
            WriteList(record.modifications, WriteBuildingModification);
            WriteList(record.modules, WriteBuildingModule);
        }

        private void WriteBuildingIdentity(BuildingState record)
        {
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteEntityId(record.settlementId);
            WriteEntityId(record.districtId);
            WriteEntityId(record.lotId);
            WriteString(record.archetypeId);
            WriteString(record.typeId);
            WriteInt32((int)record.districtType);
        }

        private void WriteBuildingTransform(BuildingState record)
        {
            WriteVector3(record.position);
            WriteQuaternion(record.rotation);
            WriteVector3(record.scale);
            WriteRect(record.footprintXZ);
            WriteInt32((int)record.streetFace);
            WriteBool(record.hasDoorAnchor);
            WriteVector3(record.doorAnchorWorld);
        }

        private void WritePoi(PoiState record)
        {
            Require(record, "poi");
            WriteEntityId(record.id);
            WriteString(record.sourceId);
            WriteEntityId(record.regionId);
            WriteString(record.archetypeId);
            WriteString(record.mapMarkerId);
            WriteVector2(record.positionXZ);
            WriteSingle(record.yawDegrees);
            WriteVector2(record.footprintMeters);
            WriteBool(record.discovered);
            WriteBool(record.depleted);
        }

        private void WriteTerrainModification(TerrainModificationState record)
        {
            Require(record, "terrain modification");
            WriteEntityId(record.id);
            WriteInt32((int)record.kind);
            WriteEntityId(record.sourceEntityId);
            WriteInt64(record.createdAtHour);
            WriteRect(record.boundsXZ);
            WriteList(record.outlineXZ, WriteVector2);
            WriteSingle(record.intensity01);
            WriteBool(record.active);
        }

        private void WriteEvent(WorldEventState record)
        {
            Require(record, "event");
            WriteEntityId(record.id);
            WriteInt64(record.sequence);
            WriteInt32((int)record.type);
            WriteEntityId(record.targetId);
            WriteInt64(record.scheduledHour);
            WriteInt64(record.resolvedHour);
            WriteSingle(record.magnitude);
            WriteInt32((int)record.status);
            WriteString(record.resultCode);
        }

        private static void Require(object record, string label)
        {
            if (record == null)
                throw new System.InvalidOperationException($"Cannot hash a null {label} record.");
        }
    }
}
