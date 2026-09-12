#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Repeatable workflow for rendering inventory item icons from prefabs.
    ///     Menu:
    ///     - Tools/Items/Inventory Icons/Open Or Create Icon Render Scene
    ///     - Tools/Items/Inventory Icons/Setup Render Rig In Active Scene
    ///     - Tools/Items/Inventory Icons/Capture Icon For Selected Prefab
    ///     - Tools/Items/Inventory Icons/Batch Capture Icons (Selected Folder)
    ///     - Tools/Items/Inventory Icons/Batch Capture Icons (Weapons Root Folder)
    /// </summary>
    public static class InventoryIconRenderPipelineTool
    {
        private const string IconRenderScenePath = "Assets/Scenes/Tools/InventoryIconRender.unity";
        private const string RenderTextureAssetPath = "Assets/Art/InventoryIcons/IconCaptureRT.renderTexture";
        private const string IconOutputFolderPath = "Assets/Art/InventoryIcons/Generated";
        private const string WeaponsPrefabRootFolderPath = "Assets/Prefabs/Weapons";

        private const string RigRootName = "InventoryIconRenderRig";
        private const string PreviewRootName = "PreviewRoot";
        private const string CameraNodeName = "IconRenderCamera";
        private const string KeyLightNodeName = "KeyLight";
        private const string FillLightNodeName = "FillLight";

        private const int RenderResolution = 1024;
        private const float CameraPitch = 30f;
        private const float CameraYaw = 35f;
        private const float FramingPadding = 1.12f;
        private const float WeaponsBatchZoomInMultiplier = 2f;

        private static readonly Color TransparentBackground = new(0f, 0f, 0f, 0f);

        [MenuItem("Tools/Items/Inventory Icons/Open Or Create Icon Render Scene", priority = -500)]
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

                Debug.Log($"[InventoryIconRenderPipelineTool] Created icon render scene at '{IconRenderScenePath}'.");
                return;
            }

            EditorSceneManager.OpenScene(IconRenderScenePath, OpenSceneMode.Single);
            EnsureRenderRigInActiveScene();
            Debug.Log($"[InventoryIconRenderPipelineTool] Opened icon render scene '{IconRenderScenePath}'.");
        }

        [MenuItem("Tools/Items/Inventory Icons/Setup Render Rig In Active Scene", priority = -500)]
        public static void SetupRenderRigInActiveScene()
        {
            EnsureRenderRigInActiveScene();
        }

        [MenuItem("Tools/Items/Inventory Icons/Capture Icon For Selected Prefab", priority = -500)]
        public static void CaptureIconForSelectedPrefab()
        {
            var selectedPrefabs = GetSelectedPrefabAssets();
            if (selectedPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Capture Inventory Icon",
                    "Select a Prefab asset in the Project window first.",
                    "OK");
                return;
            }

            var rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            var prefab = selectedPrefabs[0];
            var outputPath = BuildOutputPath(prefab.name);
            var success = CapturePrefabIcon(prefab, outputPath, rig, 1f, out var error);

            if (!success)
            {
                EditorUtility.DisplayDialog(
                    "Capture Inventory Icon",
                    $"Failed to render icon for '{prefab.name}'.\n\n{error}",
                    "OK");
                return;
            }

            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (iconSprite != null)
            {
                Selection.activeObject = iconSprite;
                EditorGUIUtility.PingObject(iconSprite);
            }

            Debug.Log($"[InventoryIconRenderPipelineTool] Captured icon for '{prefab.name}' -> {outputPath}");
        }

        [MenuItem("Tools/Items/Inventory Icons/Batch Capture Icons (Selected Folder)", priority = -500)]
        public static void BatchCaptureIconsFromSelectedFolder()
        {
            if (!TryGetSelectedFolderPath(out var folderPath))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Inventory Icons",
                    "Select a folder in the Project window that contains item prefabs.",
                    "OK");
                return;
            }

            RunBatchCaptureIconsFromRootFolder(folderPath, "Batch Capturing Inventory Icons", null, 1f);
        }

        [MenuItem("Tools/Items/Inventory Icons/Batch Capture Icons (Weapons Root Folder)", priority = -500)]
        public static void BatchCaptureIconsFromWeaponsRootFolder()
        {
            RunBatchCaptureIconsFromRootFolder(WeaponsPrefabRootFolderPath,
                "Batch Capturing Inventory Icons (Weapons Root)", null, WeaponsBatchZoomInMultiplier);
        }

        public static void BatchCaptureIconsFromRootFolder(
            string rootFolderPath,
            string progressTitle = "Batch Capturing Inventory Icons",
            IReadOnlyList<string> excludedFolderPaths = null,
            float zoomInMultiplier = 1f)
        {
            RunBatchCaptureIconsFromRootFolder(rootFolderPath, progressTitle, excludedFolderPaths, zoomInMultiplier);
        }

        private static void RunBatchCaptureIconsFromRootFolder(
            string rootFolderPath,
            string progressTitle,
            IReadOnlyList<string> excludedFolderPaths,
            float zoomInMultiplier)
        {
            var normalizedRootFolderPath = rootFolderPath?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(normalizedRootFolderPath) || !AssetDatabase.IsValidFolder(normalizedRootFolderPath))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Inventory Icons",
                    $"Root folder is invalid or missing: '{rootFolderPath}'.",
                    "OK");
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { normalizedRootFolderPath });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Inventory Icons",
                    $"No prefab assets found in '{normalizedRootFolderPath}'.",
                    "OK");
                return;
            }

            Array.Sort(prefabGuids, StringComparer.Ordinal);

            var rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            var successCount = 0;
            var skippedExistingCount = 0;
            var skippedExcludedCount = 0;
            var failureCount = 0;
            var failures = new List<string>();

            try
            {
                for (var i = 0; i < prefabGuids.Length; i++)
                {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        failureCount++;
                        failures.Add($"(null) {prefabPath}");
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        progressTitle,
                        $"Rendering {prefab.name} ({i + 1}/{prefabGuids.Length})",
                        (i + 1f) / prefabGuids.Length);

                    var outputPath = BuildOutputPath(prefab.name);
                    if (IsAssetPathInExcludedFolders(prefabPath, excludedFolderPaths))
                    {
                        skippedExcludedCount++;
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<Sprite>(outputPath) != null || File.Exists(Path.GetFullPath(outputPath)))
                    {
                        skippedExistingCount++;
                        continue;
                    }

                    var success = CapturePrefabIcon(prefab, outputPath, rig, zoomInMultiplier, out var error);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                        failures.Add($"{prefab.name}: {error}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var summary =
                $"Root Folder: {normalizedRootFolderPath}\nPrefabs Found: {prefabGuids.Length}\nCaptured: {successCount}\nSkipped (Already Exists): {skippedExistingCount}\nSkipped (Excluded Folder): {skippedExcludedCount}\nFailed: {failureCount}\nOutput: {IconOutputFolderPath}";
            if (failures.Count > 0) summary += "\n\nFailures:\n- " + string.Join("\n- ", failures);

            Debug.Log($"[InventoryIconRenderPipelineTool] Batch capture complete.\n{summary}");
            EditorUtility.DisplayDialog("Batch Capture Inventory Icons", summary, "OK");
        }

        private static RenderRig EnsureRenderRigInActiveScene()
        {
            var rigRoot = FindOrCreateRoot();
            var previewRoot = FindOrCreateChild(rigRoot, PreviewRootName);
            ResetLocalTransform(previewRoot, Vector3.zero, Quaternion.identity, Vector3.one);

            var cameraNode = FindOrCreateChild(rigRoot, CameraNodeName);
            var camera = cameraNode.GetComponent<Camera>();
            if (camera == null) camera = cameraNode.gameObject.AddComponent<Camera>();

            ConfigureCamera(camera);

            var keyLightNode = FindOrCreateChild(rigRoot, KeyLightNodeName);
            ConfigureDirectionalLight(keyLightNode, new Color(1f, 0.98f, 0.95f, 1f), 1.15f, new Vector3(42f, -36f, 0f));

            var fillLightNode = FindOrCreateChild(rigRoot, FillLightNodeName);
            ConfigureDirectionalLight(fillLightNode, new Color(0.75f, 0.80f, 1f, 1f), 0.35f,
                new Vector3(18f, 135f, 0f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

            var captureTexture = EnsureCaptureRenderTexture();
            camera.targetTexture = captureTexture;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return new RenderRig(rigRoot, previewRoot, camera, captureTexture);
        }

        private static bool CapturePrefabIcon(
            GameObject prefab,
            string outputPath,
            RenderRig rig,
            float zoomInMultiplier,
            out string error)
        {
            error = null;

            if (prefab == null)
            {
                error = "Selected prefab is null.";
                return false;
            }

            ClearPreviewRoot(rig.PreviewRoot);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) instance = Object.Instantiate(prefab);

            if (instance == null)
            {
                error = "Failed to instantiate prefab.";
                return false;
            }

            try
            {
                instance.name = prefab.name + "_Preview";
                instance.transform.SetParent(rig.PreviewRoot, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                if (!TryGetRenderableBounds(instance, out var bounds))
                {
                    error = "No Renderer found on prefab instance.";
                    return false;
                }

                // Center object bounds at world origin for consistent framing.
                instance.transform.position -= bounds.center;

                if (!TryGetRenderableBounds(instance, out bounds))
                {
                    error = "Failed to recalculate renderer bounds.";
                    return false;
                }

                FrameCameraToBounds(rig.Camera, bounds, zoomInMultiplier);

                if (!RenderCameraToPng(rig.Camera, rig.RenderTexture, outputPath, out var renderError))
                {
                    error = renderError;
                    return false;
                }

                return true;
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        private static void FrameCameraToBounds(Camera camera, Bounds bounds, float zoomInMultiplier)
        {
            var viewDirection = (Quaternion.Euler(CameraPitch, CameraYaw, 0f) * Vector3.forward).normalized;
            var radius = Mathf.Max(0.08f, bounds.extents.magnitude);
            var clampedZoom = Mathf.Max(0.01f, zoomInMultiplier);

            camera.orthographic = true;
            camera.orthographicSize = radius * FramingPadding / clampedZoom;

            var distance = Mathf.Max(2f, radius * 4f);
            var target = bounds.center;

            camera.transform.position = target - viewDirection * distance;
            camera.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);

            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(40f, distance + radius * 8f);
        }

        private static bool RenderCameraToPng(Camera camera, RenderTexture targetTexture, string outputPath,
            out string error)
        {
            error = null;

            if (camera == null)
            {
                error = "Camera is missing.";
                return false;
            }

            if (targetTexture == null)
            {
                error = "RenderTexture is missing.";
                return false;
            }

            EnsureFolderRecursive(Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            Texture2D texture = null;

            try
            {
                camera.targetTexture = targetTexture;
                camera.Render();

                RenderTexture.active = targetTexture;

                texture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
                texture.Apply(false, false);

                var pngBytes = texture.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    error = "PNG encoding failed.";
                    return false;
                }

                var fullPath = Path.GetFullPath(outputPath);
                File.WriteAllBytes(fullPath, pngBytes);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);

                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
            }

            AssetDatabase.ImportAsset(outputPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return true;
        }

        private static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var found = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static void ClearPreviewRoot(Transform previewRoot)
        {
            if (previewRoot == null) return;

            for (var i = previewRoot.childCount - 1; i >= 0; i--)
            {
                var child = previewRoot.GetChild(i);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform FindOrCreateRoot()
        {
            var existing = GameObject.Find(RigRootName);
            if (existing != null) return existing.transform;

            var created = new GameObject(RigRootName);
            Undo.RegisterCreatedObjectUndo(created, "Create Icon Render Rig");
            return created.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null) return child;

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Icon Render Rig Node");
            return childObject.transform;
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = TransparentBackground;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.orthographicSize = 1f;

            var t = camera.transform;
            t.localPosition = new Vector3(0f, 0f, -4f);
            t.localRotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            t.localScale = Vector3.one;
        }

        private static void ConfigureDirectionalLight(Transform lightNode, Color color, float intensity, Vector3 euler)
        {
            if (lightNode == null) return;

            var light = lightNode.GetComponent<Light>();
            if (light == null) light = lightNode.gameObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            ResetLocalTransform(lightNode, Vector3.zero, Quaternion.Euler(euler), Vector3.one);
        }

        private static void ResetLocalTransform(Transform transformToReset, Vector3 position, Quaternion rotation,
            Vector3 scale)
        {
            if (transformToReset == null) return;

            transformToReset.localPosition = position;
            transformToReset.localRotation = rotation;
            transformToReset.localScale = scale;
        }

        private static RenderTexture EnsureCaptureRenderTexture()
        {
            EnsureFolderRecursive(Path.GetDirectoryName(RenderTextureAssetPath)?.Replace("\\", "/"));

            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTextureAssetPath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(RenderResolution, RenderResolution, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "IconCaptureRT"
                };

                AssetDatabase.CreateAsset(renderTexture, RenderTextureAssetPath);
                AssetDatabase.SaveAssets();
            }

            var changed = false;

            if (renderTexture.width != RenderResolution || renderTexture.height != RenderResolution)
            {
                renderTexture.Release();
                renderTexture.width = RenderResolution;
                renderTexture.height = RenderResolution;
                changed = true;
            }

            if (renderTexture.antiAliasing != 1)
            {
                renderTexture.antiAliasing = 1;
                changed = true;
            }

            if (renderTexture.useMipMap)
            {
                renderTexture.useMipMap = false;
                changed = true;
            }

            if (renderTexture.autoGenerateMips)
            {
                renderTexture.autoGenerateMips = false;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(renderTexture);
                AssetDatabase.SaveAssets();
            }

            if (!renderTexture.IsCreated()) renderTexture.Create();

            return renderTexture;
        }

        private static List<GameObject> GetSelectedPrefabAssets()
        {
            var selectedAssets = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            var prefabs = new List<GameObject>(selectedAssets.Length);

            for (var i = 0; i < selectedAssets.Length; i++)
            {
                var gameObject = selectedAssets[i] as GameObject;
                if (gameObject == null) continue;

                if (PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.NotAPrefab) continue;

                prefabs.Add(gameObject);
            }

            return prefabs;
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = null;

            if (Selection.activeObject == null) return false;

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path)) return false;

            path = path.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(path))
            {
                folderPath = path;
                return true;
            }

            return false;
        }

        private static bool IsAssetPathInExcludedFolders(string assetPath, IReadOnlyList<string> excludedFolderPaths)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || excludedFolderPaths == null || excludedFolderPaths.Count == 0)
                return false;

            var normalizedAssetPath = assetPath.Replace("\\", "/").TrimEnd('/');

            for (var i = 0; i < excludedFolderPaths.Count; i++)
            {
                var excluded = excludedFolderPaths[i];
                if (string.IsNullOrWhiteSpace(excluded)) continue;

                var normalizedExcluded = excluded.Replace("\\", "/").TrimEnd('/');
                if (string.IsNullOrWhiteSpace(normalizedExcluded)) continue;

                if (normalizedAssetPath.Equals(normalizedExcluded, StringComparison.OrdinalIgnoreCase) ||
                    normalizedAssetPath.StartsWith(normalizedExcluded + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string BuildOutputPath(string prefabName)
        {
            var safeName = string.IsNullOrWhiteSpace(prefabName) ? "Icon" : prefabName.Trim();
            foreach (var invalidChar in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalidChar, '_');

            return $"{IconOutputFolderPath}/{safeName}.png";
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            folderPath = folderPath.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException($"Folder path must start with 'Assets': {folderPath}");

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;

                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private readonly struct RenderRig
        {
            public readonly Transform Root;
            public readonly Transform PreviewRoot;
            public readonly Camera Camera;
            public readonly RenderTexture RenderTexture;

            public RenderRig(Transform root, Transform previewRoot, Camera camera, RenderTexture renderTexture)
            {
                Root = root;
                PreviewRoot = previewRoot;
                Camera = camera;
                RenderTexture = renderTexture;
            }
        }
    }
}