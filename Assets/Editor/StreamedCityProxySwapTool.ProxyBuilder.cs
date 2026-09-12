#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private static readonly StreamedCityProxyGeometryHelper ProxyGeometryHelper =
            new StreamedCityProxyGeometryHelper(DisableShadowsOnProxyRenderers);

        private static bool TryBuildProxyPrefab(
            GameObject sourcePrefab,
            StreamedCityBuildingEntry entry,
            string outputPath,
            string outputMeshAssetPath,
            ISet<string> gpuInstancingEnabledMaterialPaths,
            out GameObject proxyPrefab,
            out string failureReason)
        {
            proxyPrefab = null;
            failureReason = string.Empty;

            if (sourcePrefab == null)
            {
                failureReason = "Source prefab is null.";
                return false;
            }

            var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "Source prefab path is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                failureReason = "Output proxy path is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputMeshAssetPath))
            {
                failureReason = "Output proxy mesh asset path is invalid.";
                return false;
            }

            GameObject sourceRoot = null;
            GameObject proxyRoot = null;

            try
            {
                sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
                if (sourceRoot == null)
                {
                    failureReason = "Could not load source prefab contents.";
                    return false;
                }

                EnableGpuInstancingOnMaterials(sourceRoot, gpuInstancingEnabledMaterialPaths);

                proxyRoot = Object.Instantiate(sourceRoot);
                if (proxyRoot == null)
                {
                    failureReason = "Could not clone source prefab contents.";
                    return false;
                }

                proxyRoot.name = sourcePrefab.name + "_Proxy";

                // Capture StairSocket positions BEFORE StripToVisualHierarchy destroys them.
                var stairSockets = new System.Collections.Generic.List<Vector3>();
                CollectStairSocketPositions(proxyRoot.transform, stairSockets);

                StripToVisualHierarchy(proxyRoot.transform);

                if (!HasRenderableComponents(proxyRoot))
                {
                    failureReason = "No renderable components found after strip.";
                    return false;
                }

                if (!ProxyGeometryHelper.TryCollapseMeshRenderers(proxyRoot, outputMeshAssetPath, out var combineFailure))
                {
                    failureReason = combineFailure;
                    return false;
                }

                // Recreate StairSocket markers so door-path generation works on proxies.
                for (var i = 0; i < stairSockets.Count; i++)
                {
                    var socket = new GameObject($"StairSocket_{i:D2}");
                    socket.transform.SetParent(proxyRoot.transform, false);
                    socket.transform.localPosition = stairSockets[i];
                }

                RemoveAllColliders(proxyRoot);
                ProxyGeometryHelper.EnsureProxyRootCollider(proxyRoot, entry);

                var saved = PrefabUtility.SaveAsPrefabAsset(proxyRoot, outputPath, out var saveOk);
                if (!saveOk || saved == null)
                {
                    failureReason = "SaveAsPrefabAsset failed.";
                    return false;
                }

                proxyPrefab = saved;
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                if (proxyRoot != null)
                    Object.DestroyImmediate(proxyRoot);

                if (sourceRoot != null)
                    PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static void EnableGpuInstancingOnMaterials(
            GameObject sourceRoot,
            ISet<string> gpuInstancingEnabledMaterialPaths)
        {
            if (sourceRoot == null || gpuInstancingEnabledMaterialPaths == null)
                return;

            var renderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                EnableGpuInstancingOnRendererMaterials(renderers[i], gpuInstancingEnabledMaterialPaths);
        }

        private static void EnableGpuInstancingOnRendererMaterials(
            Renderer renderer,
            ISet<string> gpuInstancingEnabledMaterialPaths)
        {
            if (renderer == null)
                return;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return;

            for (var m = 0; m < materials.Length; m++)
                TryEnableGpuInstancingOnMaterial(materials[m], gpuInstancingEnabledMaterialPaths);
        }

        private static void TryEnableGpuInstancingOnMaterial(
            Material material,
            ISet<string> gpuInstancingEnabledMaterialPaths)
        {
            if (material == null || material.enableInstancing)
                return;

            var materialPath = AssetDatabase.GetAssetPath(material);
            if (!IsProjectMaterialPath(materialPath))
                return;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            gpuInstancingEnabledMaterialPaths.Add(materialPath);
        }

        private static bool IsProjectMaterialPath(string materialPath) =>
            !string.IsNullOrWhiteSpace(materialPath)
            && materialPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);

        private static void StripToVisualHierarchy(Transform root)
        {
            if (root == null)
                return;

            _ = PruneNonVisualBranches(root, true);

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                    StripNonVisualComponentsFromNode(transforms[i]);
            }
        }

        private static void StripNonVisualComponentsFromNode(Transform node)
        {
            var components = node.GetComponents<Component>();
            for (var c = 0; c < components.Length; c++)
            {
                var component = components[c];
                if (component == null || ShouldKeepVisualComponent(component))
                    continue;

                Object.DestroyImmediate(component, true);
            }
        }

        private static bool ShouldKeepVisualComponent(Component component) =>
            component is Transform or MeshFilter or MeshRenderer or SkinnedMeshRenderer or LODGroup;

        private static void CollectStairSocketPositions(Transform root, System.Collections.Generic.List<Vector3> positions)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase))
                    positions.Add(child.localPosition);

                CollectStairSocketPositions(child, positions);
            }
        }

        private static bool PruneNonVisualBranches(Transform node, bool isRoot)
        {
            if (node == null)
                return false;

            var keep = NodeContainsVisuals(node);
            for (var i = node.childCount - 1; i >= 0; i--)
            {
                var child = node.GetChild(i);
                if (child == null)
                    continue;

                if (!PruneNonVisualBranches(child, false))
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }

                keep = true;
            }

            return isRoot || keep;
        }

        private static bool NodeContainsVisuals(Transform node)
        {
            // StairSocket markers must survive the visual-only prune so door-path
            // generation can find path origins on proxy prefabs.
            if (node.name.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase))
                return true;

            return node.GetComponent<MeshRenderer>() != null
                   || node.GetComponent<SkinnedMeshRenderer>() != null
                   || node.GetComponent<LODGroup>() != null;
        }

        private static bool HasRenderableComponents(GameObject root)
        {
            if (root == null)
                return false;

            return root.GetComponentsInChildren<MeshRenderer>(true).Length > 0
                   || root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
        }

        private static void RemoveAllColliders(GameObject root)
        {
            if (root == null)
                return;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                Object.DestroyImmediate(colliders[i], true);
            }
        }
    }
}
#endif
