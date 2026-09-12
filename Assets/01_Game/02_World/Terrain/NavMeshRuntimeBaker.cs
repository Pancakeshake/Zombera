#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Bakes a NavMesh at runtime using NavMeshBuilder directly — no NavMeshSurface asset required.
    ///     Collects MeshFilters on this object and children; falls back to all scene MeshFilters.
    ///     Attach to the ground Plane.
    /// </summary>
    public sealed class NavMeshRuntimeBaker : MonoBehaviour
    {
        [Tooltip("Extra vertical padding included in the bake bounds.")] [SerializeField]
        private float boundsHeightPadding = 10f;

        [Tooltip("Agent type ID to bake for (0 = Humanoid).")] [SerializeField]
        private int agentTypeID;

        private NavMeshDataInstance _navMeshInstance;

        private void Awake()
        {
            BakeNavMesh();
        }

        private void OnDestroy()
        {
            if (_navMeshInstance.valid) NavMesh.RemoveNavMeshData(_navMeshInstance);
        }

        private void BakeNavMesh()
        {
            var sources = CollectSources();

            if (sources.Count == 0)
            {
                Debug.LogWarning("[NavMeshRuntimeBaker] No mesh sources found. NavMesh not baked.", this);
                return;
            }

            var bounds = ComputeBounds(sources);
            var settings = NavMesh.GetSettingsByID(agentTypeID);

            var data = NavMeshBuilder.BuildNavMeshData(
                settings, sources, bounds, Vector3.zero, Quaternion.identity);

            if (data == null)
            {
                Debug.LogError("[NavMeshRuntimeBaker] BuildNavMeshData returned null.", this);
                return;
            }

            _navMeshInstance = NavMesh.AddNavMeshData(data);

            var tri = NavMesh.CalculateTriangulation();
            Debug.Log(
                $"[NavMeshRuntimeBaker] Done. Sources: {sources.Count}, Vertices: {tri.vertices.Length}, Triangles: {tri.indices.Length / 3}");
        }

        private static List<NavMeshBuildSource> CollectSources()
        {
            var sources = new List<NavMeshBuildSource>();

            // Use physics colliders as the bake geometry so that doorway gaps in
            // BoxCollider-based walls are respected. Collecting MeshFilters from
            // render meshes causes solid visual meshes (e.g. SM_DoorWall_A) to
            // fill in the door opening at runtime and override the editor-baked data.
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

            foreach (var col in colliders)
            {
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                switch (col)
                {
                    case BoxCollider box:
                        var boxSource = new NavMeshBuildSource
                        {
                            transform = Matrix4x4.TRS(
                                col.transform.TransformPoint(box.center),
                                col.transform.rotation,
                                col.transform.lossyScale),
                            area = 0,
                            shape = NavMeshBuildSourceShape.Box,
                            size = box.size
                        };
                        sources.Add(boxSource);
                        break;

                    case MeshCollider { sharedMesh: not null } meshCollider:
                        var meshSource = new NavMeshBuildSource
                        {
                            transform = col.transform.localToWorldMatrix,
                            area = 0,
                            shape = NavMeshBuildSourceShape.Mesh,
                            sourceObject = meshCollider.sharedMesh
                        };
                        sources.Add(meshSource);
                        break;

                    case TerrainCollider { terrainData: not null } terrainCollider:
                        var terrainSource = new NavMeshBuildSource
                        {
                            transform = col.transform.localToWorldMatrix,
                            area = 0,
                            shape = NavMeshBuildSourceShape.Terrain,
                            sourceObject = terrainCollider.terrainData
                        };
                        sources.Add(terrainSource);
                        break;
                }
            }

            return sources;
        }

        private Bounds ComputeBounds(List<NavMeshBuildSource> sources)
        {
            var b = new Bounds(transform.position, Vector3.zero);

            foreach (var src in sources)
                b.Encapsulate(new Vector3(src.transform.m03, src.transform.m13, src.transform.m23));

            b.Expand(new Vector3(0f, boundsHeightPadding * 2f, 0f));
            b.Expand(20f);
            return b;
        }
    }
}