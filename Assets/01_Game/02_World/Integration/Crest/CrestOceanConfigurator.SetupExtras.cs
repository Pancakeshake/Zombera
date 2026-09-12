using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Underwater renderer and sea-floor depth cache setup for Crest oceans.
    /// Wave shapes live in CrestOceanConfigurator.Waves.
    /// </summary>
    internal static partial class CrestOceanConfigurator
    {
        private const string DepthCacheObjectName = "Crest Ocean Depth Cache";
        private const int DepthCacheResolution = 1024;
        private const float DepthCacheMaxTerrainHeight = 1000f;

        public static void EnsureUnderwaterRenderer(Camera camera)
        {
            if (camera == null)
                return;

            if (camera.GetComponent<UnderwaterRenderer>() != null)
                return;

            camera.gameObject.AddComponent<UnderwaterRenderer>();
        }

        public static void EnsureDepthCache(Transform oceanRoot, float seaLevelWorldY, Rect boundsXZ)
        {
            EnsureDepthCache(oceanRoot, seaLevelWorldY, boundsXZ, water: null);
        }

        public static void EnsureDepthCache(
            Transform oceanRoot,
            float seaLevelWorldY,
            Rect boundsXZ,
            WorldWaterProfile water)
        {
            if (oceanRoot == null || boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            var cache = FindOrCreateDepthCache(oceanRoot);
            var cacheTransform = cache.transform;
            var center = boundsXZ.center;
            cacheTransform.position = new Vector3(center.x, seaLevelWorldY, center.y);

            // Match terrain AABB exactly (do not square-up via Max — that overshoots one axis).
            cacheTransform.localScale = new Vector3(
                Mathf.Max(1f, boundsXZ.width),
                1f,
                Mathf.Max(1f, boundsXZ.height));
            cacheTransform.rotation = Quaternion.identity;

            var layers = CrestSeaFloorDepthUtility.ResolveDepthCacheLayers(water);
            SetPrivateField(cache, "_type", OceanDepthCache.OceanDepthCacheType.Realtime);
            SetPrivateField(cache, "_refreshMode", OceanDepthCache.OceanDepthCacheRefreshMode.OnDemand);
            SetPrivateField(cache, "_layers", layers);
            SetPrivateField(cache, "_resolution", DepthCacheResolution);
            SetPrivateField(cache, "_cameraMaxTerrainHeight", DepthCacheMaxTerrainHeight);
            SetPrivateField(cache, "_cameraFarClipPlane", 10000f);
            SetPrivateField(cache, "_hideDepthCacheCam", true);

            cache.PopulateCache(true);
        }

        private static void RemoveGerstnerShapes(Transform oceanRoot)
        {
            var gerstners = oceanRoot.GetComponentsInChildren<ShapeGerstner>(true);
            for (var i = 0; i < gerstners.Length; i++)
            {
                var gerstner = gerstners[i];
                if (gerstner == null)
                    continue;

                var go = gerstner.gameObject;
                if (Application.isPlaying)
                    Object.Destroy(gerstner);
                else
                    Object.DestroyImmediate(gerstner);

                // Drop empty wave-shape hosts left behind by the swap.
                if (go != null && go.GetComponents<Component>().Length <= 1)
                {
                    if (Application.isPlaying)
                        Object.Destroy(go);
                    else
                        Object.DestroyImmediate(go);
                }
            }
        }

        private static OceanDepthCache FindOrCreateDepthCache(Transform oceanRoot)
        {
            var existing = oceanRoot.GetComponentsInChildren<OceanDepthCache>(true);
            OceanDepthCache keep = null;
            for (var i = 0; i < existing.Length; i++)
            {
                var cache = existing[i];
                if (cache == null)
                    continue;

                if (keep == null)
                {
                    keep = cache;
                    continue;
                }

                var duplicate = cache.gameObject;
                if (Application.isPlaying)
                    Object.Destroy(duplicate);
                else
                    Object.DestroyImmediate(duplicate);
            }

            if (keep != null)
                return keep;

            var go = new GameObject(DepthCacheObjectName);
            go.transform.SetParent(oceanRoot, false);
            return go.AddComponent<OceanDepthCache>();
        }
    }
}
