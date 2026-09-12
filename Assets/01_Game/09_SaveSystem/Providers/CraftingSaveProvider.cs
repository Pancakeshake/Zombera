using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory.Crafting;

namespace Zombera.Systems
{
    /// <summary>
    /// Handles persistence for the crafting system.
    /// </summary>
    public class CraftingSaveProvider : MonoBehaviour, ISaveProvider
    {
        public int Priority => 50;

        public void OnSave(GameSaveData saveData)
        {
            if (CraftingService.Instance == null) return;

            saveData.crafting.queueEntries.Clear();
            foreach (var entry in CraftingService.Instance.Queue)
            {
                saveData.crafting.queueEntries.Add(new CraftingQueueEntrySaveData
                {
                    queueEntryId = entry.queueEntryId,
                    recipeId = entry.recipeId,
                    quantityRemaining = entry.quantityRemaining,
                    totalQuantity = entry.totalQuantity,
                    crafterUnitId = entry.crafterUnitId,
                    stationId = entry.stationId,
                    startedAtGameTime = entry.startedAtGameTime,
                    progressSeconds = entry.progressSeconds,
                    perItemDurationSeconds = entry.perItemDurationSeconds,
                    status = (int)entry.status
                });
            }
        }

        public void OnLoad(GameSaveData saveData)
        {
            if (CraftingService.Instance == null) return;

            var newQueue = new List<CraftingQueueEntry>();
            foreach (var entryData in saveData.crafting.queueEntries)
            {
                // Note: Using constructor then manually setting fields to match saved state.
                var entry = new CraftingQueueEntry(entryData.recipeId, entryData.totalQuantity, entryData.perItemDurationSeconds, entryData.crafterUnitId, entryData.stationId)
                {
                    queueEntryId = entryData.queueEntryId,
                    quantityRemaining = entryData.quantityRemaining,
                    startedAtGameTime = entryData.startedAtGameTime,
                    progressSeconds = entryData.progressSeconds,
                    status = (CraftingStatus)entryData.status
                };
                newQueue.Add(entry);
            }

            // We use an internal method or direct assignment if accessible.
            // Since we are in the same assembly or it's public/internal, it should work.
            // In the previous step I added SetQueue to CraftingService.
            CraftingService.Instance.SetQueue(newQueue);
        }
    }
}
