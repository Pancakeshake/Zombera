using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-texel natural surface accumulation for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private void PaintTexel(in PaintTexelArgs args)
        {
            var target = args.Target;
            SamplePlanningCell(args.WorldX, args.WorldZ, out var cx, out var cz);
            var cell = args.Landforms.Index(
                Mathf.Clamp(cx, 0, args.Landforms.Width - 1),
                Mathf.Clamp(cz, 0, args.Landforms.Height - 1));

            float elev;
            float slope;
            if (UseFastPaintSampling)
            {
                elev = LandformFieldSampling.SampleClamped(args.Landforms, cx, cz);
                slope = cell < args.Biomes.Slope.Length ? args.Biomes.Slope[cell] : 0f;
            }
            else
            {
                LandformFieldSampling.SampleElevAndSlopeBilinear(
                    args.Landforms, args.WorldX, args.WorldZ, out elev, out slope);
            }

            var snapshot = GetCellSnapshot(args.Landforms, args.Water, args.Biomes, cx, cz);
            var biomeCoords = BiomeBilinearCoords.FromWorld(
                args.WorldX, args.WorldZ, _paintLandformOrigin, _paintInvLandformCellSize);

            ClearNatural(target.Map, target.Z, target.X, target.Layers);
            AccumulateBiomeWeights(new AccumulatePaintArgs
            {
                Target = target,
                WorldX = args.WorldX,
                WorldZ = args.WorldZ,
                Landforms = args.Landforms,
                Biomes = args.Biomes,
                NaturalRecords = args.NaturalRecords,
                Cell = cell,
                Slope = slope,
                Elev = elev,
                SeaLevel = args.SeaLevel,
                Snapshot = snapshot,
                BiomeCoords = biomeCoords
            });
            NormalizeNatural(target.Map, target.Z, target.X, target.Layers);
        }

        private void AccumulateBiomeWeights(in AccumulatePaintArgs args)
        {
            var decision = BuildBeachDecision(in args);
            if (TryApplyOceanCoastal(in args, in decision))
                return;

            var macroZonePainted = TryPaintInlandBase(in args, in decision);
            ApplyElevationOverlays(in args, in decision, macroZonePainted);
            TryApplyEdgeCoastalOverlay(in args, in decision);
        }

        private BeachDecision BuildBeachDecision(in AccumulatePaintArgs args)
        {
            var snapshot = args.Snapshot;
            var elevAbove = args.Elev - args.SeaLevel;
            var beachElevCap = EffectiveBeachMaxElevationMeters;
            var warpedDist = snapshot.EdgeDistMeters;
            var nearCoast = snapshot.NearOceanCoast;

            var waterClass = snapshot.WaterClass;
            if (waterClass == WorldWaterClass.Ocean &&
                !nearCoast &&
                elevAbove > beachElevCap)
            {
                waterClass = WorldWaterClass.None;
            }

            var beachStrength = ComputeBeachStrength(new BeachStrengthQueryArgs
            {
                ShoreWeight = snapshot.ShoreWeight,
                Elev = args.Elev,
                SeaLevel = args.SeaLevel,
                WaterDist = snapshot.WaterDist,
                WaterClass = waterClass,
                DominantId = snapshot.DominantId,
                NearOceanCoast = nearCoast,
                WarpedOceanEdgeDistMeters = warpedDist
            });
            var coastalT = CoastalSurfaceBlendUtility.CoastalBlendFactor(beachStrength);
            var beachElevOk = elevAbove <= beachElevCap;
            var onEdgeBeachBand = nearCoast;
            var forceBeachSand = onEdgeBeachBand && beachElevOk && args.Slope < 28f &&
                                 (coastalT > 0.04f ||
                                  snapshot.WaterDist < _oceanBeachMaxWaterDistanceMeters ||
                                  warpedDist <= EffectiveBeachOuterMeters * 0.9f);

            return new BeachDecision
            {
                CoastalT = coastalT,
                BeachElevCap = beachElevCap,
                OnEdgeBeachBand = onEdgeBeachBand,
                ForceBeachSand = forceBeachSand,
                ElevAbove = elevAbove,
                WaterDist = snapshot.WaterDist,
                EdgeDist = warpedDist,
                Moisture = snapshot.Moisture,
                WaterClass = waterClass,
                DominantId = snapshot.DominantId,
                NearOceanCoast = nearCoast,
                InteriorMask = snapshot.InteriorMask,
                InOceanBarrier = snapshot.InOceanBarrierStrip
            };
        }

        private bool TryApplyOceanCoastal(in AccumulatePaintArgs args, in BeachDecision decision)
        {
            if (decision.WaterClass != WorldWaterClass.Ocean)
                return false;

            ApplyCoastalBlend(new CoastalBlendApplyArgs
            {
                Target = args.Target,
                WorldX = args.WorldX,
                WorldZ = args.WorldZ,
                CoastalT = 1f,
                Slope = args.Slope,
                ElevAbove = decision.ElevAbove,
                WaterDist = decision.WaterDist,
                EdgeDist = decision.EdgeDist,
                Moisture = decision.Moisture,
                WaterClass = decision.WaterClass,
                DominantId = decision.DominantId,
                PreferWetSand = true
            });
            return true;
        }

        private bool TryPaintInlandBase(in AccumulatePaintArgs args, in BeachDecision decision)
        {
            var macroZonePainted = !UseFastPaintSampling &&
                !decision.OnEdgeBeachBand &&
                TryApplyMacroZoneSurfaces(
                    args.Target.Map, args.Target.Z, args.Target.X, args.Target.Layers,
                    args.WorldX, args.WorldZ);

            if (macroZonePainted)
                return true;

            if (!decision.ForceBeachSand)
            {
                ApplyBlendedBiomeSurfaces(new BiomeBlendPaintArgs
                {
                    Target = args.Target,
                    Biomes = args.Biomes,
                    NaturalRecords = args.NaturalRecords,
                    Landforms = args.Landforms,
                    Cell = args.Cell,
                    Coords = args.BiomeCoords,
                    ElevAbove = decision.ElevAbove,
                    Slope = args.Slope,
                    InteriorMask = decision.InteriorMask,
                    WorldX = args.WorldX,
                    WorldZ = args.WorldZ
                });
            }

            FillSparseNaturalBase(in args, in decision);
            return false;
        }

        private void FillSparseNaturalBase(in AccumulatePaintArgs args, in BeachDecision decision)
        {
            var target = args.Target;
            if (SumNaturalLayers(target.Map, target.Z, target.X, target.Layers) >= 0.01f)
            {
                TryReinforceMainGrass(in args, in decision);
                return;
            }

            if (decision.ForceBeachSand)
                return;

            if (ShouldUseMainGrassBase(
                    decision.WaterClass, decision.CoastalT, decision.DominantId,
                    decision.NearOceanCoast, decision.Moisture) ||
                (decision.DominantId == "Shore" && !decision.NearOceanCoast))
            {
                ApplyMainGrassMix(new MainGrassMixArgs
                {
                    Target = target,
                    WorldX = args.WorldX,
                    WorldZ = args.WorldZ,
                    Scale = 1f,
                    DominantId = decision.DominantId,
                    Moisture = decision.Moisture
                });
                return;
            }

            ApplyDominantSpecialtySurfaces(
                target.Map, target.Z, target.X, target.Layers,
                args.NaturalRecords, decision.DominantId);
        }

        private void TryReinforceMainGrass(in AccumulatePaintArgs args, in BeachDecision decision)
        {
            if (decision.ForceBeachSand)
                return;
            if (!ShouldUseMainGrassBase(
                    decision.WaterClass, decision.CoastalT, decision.DominantId,
                    decision.NearOceanCoast, decision.Moisture))
                return;
            if (decision.CoastalT >= 0.35f)
                return;

            ApplyMainGrassMix(new MainGrassMixArgs
            {
                Target = args.Target,
                WorldX = args.WorldX,
                WorldZ = args.WorldZ,
                Scale = 0.35f,
                DominantId = decision.DominantId,
                Moisture = decision.Moisture
            });
        }

        private void ApplyElevationOverlays(
            in AccumulatePaintArgs args,
            in BeachDecision decision,
            bool macroZonePainted)
        {
            // Macro zones paint base colors only. Always run the full elevation ladder
            // (dirt → highland grass → cliffs → rock/snow → peak exclusive) so Quality /
            // Balanced do not skip ApplyPeakSnow when useMacroBiomeRegions is on.
            _ = macroZonePainted;

            var target = args.Target;
            var snowBiomeWeight = SampleSnowBiomeWeight(args.Biomes, args.BiomeCoords);
            if (decision.ForceBeachSand)
                return;

            if (!(decision.CoastalT > 0.6f && args.Slope < 12f))
            {
                ApplyValleyForestFloor(new ValleyForestFloorArgs
                {
                    Target = target,
                    ElevAbove = decision.ElevAbove,
                    Slope = args.Slope,
                    DominantId = decision.DominantId,
                    Moisture = decision.Moisture
                });
            }

            ApplyPeakSnow(
                target.Map, target.Z, target.X, target.Layers,
                args.WorldX, args.WorldZ, args.Slope, args.Elev, args.SeaLevel,
                decision.DominantId, snowBiomeWeight, decision.InteriorMask,
                decision.InOceanBarrier, decision.NearOceanCoast);
        }

        private void TryApplyEdgeCoastalOverlay(in AccumulatePaintArgs args, in BeachDecision decision)
        {
            if (!decision.OnEdgeBeachBand)
                return;
            if (!(decision.CoastalT > 0.02f || decision.ForceBeachSand))
                return;

            var waterFade = Mathf.Clamp01(
                Mathf.Exp(-(decision.WaterDist - 1f) / Mathf.Max(4f, _beachInlandBlendMeters)));
            var edgeFade = Mathf.Clamp01(
                Mathf.Exp(-decision.EdgeDist / Mathf.Max(8f, _beachInlandBlendMeters * 1.25f)));
            var elevFade = 1f - Mathf.Clamp01(decision.ElevAbove / Mathf.Max(0.5f, decision.BeachElevCap));
            var coastFade = Mathf.Max(waterFade, edgeFade * Mathf.Max(elevFade, 0.4f));
            var overlayT = decision.ForceBeachSand
                ? Mathf.Max(decision.CoastalT, 0.55f) * Mathf.Max(coastFade, 0.55f)
                : Mathf.Max(decision.CoastalT, 0.35f * edgeFade) * coastFade;
            if (overlayT <= 0.02f && decision.ForceBeachSand)
                overlayT = 0.45f;
            if (overlayT <= 0.02f)
                return;

            ApplyCoastalBlend(new CoastalBlendApplyArgs
            {
                Target = args.Target,
                WorldX = args.WorldX,
                WorldZ = args.WorldZ,
                CoastalT = overlayT,
                Slope = args.Slope,
                ElevAbove = decision.ElevAbove,
                WaterDist = decision.WaterDist,
                EdgeDist = decision.EdgeDist,
                Moisture = decision.Moisture,
                WaterClass = decision.WaterClass,
                DominantId = decision.DominantId,
                PreferWetSand = decision.WaterDist < 5f || decision.ElevAbove < 2.5f
            });
        }
    }
}

