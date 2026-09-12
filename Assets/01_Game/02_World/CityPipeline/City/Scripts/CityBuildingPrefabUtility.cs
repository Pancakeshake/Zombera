using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Static prefab-inspection utilities for building placement.
    ///     Extracted from <see cref="CityPrefabDistrictBuildingPlacer"/>.
    /// </summary>
    internal static class CityBuildingPrefabUtility
    {
        /// <summary>
        ///     Finds the StairSocket child on a prefab, determines which face it sits on,
        ///     and returns the yaw offset needed so that face points toward the road.
        ///     Returns 0 if no StairSocket found (assumes +Z is the door).
        /// </summary>
        public static float GetStairSocketFaceYawOffset(GameObject prefab)
        {
            if (prefab == null) return 0f;

            var socket = FindStairSocket(prefab.transform);
            if (socket == null) return 0f;

            var lp = socket.localPosition;
            var absX = Mathf.Abs(lp.x);
            var absZ = Mathf.Abs(lp.z);

            // Determine dominant face.
            if (absZ >= absX)
            {
                // +Z or -Z face
                return lp.z >= 0f ? 0f : 180f;
            }
            else
            {
                // +X or -X face
                return lp.x >= 0f ? 270f : 90f; // +X→270 (face east), -X→90 (face west)
            }
        }

        public static Transform FindDoorChild(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var n = child.name;
                if (n.StartsWith("Door", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Entrance", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindDoorChild(child);
                if (found != null) return found;
            }
            return null;
        }

        public static Transform FindStairSocket(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindStairSocket(child);
                if (found != null) return found;
            }

            return null;
        }

        public static Vector2 MeasurePrefabFootprint(GameObject prefab)
        {
            if (prefab == null) return Vector2.zero;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Vector2(3f, 3f);

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return new Vector2(bounds.size.x, bounds.size.z);
        }

#if UNITY_EDITOR
        public static int ClearPlacedBuildingsUnder(Transform areaTransform)
        {
            if (areaTransform == null)
                return 0;

            var container = areaTransform.Find("PlacedBuildings");
            if (container == null)
                return 0;

            var count = container.childCount;
            if (Zombera.World.Roads.CityPrefabRoadNetworkBuilder.SuppressEditorDestroyUndo)
                Object.DestroyImmediate(container.gameObject);
            else
                UnityEditor.Undo.DestroyObjectImmediate(container.gameObject);
            return count;
        }
#endif
    }
}
