using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zombera.EasyBuildAdapter;

namespace Zombera.Core
{
    /// <summary>
    ///     Clears Easy Build System save artifacts (PlayerPrefs keys, json files, runtime placed parts)
    ///     when starting a fresh session. Extracted from GameManager provisioning.
    /// </summary>
    internal static class EasyBuildSessionPersistence
    {
        /// <summary>Deletes all persisted Easy Build artifacts for the given scene names. Returns deleted artifact count.</summary>
        public static int ClearForScenes(IEnumerable<string> sceneNames)
        {
            var deletedArtifacts = 0;

            foreach (var sceneName in sceneNames)
                deletedArtifacts += DeleteSaveArtifactsForScene(sceneName);

            EasyBuildFacade.TryClearAllPlacedParts(includePreplaced: false);

            return deletedArtifacts;
        }

        private static int DeleteSaveArtifactsForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return 0;

            var deletedCount = 0;
            var trimmedSceneName = sceneName.Trim();

            var saveKey = $"EBS_Save_{trimmedSceneName}";
            var metaKey = $"EBS_Meta_{trimmedSceneName}";

            if (PlayerPrefs.HasKey(saveKey))
            {
                PlayerPrefs.DeleteKey(saveKey);
                deletedCount++;
            }

            if (PlayerPrefs.HasKey(metaKey))
            {
                PlayerPrefs.DeleteKey(metaKey);
                deletedCount++;
            }

            DeleteIfExists(Path.Combine(Application.persistentDataPath, $"building_save_{trimmedSceneName}.json"),
                ref deletedCount);
            DeleteIfExists(Path.Combine(Application.persistentDataPath, $"building_save_{trimmedSceneName}.meta"),
                ref deletedCount);
            DeleteIfExists(Path.Combine(Application.dataPath, $"building_save_{trimmedSceneName}.json"),
                ref deletedCount);
            DeleteIfExists(Path.Combine(Application.dataPath, $"building_save_{trimmedSceneName}.meta"),
                ref deletedCount);

            if (deletedCount > 0) PlayerPrefs.Save();

            return deletedCount;
        }

        private static void DeleteIfExists(string path, ref int deletedCount)
        {
            if (!File.Exists(path)) return;

            File.Delete(path);
            deletedCount++;
        }

    }
}
