using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Editor
{
    /// <summary>
    /// Repeatable workflow for rendering inventory item icons from prefabs.
    ///
    /// Menu:
    /// - Tools/Zombera/Inventory Icons/Open Or Create Icon Render Scene
    /// - Tools/Zombera/Inventory Icons/Setup Render Rig In Active Scene
    /// - Tools/Zombera/Inventory Icons/Capture Icon For Selected Prefab
    /// - Tools/Zombera/Inventory Icons/Batch Capture Icons (Selected Folder)
    /// </summary>
    public static class InventoryIconRenderPipelineTool
    {
        private const string IconRenderScenePath = "Assets/Scenes/Tools/InventoryIconRender.unity";
        private const string RenderTextureAssetPath = "Assets/Art/InventoryIcons/IconCaptureRT.renderTexture";
        private const string IconOutputFolderPath = "Assets/Art/InventoryIcons/Generated";

        private const string RigRootName = "InventoryIconRenderRig";
        private const string PreviewRootName = "PreviewRoot";
        private const string CameraNodeName = "IconRenderCamera";
        private const string KeyLightNodeName = "KeyLight";
        private const string FillLightNodeName = "FillLight";

        private const int RenderResolution = 1024;
        private const float CameraPitch = 30f;
        private const float CameraYaw = 35f;
        private const float FramingPadding = 1.12f;

        private static readonly Color TransparentBackground = new Color(0f, 0f, 0f, 0f);

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

        [MenuItem("Tools/Zombera/Inventory Icons/Open Or Create Icon Render Scene")]
        public static void OpenOrCreateIconRenderScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolderRecursive(Path.GetDirectoryName(IconRenderScenePath)?.Replace("\\", "/"));

            SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IconRenderScenePath);
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

        [MenuItem("Tools/Zombera/Inventory Icons/Setup Render Rig In Active Scene")]
        public static void SetupRenderRigInActiveScene()
        {
            EnsureRenderRigInActiveScene();
        }

        [MenuItem("Tools/Zombera/Inventory Icons/Capture Icon For Selected Prefab")]
        public static void CaptureIconForSelectedPrefab()
        {
            List<GameObject> selectedPrefabs = GetSelectedPrefabAssets();
            if (selectedPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Capture Inventory Icon",
                    "Select a Prefab asset in the Project window first.",
                    "OK");
                return;
            }

            RenderRig rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            GameObject prefab = selectedPrefabs[0];
            string outputPath = BuildOutputPath(prefab.name);
            bool success = CapturePrefabIcon(prefab, outputPath, rig, out string error);

            if (!success)
            {
                EditorUtility.DisplayDialog(
                    "Capture Inventory Icon",
                    $"Failed to render icon for '{prefab.name}'.\n\n{error}",
                    "OK");
                return;
            }

            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (iconSprite != null)
            {
                Selection.activeObject = iconSprite;
                EditorGUIUtility.PingObject(iconSprite);
            }

            Debug.Log($"[InventoryIconRenderPipelineTool] Captured icon for '{prefab.name}' -> {outputPath}");
        }

        [MenuItem("Tools/Zombera/Inventory Icons/Batch Capture Icons (Selected Folder)")]
        public static void BatchCaptureIconsFromSelectedFolder()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Inventory Icons",
                    "Select a folder in the Project window that contains item prefabs.",
                    "OK");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Inventory Icons",
                    $"No prefab assets found in '{folderPath}'.",
                    "OK");
                return;
            }

            RenderRig rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            int successCount = 0;
            int failureCount = 0;
            List<string> failures = new List<string>();

            try
            {
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        failureCount++;
                        failures.Add($"(null) {prefabPath}");
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "Batch Capturing Inventory Icons",
                        $"Rendering {prefab.name} ({i + 1}/{prefabGuids.Length})",
                        (i + 1f) / prefabGuids.Length);

                    string outputPath = BuildOutputPath(prefab.name);
                    bool success = CapturePrefabIcon(prefab, outputPath, rig, out string error);
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

            string summary = $"Captured: {successCount}\nFailed: {failureCount}\nOutput: {IconOutputFolderPath}";
            if (failures.Count > 0)
            {
                summary += "\n\nFailures:\n- " + string.Join("\n- ", failures);
            }

            Debug.Log($"[InventoryIconRenderPipelineTool] Batch capture complete.\n{summary}");
            EditorUtility.DisplayDialog("Batch Capture Inventory Icons", summary, "OK");
        }

        private static RenderRig EnsureRenderRigInActiveScene()
        {
            Transform rigRoot = FindOrCreateRoot();
            Transform previewRoot = FindOrCreateChild(rigRoot, PreviewRootName);
            ResetLocalTransform(previewRoot, Vector3.zero, Quaternion.identity, Vector3.one);

            Transform cameraNode = FindOrCreateChild(rigRoot, CameraNodeName);
            Camera camera = cameraNode.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraNode.gameObject.AddComponent<Camera>();
            }

            ConfigureCamera(camera);

            Transform keyLightNode = FindOrCreateChild(rigRoot, KeyLightNodeName);
            ConfigureDirectionalLight(keyLightNode, new Color(1f, 0.98f, 0.95f, 1f), 1.15f, new Vector3(42f, -36f, 0f));

            Transform fillLightNode = FindOrCreateChild(rigRoot, FillLightNodeName);
            ConfigureDirectionalLight(fillLightNode, new Color(0.75f, 0.80f, 1f, 1f), 0.35f, new Vector3(18f, 135f, 0f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

            RenderTexture captureTexture = EnsureCaptureRenderTexture();
            camera.targetTexture = captureTexture;

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            return new RenderRig(rigRoot, previewRoot, camera, captureTexture);
        }

        private static bool CapturePrefabIcon(GameObject prefab, string outputPath, RenderRig rig, out string error)
        {
            error = null;

            if (prefab == null)
            {
                error = "Selected prefab is null.";
                return false;
            }

            ClearPreviewRoot(rig.PreviewRoot);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

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

                if (!TryGetRenderableBounds(instance, out Bounds bounds))
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

                FrameCameraToBounds(rig.Camera, bounds);

                if (!RenderCameraToPng(rig.Camera, rig.RenderTexture, outputPath, out string renderError))
                {
                    error = renderError;
                    return false;
                }

                return true;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static void FrameCameraToBounds(Camera camera, Bounds bounds)
        {
            Vector3 viewDirection = (Quaternion.Euler(CameraPitch, CameraYaw, 0f) * Vector3.forward).normalized;
            float radius = Mathf.Max(0.08f, bounds.extents.magnitude);

            camera.orthographic = true;
            camera.orthographicSize = radius * FramingPadding;

            float distance = Mathf.Max(2f, radius * 4f);
            Vector3 target = bounds.center;

            camera.transform.position = target - viewDirection * distance;
            camera.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);

            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(40f, distance + radius * 8f);
        }

        private static bool RenderCameraToPng(Camera camera, RenderTexture targetTexture, string outputPath, out string error)
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

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            Texture2D texture = null;

            try
            {
                camera.targetTexture = targetTexture;
                camera.Render();

                RenderTexture.active = targetTexture;

                texture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
                texture.Apply(false, false);

                byte[] pngBytes = texture.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    error = "PNG encoding failed.";
                    return false;
                }

                string fullPath = Path.GetFullPath(outputPath);
                File.WriteAllBytes(fullPath, pngBytes);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
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
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

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
            if (previewRoot == null)
            {
                return;
            }

            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = previewRoot.GetChild(i);
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Transform FindOrCreateRoot()
        {
            GameObject existing = GameObject.Find(RigRootName);
            if (existing != null)
            {
                return existing.transform;
            }

            GameObject created = new GameObject(RigRootName);
            Undo.RegisterCreatedObjectUndo(created, "Create Icon Render Rig");
            return created.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Icon Render Rig Node");
            return childObject.transform;
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = TransparentBackground;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.orthographicSize = 1f;

            Transform t = camera.transform;
            t.localPosition = new Vector3(0f, 0f, -4f);
            t.localRotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            t.localScale = Vector3.one;
        }

        private static void ConfigureDirectionalLight(Transform lightNode, Color color, float intensity, Vector3 euler)
        {
            if (lightNode == null)
            {
                return;
            }

            Light light = lightNode.GetComponent<Light>();
            if (light == null)
            {
                light = lightNode.gameObject.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            ResetLocalTransform(lightNode, Vector3.zero, Quaternion.Euler(euler), Vector3.one);
        }

        private static void ResetLocalTransform(Transform transformToReset, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (transformToReset == null)
            {
                return;
            }

            transformToReset.localPosition = position;
            transformToReset.localRotation = rotation;
            transformToReset.localScale = scale;
        }

        private static RenderTexture EnsureCaptureRenderTexture()
        {
            EnsureFolderRecursive(Path.GetDirectoryName(RenderTextureAssetPath)?.Replace("\\", "/"));

            RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTextureAssetPath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(RenderResolution, RenderResolution, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8,
                    useMipMap = false,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "IconCaptureRT"
                };

                AssetDatabase.CreateAsset(renderTexture, RenderTextureAssetPath);
                AssetDatabase.SaveAssets();
            }

            bool changed = false;

            if (renderTexture.width != RenderResolution || renderTexture.height != RenderResolution)
            {
                renderTexture.Release();
                renderTexture.width = RenderResolution;
                renderTexture.height = RenderResolution;
                changed = true;
            }

            if (renderTexture.antiAliasing != 8)
            {
                renderTexture.antiAliasing = 8;
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

            if (!renderTexture.IsCreated())
            {
                renderTexture.Create();
            }

            return renderTexture;
        }

        private static List<GameObject> GetSelectedPrefabAssets()
        {
            UnityEngine.Object[] selectedAssets = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            List<GameObject> prefabs = new List<GameObject>(selectedAssets.Length);

            for (int i = 0; i < selectedAssets.Length; i++)
            {
                GameObject gameObject = selectedAssets[i] as GameObject;
                if (gameObject == null)
                {
                    continue;
                }

                if (PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.NotAPrefab)
                {
                    continue;
                }

                prefabs.Add(gameObject);
            }

            return prefabs;
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = null;

            if (Selection.activeObject == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = path.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(path))
            {
                folderPath = path;
                return true;
            }

            return false;
        }

        private static string BuildOutputPath(string prefabName)
        {
            string safeName = string.IsNullOrWhiteSpace(prefabName) ? "Icon" : prefabName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return $"{IconOutputFolderPath}/{safeName}.png";
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            folderPath = folderPath.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Folder path must start with 'Assets': {folderPath}");
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
