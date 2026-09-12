#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    /// <summary>
    ///     Bakes game-ready proxy prefabs from assembled building prefabs.
    ///     A proxy collapses every renderer into one combined mesh per material
    ///     (sub-meshes saved in a paired <c>_ProxyMeshes.asset</c> bundle), fits a single
    ///     box collider, and disables shadows / light / reflection probes on the renderers.
    ///     Output naming matches <see cref="Zombera.World.City.CityBuildingPrefabNaming" />:
    ///     <c>&lt;SourceName&gt;_&lt;guid8&gt;_Proxy.prefab</c> inside the relative subfolder
    ///     that mirrors the source (e.g. <c>Residential/1x1/</c>).
    /// </summary>
    internal static class BuildingProxyBaker
    {
        internal const string ProxyMeshAssetSuffix = "_ProxyMeshes.asset";

        private static readonly StreamedCityProxyGeometryHelper GeometryHelper =
            new(disableShadowsOnProxyRenderers: true);

        /// <summary>
        ///     Bakes a proxy for <paramref name="sourcePrefabPath" /> into
        ///     <c>&lt;proxyOutputFolder&gt;/&lt;relativeSubFolder&gt;/</c>.
        /// </summary>
        internal static bool TryBuildProxy(
            string sourcePrefabPath,
            string proxyOutputFolder,
            string relativeSubFolder,
            bool enableGpuInstancingOnSourceMaterials,
            out string proxyPrefabPath,
            out string failureReason)
        {
            proxyPrefabPath = null;
            failureReason = string.Empty;

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
            {
                failureReason = "Source prefab could not be loaded: " + sourcePrefabPath;
                return false;
            }

            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePrefabPath);
            var guidSuffix = !string.IsNullOrWhiteSpace(sourceGuid) && sourceGuid.Length >= 8
                ? sourceGuid[..8]
                : "noguid";

            var proxyName = sourcePrefab.name + "_" + guidSuffix + "_Proxy";
            proxyPrefabPath = ResolveProxyPrefabPath(proxyOutputFolder, relativeSubFolder, proxyName);
            if (string.IsNullOrWhiteSpace(proxyPrefabPath))
            {
                failureReason = "Proxy output folder is invalid: " + proxyOutputFolder;
                return false;
            }

            var meshAssetPath = proxyPrefabPath[..^".prefab".Length] + ProxyMeshAssetSuffix;
            DeleteExistingProxyAssets(proxyPrefabPath, meshAssetPath);

            GameObject sourceRoot = null;
            GameObject proxyRoot = null;

            try
            {
                sourceRoot = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
                if (sourceRoot == null)
                {
                    failureReason = "Could not load prefab contents: " + sourcePrefabPath;
                    return false;
                }

                if (enableGpuInstancingOnSourceMaterials)
                    EnableGpuInstancingOnMaterials(sourceRoot);

                proxyRoot = Object.Instantiate(sourceRoot);
                proxyRoot.name = proxyName;

                // StairSockets survive the visual-only strip so door-path
                // generation still finds path origins on proxy prefabs.
                var stairSockets = new List<Vector3>();
                CollectStairSocketPositions(proxyRoot.transform, stairSockets);

                StripToVisualHierarchy(proxyRoot.transform);

                if (!GeometryHelper.TryCollapseMeshRenderers(proxyRoot, meshAssetPath, out var combineFailure))
                {
                    failureReason = combineFailure;
                    return false;
                }

                RecreateStairSockets(proxyRoot.transform, stairSockets);
                RemoveAllColliders(proxyRoot);
                GeometryHelper.EnsureProxyRootCollider(proxyRoot, null);

                PrefabUtility.SaveAsPrefabAsset(proxyRoot, proxyPrefabPath, out var saveOk);
                if (!saveOk)
                {
                    failureReason = "SaveAsPrefabAsset failed for " + proxyPrefabPath;
                    return false;
                }

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

        /// <summary>
        ///     Collects every prefab path under <paramref name="sourceFolder" />, recursively,
        ///     sorted by path for deterministic batch runs.
        /// </summary>
        internal static List<string> CollectSourcePrefabPaths(string sourceFolder)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(sourceFolder) || !AssetDatabase.IsValidFolder(sourceFolder))
                return paths;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { sourceFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) ||
                    !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        internal static string ResolveProxyPrefabPath(
            string proxyOutputFolder, string relativeSubFolder, string proxyName)
        {
            if (string.IsNullOrWhiteSpace(proxyOutputFolder))
                return null;

            var targetFolder = proxyOutputFolder.TrimEnd('/', '\\');
            if (!string.IsNullOrWhiteSpace(relativeSubFolder))
                targetFolder += "/" + relativeSubFolder.Trim('/');

            EnsureFolderExists(targetFolder);
            return targetFolder + "/" + proxyName + ".prefab";
        }

        private static void DeleteExistingProxyAssets(string proxyPrefabPath, string meshAssetPath)
        {
            if (!string.IsNullOrWhiteSpace(meshAssetPath) &&
                AssetDatabase.LoadAssetAtPath<Object>(meshAssetPath) != null)
                AssetDatabase.DeleteAsset(meshAssetPath);

            if (!string.IsNullOrWhiteSpace(proxyPrefabPath) &&
                AssetDatabase.LoadAssetAtPath<Object>(proxyPrefabPath) != null)
                AssetDatabase.DeleteAsset(proxyPrefabPath);
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            assetFolderPath = assetFolderPath.Replace('\\', '/');
            var parts = assetFolderPath.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets")
                return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static void EnableGpuInstancingOnMaterials(GameObject root)
        {
            if (root == null)
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                EnableGpuInstancingOnRendererMaterials(renderers[i]);
        }

        private static void EnableGpuInstancingOnRendererMaterials(Renderer renderer)
        {
            if (renderer == null)
                return;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return;

            for (var m = 0; m < materials.Length; m++)
                TryEnableGpuInstancingOnMaterial(materials[m]);
        }

        private static void TryEnableGpuInstancingOnMaterial(Material material)
        {
            if (material == null || material.enableInstancing)
                return;

            var materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrWhiteSpace(materialPath) ||
                !materialPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static void StripToVisualHierarchy(Transform root)
        {
            if (root == null)
                return;

            _ = PruneNonVisualBranches(root, true);

            var nodes = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                    StripNonVisualComponentsFromNode(nodes[i]);
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
            if (node.name.StartsWith("StairSocket", StringComparison.OrdinalIgnoreCase))
                return true;

            return node.GetComponent<MeshRenderer>() != null
                   || node.GetComponent<SkinnedMeshRenderer>() != null
                   || node.GetComponent<LODGroup>() != null;
        }

        private static void CollectStairSocketPositions(Transform root, List<Vector3> positions)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("StairSocket", StringComparison.OrdinalIgnoreCase))
                    positions.Add(child.localPosition);

                CollectStairSocketPositions(child, positions);
            }
        }

        private static void RecreateStairSockets(Transform root, List<Vector3> positions)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                var socket = new GameObject($"StairSocket_{i:D2}");
                socket.transform.SetParent(root, false);
                socket.transform.localPosition = positions[i];
            }
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
