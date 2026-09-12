using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        private static void ValidateLoot(BuildingLootState loot, WorldEntityId ownerId, string path, WorldValidationReport report)
        {
            if (loot?.items == null)
                return;

            for (var i = 0; i < loot.items.Count; i++)
            {
                var item = loot.items[i];
                var itemPath = $"{path}.items[{i}]";
                if (item == null)
                {
                    Add(report, HeaderInvalid, ownerId, itemPath, "Loot item is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.itemId))
                    Add(report, IdInvalid, ownerId, itemPath + ".itemId", "Loot item id is required.");
                if (item.quantity <= 0)
                    Add(report, HealthInvalid, ownerId, itemPath + ".quantity", "Loot item quantity must be positive.", "> 0", item.quantity.ToString());
            }
        }

        private static void ValidateBuildingModifications(List<BuildingModificationState> records, WorldEntityId ownerId, string path, WorldValidationReport report)
        {
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = $"{path}[{i}]";
                if (record == null)
                {
                    Add(report, HeaderInvalid, ownerId, recordPath, "Building modification is null.");
                    continue;
                }

                RequireFinite(record.localPosition, recordPath + ".localPosition", ownerId, report);
                RequireFinite(record.localRotation, recordPath + ".localRotation", ownerId, report);
                RequireFinite(record.localScale, recordPath + ".localScale", ownerId, report);
                RequirePositive(record.localScale.x, ownerId, recordPath + ".localScale.x", report);
                RequirePositive(record.localScale.y, ownerId, recordPath + ".localScale.y", report);
                RequirePositive(record.localScale.z, ownerId, recordPath + ".localScale.z", report);
            }
        }

        private static void ValidateBuildingModules(List<BuildingModuleState> records, WorldEntityId ownerId, string path, WorldValidationReport report)
        {
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = $"{path}[{i}]";
                if (record == null)
                {
                    Add(report, HeaderInvalid, ownerId, recordPath, "Building module is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.moduleId))
                    Add(report, IdInvalid, ownerId, recordPath + ".moduleId", "Building module id is required.");
                RequireRange01(record.condition01, ownerId, recordPath + ".condition01", report);
            }
        }

        private static int CompareEvents(WorldEventState left, WorldEventState right)
        {
            var hour = left.scheduledHour.CompareTo(right.scheduledHour);
            if (hour != 0)
                return hour;

            var sequence = left.sequence.CompareTo(right.sequence);
            return sequence != 0 ? sequence : left.id.CompareTo(right.id);
        }
    }
}
