using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zombera.Core
{
    [Serializable]
    public sealed class ZombieSaveData
    {
        [FormerlySerializedAs("ZombieId")] public string zombieId;
        [FormerlySerializedAs("Position")] public Vector3 position;
        [FormerlySerializedAs("Health")] public float health;
        [FormerlySerializedAs("State")] public string state;
        public string appearanceProfileJson = string.Empty;
    }

    [Serializable]
    public sealed class PlacedPieceSaveData
    {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
        public int skinIndex;
        public float health;
    }

    [Serializable]
    public sealed class LoosePickupSaveData
    {
        public string itemId;
        public int quantity;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public sealed class BaseSaveData
    {
        [FormerlySerializedAs("BaseId")] public string baseId;
        [FormerlySerializedAs("Position")] public Vector3 position;

        [FormerlySerializedAs("BuildingState")]
        public string buildingState;

        [FormerlySerializedAs("StorageItemIds")]
        public List<string> storageItemIds = new();

        [FormerlySerializedAs("StorageQuantities")]
        public List<int> storageQuantities = new();

        public List<PlacedPieceSaveData> placedPieces = new();
    }

    [Serializable]
    public sealed class LootContainerSaveData
    {
        [FormerlySerializedAs("ContainerId")] public string containerId;
        [FormerlySerializedAs("Position")] public Vector3 position;

        [FormerlySerializedAs("LootGenerated")]
        public bool lootGenerated;

        [FormerlySerializedAs("ItemIds")] public List<string> itemIds = new();
        [FormerlySerializedAs("Quantities")] public List<int> quantities = new();
    }
}
