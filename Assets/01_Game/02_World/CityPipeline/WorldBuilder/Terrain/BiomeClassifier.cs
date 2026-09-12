using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Fills <see cref="BiomeField"/> from landforms, hydrology, and palette curves.</summary>
    public static partial class BiomeClassifier
    {
        private const float TempNoiseScale = 4800f;
        private const float MoistNoiseScale = 3600f;

        public static BiomeField Classify(in ClassifyArgs args)
        {
            var records = CollectNaturalBiomes(args.Profiles.Palette);
            var stableIds = BuildStableIdArray(records);
            var biomeIndexById = BuildBiomeIndexLookup(records);
            var field = new BiomeField(args.Landforms.Width, args.Landforms.Height, stableIds);
            var landformProfile = args.Profiles.LandformProfile;
            var hydrologyProfile = args.Profiles.HydrologyProfile;
            var tempNoise = new DeterministicNoise2D(args.BiomeSeed);
            var moistNoise = new DeterministicNoise2D(args.BiomeSeed ^ 0x5F3759DF);
            var landformSeed = WorldSubsystemSeeds.Derive(
                args.Session.Seed, args.Session.ProfileVersion, WorldSubsystemSeeds.Landforms);
            var ctx = new ClassifyLoopContext
            {
                Field = field,
                Landforms = args.Landforms,
                Hydrology = args.Hydrology,
                Records = records,
                BiomeIndexById = biomeIndexById,
                TempNoise = tempNoise,
                MoistNoise = moistNoise,
                CoastNoise = landformProfile != null
                    ? new DeterministicNoise2D(landformSeed + landformProfile.EdgeBarrierSeedOffset)
                    : null,
                SeaLevel = hydrologyProfile != null ? hydrologyProfile.SeaLevelWorldY : 0f,
                ShoreBand = hydrologyProfile != null
                    ? Mathf.Max(1f, hydrologyProfile.ShoreBlendWidthMeters)
                    : 18f,
                EdgeClearance = args.Profiles.MapSize != null
                    ? args.Profiles.MapSize.EdgeClearanceMeters
                    : 500f,
                EdgeDepth = landformProfile != null
                    ? landformProfile.EdgeBarrierDepthMeters
                    : 650f,
                Bounds = args.Session.WorldBoundsXZ,
                BoundaryLayout = WorldMapBoundaryLayout.Resolve(args.Session, landformProfile),
                LandformProfile = landformProfile,
                InvTempScale = 1f / TempNoiseScale,
                InvMoistScale = 1f / MoistNoiseScale,
                NoiseOctaves = args.FastClassify ? 2 : 3
            };

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                    ClassifyCell(ctx, x, z);
            }

            return field;
        }

        private static List<WorldBiomeRecord> CollectNaturalBiomes(WorldBiomePalette palette)
        {
            var list = new List<WorldBiomeRecord>(20);
            if (palette?.Biomes == null) return list;

            for (var i = 0; i < palette.Biomes.Count; i++)
            {
                var record = palette.Biomes[i];
                if (record == null || record.IsCityAreaOverlay) continue;
                list.Add(record);
            }

            if (list.Count == 0)
                list.Add(new WorldBiomeRecord { StableId = "Plains" });
            return list;
        }

        private static string[] BuildStableIdArray(List<WorldBiomeRecord> records)
        {
            var stableIds = new string[records.Count];
            for (var i = 0; i < records.Count; i++)
                stableIds[i] = records[i].StableId;
            return stableIds;
        }

        private static Dictionary<string, int> BuildBiomeIndexLookup(List<WorldBiomeRecord> records)
        {
            var lookup = new Dictionary<string, int>(records.Count);
            for (var i = 0; i < records.Count; i++)
                lookup[records[i].StableId] = i;
            return lookup;
        }

        private static void ClassifyCell(ClassifyLoopContext ctx, int x, int z)
        {
            var cell = z * ctx.Field.Width + x;
            ctx.Landforms.TryGetHeight(x, z, out var elev);
            var waterClass = SampleWaterClass(ctx.Hydrology, cell);

            if (TryClassifyOceanCell(ctx.Field, ctx.BiomeIndexById, cell, waterClass))
                return;

            var slope = LandformFieldSampling.EstimateSlopeDegrees(ctx.Landforms, x, z);
            var center = ctx.Landforms.CellCenterXZ(x, z);
            var elevNorm = Mathf.Clamp01((elev - (ctx.SeaLevel - 20f)) / 720f);
            SampleClimate(ctx, cell, center, elevNorm, out var waterDist);

            ctx.Field.Slope[cell] = slope;
            var depth = SampleDepth(ctx.Hydrology, cell);
            var edgeDist = WorldMapBoundaryUtility.DistanceToRectEdge(center.x, center.y, ctx.Bounds);

            if (TryClassifyMountainBarrier(ctx, cell, center))
                return;

            if (TryClassifyOceanShore(ctx, cell, center, elevNorm, slope))
                return;

            var scalars = new ForcedBiomeScalars(
                waterDist,
                ctx.ShoreBand,
                elev,
                ctx.SeaLevel,
                elevNorm,
                slope,
                ctx.LandformProfile?.BiomeForcedBoostScale ?? 1f);
            FinalizeCellBiome(ctx, x, z, cell, in scalars);
            FinalizeBuildability(ctx, cell, waterClass, depth, edgeDist, slope);
        }

        private static bool TryClassifyOceanCell(
            BiomeField field,
            Dictionary<string, int> biomeIndexById,
            int cell,
            WorldWaterClass waterClass)
        {
            if (waterClass != WorldWaterClass.Ocean)
                return false;

            ForceExclusiveBiome(field, biomeIndexById, cell, "Ocean");
            field.NoBuild[cell] = true;
            field.Buildability[cell] = 0f;
            field.Slope[cell] = 0f;
            return true;
        }

        private static void SampleClimate(
            ClassifyLoopContext ctx,
            int cell,
            Vector2 center,
            float elevNorm,
            out float waterDist)
        {
            var temp = ctx.TempNoise.Fbm(
                center.x * ctx.InvTempScale, center.y * ctx.InvTempScale, ctx.NoiseOctaves);
            temp -= elevNorm * 0.55f;

            var moist = ctx.MoistNoise.Fbm(
                center.x * ctx.InvMoistScale, center.y * ctx.InvMoistScale, ctx.NoiseOctaves);
            waterDist = SampleWaterDist(ctx.Hydrology, cell);
            var flow = SampleFlow(ctx.Hydrology, cell);
            moist += Mathf.Exp(-waterDist / 80f) * 0.45f;
            moist += Mathf.Clamp01(Mathf.Log10(flow + 1f) / 4f) * 0.25f;

            if (ctx.LandformProfile != null && ctx.LandformProfile.UseMacroBiomeRegions)
            {
                BiomeMacroRegionBias.ApplyMacroClimateOffsets(
                    ctx.Bounds, center.x, center.y, out var tempOffset, out var moistOffset);
                temp += tempOffset;
                moist += moistOffset;
            }

            ctx.Field.Temperature[cell] = Mathf.Clamp01(temp);
            ctx.Field.Moisture[cell] = Mathf.Clamp01(moist);
        }

        private static bool TryClassifyMountainBarrier(ClassifyLoopContext ctx, int cell, Vector2 center)
        {
            if (!WorldMapBoundaryUtility.TryGetEdgeBarrier(
                    center.x, center.y, ctx.Bounds, ctx.EdgeDepth, ctx.BoundaryLayout,
                    out var boundaryKind) ||
                boundaryKind != WorldMapBoundaryKind.Mountains)
                return false;

            ForceExclusiveBiome(ctx.Field, ctx.BiomeIndexById, cell, "Mountains");
            ctx.Field.NoBuild[cell] = true;
            ctx.Field.Buildability[cell] = 0f;
            return true;
        }

        private static bool TryClassifyOceanShore(
            ClassifyLoopContext ctx,
            int cell,
            Vector2 center,
            float elevNorm,
            float slope)
        {
            if (ctx.LandformProfile == null)
                return false;
            if (!WorldMapBoundaryUtility.TryGetOceanEdgeDistance(
                    center.x, center.y, ctx.Bounds, ctx.BoundaryLayout, out var straightOceanDist))
                return false;

            var erosionReach = ctx.EdgeDepth +
                               Mathf.Max(0f, ctx.LandformProfile.OceanCoastErosionAmplitudeMeters);
            if (straightOceanDist >= erosionReach)
                return false;

            var oceanEdgeDist = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                straightOceanDist, center.x, center.y, ctx.CoastNoise,
                ctx.LandformProfile.OceanCoastErosionAmplitudeMeters,
                ctx.LandformProfile.OceanCoastErosionScaleMeters);
            var beachInner = ctx.LandformProfile.OceanOffshoreWidthMeters +
                             ctx.LandformProfile.OceanShoreShelfWidthMeters;
            var beachOuter = beachInner + ctx.LandformProfile.OceanBeachWidthMeters;
            var shoreStrength = EvaluateOceanShoreStrength(
                oceanEdgeDist, beachInner, beachOuter, ctx.ShoreBand);
            if (shoreStrength <= 0.01f)
                return false;

            EvaluateWeights(ctx.Field, ctx.Records, cell, elevNorm, slope);
            BlendExclusiveBiome(ctx.Field, ctx.BiomeIndexById, cell, shoreStrength, "Shore");
            NormalizeWeights(ctx.Field, cell);
            ComputeDominantBiome(ctx.Field, cell);
            ctx.Field.NoBuild[cell] = oceanEdgeDist < beachInner + ctx.ShoreBand * 0.35f;
            ctx.Field.Buildability[cell] =
                Mathf.Clamp01(1f - slope / 35f) * 0.85f * shoreStrength;
            return true;
        }

        private static void FinalizeCellBiome(
            ClassifyLoopContext ctx,
            int x,
            int z,
            int cell,
            in ForcedBiomeScalars scalars)
        {
            EvaluateWeights(ctx.Field, ctx.Records, cell, scalars.ElevNorm, scalars.Slope);
            ApplyForcedBiomes(new ForcedBiomeBoostArgs(
                ctx.Field, ctx.BiomeIndexById, cell, in scalars));

            if (ctx.LandformProfile != null && ctx.LandformProfile.UseMacroBiomeRegions)
            {
                var center = ctx.Landforms.CellCenterXZ(x, z);
                BiomeMacroZonePriors.ApplyMacroZoneBiomePriors(
                    ctx.Bounds, center.x, center.y, ctx.Field, ctx.BiomeIndexById, cell,
                    ctx.LandformProfile.BiomeForcedBoostScale);
            }

            NormalizeWeights(ctx.Field, cell);
            ComputeDominantBiome(ctx.Field, cell);
        }

        private static void FinalizeBuildability(
            ClassifyLoopContext ctx,
            int cell,
            WorldWaterClass waterClass,
            float depth,
            float edgeDist,
            float slope)
        {
            var slopeSuit = Mathf.Clamp01(1f - slope / 35f);
            var waterSuit = waterClass == WorldWaterClass.None ? 1f : 0f;
            var edgeSuit = Mathf.Clamp01(edgeDist / Mathf.Max(1f, ctx.EdgeClearance));
            var biomeMult = SampleDominantBuildability(ctx.Field, ctx.Records, cell);
            ctx.Field.Buildability[cell] = Mathf.Clamp01(slopeSuit * waterSuit * edgeSuit * biomeMult);
            ctx.Field.NoBuild[cell] =
                waterClass == WorldWaterClass.Lake ||
                (waterClass == WorldWaterClass.River && depth > 1.25f) ||
                edgeDist < 40f;
        }

        private static WorldWaterClass SampleWaterClass(HydrologyPlan hydrology, int cell) =>
            hydrology != null && cell < hydrology.WaterClass.Length
                ? hydrology.WaterClass[cell]
                : WorldWaterClass.None;

        private static float SampleWaterDist(HydrologyPlan hydrology, int cell) =>
            hydrology != null && cell < hydrology.DistanceToWaterMeters.Length
                ? hydrology.DistanceToWaterMeters[cell]
                : 9999f;

        private static float SampleFlow(HydrologyPlan hydrology, int cell) =>
            hydrology != null && cell < hydrology.FlowAccumulation.Length
                ? hydrology.FlowAccumulation[cell]
                : 1f;

        private static float SampleDepth(HydrologyPlan hydrology, int cell) =>
            hydrology != null && cell < hydrology.DepthMeters.Length
                ? hydrology.DepthMeters[cell]
                : 0f;

        private static void EvaluateWeights(
            BiomeField field,
            List<WorldBiomeRecord> records,
            int cell,
            float elevNorm,
            float slope)
        {
            var slopeNorm = Mathf.Clamp01(slope / 45f);
            for (var b = 0; b < records.Count; b++)
            {
                var record = records[b];
                var w = record.Priority;
                w *= Eval(record.TemperatureCurve, field.Temperature[cell]);
                w *= Eval(record.MoistureCurve, field.Moisture[cell]);
                w *= Eval(record.ElevationCurve, elevNorm);
                w *= Eval(record.SlopeCurve, slopeNorm);
                field.Weights[field.WeightIndex(cell, b)] = Mathf.Max(0f, w);
            }
        }

        private static void BlendExclusiveBiome(
            BiomeField field,
            Dictionary<string, int> biomeIndexById,
            int cell,
            float amount,
            params string[] stableIds)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f)
                return;

            var target = -1;
            for (var i = 0; i < stableIds.Length; i++)
            {
                if (!biomeIndexById.TryGetValue(stableIds[i], out var b))
                    continue;
                target = b;
                break;
            }

            if (target < 0)
                return;

            var keep = 1f - amount;
            for (var b = 0; b < field.BiomeCount; b++)
                field.Weights[field.WeightIndex(cell, b)] *= keep;

            field.Weights[field.WeightIndex(cell, target)] += amount;
        }

        private static float EvaluateOceanShoreStrength(
            float oceanEdgeDist,
            float beachInner,
            float beachOuter,
            float shoreBand)
        {
            var rampIn = Mathf.Max(6f, shoreBand * 0.45f);
            var fadeOut = Mathf.Max(8f, shoreBand);
            var fadeStart = beachOuter;
            var fadeEnd = beachOuter + fadeOut;

            if (oceanEdgeDist < beachInner || oceanEdgeDist > fadeEnd)
                return 0f;

            var rise = oceanEdgeDist <= beachInner + rampIn
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(beachInner, beachInner + rampIn, oceanEdgeDist))
                : 1f;

            if (oceanEdgeDist <= fadeStart)
                return rise;

            var fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, oceanEdgeDist));
            return rise * fall;
        }

        private static void ForceExclusiveBiome(
            BiomeField field,
            Dictionary<string, int> biomeIndexById,
            int cell,
            params string[] stableIds)
        {
            for (var b = 0; b < field.BiomeCount; b++)
                field.Weights[field.WeightIndex(cell, b)] = 0f;

            for (var i = 0; i < stableIds.Length; i++)
            {
                if (!biomeIndexById.TryGetValue(stableIds[i], out var b))
                    continue;
                field.Weights[field.WeightIndex(cell, b)] = 1f;
                field.DominantBiomeIndex[cell] = b;
                return;
            }

            field.Weights[field.WeightIndex(cell, 0)] = 1f;
            field.DominantBiomeIndex[cell] = 0;
        }

        private static void NormalizeWeights(BiomeField field, int cell)
        {
            var sum = 0f;
            for (var b = 0; b < field.BiomeCount; b++)
                sum += field.Weights[field.WeightIndex(cell, b)];

            if (sum <= 1e-6f)
            {
                field.Weights[field.WeightIndex(cell, 0)] = 1f;
                return;
            }

            for (var b = 0; b < field.BiomeCount; b++)
                field.Weights[field.WeightIndex(cell, b)] /= sum;
        }

        private static void ComputeDominantBiome(BiomeField field, int cell)
        {
            var best = 0;
            var bestW = float.NegativeInfinity;
            for (var b = 0; b < field.BiomeCount; b++)
            {
                var w = field.Weights[field.WeightIndex(cell, b)];
                if (w <= bestW) continue;
                bestW = w;
                best = b;
            }

            field.DominantBiomeIndex[cell] = best;
        }

        private static float SampleDominantBuildability(
            BiomeField field,
            List<WorldBiomeRecord> records,
            int cell)
        {
            var index = field.DominantBiomeIndex[cell];
            if (index < 0 || index >= records.Count)
                return 1f;
            return records[index].BuildabilityMultiplier;
        }

        private static float Eval(AnimationCurve curve, float t) =>
            curve != null ? Mathf.Max(0f, curve.Evaluate(t)) : 1f;
    }
}
