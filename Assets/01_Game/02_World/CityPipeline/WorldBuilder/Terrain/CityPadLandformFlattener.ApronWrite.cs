using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Apron accumulate/write + cone envelope (feathered outer clamp).</summary>
    public static partial class CityPadLandformFlattener
    {
        private static void AccumulateAndWrite(in AccumulateWriteArgs args)
        {
            var weightSum = new float[args.Field.WorldHeights.Length];
            var targetSum = new float[args.Field.WorldHeights.Length];
            var invProd = new float[args.Field.WorldHeights.Length];
            for (var i = 0; i < invProd.Length; i++)
                invProd[i] = 1f;

            var natural = args.Field.WorldHeights;
            var fallbackRatio = Mathf.Tan(CityPadContinuity.DefaultApproachSlopeDegrees * Mathf.Deg2Rad);

            for (var p = 0; p < args.Pads.Count; p++)
            {
                var pad = args.Pads[p];
                if (pad == null) continue;
                AccumulatePad(new AccumulatePadArgs(
                    args.Field, pad, args.CornerFrac,
                    new AccumulateBuffers(weightSum, targetSum, invProd),
                    args.Hydrology, args.MaxReclaimDepth));
            }

            var buffers = new AccumulateBuffers(weightSum, targetSum, invProd);
            for (var i = 0; i < natural.Length; i++)
                WriteAccumulatedCell(in args, natural, in buffers, i, fallbackRatio);
        }

        private static void WriteAccumulatedCell(
            in AccumulateWriteArgs args,
            float[] natural,
            in AccumulateBuffers buffers,
            int i,
            float fallbackRatio)
        {
            var sumW = buffers.WeightSum[i];
            if (sumW <= 0.0001f)
                return;

            var z = i / args.Field.Width;
            var x = i - z * args.Field.Width;
            if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(
                    args.Hydrology, x, z, args.MaxReclaimDepth))
                return;

            var combinedW = 1f - buffers.InvProd[i];
            var target = buffers.TargetSum[i] / sumW;
            var blended = Mathf.Lerp(natural[i], target, Mathf.Clamp01(combinedW));

            if (!args.EnforceConeEnvelope)
            {
                natural[i] = blended;
                return;
            }

            var center = args.Field.CellCenterXZ(x, z);
            if (!TryResolveConeEnvelope(
                    new ConeEnvelopeQueryArgs(args.Pads, center, args.CornerFrac, fallbackRatio),
                    out var envelope))
            {
                natural[i] = blended;
                return;
            }

            if (envelope.InsidePlateau)
            {
                natural[i] = envelope.PlateauY;
                return;
            }

            if (!envelope.AnyCone)
            {
                natural[i] = blended;
                return;
            }

            var clamped = Mathf.Clamp(blended, envelope.ConeLo, envelope.ConeHi);
            natural[i] = FeatherClamp(natural[i], clamped, envelope.DistOutside, envelope.FalloffUsed);
        }

        private static bool TryResolveConeEnvelope(
            in ConeEnvelopeQueryArgs args,
            out ConeEnvelopeResult result)
        {
            result = new ConeEnvelopeResult
            {
                ConeLo = float.NegativeInfinity,
                ConeHi = float.PositiveInfinity,
                FalloffUsed = 1f
            };

            if (TryResolvePlateauEnvelope(in args, ref result))
                return true;

            CollectBestConeEnvelope(in args, ref result);
            return true;
        }

        private static bool TryResolvePlateauEnvelope(
            in ConeEnvelopeQueryArgs args,
            ref ConeEnvelopeResult result)
        {
            var plateauSum = 0f;
            var plateauCount = 0;
            for (var p = 0; p < args.Pads.Count; p++)
            {
                var pad = args.Pads[p];
                if (pad == null) continue;
                if (!ContainsInclusive(pad.PlateauBoundsXZ, args.Center))
                    continue;
                plateauSum += pad.TargetHeightWorldY;
                plateauCount++;
            }

            if (plateauCount <= 0)
                return false;

            result.InsidePlateau = true;
            result.PlateauY = plateauSum / plateauCount;
            return true;
        }

        private static void CollectBestConeEnvelope(
            in ConeEnvelopeQueryArgs args,
            ref ConeEnvelopeResult result)
        {
            var intersectLo = float.NegativeInfinity;
            var intersectHi = float.PositiveInfinity;
            var intersectOk = true;
            var bestWeight = -1f;
            var bestLo = 0f;
            var bestHi = 0f;
            var bestTie = float.MinValue;
            var bestFalloff = 1f;
            var bestDist = 0f;

            for (var p = 0; p < args.Pads.Count; p++)
            {
                if (!TryEvaluatePadCone(
                        args.Pads[p], args.Center, args.CornerFrac, args.FallbackRatio, out var eval))
                    continue;

                result.AnyCone = true;
                intersectLo = Mathf.Max(intersectLo, eval.Lo);
                intersectHi = Mathf.Min(intersectHi, eval.Hi);
                if (intersectLo > intersectHi)
                    intersectOk = false;

                if (IsBetterConeCandidate(eval.Weight, eval.Tie, bestWeight, bestTie))
                {
                    bestWeight = eval.Weight;
                    bestTie = eval.Tie;
                    bestLo = eval.Lo;
                    bestHi = eval.Hi;
                    bestFalloff = eval.Falloff;
                    bestDist = eval.Dist;
                }
            }

            if (!result.AnyCone)
                return;

            if (intersectOk)
            {
                result.ConeLo = intersectLo;
                result.ConeHi = intersectHi;
            }
            else
            {
                result.ConeLo = bestLo;
                result.ConeHi = bestHi;
            }

            result.FalloffUsed = bestFalloff;
            result.DistOutside = bestDist;
        }

        private static bool IsBetterConeCandidate(
            float weight,
            float tie,
            float bestWeight,
            float bestTie) =>
            weight > bestWeight + 0.0001f ||
            (Mathf.Abs(weight - bestWeight) <= 0.0001f && tie > bestTie);

        private static bool TryEvaluatePadCone(
            CityFlattenPad pad,
            Vector2 center,
            float cornerFrac,
            float fallbackRatio,
            out PadConeEval eval)
        {
            eval = default;
            if (pad == null)
                return false;

            eval.Dist = RoundedRectDistanceOutside(center, pad.PlateauBoundsXZ, cornerFrac);
            eval.Falloff = pad.ResolveFalloffAt(center);
            if (eval.Dist > eval.Falloff)
                return false;

            var slope = pad.ResolveConeSlopeAt(center, fallbackRatio);
            var coneDelta = slope * Mathf.Max(0f, eval.Dist);
            eval.Lo = pad.TargetHeightWorldY - coneDelta;
            eval.Hi = pad.TargetHeightWorldY + coneDelta;
            var t = 1f - Mathf.Clamp01(eval.Dist / eval.Falloff);
            eval.Weight = t * t * (3f - 2f * t);
            eval.Tie = pad.TargetHeightWorldY * 1e6f - pad.CenterXZ.x * 1e3f - pad.CenterXZ.y;
            return true;
        }

        private static void AccumulatePad(in AccumulatePadArgs args)
        {
            if (!TryResolvePadCellBounds(in args, out var x0, out var x1, out var z0, out var z1))
                return;

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                    AccumulatePadCell(in args, x, z);
            }
        }

        private static bool TryResolvePadCellBounds(
            in AccumulatePadArgs args,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            var outer = args.Pad.OuterBoundsXZ;
            x0 = Mathf.Max(0, Mathf.FloorToInt((outer.xMin - args.Field.OriginXZ.x) / args.Field.CellSize));
            x1 = Mathf.Min(args.Field.Width - 1, Mathf.CeilToInt((outer.xMax - args.Field.OriginXZ.x) / args.Field.CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((outer.yMin - args.Field.OriginXZ.y) / args.Field.CellSize));
            z1 = Mathf.Min(args.Field.Height - 1, Mathf.CeilToInt((outer.yMax - args.Field.OriginXZ.y) / args.Field.CellSize));
            return true;
        }

        private static void AccumulatePadCell(in AccumulatePadArgs args, int x, int z)
        {
            if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(
                    args.Hydrology, x, z, args.MaxReclaimDepth))
                return;

            var center = args.Field.CellCenterXZ(x, z);
            if (!TryComputePadWeight(args.Pad, center, args.CornerFrac, out var weight))
                return;

            var i = args.Field.Index(x, z);
            args.Buffers.WeightSum[i] += weight;
            args.Buffers.TargetSum[i] += weight * args.Pad.TargetHeightWorldY;
            args.Buffers.InvProd[i] *= 1f - Mathf.Clamp01(weight);
        }

        private static bool TryComputePadWeight(
            CityFlattenPad pad,
            Vector2 center,
            float cornerFrac,
            out float weight)
        {
            if (ContainsInclusive(pad.PlateauBoundsXZ, center))
            {
                weight = 1f;
                return true;
            }

            var d = RoundedRectDistanceOutside(center, pad.PlateauBoundsXZ, cornerFrac);
            var falloff = pad.ResolveFalloffAt(center);
            if (d >= falloff)
            {
                weight = 0f;
                return false;
            }

            var t = 1f - Mathf.Clamp01(d / falloff);
            weight = t * t * (3f - 2f * t);
            return weight > 0.0001f;
        }
    }
}
