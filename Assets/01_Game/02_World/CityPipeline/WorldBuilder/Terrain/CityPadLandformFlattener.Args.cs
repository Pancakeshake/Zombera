using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parameter bundles for <see cref="CityPadLandformFlattener"/> (Sonar S107).</summary>
    public static partial class CityPadLandformFlattener
    {
        private readonly struct FalloffTuning
        {
            public readonly float FalloffMin;
            public readonly float FalloffMax;
            public readonly float GradeSlopeRatio;
            public readonly float ContinuityCapRatio;
            public readonly float CornerFrac;

            public FalloffTuning(
                float falloffMin,
                float falloffMax,
                float gradeSlopeRatio,
                float continuityCapRatio,
                float cornerFrac)
            {
                FalloffMin = falloffMin;
                FalloffMax = falloffMax;
                GradeSlopeRatio = gradeSlopeRatio;
                ContinuityCapRatio = continuityCapRatio;
                CornerFrac = cornerFrac;
            }
        }

        private readonly struct AdaptiveFalloffArgs
        {
            public readonly LandformField Field;
            public readonly CityFlattenPad Pad;
            public readonly LandformProfile Landforms;
            public readonly FalloffTuning Tuning;
            public readonly HydrologyPlan Hydrology;
            public readonly float MaxReclaimDepth;

            public AdaptiveFalloffArgs(
                LandformField field,
                CityFlattenPad pad,
                LandformProfile landforms,
                in FalloffTuning tuning,
                HydrologyPlan hydrology,
                float maxReclaimDepth)
            {
                Field = field;
                Pad = pad;
                Landforms = landforms;
                Tuning = tuning;
                Hydrology = hydrology;
                MaxReclaimDepth = maxReclaimDepth;
            }
        }

        private readonly struct AccumulateWriteArgs
        {
            public readonly LandformField Field;
            public readonly IReadOnlyList<CityFlattenPad> Pads;
            public readonly float CornerFrac;
            public readonly HydrologyPlan Hydrology;
            public readonly float MaxReclaimDepth;
            public readonly bool EnforceConeEnvelope;

            public AccumulateWriteArgs(
                LandformField field,
                IReadOnlyList<CityFlattenPad> pads,
                float cornerFrac,
                HydrologyPlan hydrology,
                float maxReclaimDepth,
                bool enforceConeEnvelope)
            {
                Field = field;
                Pads = pads;
                CornerFrac = cornerFrac;
                Hydrology = hydrology;
                MaxReclaimDepth = maxReclaimDepth;
                EnforceConeEnvelope = enforceConeEnvelope;
            }
        }

        private readonly struct ConeEnvelopeQueryArgs
        {
            public readonly IReadOnlyList<CityFlattenPad> Pads;
            public readonly Vector2 Center;
            public readonly float CornerFrac;
            public readonly float FallbackRatio;

            public ConeEnvelopeQueryArgs(
                IReadOnlyList<CityFlattenPad> pads,
                Vector2 center,
                float cornerFrac,
                float fallbackRatio)
            {
                Pads = pads;
                Center = center;
                CornerFrac = cornerFrac;
                FallbackRatio = fallbackRatio;
            }
        }

        private struct ConeEnvelopeResult
        {
            public bool InsidePlateau;
            public float PlateauY;
            public float ConeLo;
            public float ConeHi;
            public bool AnyCone;
            public float FalloffUsed;
            public float DistOutside;
        }

        private readonly struct AccumulateBuffers
        {
            public readonly float[] WeightSum;
            public readonly float[] TargetSum;
            public readonly float[] InvProd;

            public AccumulateBuffers(float[] weightSum, float[] targetSum, float[] invProd)
            {
                WeightSum = weightSum;
                TargetSum = targetSum;
                InvProd = invProd;
            }
        }

        private readonly struct AccumulatePadArgs
        {
            public readonly LandformField Field;
            public readonly CityFlattenPad Pad;
            public readonly float CornerFrac;
            public readonly AccumulateBuffers Buffers;
            public readonly HydrologyPlan Hydrology;
            public readonly float MaxReclaimDepth;

            public AccumulatePadArgs(
                LandformField field,
                CityFlattenPad pad,
                float cornerFrac,
                in AccumulateBuffers buffers,
                HydrologyPlan hydrology,
                float maxReclaimDepth)
            {
                Field = field;
                Pad = pad;
                CornerFrac = cornerFrac;
                Buffers = buffers;
                Hydrology = hydrology;
                MaxReclaimDepth = maxReclaimDepth;
            }
        }

        private struct PadConeEval
        {
            public float Lo;
            public float Hi;
            public float Weight;
            public float Tie;
            public float Falloff;
            public float Dist;
        }
    }
}
