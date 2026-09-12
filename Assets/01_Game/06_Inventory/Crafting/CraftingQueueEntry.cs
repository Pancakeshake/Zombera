using System;

namespace Zombera.Inventory.Crafting
{
    /// <summary>
    /// Represents the status of a crafting task in the queue.
    /// </summary>
    public enum CraftingStatus
    {
        Queued,
        Active,
        Paused,
        Complete,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Data object representing a single entry in the crafting queue.
    /// </summary>
    [Serializable]
    public class CraftingQueueEntry
    {
        public string queueEntryId;
        public string recipeId;
        public int quantityRemaining;
        public int totalQuantity;
        public string crafterUnitId; // Could be a unit name, ID, or "Player"
        public string stationId;
        public double startedAtGameTime;
        public float progressSeconds;
        public float perItemDurationSeconds;
        public CraftingStatus status;

        public CraftingQueueEntry(string recipeId, int quantity, float durationPerItem, string crafterId = null, string stationId = null)
        {
            this.queueEntryId = Guid.NewGuid().ToString();
            this.recipeId = recipeId;
            this.quantityRemaining = quantity;
            this.totalQuantity = quantity;
            this.crafterUnitId = crafterId;
            this.stationId = stationId;
            this.perItemDurationSeconds = durationPerItem;
            this.status = CraftingStatus.Queued;
        }
    }
}
