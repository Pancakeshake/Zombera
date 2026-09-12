#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Batch Animator inspector fixes for player/zombie prefabs (culling, root motion off).
    /// Runtime scripts also enforce defaults on play.
    /// </summary>
    public static class ZomberaAnimatorBatchTools
    {
        private const string MenuRoot = "Tools/Utilities/Animation/";

        [MenuItem(MenuRoot + "Apply Performance Defaults To Selected Animators", priority = -500)]
        private static void ApplyPerformanceDefaultsToSelection()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Animator batch",
                    "Select one or more GameObjects (prefab roots or scene objects).",
                    "OK");
                return;
            }

            var count = 0;
            foreach (var root in roots)
            {
                if (root == null) continue;

                var animators = root.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators)
                {
                    if (animator == null) continue;

                    Undo.RecordObject(animator, "Animator performance defaults");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullCompletely;
                    EditorUtility.SetDirty(animator);
                    count++;
                }
            }

            Debug.Log($"[ZomberaAnimatorBatchTools] Updated {count} Animator(s).");
        }
    }
}
#endif
