using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Shared city-pad apron continuity math (grading slope, falloff, continuity cap).
    ///     ConeSlopeRatio is runtime-only and recomputed each Apply — never persisted.
    /// </summary>
    public static partial class CityPadContinuity
    {
        public const float DefaultApproachSlopeDegrees = 12f;
        public const float DefaultMaxContinuityDegrees = 25f;

        public static float GradeSlopeDegrees(LandformProfile landforms) =>
            landforms != null
                ? Mathf.Max(1f, landforms.CityPadApproachSlopeDegrees)
                : DefaultApproachSlopeDegrees;

        public static float GradeSlopeRatio(LandformProfile landforms) =>
            Mathf.Tan(GradeSlopeDegrees(landforms) * Mathf.Deg2Rad);

        public static float MaxContinuityDegrees(LandformProfile landforms) =>
            landforms != null
                ? Mathf.Max(1f, landforms.CityPadMaxContinuityDegrees)
                : DefaultMaxContinuityDegrees;

        public static float MaxContinuitySlopeRatio(LandformProfile landforms) =>
            Mathf.Tan(MaxContinuityDegrees(landforms) * Mathf.Deg2Rad);

        /// <summary>
        ///     After L is clamped: ConeSlopeRatio = min(max(s_grade, Δh/L), s_cap).
        /// </summary>
        public static float ResolveConeSlopeRatio(
            float maxDelta,
            float falloffMeters,
            float gradeSlopeRatio,
            float maxContinuitySlopeRatio)
        {
            var l = Mathf.Max(1f, falloffMeters);
            var continuity = maxDelta / l;
            var ratio = Mathf.Max(Mathf.Max(0.0001f, gradeSlopeRatio), continuity);
            if (maxContinuitySlopeRatio > 0.0001f)
                ratio = Mathf.Min(ratio, maxContinuitySlopeRatio);
            return ratio;
        }

        public static float ResolveFalloffMeters(
            float maxDelta,
            float gradeSlopeRatio,
            float falloffMin,
            float falloffMax)
        {
            var required = gradeSlopeRatio > 0.0001f
                ? maxDelta / gradeSlopeRatio
                : falloffMin;
            return Mathf.Clamp(required, falloffMin, falloffMax);
        }

        /// <summary>True when uncapped continuity at FalloffMax would exceed the profile cap.</summary>
        public static bool ExceedsMaxContinuity(
            float maxDelta,
            float falloffMaxMeters,
            float maxContinuityDegrees)
        {
            var l = Mathf.Max(1f, falloffMaxMeters);
            var cap = Mathf.Tan(Mathf.Max(1f, maxContinuityDegrees) * Mathf.Deg2Rad);
            return maxDelta / l > cap + 0.0001f;
        }

        public static bool ExceedsMaxContinuity(float maxDelta, LandformProfile landforms) =>
            ExceedsMaxContinuity(
                maxDelta,
                landforms != null ? landforms.CityPadFalloffMaxMeters : 800f,
                MaxContinuityDegrees(landforms));

        /// <summary>
        ///     Coarse footprint+apron relief probe for site acceptance (LandformField-free).
        /// </summary>
        public static bool PassesQueryContinuityGate(
            in QueryContinuityGateArgs args,
            out string rejectCode)
        {
            rejectCode = null;
            if (args.TerrainQuery == null || args.Landforms == null)
                return true;

            var falloffMax = Mathf.Max(1f, args.Landforms.CityPadFalloffMaxMeters);
            var maxDelta = 0f;
            SampleQueryRing(new QueryRingArgs(
                args.TerrainQuery, args.Center, args.FootprintRadius, 0f,
                args.TargetHeightWorldY, args.IsCoastal, args.SeawardNormalXZ), ref maxDelta);
            SampleQueryRing(new QueryRingArgs(
                args.TerrainQuery, args.Center, args.FootprintRadius, falloffMax * 0.5f,
                args.TargetHeightWorldY, args.IsCoastal, args.SeawardNormalXZ), ref maxDelta);
            SampleQueryRing(new QueryRingArgs(
                args.TerrainQuery, args.Center, args.FootprintRadius, falloffMax,
                args.TargetHeightWorldY, args.IsCoastal, args.SeawardNormalXZ), ref maxDelta);

            if (!ExceedsMaxContinuity(maxDelta, args.Landforms))
                return true;

            rejectCode = "reject_pad_relief";
            return false;
        }

        /// <summary>
        ///     Field-based continuity gate for region pads (same predicate as site planner).
        /// </summary>
        public static bool PassesFieldContinuityGate(
            in FieldContinuityGateArgs args,
            out string rejectCode)
        {
            rejectCode = null;
            var core = args.Core;
            if (core.Field == null || core.Landforms == null)
                return true;

            var falloffMax = Mathf.Max(1f, core.Landforms.CityPadFalloffMaxMeters);
            var cornerFrac = Mathf.Clamp01(core.Landforms.CityPadCornerRadiusFraction);
            var maxDelta = SampleDryAnnulusMaxDelta(new AnnulusSampleArgs(
                new AnnulusGeometry(
                    core.Field, core.Plateau, core.TargetHeightWorldY, falloffMax, cornerFrac),
                new AnnulusWaterFilter(
                    core.Hydrology,
                    core.MaxReclaimDepth,
                    args.SeawardNormalXZ,
                    applySectorFilter: args.IsCoastal,
                    inlandOnly: true,
                    stride: 2)));

            if (!ExceedsMaxContinuity(maxDelta, core.Landforms))
                return true;

            rejectCode = "reject_pad_relief";
            return false;
        }

        public static float SampleDryAnnulusMaxDelta(in AnnulusSampleArgs args)
        {
            if (!args.Filter.ApplySectorFilter)
            {
                return SampleAnnulusMaxDelta(new AnnulusSampleArgs(
                    args.Geometry,
                    new AnnulusWaterFilter(
                        args.Filter.Hydrology,
                        args.Filter.MaxReclaimDepth,
                        default,
                        applySectorFilter: false,
                        inlandOnly: true,
                        args.Filter.Stride)));
            }

            return SampleAnnulusMaxDelta(new AnnulusSampleArgs(
                args.Geometry,
                new AnnulusWaterFilter(
                    args.Filter.Hydrology,
                    args.Filter.MaxReclaimDepth,
                    args.Filter.SeawardNormalXZ,
                    applySectorFilter: true,
                    inlandOnly: true,
                    args.Filter.Stride)));
        }

        /// <summary>
        ///     Annulus max |Δh| restricted to inland or seaward sector (anisotropic falloff).
        /// </summary>
        public static float SampleSectorMaxDelta(in AnnulusSampleArgs args)
        {
            var filter = args.Filter.SeawardNormalXZ.sqrMagnitude >= 0.0001f;
            return SampleAnnulusMaxDelta(new AnnulusSampleArgs(
                args.Geometry,
                new AnnulusWaterFilter(
                    args.Filter.Hydrology,
                    args.Filter.MaxReclaimDepth,
                    args.Filter.SeawardNormalXZ,
                    filter,
                    args.Filter.InlandOnly,
                    args.Filter.Stride)));
        }

        public static float MaxPadConeDegrees(IReadOnlyList<CityFlattenPad> pads, LandformProfile landforms)
        {
            var maxDegrees = GradeSlopeDegrees(landforms);
            if (pads == null)
                return maxDegrees;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null || pad.ConeSlopeRatio <= 0.0001f)
                    continue;
                maxDegrees = Mathf.Max(maxDegrees, Mathf.Atan(pad.ConeSlopeRatio) * Mathf.Rad2Deg);
            }

            return maxDegrees;
        }

        private static float SampleAnnulusMaxDelta(in AnnulusSampleArgs args)
        {
            if (args.Geometry.Field?.WorldHeights == null)
                return 0f;

            var stride = Mathf.Max(1, args.Filter.Stride);
            if (!TryResolveAnnulusBounds(in args, out var x0, out var x1, out var z0, out var z1))
                return 0f;

            var maxDelta = 0f;
            var plateauCenter = args.Geometry.Plateau.center;
            for (var z = z0; z <= z1; z += stride)
            {
                for (var x = x0; x <= x1; x += stride)
                    ConsiderAnnulusCell(in args, x, z, plateauCenter, ref maxDelta);
            }

            return maxDelta;
        }

        private static bool TryResolveAnnulusBounds(
            in AnnulusSampleArgs args,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            var field = args.Geometry.Field;
            var outer = Expand(args.Geometry.Plateau, Mathf.Max(1f, args.Geometry.ProbeMeters));
            x0 = Mathf.Max(0, Mathf.FloorToInt((outer.xMin - field.OriginXZ.x) / field.CellSize));
            x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((outer.xMax - field.OriginXZ.x) / field.CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((outer.yMin - field.OriginXZ.y) / field.CellSize));
            z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((outer.yMax - field.OriginXZ.y) / field.CellSize));
            return true;
        }

        private static void ConsiderAnnulusCell(
            in AnnulusSampleArgs args,
            int x,
            int z,
            Vector2 plateauCenter,
            ref float maxDelta)
        {
            var g = args.Geometry;
            var f = args.Filter;
            if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(f.Hydrology, x, z, f.MaxReclaimDepth))
                return;

            var center = g.Field.CellCenterXZ(x, z);
            if (ContainsInclusive(g.Plateau, center))
                return;

            if (f.ApplySectorFilter &&
                ShouldSkipSectorCell(center - plateauCenter, f.SeawardNormalXZ, f.InlandOnly))
                return;

            var d = CityPadLandformFlattener.RoundedRectDistanceOutside(
                center, g.Plateau, g.CornerFrac);
            if (d <= 0f || d > g.ProbeMeters)
                return;

            maxDelta = Mathf.Max(
                maxDelta,
                Mathf.Abs(g.Field.WorldHeights[g.Field.Index(x, z)] - g.TargetHeightWorldY));
        }

        private static bool ShouldSkipSectorCell(
            Vector2 fromPlateauCenter,
            Vector2 seawardNormalXZ,
            bool inlandOnly)
        {
            var seaward = CityCoastalPadUtility.IsSeawardBearing(fromPlateauCenter, seawardNormalXZ);
            if (inlandOnly)
                return seaward;
            return !seaward;
        }

        private static void SampleQueryRing(in QueryRingArgs args, ref float maxDelta)
        {
            var radius = Mathf.Max(1f, args.FootprintRadius + args.OutwardMeters);
            var filterCoastal = args.IsCoastal && args.SeawardNormalXZ.sqrMagnitude >= 0.0001f;
            var seawardN = args.SeawardNormalXZ.normalized;
            const int spokes = 8;
            for (var i = 0; i < spokes; i++)
            {
                var angle = (Mathf.PI * 2f * i) / spokes;
                var spokeDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (filterCoastal && Vector2.Dot(spokeDir, seawardN) >= 0.15f)
                    continue;

                var xz = args.Center + spokeDir * radius;
                if (!args.TerrainQuery.TrySampleHeight(xz, out var y))
                    continue;
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(y - args.TargetY));
            }
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point) =>
            point.x >= rect.xMin && point.x <= rect.xMax &&
            point.y >= rect.yMin && point.y <= rect.yMax;

        private static Rect Expand(Rect rect, float meters) =>
            Rect.MinMaxRect(rect.xMin - meters, rect.yMin - meters, rect.xMax + meters, rect.yMax + meters);
    }
}
