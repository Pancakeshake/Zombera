#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Adds <c>SnapSocket_Left</c> and <c>SnapSocket_Right</c> children to
    ///     fence prefabs so the city-gen fence placer can measure piece length
    ///     and chain them correctly along lot edges.
    /// </summary>
    public static class FenceSocketTool
    {
        private const string FencesFolder = "Assets/02_Shared/Prefabs/Fences";

        [MenuItem(MenuPaths.WorldFences + "Add Snap Sockets to Fence Prefabs", priority = -1000)]
        private static void AddSnapSocketsToFencePrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { FencesFolder });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[FenceSocketTool] No prefabs found in Fences folder.");
                return;
            }

            var processed = 0;
            var skipped   = 0;
            var updated   = 0;

            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path       = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var prefabName = Path.GetFileNameWithoutExtension(path);

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Adding Snap Sockets to Fence Prefabs",
                        prefabName,
                        (float)i / guids.Length))
                    {
                        Debug.Log($"[FenceSocketTool] Cancelled after {processed} of {guids.Length} prefabs.");
                        break;
                    }

                    var prefabContents = PrefabUtility.LoadPrefabContents(path);
                    if (prefabContents == null)
                    {
                        Debug.LogWarning($"[FenceSocketTool] Could not load: {path}");
                        skipped++;
                        continue;
                    }

                    var left  = prefabContents.transform.Find("SnapSocket_Left");
                    var right = prefabContents.transform.Find("SnapSocket_Right");

                    if (left != null && right != null)
                    {
                        Debug.Log($"[FenceSocketTool] Already has sockets: {prefabName}");
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                        skipped++;
                        continue;
                    }

                    // Compute left / right X extents from renderer bounds
                    ComputeLocalXExtents(prefabContents, out var leftX, out var rightX);

                    // Add or update left socket
                    if (left == null)
                    {
                        left = new GameObject("SnapSocket_Left").transform;
                        left.SetParent(prefabContents.transform, false);
                    }
                    left.localPosition = new Vector3(leftX, 0f, 0f);

                    // Add or update right socket
                    if (right == null)
                    {
                        right = new GameObject("SnapSocket_Right").transform;
                        right.SetParent(prefabContents.transform, false);
                    }
                    right.localPosition = new Vector3(rightX, 0f, 0f);

                    var savedOk = PrefabUtility.SaveAsPrefabAsset(prefabContents, path, out var success);
                    PrefabUtility.UnloadPrefabContents(prefabContents);

                    if (success && savedOk != null)
                    {
                        updated++;
                        Debug.Log($"[FenceSocketTool] Added sockets: {prefabName} (span={rightX - leftX:F2})");
                    }
                    else
                    {
                        Debug.LogError($"[FenceSocketTool] Failed to save: {path}");
                    }

                    processed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[FenceSocketTool] Done — {updated} updated, {skipped} skipped, " +
                $"{processed} processed of {guids.Length} total.");
        }

        /// <summary>
        ///     Computes the min and max local-X extents of a prefab from its
        ///     renderer bounds.  Falls back to ±0.5 when no renderers exist.
        /// </summary>
        private static void ComputeLocalXExtents(GameObject prefabRoot, out float leftX, out float rightX)
        {
            var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    $"[FenceSocketTool] No renderers on {prefabRoot.name}, using default span.");
                leftX  = -0.5f;
                rightX =  0.5f;
                return;
            }

            var bounds = renderers[0].bounds;
            for (var r = 1; r < renderers.Length; r++)
                bounds.Encapsulate(renderers[r].bounds);

            leftX  = bounds.min.x;
            rightX = bounds.max.x;
        }
    }
}
#endif
