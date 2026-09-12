#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Serializes and restores reconstruction data for player, squad, world, and simulation objects.
    /// </summary>
    public sealed partial class SaveSystem : MonoBehaviour, IGameSystem
    {
        [SerializeField] private string saveFolderName = "Saves";

        private readonly Dictionary<string, GameSaveData> _saveSlots = new();
        private readonly Dictionary<string, Task> _activeSlotWrites = new();
        private readonly object _writeLock = new();
        private readonly object _indexLock = new();
        private string _persistentDataPath;

        private string CurrentSlotId { get; set; }
        public event Action SaveListChanged;

        public bool IsInitialized { get; private set; }

        public bool IsSaving(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return false;
            lock (_writeLock)
            {
                return _activeSlotWrites.TryGetValue(slotId, out var task) && !task.IsCompleted;
            }
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_persistentDataPath))
                _persistentDataPath = Application.persistentDataPath;

            IsInitialized = true;
            EnsureSaveFolderExists();
            LoadMetadataIndex();
        }

        public void Shutdown()
        {
            IsInitialized = false;

            Task[] activeTasks;
            lock (_writeLock)
            {
                activeTasks = _activeSlotWrites.Values.ToArray();
            }

            if (activeTasks.Length > 0)
            {
                Debug.Log($"[SaveSystem] Waiting for {activeTasks.Length} active writes to complete during shutdown...");
                Task.WaitAll(activeTasks);
            }

            FlushPendingWrites();
        }

        public void LoadGame(string slotId)
        {
            CurrentSlotId = slotId;

            if (!TryLoadGameData(slotId, out var saveData))
                saveData = ReadSaveFromDisk(slotId) ?? BuildSaveData();

            ApplySaveData(saveData);
        }

        public void ApplySaveData(GameSaveData saveData)
        {
            // SaveManager handles pushing payloads to runtime managers.
            // SaveSystem caches the loaded data for downstream retrieval.
            if (saveData == null) return;

            if (!string.IsNullOrWhiteSpace(CurrentSlotId))
            {
                var cloned = CloneSaveData(saveData);
                lock (_saveSlots)
                {
                    _saveSlots[CurrentSlotId] = cloned;
                }
            }
        }
    }
}
