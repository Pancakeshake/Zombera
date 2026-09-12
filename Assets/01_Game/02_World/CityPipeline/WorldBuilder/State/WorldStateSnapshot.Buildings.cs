using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateSnapshot
    {
        private void AddBuildings(string path, List<BuildingState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
                AddBuilding(EntityPath(path, records[i].id), records[i]);
        }

        private void AddBuilding(string path, BuildingState record)
        {
            Add(path + ".id", record.id);
            Add(path + ".sourceId", record.sourceId);
            Add(path + ".settlementId", record.settlementId);
            Add(path + ".districtId", record.districtId);
            Add(path + ".lotId", record.lotId);
            Add(path + ".archetypeId", record.archetypeId);
            Add(path + ".typeId", record.typeId);
            Add(path + ".districtType", record.districtType.ToString());
            Add(path + ".position", record.position);
            Add(path + ".rotation", record.rotation);
            Add(path + ".scale", record.scale);
            Add(path + ".footprintXZ", record.footprintXZ);
            Add(path + ".streetFace", record.streetFace.ToString());
            Add(path + ".hasDoorAnchor", record.hasDoorAnchor);
            Add(path + ".doorAnchorWorld", record.doorAnchorWorld);
            Add(path + ".condition01", record.condition01);
            Add(path + ".abandoned", record.abandoned);
            Add(path + ".abandonedAtHour", record.abandonedAtHour);
            AddOccupancy(path + ".occupancy", record.occupancy);
            AddOwnership(path + ".ownership", record.ownership);
            AddUtilities(path + ".utilities", record.utilities);
            AddDamage(path + ".damage", record.damage);
            AddFire(path + ".fire", record.fire);
            AddLoot(path + ".loot", record.loot);
            AddModifications(path + ".modifications", record.modifications);
            AddModules(path + ".modules", record.modules);
        }

        private void AddOccupancy(string path, BuildingOccupancyState record)
        {
            Add(path + ".occupied", record.occupied);
            Add(path + ".capacity", record.capacity);
            Add(path + ".currentCount", record.currentCount);
        }

        private void AddOwnership(string path, BuildingOwnershipState record)
        {
            Add(path + ".ownerTypeId", record.ownerTypeId);
            Add(path + ".ownerId", record.ownerId);
        }

        private void AddUtilities(string path, BuildingUtilityState record)
        {
            Add(path + ".power", record.power.ToString());
            Add(path + ".water", record.water.ToString());
        }

        private void AddDamage(string path, BuildingDamageState record)
        {
            Add(path + ".structuralDamage01", record.structuralDamage01);
            Add(path + ".fireDamage01", record.fireDamage01);
            Add(path + ".destroyed", record.destroyed);
        }

        private void AddFire(string path, BuildingFireState record)
        {
            Add(path + ".active", record.active);
            Add(path + ".startedAtHour", record.startedAtHour);
            Add(path + ".intensity01", record.intensity01);
        }

        private void AddLoot(string path, BuildingLootState record)
        {
            Add(path + ".generated", record.generated);
            Add(path + ".generationSeed", record.generationSeed);
            Add(path + ".looted", record.looted);
            Add(path + ".items.count", record.items.Count);
            for (var i = 0; i < record.items.Count; i++)
            {
                var item = $"{path}.items[{i}]";
                Add(item + ".itemId", record.items[i].itemId);
                Add(item + ".quantity", record.items[i].quantity);
            }
        }

        private void AddModifications(string path, List<BuildingModificationState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var item = $"{path}[{i}]";
                Add(item + ".modificationId", records[i].modificationId);
                Add(item + ".typeId", records[i].typeId);
                Add(item + ".slotId", records[i].slotId);
                Add(item + ".enabled", records[i].enabled);
                Add(item + ".localPosition", records[i].localPosition);
                Add(item + ".localRotation", records[i].localRotation);
                Add(item + ".localScale", records[i].localScale);
            }
        }

        private void AddModules(string path, List<BuildingModuleState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var item = $"{path}[{records[i].moduleId}]";
                Add(item + ".moduleId", records[i].moduleId);
                Add(item + ".typeId", records[i].typeId);
                Add(item + ".enabled", records[i].enabled);
                Add(item + ".condition01", records[i].condition01);
            }
        }
    }
}
