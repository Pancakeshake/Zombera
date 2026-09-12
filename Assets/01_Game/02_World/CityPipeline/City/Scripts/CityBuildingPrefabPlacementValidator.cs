using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public static class CityBuildingPrefabPlacementValidator
    {
        public static bool FitsInsideOutline(
            GameObject instance,
            IReadOnlyList<Vector2> outline,
            float safetyMarginMeters,
            float sampleStepMeters)
        {
#if UNITY_EDITOR
            if (instance == null || outline == null || outline.Count < 3)
                return false;

            if (!CityBuildingPrefabFootprintUtility.TryMeasureWorldBoundsXZ(instance, out var boundsXZ))
                return false;

            var center = boundsXZ.center;
            var halfX = Mathf.Max(0.1f, boundsXZ.width * 0.5f - safetyMarginMeters);
            var halfZ = Mathf.Max(0.1f, boundsXZ.height * 0.5f - safetyMarginMeters);
            return CityNamedAreaPolygonUtility.ContainsAxisAlignedRectSampled(
                outline,
                center,
                halfX,
                halfZ,
                sampleStepMeters);
#else
            return true;
#endif
        }
    }
}
