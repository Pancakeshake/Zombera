using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Crest
{
    [DisallowMultipleComponent]
    internal sealed class CrestGeneratedInputCleanup : MonoBehaviour
    {
        private void OnDestroy()
        {
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var filters = GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null) meshes.Add(filters[i].sharedMesh);
            }
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                if (material == null || (material.hideFlags & HideFlags.HideAndDontSave) == 0) continue;
                materials.Add(material);
                if (material.HasProperty("_FlowMap"))
                {
                    var texture = material.GetTexture("_FlowMap");
                    if (texture != null) textures.Add(texture);
                }
            }
            DestroyObjects(meshes);
            DestroyObjects(materials);
            DestroyObjects(textures);
        }

        private static void DestroyObjects<T>(IEnumerable<T> objects) where T : Object
        {
            foreach (var target in objects)
            {
                if (target == null) continue;
                if (Application.isPlaying) Destroy(target);
                else DestroyImmediate(target);
            }
        }
    }
}
