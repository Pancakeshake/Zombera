using System.Collections.Generic;
using UnityEngine;
using Zombera.Building.Crafting;
using Zombera.Inventory.Crafting;

namespace Zombera.Characters.Work
{
    public sealed class CraftingWorkTaskProvider : IWorkTaskProvider
    {
        public void CollectAvailableTasks(List<WorkTaskDescriptor> buffer)
        {
            var service = CraftingService.Instance;
            if (service == null) return;

            var queue = service.Queue;
            for (var i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                if (entry == null) continue;
                if (entry.status is CraftingStatus.Complete or CraftingStatus.Failed or CraftingStatus.Cancelled)
                    continue;

                var registry = CraftingRecipeRegistry.Instance;
                if (registry == null) return;

                var recipe = registry.GetRecipeById(entry.recipeId);
                if (recipe == null || recipe.requiredStationType == CraftingStationType.None) continue;

                if (!TryResolveStationPosition(entry, recipe, out var stationPosition)) continue;

                var urgency = entry.status == CraftingStatus.Active ? 2f : 1f;
                var descriptor = new WorkTaskDescriptor(
                    BuildTaskId(entry),
                    WorkJobType.Crafting,
                    stationPosition,
                    urgency: urgency,
                    sourceInstanceId: entry.queueEntryId.GetHashCode());

                if (!string.IsNullOrWhiteSpace(entry.crafterUnitId))
                    descriptor.requiredUnitId = entry.crafterUnitId;

                buffer.Add(descriptor);
            }
        }

        public static string BuildTaskId(CraftingQueueEntry entry)
        {
            return entry == null || string.IsNullOrWhiteSpace(entry.queueEntryId)
                ? string.Empty
                : "craft:" + entry.queueEntryId;
        }

        public static CraftingQueueEntry ResolveEntry(WorkTaskDescriptor task)
        {
            if (!task.IsValid || task.jobType != WorkJobType.Crafting) return null;
            if (!task.taskId.StartsWith("craft:", System.StringComparison.Ordinal)) return null;

            var entryId = task.taskId.Substring("craft:".Length);
            var service = CraftingService.Instance;
            if (service == null) return null;

            var queue = service.Queue;
            for (var i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                if (entry != null && entry.queueEntryId == entryId) return entry;
            }

            return null;
        }

        private static bool TryResolveStationPosition(
            CraftingQueueEntry entry,
            CraftingRecipe recipe,
            out Vector3 stationPosition)
        {
            stationPosition = Vector3.zero;

            if (!string.IsNullOrWhiteSpace(entry.stationId))
            {
                for (var i = 0; i < CraftingStation.AllStations.Count; i++)
                {
                    var station = CraftingStation.AllStations[i];
                    if (station != null && station.stationId == entry.stationId)
                    {
                        stationPosition = station.transform.position;
                        return true;
                    }
                }
            }

            if (recipe.requiredStationType == CraftingStationType.None)
            {
                stationPosition = Vector3.zero;
                return true;
            }

            CraftingStation fallback = null;
            for (var i = 0; i < CraftingStation.AllStations.Count; i++)
            {
                var station = CraftingStation.AllStations[i];
                if (station == null || station.stationType != recipe.requiredStationType) continue;

                fallback = station;
                break;
            }

            if (fallback == null) return false;

            stationPosition = fallback.transform.position;
            return true;
        }
    }
}
