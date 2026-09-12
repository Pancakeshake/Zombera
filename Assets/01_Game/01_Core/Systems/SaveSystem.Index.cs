#region

using System;
using System.IO;
using System.Linq;
using UnityEngine;

#endregion

namespace Zombera.Core
{
    public sealed partial class SaveSystem
    {
        private void LoadMetadataIndex()
        {
            var indexPath = GetIndexFilePath();
            if (File.Exists(indexPath))
            {
                try
                {
                    var json = File.ReadAllText(indexPath);
                    var index = JsonUtility.FromJson<SaveMetadataIndex>(json);
                    if (index != null)
                    {
                        lock (_saveSlots)
                        {
                            foreach (var knownSlot in index.slotIds.Where(knownSlot => !string.IsNullOrWhiteSpace(knownSlot)))
                                _saveSlots.TryAdd(knownSlot, null);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Could not load metadata index: {e.Message}");
                }
            }

            var saveFolderPath = Path.Combine(_persistentDataPath, saveFolderName);
            if (!Directory.Exists(saveFolderPath)) return;

            try
            {
                var files = Directory.GetFiles(saveFolderPath, "*.sav");
                lock (_saveSlots)
                {
                    foreach (var file in files)
                    {
                        var id = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(id)) _saveSlots.TryAdd(id, null);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Failed to sync disk saves during init: {e.Message}");
            }
        }

        private void UpdateMetadataIndex()
        {
            lock (_indexLock)
            {
                try
                {
                    var index = new SaveMetadataIndex();

                    lock (_saveSlots)
                    {
                        index.slotIds.AddRange(_saveSlots.Keys.Where(key => !string.IsNullOrWhiteSpace(key)));
                    }

                    File.WriteAllText(GetIndexFilePath(), JsonUtility.ToJson(index));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Could not update metadata index: {e.Message}");
                }
            }
        }

        private string GetSaveFilePath(string slotId)
        {
            return Path.Combine(_persistentDataPath, saveFolderName, slotId + ".sav");
        }

        private string GetIndexFilePath()
        {
            return Path.Combine(_persistentDataPath, saveFolderName, "index.json");
        }

        private void EnsureSaveFolderExists()
        {
            if (string.IsNullOrWhiteSpace(saveFolderName)) return;

            var saveFolderPath = Path.Combine(_persistentDataPath, saveFolderName);
            Directory.CreateDirectory(saveFolderPath);
        }
    }
}
