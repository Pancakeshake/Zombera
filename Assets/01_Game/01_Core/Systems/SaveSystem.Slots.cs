#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#endregion

namespace Zombera.Core
{
    public sealed partial class SaveSystem
    {
        public List<string> GetAvailableSlotIds()
        {
            var slotIds = new HashSet<string>();

            lock (_saveSlots)
            {
                foreach (var key in _saveSlots.Keys)
                {
                    if (!string.IsNullOrEmpty(key)) slotIds.Add(key);
                }
            }

            var saveFolderPath = Path.Combine(_persistentDataPath, saveFolderName);
            if (Directory.Exists(saveFolderPath))
            {
                try
                {
                    var files = Directory.GetFiles(saveFolderPath, "*.sav");
                    foreach (var file in files)
                    {
                        var id = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(id)) slotIds.Add(id);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to scan disk for saves: {e.Message}");
                }
            }

            return slotIds.ToList();
        }

        public string GetMostRecentSlotId()
        {
            var slots = GetAvailableSlotIds();
            if (slots.Count == 0) return string.Empty;

            string mostRecentSlot = string.Empty;
            var latestTime = DateTime.MinValue;

            foreach (var slotId in slots)
            {
                var path = GetSaveFilePath(slotId);
                if (!File.Exists(path))
                {
                    path += ".bak";
                    if (!File.Exists(path)) continue;
                }

                try
                {
                    var lastWrite = File.GetLastWriteTime(path);
                    if (lastWrite > latestTime)
                    {
                        latestTime = lastWrite;
                        mostRecentSlot = slotId;
                    }
                }
                catch
                {
                    // Ignore errors for individual files
                }
            }

            return mostRecentSlot;
        }

        public void DeleteSave(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return;

            lock (_saveSlots)
            {
                _saveSlots.Remove(slotId);
            }

            var path = GetSaveFilePath(slotId);
            var backupPath = path + ".bak";

            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to delete files for slot '{slotId}': {e.Message}");
            }

            UpdateMetadataIndex();
            SaveListChanged?.Invoke();
        }

        public void RenameSave(string slotId, string newName)
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(newName)) return;
            if (slotId == newName) return;

            GameSaveData data = null;
            lock (_saveSlots)
            {
                if (_saveSlots.TryGetValue(slotId, out data))
                {
                    if (data != null)
                    {
                        data.metadata.slotId = newName;
                        data.metadata.slotName = newName;
                    }

                    _saveSlots.Remove(slotId);
                    _saveSlots[newName] = data;
                }
            }

            if (data == null) return;

            var oldPath = GetSaveFilePath(slotId);
            var oldBackup = oldPath + ".bak";
            var newPath = GetSaveFilePath(newName);
            var newBackup = newPath + ".bak";

            try
            {
                if (File.Exists(oldPath)) File.Move(oldPath, newPath);
                if (File.Exists(oldBackup)) File.Move(oldBackup, newBackup);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to rename files from '{slotId}' to '{newName}': {e.Message}");
            }

            UpdateMetadataIndex();
            SaveListChanged?.Invoke();
        }

        public List<SaveMetadata> GetAllSlotMetadata()
        {
            var result = new List<SaveMetadata>();
            var slotIds = GetAvailableSlotIds();

            foreach (var id in slotIds)
            {
                GameSaveData data = null;

                lock (_saveSlots)
                {
                    if (_saveSlots.TryGetValue(id, out var cached) && cached != null)
                        data = cached;
                }

                if (data == null)
                    data = ReadSaveFromDisk(id);

                if (data?.metadata == null) continue;

                result.Add(new SaveMetadata
                {
                    slotId = id,
                    slotName = string.IsNullOrWhiteSpace(data.metadata.slotName)
                        ? id.ToUpperInvariant()
                        : data.metadata.slotName,
                    timestamp = data.metadata.timestamp,
                    playTimeSeconds = data.metadata.playTimeSeconds,
                    dayNumber = data.metadata.dayNumber,
                    locationName = data.metadata.locationName,
                    difficulty = data.metadata.difficulty,
                    progressPercent = data.metadata.progressPercent,
                    gameVersion = data.metadata.gameVersion,
                    screenshotBase64 = data.metadata.screenshotBase64,
                    recentActivity = data.metadata.recentActivity != null
                        ? new List<string>(data.metadata.recentActivity)
                        : new List<string>()
                });
            }

            return result;
        }
    }
}
