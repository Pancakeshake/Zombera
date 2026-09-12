namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateCanonicalHashWriter
    {
        private void WriteBuildingOccupancy(BuildingOccupancyState record)
        {
            Require(record, "building occupancy");
            WriteBool(record.occupied);
            WriteInt32(record.capacity);
            WriteInt32(record.currentCount);
        }

        private void WriteBuildingOwnership(BuildingOwnershipState record)
        {
            Require(record, "building ownership");
            WriteString(record.ownerTypeId);
            WriteString(record.ownerId);
        }

        private void WriteBuildingUtilities(BuildingUtilityState record)
        {
            Require(record, "building utilities");
            WriteInt32((int)record.power);
            WriteInt32((int)record.water);
        }

        private void WriteBuildingDamage(BuildingDamageState record)
        {
            Require(record, "building damage");
            WriteSingle(record.structuralDamage01);
            WriteSingle(record.fireDamage01);
            WriteBool(record.destroyed);
        }

        private void WriteBuildingFire(BuildingFireState record)
        {
            Require(record, "building fire");
            WriteBool(record.active);
            WriteInt64(record.startedAtHour);
            WriteSingle(record.intensity01);
        }

        private void WriteBuildingLoot(BuildingLootState record)
        {
            Require(record, "building loot");
            WriteBool(record.generated);
            WriteInt32(record.generationSeed);
            WriteBool(record.looted);
            WriteList(record.items, WriteItemStack);
        }

        private void WriteItemStack(WorldItemStackState record)
        {
            Require(record, "item stack");
            WriteString(record.itemId);
            WriteInt32(record.quantity);
        }

        private void WriteBuildingModification(BuildingModificationState record)
        {
            Require(record, "building modification");
            WriteString(record.modificationId);
            WriteString(record.typeId);
            WriteString(record.slotId);
            WriteBool(record.enabled);
            WriteVector3(record.localPosition);
            WriteQuaternion(record.localRotation);
            WriteVector3(record.localScale);
        }

        private void WriteBuildingModule(BuildingModuleState record)
        {
            Require(record, "building module");
            WriteString(record.moduleId);
            WriteString(record.typeId);
            WriteBool(record.enabled);
            WriteSingle(record.condition01);
        }
    }
}
