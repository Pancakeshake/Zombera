using System.Collections.Generic;
using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Soft multi-biome surface blending for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private const float BiomeBlendMinWeight = 0.08f;
        private float StrongDominantBiomeThreshold =>
            _paintLandformProfile != null
                ? _paintLandformProfile.BiomeDominantThreshold
                : 0.88f;
        private static bool TryGetStrongDominantStableId(
            BiomeField biomes,
            int cell,
            float dominantThreshold,
            out string stableId)
        {
            stableId = null;
            if (biomes?.DominantBiomeIndex == null || biomes.BiomeCount <= 0)
                return false;
            var maxIndex = biomes.DominantBiomeIndex[cell];
            if (maxIndex < 0 || maxIndex >= biomes.BiomeCount)
                return false;
            var max = biomes.Weights[biomes.WeightIndex(cell, maxIndex)];
            if (max < dominantThreshold)
                return false;
            stableId = maxIndex < biomes.BiomeStableIds.Length
                ? biomes.BiomeStableIds[maxIndex]
                : null;
            return !string.IsNullOrEmpty(stableId);
        }
        private void ApplyBlendedBiomeSurfaces(in BiomeBlendPaintArgs args)
        {
            if (args.Biomes == null || args.NaturalRecords == null || args.Landforms == null)
                return;
            if (TryApplyFastOrStrongDominant(in args))
                return;
            ApplySoftBiomeBlendLoop(in args);
        }
        private bool TryApplyFastOrStrongDominant(in BiomeBlendPaintArgs args)
        {
            if (UseFastPaintSampling &&
                args.Biomes.TryGetDominantStableId(args.Cell, out var fastDominant) &&
                !string.IsNullOrEmpty(fastDominant))
            {
                ApplyBiomeRecordWithElevation(BuildRecordElevationArgs(in args, fastDominant, 1f));
                return true;
            }
            var useMacroSoftBlend = _paintLandformProfile != null &&
                                    _paintLandformProfile.UseMacroBiomeRegions;
            if (useMacroSoftBlend)
                return false;
            if (!TryGetStrongDominantStableId(
                    args.Biomes, args.Cell, StrongDominantBiomeThreshold, out var dominantStableId))
                return false;
            ApplyBiomeRecordWithElevation(BuildRecordElevationArgs(in args, dominantStableId, 1f));
            return true;
        }
        private void ApplySoftBiomeBlendLoop(in BiomeBlendPaintArgs args)
        {
            for (var i = 0; i < args.NaturalRecords.Count; i++)
            {
                var record = args.NaturalRecords[i];
                if (record == null)
                    continue;
                var biomeIndex = _naturalRecordBiomeIndices != null && i < _naturalRecordBiomeIndices.Length
                    ? _naturalRecordBiomeIndices[i]
                    : -1;
                if (biomeIndex < 0)
                    continue;
                var weight = args.Coords.SampleWeight(args.Biomes, biomeIndex);
                if (weight < BiomeBlendMinWeight)
                    continue;
                ApplyBiomeRecordWithElevation(BuildRecordElevationArgs(in args, record.StableId, weight));
            }
        }
        private static BiomeRecordElevationArgs BuildRecordElevationArgs(
            in BiomeBlendPaintArgs args,
            string stableId,
            float weight) =>
            new()
            {
                Target = args.Target,
                NaturalRecords = args.NaturalRecords,
                StableId = stableId,
                ElevAbove = args.ElevAbove,
                Slope = args.Slope,
                Weight = weight,
                InteriorMask = args.InteriorMask,
                WorldX = args.WorldX,
                WorldZ = args.WorldZ
            };
        private void ApplyBiomeRecordWithElevation(in BiomeRecordElevationArgs args)
        {
            var record = FindBiomeRecord(args.NaturalRecords, args.StableId);
            if (record == null || args.Weight <= 0f)
                return;
            var scale = ComputeBiomeSurfaceScale(
                args.StableId, args.ElevAbove, args.Slope, args.InteriorMask,
                args.WorldX, args.WorldZ) * args.Weight;
            if (scale <= 0.01f)
                return;
            var paintMin = _paintLandformProfile != null
                ? _paintLandformProfile.InteriorMountainPaintMinMask
                : 0.55f;
            var interiorGate = Mathf.SmoothStep(paintMin, 0.92f, args.InteriorMask);
            var target = args.Target;
            // Forest underpaint only on coastal foothills — interior tiers own cliff layers.
            if (args.StableId == "Mountains" && scale < 0.92f && interiorGate < 0.25f)
            {
                var forest = FindBiomeRecord(args.NaturalRecords, "Forest");
                if (forest != null)
                {
                    ApplyExplicitSurfaceWeights(
                        target.Map, target.Z, target.X, target.Layers,
                        forest, args.Weight * (1f - scale * 0.85f));
                }
            }
            ApplyExplicitSurfaceWeights(target.Map, target.Z, target.X, target.Layers, record, scale);
        }
        private float ComputeBiomeSurfaceScale(
            string stableId,
            float elevAbove,
            float slope,
            float interiorMask,
            float worldX,
            float worldZ)
        {
            if (stableId == "Mountains")
            {
                var slopeFactor = Mathf.InverseLerp(8f, 28f, slope);
                var elevFactor = elevAbove <= _rockMinElevationMeters
                    ? 0f
                    : Mathf.InverseLerp(_rockMinElevationMeters - 50f, _rockFullElevationMeters, elevAbove);
                var scale = Mathf.Clamp01(Mathf.Max(slopeFactor * 0.7f, elevFactor));
                var paintMin = _paintLandformProfile != null
                    ? _paintLandformProfile.InteriorMountainPaintMinMask
                    : 0.55f;
                var interiorGate = Mathf.SmoothStep(paintMin, 0.92f, interiorMask);
                return scale * (1f - interiorGate * 0.85f);
            }
            if (stableId == "Snow" || stableId == "AlpineSnow")
            {
                var offset = SampleSnowLineOffsetMeters(worldX, worldZ);
                var snowMin = _snowMinElevationMeters + offset;
                var snowFull = Mathf.Max(snowMin + 40f, _snowFullElevationMeters + offset);
                if (elevAbove <= snowMin)
                    return 0f;
                return Mathf.Clamp01(Mathf.InverseLerp(snowMin - 35f, snowFull, elevAbove));
            }
            return 1f;
        }
    }
}

