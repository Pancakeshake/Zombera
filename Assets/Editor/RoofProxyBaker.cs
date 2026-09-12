#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Bakes a combined proxy mesh from a generated building's roof assembly.
    ///     Run via Tools → Build → Mod Kits → Building Generator → Bake Roof Proxy.
    ///     Select a generated building prefab, or it processes the selected asset.
    /// </summary>
    internal static class RoofProxyBaker
    {
        private const string MenuPath = "Tools/Build/Mod Kits/Building Generator/Bake Roof Proxy";

        [MenuItem(MenuPath, priority = -497)]
        private static void BakeSelected()
        {
            var targets = new List<GameObject>();
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab"))
                    continue;
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (loaded != null)
                    targets.Add(loaded);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake Roof Proxy",
                    "Select one or more generated building prefabs in the Project window first.", "OK");
                return;
            }

            var baked = 0;
            foreach (var target in targets)
                if (BakeRoofProxy(target))
                    baked++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Bake Roof Proxy",
                $"Baked proxy for {baked} / {targets.Count} building(s).", "OK");
        }

        private static bool BakeRoofProxy(GameObject buildingPrefab)
        {
            var prefabPath = AssetDatabase.GetAssetPath(buildingPrefab);
            if (string.IsNullOrWhiteSpace(prefabPath))
                return false;

            var content = PrefabUtility.LoadPrefabContents(prefabPath);
            if (content == null)
                return false;

            try
            {
                var roofParent = content.transform.Find("Roof");
                if (roofParent == null)
                {
                    Debug.LogWarning($"[RoofProxyBaker] No 'Roof' child found in {buildingPrefab.name}.");
                    return false;
                }

                var renderers = roofParent.GetComponentsInChildren<MeshRenderer>(true);
                var filters = roofParent.GetComponentsInChildren<MeshFilter>(true);

                if (renderers.Length == 0 || filters.Length == 0)
                {
                    Debug.LogWarning($"[RoofProxyBaker] No renderers under 'Roof' in {buildingPrefab.name}.");
                    return false;
                }

                // Build combined mesh
                var combineInstances = new List<CombineInstance>();
                var materials = new List<Material>();

                for (var i = 0; i < filters.Length && i < renderers.Length; i++)
                {
                    if (filters[i] == null || renderers[i] == null || filters[i].sharedMesh == null)
                        continue;

                    var mats = renderers[i].sharedMaterials;
                    var baseIndex = materials.Count;

                    var ci = new CombineInstance
                    {
                        mesh = filters[i].sharedMesh,
                        transform = roofParent.worldToLocalMatrix * filters[i].transform.localToWorldMatrix
                    };

                    foreach (var mat in mats)
                    {
                        combineInstances.Add(ci);
                        materials.Add(mat);
                    } // first mat for each, rest ignored if > 1
                }

                if (combineInstances.Count == 0)
                    return false;

                var combinedMesh = new Mesh { name = $"{buildingPrefab.name}_RoofProxy" };
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

                // Create proxy GameObject as sibling to Roof
                var proxyGo = new GameObject("Roof_Proxy");
                proxyGo.transform.SetParent(content.transform, false);
                proxyGo.transform.localPosition = Vector3.zero;
                proxyGo.transform.localRotation = Quaternion.identity;

                var mf = proxyGo.AddComponent<MeshFilter>();
                mf.sharedMesh = combinedMesh;

                var mr = proxyGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = materials.ToArray();

                // Save the proxy mesh as an asset
                var meshDir = System.IO.Path.GetDirectoryName(prefabPath);
                var meshPath = $"{meshDir}/{buildingPrefab.name}_RoofProxy.asset";
                AssetDatabase.CreateAsset(combinedMesh, meshPath);

                // Save the updated prefab
                PrefabUtility.SaveAsPrefabAsset(content, prefabPath);
                Debug.Log($"[RoofProxyBaker] Baked roof proxy for '{buildingPrefab.name}' ({combineInstances.Count} instances).");

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(content);
            }
        }
    }
}
#endif
