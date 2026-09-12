#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.City;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private static int EnableProxySwapOnLoadedSceneBuilders()
        {
            var changed = 0;
            var builders = Object.FindObjectsByType<WorldStreamedCityBuilder>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < builders.Length; i++)
            {
                var builder = builders[i];
                if (builder == null)
                    continue;

                if (!StreamedCityProxySceneBuilderAdapter.TryEnableProxySwapDefaults(
                        builder,
                        DefaultSwapDistanceMeters,
                        out var dirty,
                        out var warning))
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                        Debug.LogWarning("[StreamedCityProxySwapTool] " + warning, builder);

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(warning))
                    Debug.LogWarning("[StreamedCityProxySwapTool] " + warning, builder);

                if (!dirty)
                    continue;

                EditorUtility.SetDirty(builder);
                EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
                changed++;
            }

            return changed;
        }
    }
}
#endif
