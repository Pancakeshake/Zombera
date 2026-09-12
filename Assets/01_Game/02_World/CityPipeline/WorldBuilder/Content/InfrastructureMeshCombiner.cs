using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Fuses aligned modular infrastructure visuals into one root-local mesh.</summary>
    internal static class InfrastructureMeshCombiner
    {
        private readonly struct SourceSubMesh
        {
            public readonly CombineInstance Combine;
            public readonly Material Material;
            public readonly MaterialPropertyBlock Properties;

            public SourceSubMesh(
                CombineInstance combine,
                Material material,
                MaterialPropertyBlock properties)
            {
                Combine = combine;
                Material = material;
                Properties = properties;
            }
        }

        public static bool FuseIntoRoot(GameObject root, string meshName)
        {
            if (root == null)
                return false;

            var sources = CollectSources(root.transform);
            if (sources.Count == 0)
                return false;

            var mesh = BuildMesh(sources, meshName);
            if (mesh == null)
                return false;

            ApplyCombinedMesh(root, mesh, sources);
            DisableSourceMeshes(root.transform);
            return true;
        }

        private static List<SourceSubMesh> CollectSources(Transform root)
        {
            var sources = new List<SourceSubMesh>(12);
            var rootToWorld = root.worldToLocalMatrix;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                AppendRenderer(root, rootToWorld, renderers[i], sources);
            return sources;
        }

        private static void AppendRenderer(
            Transform root,
            Matrix4x4 rootToWorld,
            MeshRenderer renderer,
            List<SourceSubMesh> sources)
        {
            if (renderer == null || renderer.transform == root)
                return;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            var mesh = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            var transform = rootToWorld * filter.transform.localToWorldMatrix;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var material = subMesh < materials.Length ? materials[subMesh] : renderer.sharedMaterial;
                sources.Add(new SourceSubMesh(
                    new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMesh,
                        transform = transform
                    },
                    material,
                    block));
            }
        }

        private static Mesh BuildMesh(IReadOnlyList<SourceSubMesh> sources, string meshName)
        {
            var combines = new CombineInstance[sources.Count];
            var vertexBudget = 0;
            for (var i = 0; i < sources.Count; i++)
            {
                combines[i] = sources[i].Combine;
                vertexBudget += sources[i].Combine.mesh.vertexCount;
            }

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertexBudget > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.CombineMeshes(combines, mergeSubMeshes: false, useMatrices: true, hasLightmapData: false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ApplyCombinedMesh(
            GameObject root,
            Mesh mesh,
            IReadOnlyList<SourceSubMesh> sources)
        {
            var filter = root.GetComponent<MeshFilter>() ?? root.AddComponent<MeshFilter>();
            var renderer = root.GetComponent<MeshRenderer>() ?? root.AddComponent<MeshRenderer>();
            var collider = root.GetComponent<MeshCollider>() ?? root.AddComponent<MeshCollider>();
            var materials = new Material[sources.Count];
            for (var i = 0; i < sources.Count; i++)
                materials[i] = sources[i].Material;

            filter.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            for (var i = 0; i < sources.Count; i++)
                renderer.SetPropertyBlock(sources[i].Properties, i);
            collider.sharedMesh = mesh;
            collider.convex = false;
        }

        private static void DisableSourceMeshes(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.transform == root)
                    continue;
                renderer.enabled = false;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                    filter.sharedMesh = null;
            }
        }
    }
}
