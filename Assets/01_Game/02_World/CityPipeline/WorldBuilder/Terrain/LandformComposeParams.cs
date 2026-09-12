using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Thread-safe snapshot of <see cref="LandformProfile"/> values for parallel landform fill.
    /// Do not pass ScriptableObjects into <c>Parallel.For</c> workers.
    /// </summary>
    public readonly struct LandformComposeParams
    {
        private const int MountainMaskLutSize = 64;

        public readonly float ContinentalnessAmplitude;
        public readonly float DomainWarpAmplitude;
        public readonly float DomainWarpScale;
        public readonly float HillsAmplitude;
        public readonly float MountainAmplitude;
        public readonly float ResidualMountainAmplitudeMeters;
        public readonly float MicroDetailAmplitudeMeters;
        public readonly float InteriorHillsMultiplier;

        public readonly float IslandNoiseScaleMeters;
        public readonly float ArchipelagoNoiseScaleMeters;
        public readonly float IslandPeakThreshold;
        public readonly float ArchipelagoPeakThreshold;
        public readonly float MainlandMaskLo;
        public readonly float MainlandMaskHi;

        public readonly float InteriorRollingScaleMeters;
        public readonly float InteriorRollingAmplitudeMeters;
        public readonly float InteriorReliefInsetMeters;
        public readonly float OceanKeepOutMeters;
        public readonly float CrestSharpness;
        public readonly float SaddleDepth;
        public readonly float PassCorridorHalfWidthMeters;
        public readonly float StructuralValleyDepthMeters;

        public readonly float EdgeBarrierDepthMeters;
        public readonly float EdgeBarrierPeakMeters;
        public readonly float EdgeBarrierFalloffMeters;
        public readonly float EdgeBarrierNoiseScaleMeters;
        public readonly float EdgeBarrierNoiseAmplitudeMeters;

        public readonly float CoastFalloffWidthMeters;
        public readonly float OceanCoastStripWidthMeters;
        public readonly float OceanBeachMaxElevationMeters;
        public readonly float OceanOffshoreWidthMeters;
        public readonly float OceanShoreShelfWidthMeters;
        public readonly float OceanBeachWidthMeters;
        public readonly float OceanTrenchDepthMeters;
        public readonly float OceanTrenchSteepness;
        public readonly float OceanTrenchNoiseAmplitudeMeters;
        public readonly float OceanShelfOuterDepthMeters;
        public readonly float OceanShelfInnerDepthMeters;
        public readonly float OceanShelfReefNoiseScaleMeters;
        public readonly float OceanShelfReefNoiseAmplitudeMeters;
        public readonly float OceanShelfReefCoverage;
        public readonly float OceanCoastErosionAmplitudeMeters;
        public readonly float OceanCoastErosionScaleMeters;
        public readonly float InlandDryFloorMetersAboveSea;
        public readonly float InlandDryFloorBlendMeters;

        /// <summary>Optional LUT for legacy mountain mask curve; null uses SmoothStep.</summary>
        private readonly float[] MountainMaskLut;

        private LandformComposeParams(
            LandformProfile profile,
            float[] mountainMaskLut)
        {
            ContinentalnessAmplitude = profile.ContinentalnessAmplitude;
            DomainWarpAmplitude = profile.DomainWarpAmplitude;
            DomainWarpScale = profile.DomainWarpScale;
            HillsAmplitude = profile.HillsAmplitude;
            MountainAmplitude = profile.MountainAmplitude;
            ResidualMountainAmplitudeMeters = profile.ResidualMountainAmplitudeMeters;
            MicroDetailAmplitudeMeters = profile.MicroDetailAmplitudeMeters;
            InteriorHillsMultiplier = profile.InteriorHillsMultiplier;

            IslandNoiseScaleMeters = profile.IslandNoiseScaleMeters;
            ArchipelagoNoiseScaleMeters = profile.ArchipelagoNoiseScaleMeters;
            IslandPeakThreshold = profile.IslandPeakThreshold;
            ArchipelagoPeakThreshold = profile.ArchipelagoPeakThreshold;
            MainlandMaskLo = profile.MainlandMaskLo;
            MainlandMaskHi = profile.MainlandMaskHi;

            InteriorRollingScaleMeters = profile.InteriorRollingScaleMeters;
            InteriorRollingAmplitudeMeters = profile.InteriorRollingAmplitudeMeters;
            InteriorReliefInsetMeters = profile.InteriorReliefInsetMeters;
            var wet = profile.OceanOffshoreWidthMeters + profile.OceanShoreShelfWidthMeters;
            OceanKeepOutMeters = Mathf.Clamp(wet, 40f, 400f);
            CrestSharpness = profile.CrestSharpness;
            SaddleDepth = profile.SaddleDepth;
            PassCorridorHalfWidthMeters = profile.PassCorridorHalfWidthMeters;
            StructuralValleyDepthMeters = profile.StructuralValleyDepthMeters;

            EdgeBarrierDepthMeters = profile.EdgeBarrierDepthMeters;
            EdgeBarrierPeakMeters = profile.EdgeBarrierPeakMeters;
            EdgeBarrierFalloffMeters = profile.EdgeBarrierFalloffMeters;
            EdgeBarrierNoiseScaleMeters = profile.EdgeBarrierNoiseScaleMeters;
            EdgeBarrierNoiseAmplitudeMeters = profile.EdgeBarrierNoiseAmplitudeMeters;

            CoastFalloffWidthMeters = profile.CoastFalloffWidthMeters;
            OceanCoastStripWidthMeters = profile.OceanCoastStripWidthMeters;
            OceanBeachMaxElevationMeters = profile.OceanBeachMaxElevationMeters;
            OceanOffshoreWidthMeters = profile.OceanOffshoreWidthMeters;
            OceanShoreShelfWidthMeters = profile.OceanShoreShelfWidthMeters;
            OceanBeachWidthMeters = profile.OceanBeachWidthMeters;
            OceanTrenchDepthMeters = profile.OceanTrenchDepthMeters;
            OceanTrenchSteepness = profile.OceanTrenchSteepness;
            OceanTrenchNoiseAmplitudeMeters = profile.OceanTrenchNoiseAmplitudeMeters;
            OceanShelfOuterDepthMeters = profile.OceanShelfOuterDepthMeters;
            OceanShelfInnerDepthMeters = profile.OceanShelfInnerDepthMeters;
            OceanShelfReefNoiseScaleMeters = profile.OceanShelfReefNoiseScaleMeters;
            OceanShelfReefNoiseAmplitudeMeters = profile.OceanShelfReefNoiseAmplitudeMeters;
            OceanShelfReefCoverage = profile.OceanShelfReefCoverage;
            OceanCoastErosionAmplitudeMeters = profile.OceanCoastErosionAmplitudeMeters;
            OceanCoastErosionScaleMeters = profile.OceanCoastErosionScaleMeters;
            InlandDryFloorMetersAboveSea = profile.InlandDryFloorMetersAboveSea;
            InlandDryFloorBlendMeters = profile.InlandDryFloorBlendMeters;

            MountainMaskLut = mountainMaskLut;
        }

        public static LandformComposeParams Capture(LandformProfile profile, bool bakeMountainMaskCurve)
        {
            if (profile == null)
                throw new System.ArgumentNullException(nameof(profile));

            float[] lut = null;
            if (bakeMountainMaskCurve && profile.MountainMaskCurve != null)
                lut = BakeCurve(profile.MountainMaskCurve);

            return new LandformComposeParams(profile, lut);
        }

        public float EvaluateMountainMask(float continental01)
        {
            if (MountainMaskLut == null || MountainMaskLut.Length == 0)
                return Mathf.SmoothStep(0.45f, 0.88f, continental01);

            var t = Mathf.Clamp01(continental01) * (MountainMaskLut.Length - 1);
            var i = (int)t;
            if (i >= MountainMaskLut.Length - 1)
                return MountainMaskLut[MountainMaskLut.Length - 1];
            return Mathf.Lerp(MountainMaskLut[i], MountainMaskLut[i + 1], t - i);
        }

        private static float[] BakeCurve(AnimationCurve curve)
        {
            var lut = new float[MountainMaskLutSize];
            var denom = MountainMaskLutSize - 1;
            for (var i = 0; i < MountainMaskLutSize; i++)
                lut[i] = Mathf.Clamp01(curve.Evaluate(i / (float)denom));
            return lut;
        }
    }
}
