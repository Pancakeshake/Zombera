using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Snow-line noise and tall-peak exclusive mixes for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private float SampleSnowLineOffsetMeters(float worldX, float worldZ)
        {
            var amp = Mathf.Max(0f, _snowLineNoiseAmplitudeMeters);
            if (amp <= 0.01f)
                return 0f;
            var scale = Mathf.Max(40f, _snowLineNoiseScaleMeters);
            var n = MountainBlendNoise.Fbm(worldX / scale, worldZ / scale + 41.7f, 2);
            return (n * 2f - 1f) * amp;
        }
        private void ResolveSnowElevationBands(
            float worldX,
            float worldZ,
            out float snowMin,
            out float snowFull,
            out float peakMin,
            out float peakFull)
        {
            var offset = SampleSnowLineOffsetMeters(worldX, worldZ);
            snowMin = _snowMinElevationMeters + offset;
            snowFull = Mathf.Max(snowMin + 40f, _snowFullElevationMeters + offset);
            peakMin = _peakExclusiveMinElevationMeters + offset;
            peakFull = Mathf.Max(peakMin + 40f, _peakExclusiveFullElevationMeters + offset);
        }
        private void ApplyPeakSnow(
            float[,,] map,
            int z,
            int x,
            int layers,
            float worldX,
            float worldZ,
            float slope,
            float elev,
            float seaLevel,
            string dominantId,
            float snowBiomeWeight,
            float interiorMask,
            bool inOceanBarrierStrip,
            bool nearOceanCoast)
        {
            var elevAbove = elev - seaLevel;
            if (!ShouldPaintMountainElevationBands(
                    elevAbove, dominantId, snowBiomeWeight, interiorMask, inOceanBarrierStrip))
                return;
            ResolveSnowElevationBands(
                worldX, worldZ, out var snowMin, out var snowFull, out var peakMin, out var peakFull);
            var exclusiveFull = Mathf.Max(peakMin + 1f, peakFull);
            var exclusiveT = Mathf.InverseLerp(peakMin, exclusiveFull, elevAbove);
            if (exclusiveT >= 0.95f)
            {
                ApplyTallPeakExclusiveMix(
                    map, z, x, layers, worldX, worldZ, slope, elevAbove, 1f, snowMin, snowFull);
                return;
            }
            var target = new AlphamapWriteTarget(map, z, x, layers);
            ApplyElevationDirtBand(map, z, x, layers, slope, elevAbove);
            ApplyHighlandGrass(new HighlandGrassArgs
            {
                Target = target,
                WorldX = worldX,
                WorldZ = worldZ,
                Slope = slope,
                ElevAbove = elevAbove,
                SnowMin = snowMin,
                SnowFull = snowFull
            });
            var tierWeight = ApplyPartitionedCliffTiers(new CliffTierArgs
            {
                Target = target,
                WorldX = worldX,
                WorldZ = worldZ,
                Slope = slope,
                ElevAbove = elevAbove,
                InteriorMask = interiorMask,
                InOceanBarrierStrip = inOceanBarrierStrip,
                NearOceanCoast = nearOceanCoast,
                DominantId = dominantId
            });
            var rockStrength = tierWeight;
            if (tierWeight < 0.08f)
            {
                rockStrength = ApplyElevationRockBand(new ElevationRockBandArgs
                {
                    Target = target,
                    WorldX = worldX,
                    WorldZ = worldZ,
                    Slope = slope,
                    ElevAbove = elevAbove,
                    DominantId = dominantId,
                    SnowBiomeWeight = snowBiomeWeight,
                    SnowMin = snowMin,
                    SnowFull = snowFull
                });
            }
            ApplyElevationSnowBand(new ElevationSnowBandArgs
            {
                Target = target,
                WorldX = worldX,
                WorldZ = worldZ,
                Slope = slope,
                ElevAbove = elevAbove,
                SnowBiomeWeight = snowBiomeWeight,
                RockStrength = Mathf.Max(rockStrength, tierWeight),
                SnowMin = snowMin,
                SnowFull = snowFull
            });
            if (exclusiveT > 0.12f)
            {
                ApplyTallPeakExclusiveMix(
                    map, z, x, layers, worldX, worldZ, slope, elevAbove, exclusiveT,
                    snowMin, snowFull);
            }
        }
        /// <summary>
        /// Tall peaks: exclusive mix of CliffDark (dark rock), SnowRock (dark rock with snow), and Snow.
        /// </summary>
        private void ApplyTallPeakExclusiveMix(
            float[,,] map,
            int z,
            int x,
            int layers,
            float worldX,
            float worldZ,
            float slope,
            float elevAbove,
            float exclusiveT)
        {
            ResolveSnowElevationBands(
                worldX, worldZ, out var snowMin, out var snowFull, out _, out _);
            ApplyTallPeakExclusiveMix(
                map, z, x, layers, worldX, worldZ, slope, elevAbove, exclusiveT, snowMin, snowFull);
        }
        private void ApplyTallPeakExclusiveMix(
            float[,,] map,
            int z,
            int x,
            int layers,
            float worldX,
            float worldZ,
            float slope,
            float elevAbove,
            float exclusiveT,
            float snowMin,
            float snowFull)
        {
            exclusiveT = Mathf.Clamp01(exclusiveT);
            if (exclusiveT <= 0.01f)
                return;
            if (_layerCliffDark < 0 && _layerSnowRock < 0 && _layerSnow < 0)
                return;
            var blend = EvaluateMountainSurfaceWeights(
                worldX,
                worldZ,
                slope,
                elevAbove,
                _rockMinElevationMeters,
                _rockFullElevationMeters,
                snowMin,
                snowFull);
            if (blend.sqrMagnitude < 1e-5f)
                return;
            if (exclusiveT >= 0.95f)
            {
                ClearNatural(map, z, x, layers);
                AddLayerByIndex(map, z, x, layers, _layerCliffDark, blend.x);
                AddLayerByIndex(map, z, x, layers, _layerSnowRock, blend.y);
                AddLayerByIndex(map, z, x, layers, _layerSnow, blend.z);
                return;
            }
            SuppressAllNaturalExceptPeakTrio(map, z, x, layers, exclusiveT);
            AddLayerByIndex(map, z, x, layers, _layerCliffDark, blend.x * exclusiveT);
            AddLayerByIndex(map, z, x, layers, _layerSnowRock, blend.y * exclusiveT);
            AddLayerByIndex(map, z, x, layers, _layerSnow, blend.z * exclusiveT);
        }

        /// <summary>
        ///     Returns normalized CliffDark, SnowRock, and Snow weights. Elevation and slope
        ///     responses are smooth so nearby texels do not form hard rings around mountains.
        /// </summary>
        private Vector3 EvaluateMountainSurfaceWeights(
            float worldX,
            float worldZ,
            float slope,
            float elevAbove,
            float rockMin,
            float rockFull,
            float snowMin,
            float snowFull)
        {
            var detailScale = Mathf.Max(8f, _snowDetailNoiseScaleMeters);
            var rockScale = Mathf.Max(12f, _rockExposureNoiseScaleMeters);
            var patch = MountainBlendNoise.Fbm(worldX / detailScale, worldZ / detailScale, 2);
            var ridged = MountainBlendNoise.Ridged(worldX / rockScale, worldZ / rockScale, 2);
            return CalculateMountainSurfaceWeights(
                slope, elevAbove, rockMin, rockFull, snowMin, snowFull, patch, ridged);
        }

        private static Vector3 CalculateMountainSurfaceWeights(
            float slope,
            float elevAbove,
            float rockMin,
            float rockFull,
            float snowMin,
            float snowFull,
            float patch,
            float ridged)
        {
            var rockT = SmoothBand(rockMin, rockFull, elevAbove);
            var steep = SmoothBand(20f, 46f, slope);
            var gentle = 1f - SmoothBand(12f, 38f, slope);
            var rockPresence = Mathf.Max(rockT, steep * 0.45f);
            if (rockPresence <= 0.001f)
                return Vector3.zero;

            var snowRockStart = rockFull - Mathf.Max(60f, (rockFull - rockMin) * 0.25f);
            var snowRockT = SmoothBand(snowRockStart, snowMin, elevAbove);
            var snowT = SmoothBand(snowMin, snowFull, elevAbove);
            var transitionCrest = 4f * snowT * (1f - snowT);
            var patchMod = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(patch));
            var ridgeMod = Mathf.Lerp(0.82f, 1.15f, Mathf.Clamp01(ridged));
            // Once fully into the snow band, keep white snow readable on steep caps
            // instead of letting CliffDark / SnowRock dominate from a distance.
            var capSnow = snowT * snowT;

            var darkRock = rockPresence * Mathf.Lerp(1f, 0.12f, snowT) *
                           Mathf.Lerp(0.75f, 1.15f, steep) * ridgeMod *
                           Mathf.Lerp(1f, 0.55f, capSnow);
            var snowRock = rockPresence * snowRockT *
                           (0.22f + snowT * 0.38f + transitionCrest * 0.55f) *
                           Mathf.Lerp(0.85f, 1.2f, steep) * patchMod *
                           Mathf.Lerp(1f, 0.7f, capSnow);
            var snow = snowT * Mathf.Lerp(0.7f, 1.4f, gentle) * patchMod *
                       Mathf.Lerp(1f, 1.85f, capSnow);
            return NormalizeMountainWeights(darkRock, snowRock, snow);
        }

        private static float SmoothBand(float min, float full, float value)
        {
            var t = Mathf.InverseLerp(min, Mathf.Max(min + 0.01f, full), value);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private static Vector3 NormalizeMountainWeights(float darkRock, float snowRock, float snow)
        {
            var sum = darkRock + snowRock + snow;
            return sum > 1e-5f
                ? new Vector3(darkRock / sum, snowRock / sum, snow / sum)
                : Vector3.zero;
        }
        private void SuppressAllNaturalExceptPeakTrio(
            float[,,] map,
            int z,
            int x,
            int layers,
            float exclusiveT)
        {
            var keep = 1f - Mathf.Clamp01(exclusiveT);
            ScaleNaturalExceptLayers(
                map, z, x, layers, keep, _layerCliffDark, _layerSnowRock, _layerSnow);
        }
    }
}
