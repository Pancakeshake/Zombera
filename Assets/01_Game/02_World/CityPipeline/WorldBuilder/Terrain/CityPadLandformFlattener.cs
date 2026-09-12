using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Outward-only city pad apron on the planning landform field.
    ///     Anisotropic inland/seaward falloff + feathered outer cone clamp (algo v5).
    /// </summary>
    public static partial class CityPadLandformFlattener
    {
        private static readonly List<float> PlateauSamples = new(256);

        public static void Apply(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            LandformProfile landforms,
            RoadNetworkSettings roads,
            HydrologyPlan hydrology,
            float seaLevelWorldY = 0f,
            bool enforceConeEnvelope = true)
        {
            if (field?.WorldHeights == null || pads == null || pads.Count == 0 || landforms == null)
                return;

            _ = roads;
            var gradeSlopeRatio = CityPadContinuity.GradeSlopeRatio(landforms);
            var continuityCapRatio = CityPadContinuity.MaxContinuitySlopeRatio(landforms);
            var falloffMin = Mathf.Max(1f, landforms.CityPadFalloffMinMeters);
            var falloffMax = Mathf.Max(falloffMin, landforms.CityPadFalloffMaxMeters);
            var cornerFrac = Mathf.Clamp01(landforms.CityPadCornerRadiusFraction);
            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(landforms);
            var minPadY = CityPadReclaimPolicy.MinPadHeightWorldY(seaLevelWorldY, landforms);
            var quayFalloff = CityCoastalPadUtility.ResolveQuayFalloffMeters(landforms);

            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;

                EnsureCoastalDerived(pad, hydrology, landforms, quayFalloff);
                ResolvePadHeight(field, pad, hydrology, maxReclaimDepth, minPadY, seaLevelWorldY);
                ResolveAdaptiveFalloff(new AdaptiveFalloffArgs(
                    field,
                    pad,
                    landforms,
                    new FalloffTuning(
                        falloffMin,
                        falloffMax,
                        gradeSlopeRatio,
                        continuityCapRatio,
                        cornerFrac),
                    hydrology,
                    maxReclaimDepth));
            }

            AccumulateAndWrite(new AccumulateWriteArgs(
                field,
                pads,
                cornerFrac,
                hydrology,
                maxReclaimDepth,
                enforceConeEnvelope));
        }

        /// <summary>
        ///     Re-applies per-pad cone envelopes after masked talus (feathered on outer apron).
        /// </summary>
        public static void ReclampApronCones(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            LandformProfile landforms,
            RoadNetworkSettings roads,
            HydrologyPlan hydrology = null,
            bool[] corridorMask = null)
        {
            if (field?.WorldHeights == null || pads == null || pads.Count == 0 || landforms == null)
                return;

            _ = roads;
            var cornerFrac = Mathf.Clamp01(landforms.CityPadCornerRadiusFraction);
            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(landforms);
            var fallbackRatio = CityPadContinuity.GradeSlopeRatio(landforms);

            for (var i = 0; i < field.WorldHeights.Length; i++)
            {
                if (corridorMask != null && i < corridorMask.Length && corridorMask[i])
                    continue;

                var z = i / field.Width;
                var x = i - z * field.Width;
                if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(hydrology, x, z, maxReclaimDepth))
                    continue;

                var center = field.CellCenterXZ(x, z);
                if (!TryResolveConeEnvelope(
                        new ConeEnvelopeQueryArgs(pads, center, cornerFrac, fallbackRatio),
                        out var envelope))
                    continue;

                if (envelope.InsidePlateau)
                {
                    field.WorldHeights[i] = envelope.PlateauY;
                    continue;
                }

                if (!envelope.AnyCone)
                    continue;

                var clamped = Mathf.Clamp(
                    field.WorldHeights[i], envelope.ConeLo, envelope.ConeHi);
                field.WorldHeights[i] = FeatherClamp(
                    field.WorldHeights[i], clamped, envelope.DistOutside, envelope.FalloffUsed);
            }
        }

        public static void SnapshotHalos(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            float[] destination)
        {
            if (field?.WorldHeights == null || destination == null ||
                destination.Length != field.WorldHeights.Length)
                return;

            System.Array.Copy(field.WorldHeights, destination, field.WorldHeights.Length);
            _ = pads;
        }

        public static void RestoreFromSnapshot(LandformField field, float[] snapshot)
        {
            if (field?.WorldHeights == null || snapshot == null ||
                snapshot.Length != field.WorldHeights.Length)
                return;

            System.Array.Copy(snapshot, field.WorldHeights, field.WorldHeights.Length);
        }

        /// <summary>Signed distance outside a rounded rect; &lt;= 0 means inside.</summary>
        public static float RoundedRectDistanceOutside(Vector2 point, Rect plateau, float cornerFrac)
        {
            var half = new Vector2(plateau.width * 0.5f, plateau.height * 0.5f);
            if (half.x <= 0.01f || half.y <= 0.01f)
                return Vector2.Distance(point, plateau.center);

            var center = plateau.center;
            var q = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y));
            var radius = Mathf.Min(half.x, half.y) * Mathf.Clamp01(cornerFrac);
            var halfCore = half - new Vector2(radius, radius);
            halfCore.x = Mathf.Max(0f, halfCore.x);
            halfCore.y = Mathf.Max(0f, halfCore.y);

            var d = q - halfCore;
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        private static void EnsureCoastalDerived(
            CityFlattenPad pad,
            HydrologyPlan hydrology,
            LandformProfile landforms,
            float quayFalloff)
        {
            if (pad.IsCoastal && pad.SeawardNormalXZ.sqrMagnitude > 0.0001f)
            {
                pad.FalloffMetersSeaward = Mathf.Max(1f, quayFalloff);
                return;
            }

            if (!CityCoastalPadUtility.TryDeriveCoastal(
                    pad.PlateauBoundsXZ, hydrology, landforms, out var seaward, out var exposure))
                return;

            pad.IsCoastal = true;
            pad.SeawardNormalXZ = seaward;
            pad.CoastExposure01 = exposure;
            pad.FalloffMetersSeaward = Mathf.Max(1f, quayFalloff);
        }

        private static void ResolvePadHeight(
            LandformField field,
            CityFlattenPad pad,
            HydrologyPlan hydrology,
            float maxReclaimDepth,
            float minPadY,
            float seaLevelWorldY)
        {
            PlateauSamples.Clear();
            SampleRectBounds(field, pad.PlateauBoundsXZ, (x, z, h) =>
            {
                if (!ContainsInclusive(pad.PlateauBoundsXZ, field.CellCenterXZ(x, z)))
                    return;
                if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(hydrology, x, z, maxReclaimDepth))
                    return;
                PlateauSamples.Add(h);
            });

            if (PlateauSamples.Count == 0)
            {
                pad.TargetHeightWorldY = Mathf.Max(pad.TargetHeightWorldY, minPadY);
                return;
            }

            PlateauSamples.Sort();
            float chosen;
            if (pad.IsCoastal)
            {
                // Bias toward seaward lower quartile so quay isn't perched on inland hills.
                var qIndex = Mathf.Clamp(PlateauSamples.Count / 4, 0, PlateauSamples.Count - 1);
                chosen = PlateauSamples[qIndex];
                chosen = Mathf.Min(chosen, Mathf.Lerp(minPadY, PlateauSamples[PlateauSamples.Count / 2], 0.65f));
                _ = seaLevelWorldY;
            }
            else
            {
                chosen = PlateauSamples[PlateauSamples.Count / 2];
            }

            pad.TargetHeightWorldY = Mathf.Max(chosen, minPadY);
        }

        private static void ResolveAdaptiveFalloff(in AdaptiveFalloffArgs args)
        {
            var pad = args.Pad;
            var inlandDelta = CityPadContinuity.SampleSectorMaxDelta(new CityPadContinuity.AnnulusSampleArgs(
                new CityPadContinuity.AnnulusGeometry(
                    args.Field,
                    pad.PlateauBoundsXZ,
                    pad.TargetHeightWorldY,
                    args.Tuning.FalloffMax,
                    args.Tuning.CornerFrac),
                new CityPadContinuity.AnnulusWaterFilter(
                    args.Hydrology,
                    args.MaxReclaimDepth,
                    pad.SeawardNormalXZ,
                    applySectorFilter: pad.SeawardNormalXZ.sqrMagnitude >= 0.0001f,
                    inlandOnly: true,
                    stride: 2)));

            pad.DiagnosticsInlandMaxDelta = inlandDelta;
            pad.DiagnosticsExceedsContinuityCap =
                CityPadContinuity.ExceedsMaxContinuity(inlandDelta, args.Landforms);

            pad.FalloffMeters = CityPadContinuity.ResolveFalloffMeters(
                inlandDelta, args.Tuning.GradeSlopeRatio, args.Tuning.FalloffMin, args.Tuning.FalloffMax);

            if (pad.FalloffMeters + 0.5f < args.Tuning.FalloffMax)
            {
                inlandDelta = CityPadContinuity.SampleSectorMaxDelta(new CityPadContinuity.AnnulusSampleArgs(
                    new CityPadContinuity.AnnulusGeometry(
                        args.Field,
                        pad.PlateauBoundsXZ,
                        pad.TargetHeightWorldY,
                        pad.FalloffMeters,
                        args.Tuning.CornerFrac),
                    new CityPadContinuity.AnnulusWaterFilter(
                        args.Hydrology,
                        args.MaxReclaimDepth,
                        pad.SeawardNormalXZ,
                        applySectorFilter: pad.SeawardNormalXZ.sqrMagnitude >= 0.0001f,
                        inlandOnly: true,
                        stride: 2)));
                pad.DiagnosticsInlandMaxDelta = inlandDelta;
            }

            pad.ConeSlopeRatio = CityPadContinuity.ResolveConeSlopeRatio(
                inlandDelta, pad.FalloffMeters, args.Tuning.GradeSlopeRatio, args.Tuning.ContinuityCapRatio);

            if (!pad.IsCoastal)
            {
                pad.FalloffMetersSeaward = pad.FalloffMeters;
                pad.ConeSlopeSeaward = pad.ConeSlopeRatio;
                return;
            }

            pad.FalloffMetersSeaward = Mathf.Max(1f, pad.FalloffMetersSeaward);
            // Quay: steeper short face toward ocean (intentional rock terrace).
            var quayCap = Mathf.Tan(35f * Mathf.Deg2Rad);
            pad.ConeSlopeSeaward = Mathf.Max(args.Tuning.GradeSlopeRatio * 1.5f, quayCap * 0.55f);
        }

        private static void SampleRectBounds(
            LandformField field,
            Rect bounds,
            System.Action<int, int, float> visitor)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - field.OriginXZ.x) / field.CellSize));
            var x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((bounds.xMax - field.OriginXZ.x) / field.CellSize));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - field.OriginXZ.y) / field.CellSize));
            var z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((bounds.yMax - field.OriginXZ.y) / field.CellSize));

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                    visitor(x, z, field.WorldHeights[field.Index(x, z)]);
            }
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point) =>
            point.x >= rect.xMin && point.x <= rect.xMax &&
            point.y >= rect.yMin && point.y <= rect.yMax;

        public static float FeatherClamp(float natural, float clamped, float distOutside, float falloff)
        {
            var l = Mathf.Max(1f, falloff);
            if (distOutside <= 0f)
                return clamped;
            // Strength → 0 over outer 25% of falloff.
            var strength = 1f - Mathf.SmoothStep(l * 0.75f, l, distOutside);
            return Mathf.Lerp(natural, clamped, Mathf.Clamp01(strength));
        }
    }
}
