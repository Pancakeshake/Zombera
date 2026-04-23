using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombera.Editor
{
    [InitializeOnLoad]
    internal static class UmaCrossSceneReferenceGuard
    {
        static UmaCrossSceneReferenceGuard()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
        }

        [MenuItem("Tools/Zombera/UMA/Clear Cross-Scene UMA References (All Open Scenes)")]
        private static void ClearCrossSceneUmaReferencesAcrossOpenScenesMenu()
        {
            int cleared = ClearCrossSceneUmaReferencesAcrossOpenScenes(logSummary: false);
            EditorUtility.DisplayDialog(
                "Clear Cross-Scene UMA Refs",
                cleared > 0
                    ? $"Cleared {cleared} cross-scene UMA reference(s) across open scenes.\n\nSave scenes to persist changes."
                    : "No cross-scene UMA references found in open scenes.",
                "OK");
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            ClearCrossSceneUmaReferencesAcrossOpenScenes(logSummary: true);
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            _ = scene;
            _ = mode;
            ClearCrossSceneUmaReferencesAcrossOpenScenes(logSummary: false);
        }

        private static int ClearCrossSceneUmaReferencesAcrossOpenScenes(bool logSummary)
        {
            DynamicCharacterAvatar[] avatars = Object.FindObjectsByType<DynamicCharacterAvatar>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int cleared = 0;

            for (int i = 0; i < avatars.Length; i++)
            {
                DynamicCharacterAvatar avatar = avatars[i];
                if (avatar == null)
                {
                    continue;
                }

                Scene ownerScene = avatar.gameObject.scene;
                if (!ownerScene.IsValid())
                {
                    continue;
                }

                bool changed = false;

                if (avatar.context != null && avatar.context.gameObject.scene != ownerScene)
                {
                    avatar.context = null;
                    changed = true;
                    cleared++;
                }

                if (avatar.umaGenerator != null && avatar.umaGenerator.gameObject.scene != ownerScene)
                {
                    avatar.umaGenerator = null;
                    changed = true;
                    cleared++;
                }

                if (avatar.umaData != null &&
                    avatar.umaData.umaGenerator != null &&
                    avatar.umaData.umaGenerator.gameObject.scene != ownerScene)
                {
                    avatar.umaData.umaGenerator = null;
                    EditorUtility.SetDirty(avatar.umaData);
                    changed = true;
                    cleared++;
                }

                if (!changed)
                {
                    continue;
                }

                EditorUtility.SetDirty(avatar);
                EditorSceneManager.MarkSceneDirty(ownerScene);
            }

            if (logSummary && cleared > 0)
            {
                Debug.Log($"[UmaCrossSceneReferenceGuard] Cleared {cleared} cross-scene UMA reference(s) across open scenes.");
            }

            return cleared;
        }
    }
}
