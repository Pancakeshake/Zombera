using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Noisy plateau escarpment write outside arterial flat cores.</summary>
    public static partial class CityPadCoreUtility
    {
        private const int PlateauNoiseSeed = unchecked((int)0xC17E51D5);

        /// <summary>
        ///     Grades pad height into surrounding terrain over <see cref="PlateauSlopeMeters"/>
        ///     with distance warping + mid-slope detail noise (escarpment, not a smooth apron).
        /// </summary>
        public static void WriteNoisyPlateauSlopes(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            LandformProfile landforms,
            HydrologyPlan hydrology)
        {
            if (field?.WorldHeights == null || pads == null || pads.Count == 0 || landforms == null)
                return;

            var slopeReach = PlateauSlopeMeters(landforms);
            var cornerFrac = Mathf.Clamp01(landforms.CityPadCornerRadiusFraction);
            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(landforms);
            var noise = new DeterministicNoise2D(PlateauNoiseSeed + landforms.HillsSeedOffset);
            var noiseScale = Mathf.Max(28f, slopeReach * 0.28f);
            var detailScale = Mathf.Max(12f, slopeReach * 0.12f);

            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null)
                    continue;

                var reach = Mathf.Max(40f, pad.FalloffMeters > 1f ? pad.FalloffMeters : slopeReach);
                WritePadEscarpment(
                    field,
                    pad,
                    reach,
                    cornerFrac,
                    maxReclaimDepth,
                    hydrology,
                    noise,
                    noiseScale,
                    detailScale);
            }
        }

        private static void WritePadEscarpment(
            LandformField field,
            CityFlattenPad pad,
            float reach,
            float cornerFrac,
            float maxReclaimDepth,
            HydrologyPlan hydrology,
            DeterministicNoise2D noise,
            float noiseScale,
            float detailScale)
        {
            var outer = Expand(pad.PlateauBoundsXZ, reach);
            WorldToCellRect(field, outer, out var x0, out var x1, out var z0, out var z1);
            var padY = pad.TargetHeightWorldY;

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(hydrology, x, z, maxReclaimDepth))
                        continue;

                    var center = field.CellCenterXZ(x, z);
                    if (ContainsInclusive(pad.PlateauBoundsXZ, center))
                        continue;

                    var d = CityPadLandformFlattener.RoundedRectDistanceOutside(
                        center, pad.PlateauBoundsXZ, cornerFrac);
                    if (d <= 0f || d >= reach)
                        continue;

                    var warp = noise.Fbm(center.x / noiseScale, center.y / noiseScale, 3);
                    var warpedD = d + (warp - 0.5f) * reach * 0.28f;
                    warpedD = Mathf.Clamp(warpedD, 0.01f, reach);
                    var t = Mathf.SmoothStep(0f, 1f, warpedD / reach);

                    var i = field.Index(x, z);
                    var natural = field.WorldHeights[i];
                    var graded = Mathf.Lerp(padY, natural, t);

                    // Mid-slope rubble / gullying — strongest mid-band, fades at rim and toe.
                    var band = 4f * t * (1f - t);
                    var detail = noise.Ridged(center.x / detailScale, center.y / detailScale, 2);
                    var relief = (detail - 0.5f) * band * Mathf.Min(14f, Mathf.Abs(padY - natural) * 0.08f);
                    field.WorldHeights[i] = graded + relief;
                }
            }
        }

        private static Rect Expand(Rect rect, float meters) =>
            Rect.MinMaxRect(
                rect.xMin - meters,
                rect.yMin - meters,
                rect.xMax + meters,
                rect.yMax + meters);

        private static bool ContainsInclusive(Rect rect, Vector2 point) =>
            point.x >= rect.xMin && point.x <= rect.xMax &&
            point.y >= rect.yMin && point.y <= rect.yMax;
    }
}
