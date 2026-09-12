#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace Zombera.Core
{
    public sealed partial class SaveSystem
    {
        public void SaveGameData(string slotId, GameSaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return;

            EnsureSaveFolderExists();

            CurrentSlotId = slotId;
            var cloned = CloneSaveData(saveData);

            lock (_saveSlots)
            {
                _saveSlots[slotId] = cloned;
            }

            byte[] bytes;
            try
            {
                bytes = SaveFileCodec.Serialize(cloned);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Serialization failed for slot '{slotId}': {e.Message}");
                return;
            }

            var path = GetSaveFilePath(slotId);

            lock (_writeLock)
            {
                var previousTask = _activeSlotWrites.GetValueOrDefault(slotId);

                var newTask = Task.Run(async () =>
                {
                    if (previousTask != null)
                    {
                        try { await previousTask; } catch { /* Continue on previous failure */ }
                    }

                    var success = false;
                    try
                    {
                        SaveFileCodec.TryWriteAtomic(path, bytes);
                        Debug.Log($"[SaveSystem] Successfully committed slot '{slotId}' to disk.");
                        success = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SaveSystem] Atomic background write failed for slot '{slotId}': {e.Message}");
                    }

                    if (success)
                        UpdateMetadataIndex();

                    return success;
                });

                _activeSlotWrites[slotId] = newTask;

                newTask.ContinueWith(t =>
                {
                    lock (_writeLock)
                    {
                        if (_activeSlotWrites.TryGetValue(slotId, out var current) && current == newTask)
                            _activeSlotWrites.Remove(slotId);
                    }

                    if (t.IsCompletedSuccessfully && t.Result)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.delayCall += () => SaveListChanged?.Invoke();
#else
                        SaveListChanged?.Invoke();
#endif
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public bool TryLoadGameData(string slotId, out GameSaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                saveData = null;
                return false;
            }

            GameSaveData cachedSave;
            lock (_saveSlots)
            {
                _saveSlots.TryGetValue(slotId, out cachedSave);
            }

            if (cachedSave == null)
            {
                cachedSave = ReadSaveFromDisk(slotId);
                if (cachedSave != null)
                {
                    lock (_saveSlots)
                    {
                        _saveSlots[slotId] = cachedSave;
                    }
                }
            }

            if (cachedSave == null)
            {
                saveData = null;
                return false;
            }

            saveData = CloneSaveData(cachedSave);
            return true;
        }

        private static GameSaveData BuildSaveData()
        {
            // SaveManager owns snapshot population; SaveSystem only handles I/O.
            return new GameSaveData();
        }

        private static GameSaveData CloneSaveData(GameSaveData source)
        {
            if (source == null) return new GameSaveData();

            var json = JsonUtility.ToJson(source);
            var cloned = JsonUtility.FromJson<GameSaveData>(json);
            return cloned ?? new GameSaveData();
        }

        private void WriteSaveToDisk(string slotId, GameSaveData saveData)
        {
            var path = GetSaveFilePath(slotId);

            try
            {
                SaveFileCodec.TryWriteAtomic(path, SaveFileCodec.Serialize(saveData));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save slot '{slotId}': {e.Message}");
            }
        }

        private GameSaveData ReadSaveFromDisk(string slotId)
        {
            var path = GetSaveFilePath(slotId);

            if (!File.Exists(path))
            {
                path += ".bak";
                if (!File.Exists(path)) return null;
            }

            try
            {
                var json = Encoding.UTF8.GetString(File.ReadAllBytes(path));
                var loaded = SaveFileCodec.Deserialize(json);

                if (loaded != null)
                {
                    lock (_saveSlots)
                    {
                        _saveSlots[slotId] = loaded;
                    }
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to read save slot '{slotId}': {e.Message}");
                return null;
            }
        }

        private void FlushPendingWrites()
        {
            KeyValuePair<string, GameSaveData>[] pending;
            lock (_saveSlots)
            {
                pending = _saveSlots.Where(pair => pair.Value != null).ToArray();
            }

            foreach (var pair in pending)
                WriteSaveToDisk(pair.Key, pair.Value);
        }
    }
}
