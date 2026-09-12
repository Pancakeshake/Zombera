#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Creates proxy prefabs (combined mesh) for all prefabs under Building Parts Modular,
    ///     preserving StairSocket and SnapSocket GameObjects.
    ///     Menu: Tools > 2. Build Generation Tools > 3. Fences > Build Modular Proxies
    /// </summary>
    public static class PrefabModularProxyBuilder
    {
        private const string SourceRoot = "Assets/02_Shared/Prefabs/Building/Building Parts Modular";
        private const string ProxiesFolderName = "Proxies";
        private const string MeshAssetsFolderName = "ProxyMeshes";

        [MenuItem("Tools/Build/Mod Kits/3. Fences/Build Modular Proxies (All Subfolders)", priority = -500)]
        private static void BuildAllModularProxies()
        {
            if (!Directory.Exists(SourceRoot))
            {
                Debug.LogError("[PrefabModularProxyBuilder] Source root not found: " + SourceRoot);
                return;
            }

            var prefabFiles = Directory.GetFiles(SourceRoot, "*.prefab", SearchOption.AllDirectories);
            if (prefabFiles.Length == 0)
            {
                Debug.Log("[PrefabModularProxyBuilder] No prefabs found under " + SourceRoot);
                return;
            }

            var built = 0;
            var skipped = 0;
            var failed = 0;

            try
            {
                for (var i = 0; i < prefabFiles.Length; i++)
                {
                    var assetPath = prefabFiles[i].Replace('\\', '/');
                    if (!assetPath.StartsWith("Assets/")) continue;

                    // Skip prefabs that are already inside a Proxies folder.
                    if (assetPath.Contains("/" + ProxiesFolderName + "/"))
                    {
                        skipped++;
                        continue;
                    }

                    var cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Building Modular Proxies",
                        Path.GetFileNameWithoutExtension(assetPath) + " (" + (i + 1) + "/" + prefabFiles.Length + ")",
                        (i + 1) / (float)prefabFiles.Length);

                    if (cancelled)
                    {
                        Debug.LogWarning("[PrefabModularProxyBuilder] Cancelled by user after " + built + " proxies.");
                        break;
                    }

                    if (BuildProxyForPrefab(assetPath))
                        built++;
                    else
                        failed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log("[PrefabModularProxyBuilder] Done: built=" + built + ", skipped=" + skipped + ", failed=" + failed + ".");
        }

        [MenuItem("Tools/Build/Mod Kits/3. Fences/Build Modular Proxies (Selected Folder)", priority = -500)]
        private static void BuildProxiesForSelectedFolder()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath))
            {
                Debug.LogWarning("[PrefabModularProxyBuilder] Select a folder in the Project window first.");
                return;
            }

            if (!selectedPath.StartsWith(SourceRoot))
            {
                Debug.LogWarning("[PrefabModularProxyBuilder] Selected folder must be under " + SourceRoot);
                return;
            }

            var prefabFiles = Directory.GetFiles(selectedPath, "*.prefab", SearchOption.TopDirectoryOnly);
            if (prefabFiles.Length == 0)
            {
                Debug.Log("[PrefabModularProxyBuilder] No prefabs in selected folder.");
                return;
            }

            var built = 0;
            var failed = 0;

            try
            {
                for (var i = 0; i < prefabFiles.Length; i++)
                {
                    var assetPath = prefabFiles[i].Replace('\\', '/');
                    if (!AssetDatabase.LoadAssetAtPath<GameObject>(assetPath))
                    {
                        failed++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Building Proxies",
                        Path.GetFileNameWithoutExtension(assetPath),
                        (i + 1) / (float)prefabFiles.Length);

                    if (BuildProxyForPrefab(assetPath))
                        built++;
                    else
                        failed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log("[PrefabModularProxyBuilder] Built=" + built + ", failed=" + failed + ".");
        }

        [MenuItem("Tools/Build/Mod Kits/3. Fences/Build Modular Proxies (Selected Folder)", true, priority = -500)]
        private static bool ValidateBuildProxiesForSelectedFolder()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        private static bool BuildProxyForPrefab(string sourceAssetPath)
        {
            var sourceDir = Path.GetDirectoryName(sourceAssetPath).Replace('\\', '/');
            var proxiesDir = sourceDir + "/" + ProxiesFolderName;
            var meshDir = sourceDir + "/" + MeshAssetsFolderName;
            var prefabName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            var proxyAssetPath = proxiesDir + "/" + prefabName + "_Proxy.prefab";
            var meshAssetPath = meshDir + "/" + prefabName + "_Proxy.asset";

            if (!Directory.Exists(proxiesDir))
                Directory.CreateDirectory(proxiesDir);
            if (!Directory.Exists(meshDir))
                Directory.CreateDirectory(meshDir);

            // Delete existing proxy if present.
            if (File.Exists(proxyAssetPath))
                AssetDatabase.DeleteAsset(proxyAssetPath);
            if (File.Exists(meshAssetPath))
                AssetDatabase.DeleteAsset(meshAssetPath);

            GameObject sourceRoot = null;
            GameObject proxyRoot = null;

            try
            {
                sourceRoot = PrefabUtility.LoadPrefabContents(sourceAssetPath);
                if (sourceRoot == null) return false;

                proxyRoot = Object.Instantiate(sourceRoot);
                proxyRoot.name = prefabName + "_Proxy";

                // Capture socket positions before stripping.
                var sockets = new List<(Vector3 pos, Quaternion rot, string name)>();
                CollectSockets(proxyRoot.transform, sockets);

                // Remove all non-mesh components (keep Transforms + MeshFilter + MeshRenderer).
                StripNonVisualComponents(proxyRoot.transform);

                // Combine all meshes into one.
                if (!TryCombineMeshes(proxyRoot, meshAssetPath))
                    return false;

                // Remove colliders.
                var colliders = proxyRoot.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                    Object.DestroyImmediate(c);

                // Recreate sockets at their original local positions.
                foreach (var s in sockets)
                {
                    var socket = new GameObject(s.name);
                    socket.transform.SetParent(proxyRoot.transform, false);
                    socket.transform.localPosition = s.pos;
                    socket.transform.localRotation = s.rot;
                }

                PrefabUtility.SaveAsPrefabAsset(proxyRoot, proxyAssetPath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[PrefabModularProxyBuilder] Failed for " + prefabName + ": " + ex.Message);
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

        private static void CollectSockets(Transform root, List<(Vector3 pos, Quaternion rot, string name)> results)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var n = child.name;
                if (n.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("SnapSocket", System.StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((child.localPosition, child.localRotation, n));
                }

                CollectSockets(child, results);
            }
        }

        private static void StripNonVisualComponents(Transform root)
        {
            if (root == null)
                return;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    StripNonVisualComponentsFromNode(all[i]);
            }

            RemoveEmptyBranches(root);
        }

        private static void StripNonVisualComponentsFromNode(Transform node)
        {
            var components = node.GetComponents<Component>();
            for (var c = 0; c < components.Length; c++)
            {
                var comp = components[c];
                if (comp == null || ShouldKeepVisualComponent(comp))
                    continue;

                Object.DestroyImmediate(comp, true);
            }
        }

        private static bool ShouldKeepVisualComponent(Component comp) =>
            comp is Transform or MeshFilter or MeshRenderer or SkinnedMeshRenderer or LODGroup;

        private static void RemoveEmptyBranches(Transform node)
        {
            for (var i = node.childCount - 1; i >= 0; i--)
            {
                var child = node.GetChild(i);
                RemoveEmptyBranches(child);

                var hasMesh = child.GetComponent<MeshFilter>() != null
                           || child.GetComponent<MeshRenderer>() != null
                           || child.GetComponent<SkinnedMeshRenderer>() != null;

                var hasSocket = child.name.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase)
                             || child.name.StartsWith("SnapSocket", System.StringComparison.OrdinalIgnoreCase);

                if (!hasMesh && !hasSocket && child.childCount == 0)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool TryCombineMeshes(GameObject root, string meshAssetPath)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length == 0)
                return true;

            BuildCombineInstances(
                root,
                meshFilters,
                out var _,
                out var materialMap,
                out var allCombined);

            if (allCombined.Count == 0)
                return false;

            if (allCombined.Count == 1)
                return TryApplySingleCombinedMesh(root, meshAssetPath, materialMap, allCombined[0].mesh);

            return TryApplyMergedCombinedMesh(root, meshAssetPath, materialMap, allCombined);
        }

        private static void BuildCombineInstances(
            GameObject root,
            MeshFilter[] meshFilters,
            out List<CombineInstance> unassignedInstances,
            out Dictionary<Material, List<CombineInstance>> materialMap,
            out List<CombineInstance> allCombined)
        {
            unassignedInstances = new List<CombineInstance>(meshFilters.Length);
            materialMap = new Dictionary<Material, List<CombineInstance>>();
            allCombined = new List<CombineInstance>();

            for (var i = 0; i < meshFilters.Length; i++)
                AddMeshFilterCombineInstance(root, meshFilters[i], unassignedInstances, materialMap);

            if (unassignedInstances.Count > 0)
            {
                var combinedMesh = new Mesh();
                combinedMesh.CombineMeshes(unassignedInstances.ToArray(), true);
                allCombined.Add(new CombineInstance { mesh = combinedMesh, transform = Matrix4x4.identity });
            }

            foreach (var kvp in materialMap)
            {
                var combinedMesh = new Mesh();
                combinedMesh.CombineMeshes(kvp.Value.ToArray(), true);
                allCombined.Add(new CombineInstance { mesh = combinedMesh, transform = Matrix4x4.identity });
            }
        }

        private static void AddMeshFilterCombineInstance(
            GameObject root,
            MeshFilter meshFilter,
            List<CombineInstance> unassignedInstances,
            Dictionary<Material, List<CombineInstance>> materialMap)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            var combineInstance = new CombineInstance
            {
                mesh = meshFilter.sharedMesh,
                transform = root.transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix
            };

            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            var material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (material == null)
            {
                unassignedInstances.Add(combineInstance);
                return;
            }

            if (!materialMap.TryGetValue(material, out var materialInstances))
            {
                materialInstances = new List<CombineInstance>();
                materialMap[material] = materialInstances;
            }

            materialInstances.Add(combineInstance);
        }

        private static bool TryApplySingleCombinedMesh(
            GameObject root,
            string meshAssetPath,
            Dictionary<Material, List<CombineInstance>> materialMap,
            Mesh finalMesh)
        {
            finalMesh.name = root.name + "_ProxyMesh";
            AssetDatabase.CreateAsset(finalMesh, meshAssetPath);

            var meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = finalMesh;

            var firstMaterial = GetFirstMaterial(materialMap);
            if (firstMaterial == null)
                return true;

            var meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = firstMaterial;
            return true;
        }

        private static bool TryApplyMergedCombinedMesh(
            GameObject root,
            string meshAssetPath,
            Dictionary<Material, List<CombineInstance>> materialMap,
            List<CombineInstance> allCombined)
        {
            var merged = new Mesh { name = root.name + "_ProxyMesh" };
            merged.CombineMeshes(allCombined.ToArray(), false);
            AssetDatabase.CreateAsset(merged, meshAssetPath);

            var rootMeshFilter = root.AddComponent<MeshFilter>();
            rootMeshFilter.sharedMesh = merged;
            var rootMeshRenderer = root.AddComponent<MeshRenderer>();
            rootMeshRenderer.sharedMaterial = GetFirstMaterial(materialMap);
            return true;
        }

        private static Material GetFirstMaterial(Dictionary<Material, List<CombineInstance>> map)
        {
            if (map == null || map.Count == 0)
                return null;

            using var enumerator = map.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current.Key : null;
        }

        private static string GetSelectedFolderPath()
        {
            var selected = Selection.activeObject;
            if (selected == null) return null;

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path)) return null;

            if (AssetDatabase.IsValidFolder(path))
                return path;

            // If a file is selected, return its containing folder.
            var dir = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(dir) ? dir.Replace('\\', '/') : null;
        }
    }
}
#endif
