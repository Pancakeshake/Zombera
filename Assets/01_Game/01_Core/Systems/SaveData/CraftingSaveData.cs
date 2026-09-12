using System;
using System.Collections.Generic;

namespace Zombera.Core
{
    [Serializable]
    public sealed class CraftingQueueEntrySaveData
    {
        public string queueEntryId;
        public string recipeId;
        public int quantityRemaining;
        public int totalQuantity;
        public string crafterUnitId;
        public string stationId;
        public double startedAtGameTime;
        public float progressSeconds;
        public float perItemDurationSeconds;
        public int status; // CraftingStatus enum cast to int
    }

    [Serializable]
    public sealed class CraftingSaveData
    {
        public List<CraftingQueueEntrySaveData> queueEntries = new();
    }
}
