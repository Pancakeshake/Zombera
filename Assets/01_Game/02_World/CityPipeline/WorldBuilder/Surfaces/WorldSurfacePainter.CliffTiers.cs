using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Coastal vs interior partitioned cliff tiers for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private bool ShouldPaintMountainElevationBands(
            float elevAbove,
            string dominantId,
            float snowBiomeWeight,
            float interiorMask,
            bool inOceanBarrierStrip)
        {
            var paintMin = _paintLandformProfile != null
                ? _paintLandformProfile.InteriorMountainPaintMinMask
                : 0.55f;
            if (inOceanBarrierStrip && interiorMask < paintMin)
                return false;
            if (IsRockyBiome(dominantId) || IsSnowyBiome(dominantId) || snowBiomeWeight >= 0.1f)
                return true;
            if (interiorMask < paintMin)
                return elevAbove >= _peakExclusiveMinElevationMeters;
            return elevAbove >= _rockMinElevationMeters - 20f ||
                   elevAbove >= _peakExclusiveMinElevationMeters - 20f;
        }
        private float ApplyPartitionedCliffTiers(in CliffTierArgs args)
        {
            var paintMin = _paintLandformProfile != null
                ? _paintLandformProfile.InteriorMountainPaintMinMask
                : 0.55f;
            var beachMax = _paintLandformProfile != null
                ? _paintLandformProfile.OceanBeachMaxElevationMeters
                : Mathf.Max(0.5f, _beachMaxElevationAboveSea);
            var barrierPeak = _paintLandformProfile != null
                ? _paintLandformProfile.EdgeBarrierPeakMeters
                : 220f;
            var coastalGate = ResolveCoastalCliffGate(in args);
            var interiorGate = Mathf.SmoothStep(paintMin, 0.92f, args.InteriorMask);
            var noise = MountainBlendNoise;
            var rockScale = Mathf.Max(8f, _rockExposureNoiseScaleMeters);
            var patch = noise.Ridged(args.WorldX / rockScale, args.WorldZ / rockScale, 3);
            var tierSum = ApplyCoastalBarrierCliffTier(in args, coastalGate, beachMax, barrierPeak, patch);
            tierSum += ApplyInteriorCliffTiers(in args, interiorGate, patch);
            return tierSum;
        }
        private static float ResolveCoastalCliffGate(in CliffTierArgs args)
        {
            var coastalGate = Mathf.Clamp01(1f - args.InteriorMask);
            if (args.NearOceanCoast)
                coastalGate = Mathf.Max(coastalGate, 0.65f);
            if (!(args.NearOceanCoast || args.InOceanBarrierStrip))
                coastalGate = 0f;
            return coastalGate;
        }
        private float ApplyCoastalBarrierCliffTier(
            in CliffTierArgs args,
            float coastalGate,
            float beachMax,
            float barrierPeak,
            float patch)
        {
            if (coastalGate < 0.35f)
                return 0f;
            if (args.ElevAbove < beachMax + 2f || args.ElevAbove > barrierPeak * 0.92f)
                return 0f;
            if (args.Slope < 16f)
                return 0f;
            var elevFactor = Mathf.InverseLerp(beachMax + 2f, barrierPeak * 0.55f, args.ElevAbove);
            var slopeFactor = Mathf.InverseLerp(16f, 42f, args.Slope);
            var coastalRock = elevFactor * slopeFactor * Mathf.SmoothStep(0.35f, 0.85f, coastalGate);
            if (coastalRock <= 0.03f)
                return 0f;
            var target = args.Target;
            SuppressVegetationForElevationBand(target.Map, target.Z, target.X, coastalRock * 0.55f);
            var bright = coastalRock * Mathf.Lerp(0.55f, 1f, patch);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerCliffBright, bright);
            var dark = coastalRock *
                       Mathf.Max(Mathf.InverseLerp(28f, 48f, args.Slope), coastalRock * 0.5f) * 0.85f;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerCliffDark, dark);
            return coastalRock;
        }
        private float ApplyInteriorCliffTiers(in CliffTierArgs args, float interiorGate, float patch)
        {
            if (interiorGate <= 0f)
                return 0f;
            var elevRock = Mathf.InverseLerp(
                _rockMinElevationMeters - 10f, _rockFullElevationMeters, args.ElevAbove);
            var slopeRock = Mathf.Max(Mathf.InverseLerp(24f, 48f, args.Slope), elevRock * 0.2f);
            var interiorRock = elevRock * slopeRock * interiorGate;
            var tierSum = ApplyInteriorRockLayers(in args, interiorRock, patch);
            tierSum += ApplyInteriorPinkBand(in args, interiorGate, interiorRock);
            tierSum += ApplyInteriorSnowRockCap(in args, interiorRock, patch);
            return tierSum;
        }
        private float ApplyInteriorRockLayers(in CliffTierArgs args, float interiorRock, float patch)
        {
            if (interiorRock <= 0.03f)
                return 0f;
            var target = args.Target;
            SuppressVegetationForElevationBand(target.Map, target.Z, target.X, interiorRock * 0.45f);
            AddLayerByIndex(
                target.Map, target.Z, target.X, target.Layers, _layerCliffDark,
                interiorRock * Mathf.Lerp(0.65f, 1f, patch));
            if ((args.DominantId is "Plains" or "Hills" or "Forest") && interiorRock > 0.12f)
            {
                if (_layerGrassGreen >= 0)
                    target.Map[target.Z, target.X, _layerGrassGreen] *= 1f - interiorRock * 0.35f;
                if (_layerGrassYellow >= 0)
                    target.Map[target.Z, target.X, _layerGrassYellow] *= 1f - interiorRock * 0.35f;
            }
            return interiorRock;
        }
        private float ApplyInteriorPinkBand(in CliffTierArgs args, float interiorGate, float interiorRock)
        {
            var pinkElev = Mathf.InverseLerp(
                _dirtBandMinElevationMeters, _rockMinElevationMeters - 20f, args.ElevAbove);
            var pinkSlope = Mathf.InverseLerp(28f, 12f, args.Slope) *
                            Mathf.Clamp01(Mathf.InverseLerp(10f, 18f, args.Slope));
            var pink = interiorGate * pinkElev * pinkSlope * (1f - interiorRock * 0.85f) * 0.4f;
            if (pink <= 0.02f || args.ElevAbove >= _peakExclusiveMinElevationMeters)
                return 0f;
            var target = args.Target;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerCliffPink, pink);
            return pink;
        }
        private float ApplyInteriorSnowRockCap(in CliffTierArgs args, float interiorRock, float patch)
        {
            if (args.ElevAbove < _snowMinElevationMeters - 50f || interiorRock <= 0.08f)
                return 0f;
            var snowMin = _snowMinElevationMeters + SampleSnowLineOffsetMeters(args.WorldX, args.WorldZ);
            var snowFull = Mathf.Max(
                snowMin + 40f, _snowFullElevationMeters + (snowMin - _snowMinElevationMeters));
            if (args.ElevAbove < snowMin)
                return 0f;
            var blend = EvaluateMountainSurfaceWeights(
                args.WorldX,
                args.WorldZ,
                args.Slope,
                args.ElevAbove,
                _rockMinElevationMeters,
                _rockFullElevationMeters,
                snowMin,
                snowFull);
            var snowRock = interiorRock * blend.y * Mathf.Lerp(0.85f, 1.1f, patch);
            if (snowRock <= 0.01f)
                return 0f;
            var target = args.Target;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSnowRock, snowRock);
            return snowRock;
        }
    }
}

