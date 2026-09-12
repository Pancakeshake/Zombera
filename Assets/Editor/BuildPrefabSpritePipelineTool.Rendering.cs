#region

using System;
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
    public static partial class BuildPrefabSpritePipelineTool
    {
        private readonly struct RenderRig
        {
            public readonly Transform PreviewRoot;
            public readonly Camera Camera;
            public readonly RenderTexture RenderTexture;

            public RenderRig(Transform previewRoot, Camera camera, RenderTexture renderTexture)
            {
                PreviewRoot = previewRoot;
                Camera = camera;
                RenderTexture = renderTexture;
            }
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

            return new RenderRig(previewRoot, camera, captureTexture);
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

                instance.transform.position -= bounds.center;

                if (!TryGetRenderableBounds(instance, out bounds))
                {
                    error = "Failed to recalculate renderer bounds.";
                    return false;
                }

                FrameCameraToBounds(rig.Camera, bounds);

                if (!RenderCameraToPng(rig.Camera, rig.RenderTexture, outputPath, out var renderError))
                {
                    error = renderError;
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void FrameCameraToBounds(Camera camera, Bounds bounds)
        {
            var viewDirection = (Quaternion.Euler(CameraPitch, CameraYaw, 0f) * Vector3.forward).normalized;
            var radius = Mathf.Max(0.08f, bounds.extents.magnitude);

            camera.orthographic = true;
            camera.orthographicSize = radius * FramingPadding;

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

            if (AssetImporter.GetAtPath(outputPath) is TextureImporter importer)
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
            Undo.RegisterCreatedObjectUndo(created, "Create Build Icon Render Rig");
            return created.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null) return child;

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Build Icon Render Rig Node");
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
                    name = "BuildIconCaptureRT"
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
    }
}
