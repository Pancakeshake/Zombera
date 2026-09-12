using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Emits a strip mesh either as a GameObject or into a layer accumulator.</summary>
    internal static class ProceduralMeshEmitUtility
    {
        public static bool Emit(
            Transform parent,
            string folderName,
            string objectName,
            Mesh mesh,
            Material material,
            ProceduralLayerMeshAccumulator accumulator)
        {
            if (mesh == null || material == null)
                return false;

            if (accumulator != null)
            {
                accumulator.AppendMesh(folderName, material, mesh);
                return true;
            }

            if (parent == null)
            {
                DestroyMesh(mesh);
                return false;
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
            return true;
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
    }
}
