using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Sea-floor depth helpers. Terrains use <see cref="OceanDepthCache"/> (Crest docs);
    /// <see cref="RegisterSeaFloorDepthInput"/> is for MeshRenderer bathymetry (piers, carved meshes).
    /// </summary>
    internal static class CrestSeaFloorDepthUtility
    {
        public static LayerMask ResolveDepthCacheLayers(WorldWaterProfile water)
        {
            var mask = water != null ? water.DepthCacheLayers.value : 0;
            if (mask == 0)
                mask = 1 << 0; // Default

            var terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null)
                    continue;
                mask |= 1 << terrain.gameObject.layer;
            }

            return mask;
        }

        /// <summary>
        /// Attaches Crest per-frame depth input to MeshRenderers on the profile's sea-floor geometry layers.
        /// Skips Crest clip inputs and already-registered objects.
        /// </summary>
        public static void EnsureMeshSeaFloorInputs(Transform root, LayerMask geometryLayers)
        {
            if (root == null || geometryLayers.value == 0)
                return;

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                if (((1 << renderer.gameObject.layer) & geometryLayers.value) == 0)
                    continue;
                if (renderer.GetComponent<RegisterClipSurfaceInput>() != null)
                    continue;
                if (renderer.GetComponent<RegisterSeaFloorDepthInput>() != null)
                    continue;

                var input = renderer.gameObject.AddComponent<RegisterSeaFloorDepthInput>();
                CrestOceanConfigurator.EnableEditModeLodInput(input);
            }
        }
    }
}
