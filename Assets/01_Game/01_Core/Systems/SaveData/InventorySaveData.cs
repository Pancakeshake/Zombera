using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Zombera.Core
{
    [Serializable]
    public sealed class ItemInstanceSaveData
    {
        public string instanceId;
        public string itemId;
        public int quantity;
        public float durability = 1f;
        public string customDataJson = string.Empty;
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        [FormerlySerializedAs("ItemIds")] public List<string> itemIds = new();
        [FormerlySerializedAs("Quantities")] public List<int> quantities = new();

        [FormerlySerializedAs("CurrentWeight")]
        public float currentWeight;

        public List<ItemInstanceSaveData> itemInstances = new();
    }
}
