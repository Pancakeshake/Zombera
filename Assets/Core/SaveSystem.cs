using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Serializes and restores reconstruction data for player, squad, world, and simulation objects.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour, IGameSystem
    {
        [SerializeField] private string saveFolderName = "Saves";

        private readonly Dictionary<string, GameSaveData> saveSlots = new Dictionary<string, GameSaveData>();

        public bool IsInitialized { get; private set; }
        public string CurrentSlotId { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;

            // TODO: Ensure save folder exists and load metadata index.
            // TODO: Migrate old save versions to current schema.
        }

        public void Shutdown()
        {
            IsInitialized = false;

            // TODO: Flush pending save requests before shutdown.
        }

        public void SaveGame(string slotId)
        {
            CurrentSlotId = slotId;
            GameSaveData saveData = BuildSaveData();
            SaveGameData(slotId, saveData);
        }

        public void LoadGame(string slotId)
        {
            CurrentSlotId = slotId;

            if (!TryLoadGameData(slotId, out GameSaveData saveData))
            {
                // TODO: Read save payload from disk and validate version/hash.
                saveData = new GameSaveData();
            }

            ApplySaveData(saveData);
        }

        public void SaveGameData(string slotId, GameSaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            CurrentSlotId = slotId;
            saveSlots[slotId] = CloneSaveData(saveData);

            // TODO: Serialize saveData to disk (JSON/binary/chunk files).
            // TODO: Add async pipeline and backup/rollback strategy.
            // TODO: Save index metadata and slot screenshots.
        }

        public bool TryLoadGameData(string slotId, out GameSaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                saveData = null;
                return false;
            }

            if (!saveSlots.TryGetValue(slotId, out GameSaveData cachedSave))
            {
                saveData = null;
                return false;
            }

            saveData = CloneSaveData(cachedSave);
            return true;
        }

        public GameSaveData BuildSaveData()
        {
            GameSaveData saveData = new GameSaveData();

            // TODO: Pull reconstruction-only data from all runtime systems.
            // TODO: Save player, squad, inventory, world chunks, zombies, bases, loot containers.

            return saveData;
        }

        public void ApplySaveData(GameSaveData saveData)
        {
            // TODO: Reconstruct runtime objects from saveData in deterministic order.
            // TODO: Resolve cross-object references (units, chunk entities, bases).
            _ = saveData;
        }

        private static GameSaveData CloneSaveData(GameSaveData source)
        {
            if (source == null)
            {
                return new GameSaveData();
            }

            string json = JsonUtility.ToJson(source);
            GameSaveData cloned = JsonUtility.FromJson<GameSaveData>(json);
            return cloned ?? new GameSaveData();
        }
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public PlayerSaveData Player = new PlayerSaveData();
        public List<SquadMemberSaveData> Squad = new List<SquadMemberSaveData>();
        public InventorySaveData Inventory = new InventorySaveData();
        public List<WorldChunkSaveData> WorldChunks = new List<WorldChunkSaveData>();
        public List<ZombieSaveData> Zombies = new List<ZombieSaveData>();
        public List<BaseSaveData> Bases = new List<BaseSaveData>();
        public List<LootContainerSaveData> LootContainers = new List<LootContainerSaveData>();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Health;
    }

    [Serializable]
    public sealed class SquadMemberSaveData
    {
        public string UnitId;
        public Vector3 Position;
        public float Health;
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public List<string> ItemIds = new List<string>();
        public List<int> Quantities = new List<int>();
        public float CurrentWeight;
    }

    [Serializable]
    public sealed class WorldChunkSaveData
    {
        public Vector2Int Coordinates;
        public int Seed;
        public string RegionId;
    }

    [Serializable]
    public sealed class ZombieSaveData
    {
        public string ZombieId;
        public Vector3 Position;
        public float Health;
        public string State;
    }

    [Serializable]
    public sealed class BaseSaveData
    {
        public string BaseId;
        public Vector3 Position;
        public string BuildingState;
    }

    [Serializable]
    public sealed class LootContainerSaveData
    {
        public string ContainerId;
        public Vector3 Position;
        public bool LootGenerated;
        public List<string> ItemIds = new List<string>();
        public List<int> Quantities = new List<int>();
    }
}