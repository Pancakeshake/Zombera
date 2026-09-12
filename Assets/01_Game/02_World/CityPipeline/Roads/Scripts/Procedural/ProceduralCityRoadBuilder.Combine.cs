using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.World.Roads
{
    public static partial class ProceduralCityRoadBuilder
    {
        private static CombineInstance[] _combineArray = Array.Empty<CombineInstance>();

        public static void CombineLayerMeshes(Transform networkRoot, RoadNetworkSettings settings)
        {
            if (networkRoot == null || settings == null || !settings.combineProceduralMeshesPerLayer)
                return;

            var combined = 0;
            combined += CombineFolderMeshes(networkRoot, ProceduralRoadNetworkNames.Asphalt);
            combined += CombineFolderMeshes(networkRoot, ProceduralRoadNetworkNames.Sidewalks);
            combined += CombineFolderMeshes(networkRoot, ProceduralRoadNetworkNames.Footpaths);
            combined += CombineFolderMeshes(networkRoot, ProceduralRoadNetworkNames.Driveways);

            if (combined > 0)
            {
                Debug.Log(
                    "[ProceduralCityRoadBuilder] Combined procedural meshes into " + combined +
                    " layer object(s). Disable combineProceduralMeshesPerLayer to keep per-strip objects.",
                    networkRoot);
            }
        }

        public static int FinalizeAccumulator(
            Transform networkRoot,
            RoadNetworkSettings settings,
            ProceduralLayerMeshAccumulator accumulator)
        {
            if (networkRoot == null || settings == null || accumulator == null ||
                !settings.combineProceduralMeshesPerLayer)
                return 0;

            var combined = accumulator.FinalizeAll(networkRoot);
            if (combined > 0)
            {
                Debug.Log(
                    "[ProceduralCityRoadBuilder] Accumulated procedural meshes into " + combined +
                    " layer object(s) (verts=" + accumulator.TotalVertexCount +
                    ", strips=" + accumulator.StripAppendCount + ").",
                    networkRoot);
            }

            return combined;
        }

        private static int CombineFolderMeshes(Transform networkRoot, string folderName)
        {
            var folder = networkRoot.Find(folderName);
            if (folder == null || folder.childCount == 0)
                return 0;

            var groups = new Dictionary<Material, List<CombineInstance>>(4);
            var sources = new List<GameObject>(folder.childCount);

            for (var i = 0; i < folder.childCount; i++)
            {
                var child = folder.GetChild(i);
                if (child == null || child.name.StartsWith("Combined_"))
                    continue;

                var meshFilter = child.GetComponent<MeshFilter>();
                var meshRenderer = child.GetComponent<MeshRenderer>();
                if (meshFilter == null || meshRenderer == null || meshFilter.sharedMesh == null)
                    continue;

                var material = meshRenderer.sharedMaterial;
                if (material == null)
                    continue;

                if (!groups.TryGetValue(material, out var instances))
                {
                    instances = new List<CombineInstance>(32);
                    groups[material] = instances;
                }

                // Strip verts are authored in world space under an identity parent.
                instances.Add(new CombineInstance
                {
                    mesh = meshFilter.sharedMesh,
                    transform = Matrix4x4.identity
                });
                sources.Add(child.gameObject);
            }

            if (groups.Count == 0)
                return 0;

            var combinedCount = 0;
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2)
                    continue;

                var count = pair.Value.Count;
                EnsureCombineArray(count);
                for (var i = 0; i < count; i++)
                    _combineArray[i] = pair.Value[i];

                var combinedMesh = new Mesh
                {
                    name = folderName + "_Combined",
                    indexFormat = IndexFormat.UInt32
                };
                combinedMesh.CombineMeshes(_combineArray, true, true);
                combinedMesh.RecalculateBounds();

                var go = new GameObject("Combined_" + pair.Key.name);
                go.transform.SetParent(folder, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = combinedMesh;
                mr.sharedMaterial = pair.Key;
                combinedCount++;
            }

            if (combinedCount == 0)
                return 0;

            for (var i = 0; i < sources.Count; i++)
                DestroyObject(sources[i]);

            return combinedCount;
        }

        private static void EnsureCombineArray(int count)
        {
            if (_combineArray != null && _combineArray.Length == count)
                return;
            _combineArray = new CombineInstance[count];
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif
            UnityEngine.Object.Destroy(target);
        }
    }
}
