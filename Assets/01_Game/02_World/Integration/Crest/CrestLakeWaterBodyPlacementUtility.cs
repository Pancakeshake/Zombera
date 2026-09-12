using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Lake presentation eligibility for Crest generation. No <c>WaterBody</c> AABB or clip-mesh is
    /// produced: geometry comes from <see cref="InlandWaterFootprintPlan"/>'s lake medial spine and
    /// <see cref="CrestLakeSplineBuilder"/> renders it as a direct <c>Lake_&lt;id&gt;</c> spline root.
    /// </summary>
    public static class CrestLakeWaterBodyPlacementUtility
    {
        private const float ElevatedHeightEpsilon = 0.05f;

        /// <summary>Resolved Crest lake surface decision.</summary>
        public readonly struct LakePlacement
        {
            public ulong StableId { get; }
            public float SurfaceWorldY { get; }

            /// <summary>True when the lake sits above sea level and needs its own height input.</summary>
            public bool NeedsHeightSpline { get; }

            public LakePlacement(ulong stableId, float surfaceWorldY, bool needsHeightSpline)
            {
                StableId = stableId;
                SurfaceWorldY = surfaceWorldY;
                NeedsHeightSpline = needsHeightSpline;
            }
        }

        public static bool TryBuildPlacement(
            LakeRecord lake,
            float seaLevelWorldY,
            float seaLevelToleranceMeters,
            WorldBuildScope scope,
            out LakePlacement placement)
        {
            placement = default;
            if (lake == null || !LakeCrestPlacementUtility.IsInScope(scope, lake))
                return false;

            var tolerance = Mathf.Max(ElevatedHeightEpsilon, seaLevelToleranceMeters);
            var needsHeightSpline = Mathf.Abs(lake.SurfaceWorldY - seaLevelWorldY) > tolerance;
            var surfaceY = needsHeightSpline ? lake.SurfaceWorldY : seaLevelWorldY;
            placement = new LakePlacement(lake.StableId, surfaceY, needsHeightSpline);
            return true;
        }
    }
}
