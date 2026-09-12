using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Crest-agnostic lake eligibility, scope and footprint bounds.</summary>
    public static class LakeCrestPlacementUtility
    {
        public const float DefaultSeaLevelToleranceMeters = 0.05f;
        public const float MinExtentMeters = 8f;

        /// <summary>True when a lake's surface is close enough to sea level to share the ocean plane.</summary>
        public static bool IsCrestEligible(LakeRecord lake, float seaLevelWorldY, float toleranceMeters)
        {
            if (lake == null)
                return false;
            return Mathf.Abs(lake.SurfaceWorldY - seaLevelWorldY) <= Mathf.Max(0f, toleranceMeters);
        }

        /// <summary>Bounds of the lake basin, from its record bounds or its basin cells.</summary>
        public static bool TryComputeBoundsXZ(LakeRecord lake, out Rect boundsXZ)
        {
            boundsXZ = default;
            if (lake == null)
                return false;
            if (lake.BoundsXZ.width > 0f && lake.BoundsXZ.height > 0f)
            {
                boundsXZ = lake.BoundsXZ;
                return true;
            }
            return TryBoundsFromBasin(lake.BasinCellCentersXZ, out boundsXZ);
        }

        public static bool IsInScope(WorldBuildScope scope, LakeRecord lake)
        {
            if (lake == null)
                return false;
            var radius = Mathf.Sqrt(Mathf.Max(1f, lake.AreaMetersSq) / Mathf.PI);
            return HydrologySurfaceScopeUtility.ContainsPoint(scope, lake.CenterXZ, radius);
        }

        private static bool TryBoundsFromBasin(Vector2[] basin, out Rect boundsXZ)
        {
            boundsXZ = default;
            if (basin == null || basin.Length == 0)
                return false;

            var xMin = basin[0].x;
            var xMax = basin[0].x;
            var zMin = basin[0].y;
            var zMax = basin[0].y;
            for (var i = 1; i < basin.Length; i++)
            {
                xMin = Mathf.Min(xMin, basin[i].x);
                xMax = Mathf.Max(xMax, basin[i].x);
                zMin = Mathf.Min(zMin, basin[i].y);
                zMax = Mathf.Max(zMax, basin[i].y);
            }

            var width = Mathf.Max(MinExtentMeters, xMax - xMin);
            var height = Mathf.Max(MinExtentMeters, zMax - zMin);
            boundsXZ = new Rect(
                (xMin + xMax - width) * 0.5f,
                (zMin + zMax - height) * 0.5f,
                width,
                height);
            return true;
        }
    }
}
