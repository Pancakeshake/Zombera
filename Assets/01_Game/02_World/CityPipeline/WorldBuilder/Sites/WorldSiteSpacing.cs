using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Size-aware minimum center distances so pads leave room for blend aprons
    ///     and larger hubs spread across the map instead of clustering.
    /// </summary>
    public static class WorldSiteSpacing
    {
        public const float DefaultFalloffGuardMeters = 120f;

        /// <summary>Extra gap beyond footprint reserved so pads spread and leave natural terrain between them.</summary>
        public static float ApronGuardMeters(CitySiteType type, float falloffMinMeters)
        {
            var baseGuard = Mathf.Max(80f, falloffMinMeters);
            return type switch
            {
                CitySiteType.Metropolis => baseGuard * 2.5f,
                CitySiteType.City => baseGuard * 2.1f,
                CitySiteType.Town => baseGuard * 1.5f,
                CitySiteType.SmallTown => baseGuard * 1.2f,
                CitySiteType.Village => baseGuard,
                _ => baseGuard
            };
        }

        public static float MinCenterSeparationMeters(
            CitySiteType typeA,
            float footprintRadiusA,
            CitySiteType typeB,
            float footprintRadiusB,
            float roadPadMeters,
            float falloffMinMeters)
        {
            var rA = Mathf.Max(40f, footprintRadiusA);
            var rB = Mathf.Max(40f, footprintRadiusB);
            var guardA = ApronGuardMeters(typeA, falloffMinMeters);
            var guardB = ApronGuardMeters(typeB, falloffMinMeters);
            return rA + rB + guardA + guardB + Mathf.Max(0f, roadPadMeters);
        }

        public static float MinCenterSeparationMeters(
            WorldCitySite other,
            CitySiteType candidateType,
            float candidateFootprintRadius,
            float roadPadMeters,
            float falloffMinMeters)
        {
            if (other == null)
                return candidateFootprintRadius + Mathf.Max(0f, roadPadMeters);

            var otherR = Mathf.Max(other.HalfWidthMeters, other.HalfDepthMeters);
            return MinCenterSeparationMeters(
                other.SiteType,
                otherR,
                candidateType,
                candidateFootprintRadius,
                roadPadMeters,
                falloffMinMeters);
        }
    }
}
