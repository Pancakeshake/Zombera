#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Zombera.Editor.StyleMatch
{
    internal static class StyleMatchCaptureRigSetup
    {
        private const string MenuPath = "Tools/World/Style Match/Ensure Scene Capture Camera";

        [MenuItem(MenuPath)]
        private static void MenuEnsureSceneCaptureCamera()
        {
            if (!StyleMatchLoopRunner.OpenWorldGenerationSceneForSetup())
            {
                EditorUtility.DisplayDialog(
                    "Style Match Camera",
                    "Open World Generation scene first:\n" +
                    StyleMatchLoopRunner.WorldGenerationScenePath,
                    "OK");
                return;
            }

            if (!StyleMatchCaptureRig.EnsureSceneRig())
            {
                EditorUtility.DisplayDialog(
                    "Style Match Camera",
                    "Failed to create capture rig. Check console for details.",
                    "OK");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog(
                "Style Match Camera",
                "StyleMatchCamera created under WorldGen/StyleMatchRig and aligned to terrain.",
                "OK");
        }
    }
}
#endif
