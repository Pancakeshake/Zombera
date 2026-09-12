using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Shared coastal reclaim rules for site acceptance, pad flatten, hydrology prune,
    ///     and highway corridor carve.
    /// </summary>
    public static class CityPadReclaimPolicy
    {
        public const float DefaultMinClearanceAboveSeaMeters = 2f;
        public const float DefaultMaxReclaimDepthMeters = 8f;
        public const float ReclaimedBuildabilityFloor = 0.75f;

        public static float MaxReclaimDepthMeters(LandformProfile landforms) =>
            landforms != null
                ? Mathf.Max(0f, landforms.CityPadMaxReclaimDepthMeters)
                : DefaultMaxReclaimDepthMeters;

        public static float MinClearanceAboveSeaMeters(LandformProfile landforms) =>
            landforms != null
                ? Mathf.Max(0f, landforms.CityPadMinClearanceAboveSeaMeters)
                : DefaultMinClearanceAboveSeaMeters;

        public static float MinPadHeightWorldY(float seaLevelWorldY, LandformProfile landforms) =>
            seaLevelWorldY + MinClearanceAboveSeaMeters(landforms);

        public static bool IsReclaimable(
            WorldWaterClass waterClass,
            float depthMeters,
            float maxReclaimDepthMeters)
        {
            if (waterClass == WorldWaterClass.None)
                return false;

            return depthMeters <= Mathf.Max(0f, maxReclaimDepthMeters);
        }

        public static bool IsReclaimable(
            WorldWaterClass waterClass,
            float depthMeters,
            LandformProfile landforms) =>
            IsReclaimable(waterClass, depthMeters, MaxReclaimDepthMeters(landforms));

        /// <summary>
        ///     True when a hydrology cell should be left untouched by pad/highway terrain writes
        ///     (deep or non-reclaimable water).
        /// </summary>
        public static bool ShouldSkipWaterForTerrainWrite(
            HydrologyPlan hydrology,
            int x,
            int z,
            float maxReclaimDepthMeters)
        {
            if (hydrology == null || x < 0 || z < 0 || x >= hydrology.Width || z >= hydrology.Height)
                return false;

            var i = hydrology.Index(x, z);
            var waterClass = hydrology.WaterClass[i];
            if (waterClass == WorldWaterClass.None)
                return false;

            var depth = hydrology.DepthMeters != null && i < hydrology.DepthMeters.Length
                ? hydrology.DepthMeters[i]
                : float.MaxValue;
            return !IsReclaimable(waterClass, depth, maxReclaimDepthMeters);
        }

        public static bool IsDryOrReclaimableSample(
            WorldWaterClass waterClass,
            float depthMeters,
            float maxReclaimDepthMeters)
        {
            if (waterClass == WorldWaterClass.None)
                return true;
            return IsReclaimable(waterClass, depthMeters, maxReclaimDepthMeters);
        }
    }
}
