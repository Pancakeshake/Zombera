using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     In-memory per-folder/material mesh builder for hub combine mode.
    ///     Appends strip geometry without creating temporary strip GameObjects.
    /// </summary>
    public sealed class ProceduralLayerMeshAccumulator
    {
        private readonly Dictionary<LayerKey, LayerBuffers> _layers = new(8);

        public int LayerCount => _layers.Count;
        public int TotalVertexCount { get; private set; }
        public int StripAppendCount { get; private set; }

        public void Append(
            string folderName,
            Material material,
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<int> triangles)
        {
            if (string.IsNullOrEmpty(folderName) || material == null ||
                vertices == null || uvs == null || triangles == null ||
                vertices.Count < 3 || triangles.Count < 3)
                return;

            if (!TryValidateVertices(vertices))
                return;

            var key = new LayerKey(folderName, material);
            if (!_layers.TryGetValue(key, out var buffers))
            {
                buffers = new LayerBuffers(vertices.Count, triangles.Count);
                _layers[key] = buffers;
            }

            var baseIndex = buffers.Vertices.Count;
            for (var i = 0; i < vertices.Count; i++)
            {
                buffers.Vertices.Add(vertices[i]);
                buffers.Uvs.Add(i < uvs.Count ? uvs[i] : default);
            }

            for (var i = 0; i < triangles.Count; i++)
                buffers.Triangles.Add(baseIndex + triangles[i]);

            TotalVertexCount += vertices.Count;
            StripAppendCount++;
        }

        public void AppendMesh(string folderName, Material material, Mesh mesh)
        {
            if (mesh == null)
                return;

            var vertices = new List<Vector3>(mesh.vertexCount);
            var uvs = new List<Vector2>(mesh.vertexCount);
            var triangles = new List<int>(mesh.triangles.Length);
            mesh.GetVertices(vertices);
            mesh.GetUVs(0, uvs);
            mesh.GetTriangles(triangles, 0);
            Append(folderName, material, vertices, uvs, triangles);
            DestroyMesh(mesh);
        }

        public int FinalizeAll(Transform networkRoot)
        {
            if (networkRoot == null || _layers.Count == 0)
                return 0;

            var combined = 0;
            foreach (var pair in _layers)
            {
                var buffers = pair.Value;
                if (buffers.Vertices.Count < 3 || buffers.Triangles.Count < 3)
                    continue;

                var folder = EnsureChildFolder(networkRoot, pair.Key.FolderName);
                var mesh = new Mesh
                {
                    name = pair.Key.FolderName + "_Combined",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.SetVertices(buffers.Vertices);
                mesh.SetUVs(0, buffers.Uvs);
                mesh.SetTriangles(buffers.Triangles, 0, true);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                if (!IsFiniteBounds(mesh.bounds))
                {
                    DestroyMesh(mesh);
                    continue;
                }

                var go = new GameObject("Combined_" + pair.Key.Material.name);
                go.transform.SetParent(folder, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = pair.Key.Material;
                combined++;
            }

            _layers.Clear();
            return combined;
        }

        private static bool TryValidateVertices(IReadOnlyList<Vector3> vertices)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                    return false;
            }

            return true;
        }

        private static bool IsFiniteBounds(Bounds bounds) =>
            !(float.IsNaN(bounds.center.x) || float.IsNaN(bounds.center.y) || float.IsNaN(bounds.center.z) ||
              float.IsInfinity(bounds.center.x) || float.IsInfinity(bounds.center.y) ||
              float.IsInfinity(bounds.center.z) ||
              float.IsNaN(bounds.extents.x) || float.IsNaN(bounds.extents.y) || float.IsNaN(bounds.extents.z) ||
              float.IsInfinity(bounds.extents.x) || float.IsInfinity(bounds.extents.y) ||
              float.IsInfinity(bounds.extents.z) ||
              bounds.extents.x < 0f || bounds.extents.y < 0f || bounds.extents.z < 0f);

        private static Transform EnsureChildFolder(Transform parent, string folderName)
        {
            var existing = parent.Find(folderName);
            if (existing != null)
                return existing;

            var folder = new GameObject(folderName);
            folder.transform.SetParent(parent, false);
            return folder.transform;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(mesh);
                return;
            }
#endif
            Object.Destroy(mesh);
        }

        private readonly struct LayerKey
        {
            public readonly string FolderName;
            public readonly Material Material;

            public LayerKey(string folderName, Material material)
            {
                FolderName = folderName;
                Material = material;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FolderName != null ? FolderName.GetHashCode() : 0) * 397 ^
                           (Material != null ? Material.GetHashCode() : 0);
                }
            }

            public override bool Equals(object obj) =>
                obj is LayerKey other && FolderName == other.FolderName && Material == other.Material;
        }

        private sealed class LayerBuffers
        {
            public readonly List<Vector3> Vertices;
            public readonly List<Vector2> Uvs;
            public readonly List<int> Triangles;

            public LayerBuffers(int vertexHint, int triangleHint)
            {
                Vertices = new List<Vector3>(Mathf.Max(64, vertexHint));
                Uvs = new List<Vector2>(Mathf.Max(64, vertexHint));
                Triangles = new List<int>(Mathf.Max(128, triangleHint));
            }
        }
    }
}
