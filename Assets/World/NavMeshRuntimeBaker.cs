using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Zombera.World
{
    /// <summary>
    /// Bakes a NavMesh at runtime using NavMeshBuilder directly — no NavMeshSurface asset required.
    /// Collects MeshFilters on this object and children; falls back to all scene MeshFilters.
    /// Attach to the ground Plane.
    /// </summary>
    public sealed class NavMeshRuntimeBaker : MonoBehaviour
    {
        [Tooltip("Extra vertical padding included in the bake bounds.")]
        [SerializeField] private float boundsHeightPadding = 10f;

        [Tooltip("Agent type ID to bake for (0 = Humanoid).")]
        [SerializeField] private int agentTypeID = 0;

        private NavMeshDataInstance navMeshInstance;

        private void Awake()
        {
            BakeNavMesh();
        }

        private void OnDestroy()
        {
            if (navMeshInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshInstance);
            }
        }

        private void BakeNavMesh()
        {
            List<NavMeshBuildSource> sources = CollectSources();

            if (sources.Count == 0)
            {
                Debug.LogWarning("[NavMeshRuntimeBaker] No mesh sources found. NavMesh not baked.", this);
                return;
            }

            Bounds bounds = ComputeBounds(sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeID);

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                settings, sources, bounds, Vector3.zero, Quaternion.identity);

            if (data == null)
            {
                Debug.LogError("[NavMeshRuntimeBaker] BuildNavMeshData returned null.", this);
                return;
            }

            navMeshInstance = NavMesh.AddNavMeshData(data);

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[NavMeshRuntimeBaker] Done. Sources: {sources.Count}, Vertices: {tri.vertices.Length}, Triangles: {tri.indices.Length / 3}");
        }

        private List<NavMeshBuildSource> CollectSources()
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();

            // Use physics colliders as the bake geometry so that doorway gaps in
            // BoxCollider-based walls are respected. Collecting MeshFilters from
            // render meshes causes solid visual meshes (e.g. SM_DoorWall_A) to
            // fill in the door opening at runtime and override the editor-baked data.
            Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

            foreach (Collider col in colliders)
            {
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                NavMeshBuildSource src = new NavMeshBuildSource
                {
                    transform = col.transform.localToWorldMatrix,
                    area      = 0
                };

                if (col is BoxCollider box)
                {
                    src.shape     = NavMeshBuildSourceShape.Box;
                    src.transform = Matrix4x4.TRS(
                        col.transform.TransformPoint(box.center),
                        col.transform.rotation,
                        col.transform.lossyScale);
                    src.size = box.size;
                    sources.Add(src);
                }
                else if (col is MeshCollider mc && mc.sharedMesh != null)
                {
                    src.shape        = NavMeshBuildSourceShape.Mesh;
                    src.sourceObject = mc.sharedMesh;
                    sources.Add(src);
                }
                else if (col is TerrainCollider tc && tc.terrainData != null)
                {
                    src.shape        = NavMeshBuildSourceShape.Terrain;
                    src.sourceObject = tc.terrainData;
                    sources.Add(src);
                }
            }

            return sources;
        }

        private Bounds ComputeBounds(List<NavMeshBuildSource> sources)
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);

            foreach (NavMeshBuildSource src in sources)
            {
                b.Encapsulate(new Vector3(src.transform.m03, src.transform.m13, src.transform.m23));
            }

            b.Expand(new Vector3(0f, boundsHeightPadding * 2f, 0f));
            b.Expand(20f);
            return b;
        }
    }
}
