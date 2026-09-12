#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    internal sealed class StreamedCityProxyGeometryHelper
    {
        private readonly bool disableShadowsOnProxyRenderers;

        internal StreamedCityProxyGeometryHelper(bool disableShadowsOnProxyRenderers)
        {
            this.disableShadowsOnProxyRenderers = disableShadowsOnProxyRenderers;
        }

        internal bool TryCollapseMeshRenderers(
            GameObject proxyRoot,
            string outputMeshAssetPath,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (proxyRoot == null)
            {
                failureReason = "Proxy root is null.";
                return false;
            }

            var grouped = BuildMeshCombineGroups(proxyRoot.transform);
            if (grouped.Count == 0)
            {
                failureReason = "No combineable MeshRenderer/MeshFilter pairs were found for proxy generation.";
                return false;
            }

            var combined = new List<(Material material, Mesh mesh)>(grouped.Count);
            var meshIndex = 0;

            foreach (var kvp in grouped)
            {
                var instances = kvp.Value;
                if (instances == null || instances.Count == 0)
                    continue;

                var estimatedVertexCount = 0;
                for (var i = 0; i < instances.Count; i++)
                    if (instances[i].mesh != null)
                        estimatedVertexCount += instances[i].mesh.vertexCount;

                var combinedMesh = new Mesh
                {
                    name = "ProxyMesh_" + meshIndex
                };

                if (estimatedVertexCount > 65535)
                    combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                combinedMesh.CombineMeshes(instances.ToArray(), true, true, false);
                combinedMesh.RecalculateBounds();

                if (combinedMesh.vertexCount == 0)
                {
                    Object.DestroyImmediate(combinedMesh);
                    continue;
                }

                combined.Add((kvp.Key, combinedMesh));
                meshIndex++;
            }

            if (combined.Count == 0)
            {
                failureReason = "Mesh combine produced no valid output meshes.";
                return false;
            }

            var existingMeshAsset = AssetDatabase.LoadAssetAtPath<Object>(outputMeshAssetPath);
            if (existingMeshAsset != null)
                AssetDatabase.DeleteAsset(outputMeshAssetPath);

            AssetDatabase.CreateAsset(combined[0].mesh, outputMeshAssetPath);
            for (var i = 1; i < combined.Count; i++)
                AssetDatabase.AddObjectToAsset(combined[i].mesh, outputMeshAssetPath);

            AssetDatabase.ImportAsset(outputMeshAssetPath, ImportAssetOptions.ForceUpdate);

            DestroyAllChildren(proxyRoot.transform);

            for (var i = 0; i < combined.Count; i++)
            {
                var child = new GameObject(combined[i].mesh.name);
                child.transform.SetParent(proxyRoot.transform, false);

                var filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = combined[i].mesh;

                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = combined[i].material;

                if (disableShadowsOnProxyRenderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            return true;
        }

        internal void EnsureProxyRootCollider(GameObject proxyRoot, StreamedCityBuildingEntry entry)
        {
            if (proxyRoot == null)
                return;

            var boxCollider = proxyRoot.GetComponent<BoxCollider>();
            if (boxCollider == null)
                boxCollider = proxyRoot.AddComponent<BoxCollider>();

            var widthFromEntry = entry != null ? Mathf.Max(1f, entry.footprintWidthMeters) : 8f;
            var depthFromEntry = entry != null ? Mathf.Max(1f, entry.footprintDepthMeters) : 8f;

            if (TryComputeLocalRendererBounds(proxyRoot.transform, out var localBounds))
            {
                var size = localBounds.size;
                size.x = Mathf.Max(size.x, widthFromEntry * 0.8f);
                size.y = Mathf.Max(size.y, 2.2f);
                size.z = Mathf.Max(size.z, depthFromEntry * 0.8f);

                boxCollider.center = localBounds.center;
                boxCollider.size = size;
            }
            else
            {
                boxCollider.center = new Vector3(0f, 1.5f, 0f);
                boxCollider.size = new Vector3(widthFromEntry, 3f, depthFromEntry);
            }

            boxCollider.isTrigger = false;
            boxCollider.enabled = true;
        }

        private static Dictionary<Material, List<CombineInstance>> BuildMeshCombineGroups(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            if (root == null)
                return groups;

            var rootWorldToLocal = root.worldToLocalMatrix;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;

                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    continue;

                var subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
                if (subMeshCount <= 0)
                    continue;

                var localToRoot = rootWorldToLocal * renderer.transform.localToWorldMatrix;
                for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    var material = materials[subMeshIndex];
                    if (material == null)
                        continue;

                    if (!groups.TryGetValue(material, out var combineList))
                    {
                        combineList = new List<CombineInstance>();
                        groups[material] = combineList;
                    }

                    combineList.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMeshIndex,
                        transform = localToRoot
                    });
                }
            }

            return groups;
        }

        private static void DestroyAllChildren(Transform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool TryComputeLocalRendererBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;

            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var worldBounds = renderer.bounds;
                var corners = BuildBoundsCorners(worldBounds);
                for (var c = 0; c < corners.Length; c++)
                {
                    var local = root.InverseTransformPoint(corners[c]);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(local);
                    }
                }
            }

            return hasBounds;
        }

        private static Vector3[] BuildBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;

            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }
    }
}
#endif
