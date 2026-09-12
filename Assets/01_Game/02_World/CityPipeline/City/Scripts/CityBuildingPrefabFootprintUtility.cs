using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    public static class CityBuildingPrefabFootprintUtility
    {
        public const float ModularGridCellMeters = 3f;

        /// <summary>
        ///     Per-prefab measurement cache. Building placement re-measures the same
        ///     catalog prefabs thousands of times per region build; hierarchy walks
        ///     (and worst case, throwaway temporary instances) dominated the runtime.
        /// </summary>
        private static readonly Dictionary<GameObject, BuildingFootprintInfo> FootprintCache = new();

        public static void ClearFootprintCache() => FootprintCache.Clear();

        public static bool TryEstimateFootprint(GameObject prefab, out float width, out float depth)
        {
            if (TryMeasureFootprint(prefab, out var info))
            {
                width = info.WidthMeters;
                depth = info.DepthMeters;
                return true;
            }

            width = 0f;
            depth = 0f;
            return false;
        }

        public static bool TryMeasureFootprint(GameObject prefab, out BuildingFootprintInfo info)
        {
            info = BuildingFootprintInfo.Invalid;
            if (prefab == null)
                return false;

            if (FootprintCache.TryGetValue(prefab, out info))
                return info.IsValid;


            // Prefer hierarchy-based measurement — reads renderer localBounds from the
            // prefab asset directly without instantiating. Falls back to temporary instance
            // only for prefabs whose bounds can't be resolved from the hierarchy.
            var measured = TryMeasureFromHierarchy(prefab, out info);
#if UNITY_EDITOR
            if (!measured)
                measured = TryMeasureFromTemporaryInstance(prefab, out info);
#endif
            if (measured)
                FootprintCache[prefab] = info;

            return measured;
        }

        public static Vector3 ResolveRootPosition(Vector3 footprintCenter, Quaternion rotation, Vector2 centerOffsetXZ)
        {
            var localOffset = new Vector3(centerOffsetXZ.x, 0f, centerOffsetXZ.y);
            return footprintCenter - rotation * localOffset;
        }

        /// <summary>
        ///     Uniformly scales an instance on XZ so its world AABB matches
        ///     <paramref name="lotRect"/>, then snaps the bounds centre to the
        ///     lot centre. Used by commercial store-lot placement so catalog
        ///     pieces fill the lot exactly (wall-to-wall with neighbours).
        /// </summary>
        public static bool TryScaleInstanceToLotRect(GameObject instance, Rect lotRect, out Rect placedBounds)
        {
            placedBounds = default;
            if (instance == null || lotRect.width < 0.5f || lotRect.height < 0.5f)
                return false;

#if UNITY_EDITOR
            if (!TryMeasureWorldBoundsXZ(instance, out var bounds) ||
                bounds.width < 0.05f || bounds.height < 0.05f)
                return false;

            var sx = lotRect.width / bounds.width;
            var sz = lotRect.height / bounds.height;
            var yawQuarter = Mathf.RoundToInt(instance.transform.eulerAngles.y / 90f) % 4;
            if (yawQuarter < 0) yawQuarter += 4;

            var scale = instance.transform.localScale;
            // Cardinal yaw: even quarters keep local X→world X; odd quarters swap.
            if ((yawQuarter & 1) == 0)
            {
                scale.x *= sx;
                scale.z *= sz;
            }
            else
            {
                scale.x *= sz;
                scale.z *= sx;
            }

            instance.transform.localScale = scale;

            if (!TryMeasureWorldBoundsXZ(instance, out placedBounds))
                return false;

            var delta = new Vector3(
                lotRect.center.x - placedBounds.center.x,
                0f,
                lotRect.center.y - placedBounds.center.y);
            instance.transform.position += delta;
            return TryMeasureWorldBoundsXZ(instance, out placedBounds);
#else
            return false;
#endif
        }

        public static void ClampToBlock(ref float width, ref float depth, Rect blockBounds, float maxFraction = 0.35f)
        {
            if (blockBounds.width <= 0.01f || blockBounds.height <= 0.01f)
                return;

            var maxWidth = Mathf.Max(ModularGridCellMeters, blockBounds.width * maxFraction);
            var maxDepth = Mathf.Max(ModularGridCellMeters, blockBounds.height * maxFraction);
            width = Mathf.Min(width, maxWidth);
            depth = Mathf.Min(depth, maxDepth);
        }

#if UNITY_EDITOR
        public static bool TryMeasureWorldBoundsXZ(GameObject instance, out Rect boundsXZ)
        {
            boundsXZ = default;
            if (instance == null)
                return false;

            var hasBounds = false;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var worldBounds = renderer.bounds;
                min.x = Mathf.Min(min.x, worldBounds.min.x);
                min.y = Mathf.Min(min.y, worldBounds.min.z);
                max.x = Mathf.Max(max.x, worldBounds.max.x);
                max.y = Mathf.Max(max.y, worldBounds.max.z);
                hasBounds = true;
            }

            if (!hasBounds)
                return false;

            boundsXZ = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return boundsXZ.width > 0.01f && boundsXZ.height > 0.01f;
        }

        private static bool TryMeasureFromTemporaryInstance(GameObject prefab, out BuildingFootprintInfo info)
        {
            info = BuildingFootprintInfo.Invalid;
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    return false;

                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return TryMeasureWorldBoundsXZ(instance, out var boundsXZ) &&
                       TryCreateFromBounds(boundsXZ, out info);
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }
#endif

        private static bool TryMeasureFromHierarchy(GameObject prefab, out BuildingFootprintInfo info)
        {
            info = BuildingFootprintInfo.Invalid;
            var root = prefab.transform;
            var hasBounds = false;
            var min = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, 0f, float.NegativeInfinity);

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var localBounds = renderer.localBounds;
                var matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var center = matrix.MultiplyPoint3x4(localBounds.center);
                var extents = localBounds.extents;
                var axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
                var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
                var corner = new Vector3(
                    center.x + Mathf.Abs(axisX.x) + Mathf.Abs(axisZ.x),
                    0f,
                    center.z + Mathf.Abs(axisX.z) + Mathf.Abs(axisZ.z));
                var opposite = new Vector3(
                    center.x - Mathf.Abs(axisX.x) - Mathf.Abs(axisZ.x),
                    0f,
                    center.z - Mathf.Abs(axisX.z) - Mathf.Abs(axisZ.z));
                min = Vector3.Min(min, opposite);
                max = Vector3.Max(max, corner);
                hasBounds = true;
            }

            if (!hasBounds)
                return false;

            var boundsXZ = Rect.MinMaxRect(min.x, min.z, max.x, max.z);
            return TryCreateFromBounds(boundsXZ, out info);
        }

        private static bool TryCreateFromBounds(Rect boundsXZ, out BuildingFootprintInfo info)
        {
            info = BuildingFootprintInfo.Invalid;
            if (boundsXZ.width <= 0.01f || boundsXZ.height <= 0.01f)
                return false;

            info = new BuildingFootprintInfo
            {
                WidthMeters = boundsXZ.width,
                DepthMeters = boundsXZ.height,
                CenterOffsetXZ = boundsXZ.center,
                IsValid = true
            };
            return true;
        }
    }
}
