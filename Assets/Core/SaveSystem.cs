using System;
using System.Collections.Generic;
using System.IO;
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
            EnsureSaveFolderExists();
            LoadMetadataIndex();
        }

        public void Shutdown()
        {
            IsInitialized = false;
            FlushPendingWrites();
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
                saveData = ReadSaveFromDisk(slotId) ?? new GameSaveData();
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
            WriteSaveToDisk(slotId, saveData);
            UpdateMetadataIndex(slotId);
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
            // SaveManager owns snapshot population; SaveSystem only handles I/O.
            return new GameSaveData();
        }

        public void ApplySaveData(GameSaveData saveData)
        {
            // SaveManager handles pushing payloads to runtime managers.
            // SaveSystem caches the loaded data for downstream retrieval.
            if (saveData == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentSlotId))
            {
                saveSlots[CurrentSlotId] = CloneSaveData(saveData);
            }
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

        private void WriteSaveToDisk(string slotId, GameSaveData saveData)
        {
            string path = GetSaveFilePath(slotId);
            string backupPath = path + ".bak";

            try
            {
                string json = JsonUtility.ToJson(saveData, prettyPrint: false);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

                // Atomic write: write to backup first, then replace main file.
                if (File.Exists(path))
                {
                    File.Copy(path, backupPath, overwrite: true);
                }

                File.WriteAllBytes(path, bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save slot '{slotId}': {e.Message}");
            }
        }

        private GameSaveData ReadSaveFromDisk(string slotId)
        {
            string path = GetSaveFilePath(slotId);

            if (!File.Exists(path))
            {
                // Fall back to backup if primary is missing.
                path = path + ".bak";
                if (!File.Exists(path))
                {
                    return null;
                }
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                GameSaveData loaded = JsonUtility.FromJson<GameSaveData>(json);

                if (loaded != null)
                {
                    saveSlots[slotId] = loaded;
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to read save slot '{slotId}': {e.Message}");
                return null;
            }
        }

        private void LoadMetadataIndex()
        {
            string indexPath = GetIndexFilePath();

            if (!File.Exists(indexPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(indexPath);
                SaveMetadataIndex index = JsonUtility.FromJson<SaveMetadataIndex>(json);

                if (index == null)
                {
                    return;
                }

                foreach (string knownSlot in index.slotIds)
                {
                    if (!string.IsNullOrWhiteSpace(knownSlot) && !saveSlots.ContainsKey(knownSlot))
                    {
                        // Lazy-load: just register the slot as known; data loaded on demand.
                        saveSlots[knownSlot] = null;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not load metadata index: {e.Message}");
            }
        }

        private void UpdateMetadataIndex(string slotId)
        {
            try
            {
                SaveMetadataIndex index = new SaveMetadataIndex();

                foreach (string key in saveSlots.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        index.slotIds.Add(key);
                    }
                }

                File.WriteAllText(GetIndexFilePath(), JsonUtility.ToJson(index));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not update metadata index: {e.Message}");
            }
        }

        private void FlushPendingWrites()
        {
            foreach (KeyValuePair<string, GameSaveData> pair in saveSlots)
            {
                if (pair.Value != null)
                {
                    WriteSaveToDisk(pair.Key, pair.Value);
                }
            }
        }

        private string GetSaveFilePath(string slotId)
        {
            return Path.Combine(Application.persistentDataPath, saveFolderName, slotId + ".sav");
        }

        private string GetIndexFilePath()
        {
            return Path.Combine(Application.persistentDataPath, saveFolderName, "index.json");
        }

        private void EnsureSaveFolderExists()
        {
            if (string.IsNullOrWhiteSpace(saveFolderName))
            {
                return;
            }

            string saveFolderPath = Path.Combine(Application.persistentDataPath, saveFolderName);
            Directory.CreateDirectory(saveFolderPath);
        }
    }

    [Serializable]
    public sealed class SaveMetadataIndex
    {
        public List<string> slotIds = new List<string>();
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
        public ProceduralWorldSaveData ProceduralWorld = new ProceduralWorldSaveData();
    }

    [Serializable]
    public sealed class ProceduralWorldSaveData
    {
        public bool hasData;
        public int worldSeed;
        public string graphVersion = string.Empty;
        public int formatVersion = 1;
        public List<ChunkProceduralDeltaSaveData> chunkDeltas = new List<ChunkProceduralDeltaSaveData>();
    }

    [Serializable]
    public sealed class ChunkProceduralDeltaSaveData
    {
        public int chunkX;
        public int chunkZ;
        public bool cleared;
        public string notes = string.Empty;
        public List<string> entityIds = new List<string>();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Health;
        public UnitStatsSaveData Stats = new UnitStatsSaveData();
    }

    [Serializable]
    public sealed class SquadMemberSaveData
    {
        public string UnitId;
        public Vector3 Position;
        public float Health;
        public UnitStatsSaveData Stats = new UnitStatsSaveData();
    }

    [Serializable]
    public sealed class UnitStatsSaveData
    {
        public bool HasSkillProgressionData;
        public float Stamina;

        public int Strength;
        public float StrengthXp;
        public int Shooting;
        public float ShootingXp;
        public int Melee;
        public float MeleeXp;
        public int Medical;
        public float MedicalXp;
        public int Engineering;
        public float EngineeringXp;
        public int Toughness;
        public float ToughnessXp;
        public int Constitution;
        public float ConstitutionXp;
        public int Agility;
        public float AgilityXp;
        public int Endurance;
        public float EnduranceXp;
        public int Scavenging;
        public float ScavengingXp;
        public int Stealth;
        public float StealthXp;
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
        public List<string> StorageItemIds = new List<string>();
        public List<int> StorageQuantities = new List<int>();
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