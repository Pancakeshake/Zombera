using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Characters.Work
{
    public sealed class LootWorkTaskProvider : IWorkTaskProvider
    {
        public void CollectAvailableTasks(List<WorkTaskDescriptor> buffer)
        {
            var containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            for (var i = 0; i < containers.Length; i++)
            {
                var container = containers[i];
                if (container == null || !container.HasLootRemaining()) continue;

                buffer.Add(new WorkTaskDescriptor(
                    BuildTaskId(container),
                    WorkJobType.Looting,
                    container.transform.position,
                    urgency: container.HasGeneratedLoot ? 1.5f : 1f,
                    sourceInstanceId: container.GetInstanceID()));
            }
        }

        public static string BuildTaskId(LootContainer container)
        {
            return container == null ? string.Empty : "loot:" + container.GetInstanceID();
        }

        public static LootContainer ResolveContainer(WorkTaskDescriptor task)
        {
            if (!task.IsValid || task.jobType != WorkJobType.Looting) return null;

            if (task.sourceInstanceId != 0)
            {
                var containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
                for (var i = 0; i < containers.Length; i++)
                {
                    if (containers[i] != null && containers[i].GetInstanceID() == task.sourceInstanceId)
                        return containers[i];
                }
            }

            if (!task.taskId.StartsWith("loot:", System.StringComparison.Ordinal)) return null;
            var idText = task.taskId.Substring("loot:".Length);
            if (!int.TryParse(idText, out var instanceId)) return null;

            var matches = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            for (var i = 0; i < matches.Length; i++)
            {
                if (matches[i] != null && matches[i].GetInstanceID() == instanceId)
                    return matches[i];
            }

            return null;
        }
    }
}
