#region

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.BuildingSystem;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Repeatable workflow for rendering build prefab sprites and storing a part-reference -> sprite library.
    ///     Menu:
    ///     - Tools/World/Building Icons/Open Or Create Icon Render Scene
    ///     - Tools/World/Building Icons/Setup Render Rig In Active Scene
    ///     - Tools/World/Building Icons/Capture Icon For Selected Prefab
    ///     - Tools/World/Building Icons/Batch Capture Icons (Selected Folder)
    ///     - Tools/World/Building Icons/Batch Capture Icons (Configured Folders)
    /// </summary>
    public static partial class BuildPrefabSpritePipelineTool
    {
        [MenuItem("Tools/World/Building Icons/Open Or Create Icon Render Scene", priority = -500)]
        public static void OpenOrCreateIconRenderScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolderRecursive(Path.GetDirectoryName(IconRenderScenePath)?.Replace("\\", "/"));

            var existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IconRenderScenePath);
            if (existingScene == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureRenderRigInActiveScene();
                EditorSceneManager.SaveScene(scene, IconRenderScenePath);
                AssetDatabase.Refresh();

                Debug.Log($"[BuildPrefabSpritePipelineTool] Created icon render scene at '{IconRenderScenePath}'.");
                return;
            }

            EditorSceneManager.OpenScene(IconRenderScenePath, OpenSceneMode.Single);
            EnsureRenderRigInActiveScene();
            Debug.Log($"[BuildPrefabSpritePipelineTool] Opened icon render scene '{IconRenderScenePath}'.");
        }

        [MenuItem("Tools/World/Building Icons/Setup Render Rig In Active Scene", priority = -500)]
        public static void SetupRenderRigInActiveScene()
        {
            EnsureRenderRigInActiveScene();
        }

        [MenuItem("Tools/World/Building Icons/Capture Icon For Selected Prefab", priority = -500)]
        public static void CaptureIconForSelectedPrefab()
        {
            var selectedPrefabs = GetSelectedPrefabAssets();
            if (selectedPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Capture Build Prefab Icon",
                    "Select a Prefab asset in the Project window first.",
                    "OK");
                return;
            }

            var prefab = selectedPrefabs[0];
            var rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            var partReference = ResolvePartReference(prefab);
            var outputPath = BuildOutputPath(prefab.name, partReference);
            var success = CapturePrefabIcon(prefab, outputPath, rig, out var error);

            if (!success)
            {
                EditorUtility.DisplayDialog(
                    "Capture Build Prefab Icon",
                    $"Failed to render icon for '{prefab.name}'.\n\n{error}",
                    "OK");
                return;
            }

            AssetDatabase.ImportAsset(outputPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);

            if (iconSprite != null)
            {
                Selection.activeObject = iconSprite;
                EditorGUIUtility.PingObject(iconSprite);
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            CreateOrUpdateSpriteLibrary(new System.Collections.Generic.List<BuildPrefabSpriteEntry>
            {
                new()
                {
                    partReference = partReference,
                    sprite = iconSprite,
                    prefabAssetPath = prefabPath
                }
            }, mergeWithExisting: true);

            Debug.Log($"[BuildPrefabSpritePipelineTool] Captured icon for '{prefab.name}' ({partReference}) -> {outputPath}");
        }

        [MenuItem("Tools/World/Building Icons/Batch Capture Icons (Selected Folder)", priority = -500)]
        public static void BatchCaptureIconsFromSelectedFolder()
        {
            if (!TryGetSelectedFolderPath(out var folderPath))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Build Prefab Icons",
                    "Select a folder in the Project window that contains build prefabs.",
                    "OK");
                return;
            }

            BatchCaptureFromFolders(new[] { folderPath }, "Batch Capture Build Prefab Icons");
        }

        [MenuItem("Tools/World/Building Icons/Batch Capture Icons (Configured Folders)", priority = -500)]
        public static void BatchCaptureIconsFromConfiguredFolders()
        {
            BatchCaptureFromFolders(DefaultPrefabFolders, "Batch Capture Build Prefab Icons (Configured Folders)");
        }
    }
}
