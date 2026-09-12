using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-cell landform compose: shelf + partitioned hills + residual mountains + orogen.</summary>
    public static partial class LandformGenerator
    {
        private const float MainlandFullThreshold = 0.999f;
        private const float GateEpsilon = 1e-4f;

        private static float EvaluateCell(float worldX, float worldZ, in LandformComposeArgs args)
        {
            var noises = args.Noises;
            var profile = args.Profile;
            var scales = args.Scales;
            var octaves = args.Octaves;
            var bounds = args.Bounds;
            var boundaryLayout = args.BoundaryLayout;

            var continental01 = noises.Continental.Fbm(worldX * scales.InvC, worldZ * scales.InvC, octaves.Cont);
            // Mainland from continentalness; islands/archipelagos from shorter ridged noise outside mainland.
            var mainlandMask = Mathf.SmoothStep(
                profile.MainlandMaskLo,
                profile.MainlandMaskHi,
                continental01);
            var islandMask = 0f;
            if (mainlandMask < MainlandFullThreshold)
            {
                var invIslandScale = 1f / profile.IslandNoiseScaleMeters;
                var invArchScale = 1f / profile.ArchipelagoNoiseScaleMeters;
                var islandRidge = noises.Mountains.Ridged(worldX * invIslandScale, worldZ * invIslandScale, 3);
                var archRidge = noises.Mountains.Ridged(
                    worldX * invArchScale + 11.3f,
                    worldZ * invArchScale - 5.7f,
                    2);
                var islandGate = Mathf.SmoothStep(
                    profile.IslandPeakThreshold,
                    Mathf.Min(0.98f, profile.IslandPeakThreshold + 0.14f),
                    islandRidge);
                var archGate = Mathf.SmoothStep(
                    profile.ArchipelagoPeakThreshold,
                    Mathf.Min(0.98f, profile.ArchipelagoPeakThreshold + 0.12f),
                    archRidge);
                islandMask = Mathf.Max(islandGate, archGate * 0.85f) * (1f - mainlandMask);
            }

            var landMask = Mathf.Max(mainlandMask, islandMask);
            var continentalHeight = (continental01 - 0.5f) * 2f * profile.ContinentalnessAmplitude;
            // Lift island pads a bit so they clear sea even in wet continental noise.
            continentalHeight += islandMask * Mathf.Max(12f, profile.HillsAmplitude * 0.35f);

            noises.Warp.WarpCoordinates(
                worldX,
                worldZ,
                profile.DomainWarpAmplitude,
                profile.DomainWarpScale,
                out var warpedX,
                out var warpedZ);

            var interiorMask = InteriorLandformRelief.EvaluateInteriorMask(
                worldX,
                worldZ,
                bounds,
                boundaryLayout,
                profile.InteriorReliefInsetMeters,
                profile.OceanKeepOutMeters,
                profile.EdgeBarrierDepthMeters);

            // Orogen spines are authored in world XZ; residual hills/detail use warped coords.
            // Do not let continental landMask alone suppress interior orogens (can be "wet" inland).
            var orogenLandMask = Mathf.Max(landMask, interiorMask);
            var orogen = InteriorLandformRelief.Sample(new InteriorLandformRelief.OrogenComposeSampleArgs(
                worldX,
                worldZ,
                in profile,
                args.InteriorRanges,
                noises.InteriorRidge,
                noises.Rolling,
                new InteriorLandformRelief.OrogenComposeMasks(
                    orogenLandMask, interiorMask, args.FastLandforms)));

            var structureGate = Mathf.Max(orogen.OrogenMask, orogen.FoothillMask * 0.85f);
            var hillOnly = 1f - structureGate;
            var hillsBoost = Mathf.Lerp(1f, profile.InteriorHillsMultiplier, interiorMask * hillOnly);

            var hillsHeight = 0f;
            var hillsGate = landMask * hillsBoost * hillOnly;
            if (hillsGate > GateEpsilon)
            {
                var hill01 = noises.Hills.Fbm(warpedX * scales.InvH, warpedZ * scales.InvH, octaves.Hill);
                var hillRidge = noises.Hills.Ridged(
                    warpedX * scales.InvHRidged,
                    warpedZ * scales.InvHRidged,
                    octaves.HillRidge);
                hillsHeight = (hill01 - 0.5f) * 2f * profile.HillsAmplitude * hillsGate;
                hillsHeight += hillRidge * profile.HillsAmplitude * 0.45f * hillsGate;
            }

            var residualAmp = args.UseOrogenAuthority
                ? Mathf.Max(0f, profile.ResidualMountainAmplitudeMeters)
                : profile.MountainAmplitude;
            var residualGate = args.UseOrogenAuthority
                ? (1f - orogen.OrogenMask) * landMask
                : landMask * profile.EvaluateMountainMask(continental01);
            // FBM on foothills; ridged only where residual gate is open and foothill is low.
            var foothillSoft = 1f - orogen.FoothillMask;
            var mountainHeight = 0f;
            if (residualAmp > GateEpsilon && residualGate * foothillSoft > GateEpsilon)
            {
                var ridge = noises.Mountains.Ridged(
                    warpedX * scales.InvM,
                    warpedZ * scales.InvM,
                    octaves.Mountain);
                var ridgeFine = noises.Mountains.Ridged(
                    warpedX * scales.InvMRidged,
                    warpedZ * scales.InvMRidged,
                    octaves.MountainFine);
                mountainHeight = (ridge * 0.55f + ridgeFine * 0.25f) *
                                 residualAmp * residualGate * foothillSoft;
            }

            if (orogen.FoothillMask > 0.01f && args.UseOrogenAuthority)
            {
                var softDetail = noises.Mountains.Fbm(warpedX * scales.InvM, warpedZ * scales.InvM, 2);
                mountainHeight += (softDetail - 0.5f) * 2f *
                                  profile.MicroDetailAmplitudeMeters *
                                  orogen.FoothillMask *
                                  landMask;
            }

            var micro = 0f;
            if (args.UseOrogenAuthority &&
                profile.MicroDetailAmplitudeMeters > 0.01f &&
                orogen.OrogenMask > 0.05f)
            {
                var microN = noises.Mountains.Fbm(
                    warpedX * scales.InvMRidged,
                    warpedZ * scales.InvMRidged,
                    2);
                micro = (microN - 0.5f) * 2f * profile.MicroDetailAmplitudeMeters * orogen.OrogenMask * 0.35f;
            }

            var plains = args.PlainsBias * profile.HillsAmplitude * 0.25f * landMask * hillOnly;
            var raw = args.SeaLevel + continentalHeight + plains + hillsHeight + mountainHeight +
                      orogen.Height + micro;

            // Wet compose cells settle toward a shallow seafloor shelf (edge trenches still carve deeper).
            if (landMask < 0.35f && islandMask < 0.05f)
            {
                var seafloor = args.SeaLevel - Mathf.Lerp(8f, 36f, 1f - landMask);
                raw = Mathf.Lerp(raw, seafloor, 1f - landMask);
            }

            raw = IslandShoreUtility.ApplyIslandShore(
                raw,
                args.SeaLevel,
                landMask,
                islandMask,
                mainlandMask,
                profile.OceanBeachWidthMeters,
                profile.OceanBeachMaxElevationMeters,
                worldX,
                worldZ,
                profile.OceanShelfReefNoiseScaleMeters,
                profile.OceanShelfReefNoiseAmplitudeMeters,
                profile.OceanShelfReefCoverage,
                noises.Barrier);

            if (boundaryLayout.North == WorldMapBoundaryKind.Mountains)
            {
                var northDist = bounds.yMax - worldZ;
                var foothillDepth = profile.EdgeBarrierDepthMeters * 3f;
                if (northDist < foothillDepth)
                {
                    var foothillT = 1f - Mathf.Clamp01(northDist / foothillDepth);
                    foothillT = foothillT * foothillT * (3f - 2f * foothillT);
                    raw += foothillT * profile.EdgeBarrierPeakMeters * 0.32f;
                }
            }

            return ApplyCoastsAndBarriers(raw, worldX, worldZ, landMask, in args);
        }

        private static float ApplyCoastsAndBarriers(
            float raw,
            float worldX,
            float worldZ,
            float landMask,
            in LandformComposeArgs args)
        {
            var profile = args.Profile;
            var noises = args.Noises;
            var bounds = args.Bounds;
            var boundaryLayout = args.BoundaryLayout;
            var height = raw;
            var hasWarpedOcean = false;
            var warpedOceanDist = 0f;

            // Shared ocean distance + warp for coast falloff and inland dry floor (same inputs).
            var hasAnyOcean = WorldMapBoundaryUtility.TryGetOceanEdgeDistance(
                worldX, worldZ, bounds, boundaryLayout, out var oceanEdgeDist);
            if (hasAnyOcean)
            {
                warpedOceanDist = WorldMapBoundaryUtility.WarpOceanEdgeDistance(
                    oceanEdgeDist,
                    worldX,
                    worldZ,
                    noises.Barrier,
                    profile.OceanCoastErosionAmplitudeMeters,
                    profile.OceanCoastErosionScaleMeters);
                hasWarpedOcean = true;
            }

            WorldMapBoundaryUtility.TryGetNearestEdgeSide(
                worldX,
                worldZ,
                bounds,
                out var nearestSide,
                out var edgeDistance);

            // Side-aware coast: only when the nearest edge is ocean (matches prior TryGetSideAware path).
            if (hasWarpedOcean && boundaryLayout.Get(nearestSide) == WorldMapBoundaryKind.Ocean)
                height = ApplyOceanCoastHeights(raw, worldX, worldZ, warpedOceanDist, in args);

            if (WorldMapBoundaryUtility.TryGetEdgeBarrier(
                    worldX,
                    worldZ,
                    bounds,
                    profile.EdgeBarrierDepthMeters,
                    boundaryLayout,
                    out var boundaryKind) &&
                boundaryKind == WorldMapBoundaryKind.Mountains)
            {
                height = ApplyMountainEdgeBarrier(
                    height, worldX, worldZ, edgeDistance, nearestSide, in args);
            }

            return ApplyInlandDryFloorToCell(height, landMask, hasWarpedOcean, warpedOceanDist, in args);
        }

        private static float ApplyOceanCoastHeights(
            float raw,
            float worldX,
            float worldZ,
            float warpedOceanDist,
            in LandformComposeArgs args)
        {
            var profile = args.Profile;
            var seaLevel = args.SeaLevel;
            var height = WorldMapBoundaryUtility.EvaluateOceanCoastFalloff(
                raw,
                seaLevel,
                warpedOceanDist,
                profile.OceanCoastStripWidthMeters,
                profile.CoastFalloffWidthMeters,
                profile.EdgeBarrierDepthMeters,
                profile.OceanBeachMaxElevationMeters);

            var oceanBlend = WorldMapBoundaryUtility.EvaluateOceanCoastBlend(
                warpedOceanDist,
                profile.EdgeBarrierDepthMeters,
                profile.OceanCoastStripWidthMeters);
            if (oceanBlend < 1f)
                return height;

            var along = WorldMapBoundaryUtility.GetAlongEdgeCoordinate(
                worldX,
                worldZ,
                args.Bounds,
                PickOceanAlongSide(worldX, worldZ, args.Bounds, args.BoundaryLayout));
            return WorldMapBoundaryUtility.EvaluateOceanCoastHeight(new OceanCoastHeightParams
            {
                LandHeight = raw,
                SeaLevel = seaLevel,
                EdgeDistanceMeters = warpedOceanDist,
                StripDepthMeters = profile.EdgeBarrierDepthMeters,
                OffshoreWidthMeters = profile.OceanOffshoreWidthMeters,
                ShoreShelfWidthMeters = profile.OceanShoreShelfWidthMeters,
                BeachWidthMeters = profile.OceanBeachWidthMeters,
                BeachMaxElevationMeters = profile.OceanBeachMaxElevationMeters,
                TrenchDepthMeters = profile.OceanTrenchDepthMeters,
                TrenchSteepness = profile.OceanTrenchSteepness,
                TrenchNoiseAmplitudeMeters = profile.OceanTrenchNoiseAmplitudeMeters,
                AlongEdgeMeters = along,
                WorldX = worldX,
                WorldZ = worldZ,
                ShelfOuterDepthMeters = profile.OceanShelfOuterDepthMeters,
                ShelfInnerDepthMeters = profile.OceanShelfInnerDepthMeters,
                ShelfReefNoiseScaleMeters = profile.OceanShelfReefNoiseScaleMeters,
                ShelfReefNoiseAmplitudeMeters = profile.OceanShelfReefNoiseAmplitudeMeters,
                ShelfReefCoverage = profile.OceanShelfReefCoverage,
                Noise = args.Noises.Barrier
            });
        }

        private static float ApplyMountainEdgeBarrier(
            float height,
            float worldX,
            float worldZ,
            float edgeDistance,
            WorldMapEdgeSide nearestSide,
            in LandformComposeArgs args)
        {
            var profile = args.Profile;
            var noises = args.Noises;
            var octaves = args.Octaves;
            var stripBlend = WorldMapBoundaryUtility.EvaluateEdgeBarrierBlend(
                edgeDistance,
                profile.EdgeBarrierDepthMeters,
                profile.EdgeBarrierFalloffMeters);
            if (stripBlend <= GateEpsilon)
                return height;

            var along = WorldMapBoundaryUtility.GetAlongEdgeCoordinate(
                worldX,
                worldZ,
                args.Bounds,
                nearestSide);
            var noiseScale = Mathf.Max(1f, profile.EdgeBarrierNoiseScaleMeters);
            var ridged = noises.Barrier.Ridged(
                along / noiseScale,
                edgeDistance / (noiseScale * 0.32f),
                octaves.Barrier);
            var ridgedFine = noises.Barrier.Ridged(
                along / (noiseScale * 0.55f),
                edgeDistance / (noiseScale * 0.22f),
                octaves.BarrierFine);
            var macro = noises.Barrier.Fbm(
                along / (noiseScale * 2.4f),
                edgeDistance / noiseScale,
                octaves.BarrierMacro);
            var peak = args.SeaLevel + profile.EdgeBarrierPeakMeters *
                       (0.18f + ridged * 0.58f + ridgedFine * 0.24f);
            peak += (macro - 0.5f) * 2f * profile.EdgeBarrierNoiseAmplitudeMeters;
            peak += noises.Mountains.Ridged(
                        worldX * args.Scales.InvMBarrier,
                        worldZ * args.Scales.InvMBarrier,
                        4)
                    * profile.EdgeBarrierPeakMeters * 0.28f;
            height = Mathf.Lerp(height, peak, stripBlend);
            if (stripBlend > 0.5f)
                height = Mathf.Max(height, peak * stripBlend + height * (1f - stripBlend));
            return height;
        }

        private static float ApplyInlandDryFloorToCell(
            float height,
            float landMask,
            bool hasWarpedOcean,
            float warpedOceanDist,
            in LandformComposeArgs args)
        {
            var profile = args.Profile;
            var dryFloor = profile.InlandDryFloorMetersAboveSea;
            if (dryFloor <= 0f)
                return height;

            // Seafloor / channels stay wet; only dry the composed mainland/island pads.
            if (landMask < 0.5f)
                return height;

            if (!hasWarpedOcean)
                return Mathf.Max(height, args.SeaLevel + dryFloor);

            return WorldMapBoundaryUtility.ApplyInlandDryFloor(
                height,
                args.SeaLevel,
                warpedOceanDist,
                profile.OceanCoastStripWidthMeters,
                dryFloor,
                profile.InlandDryFloorBlendMeters);
        }

        private static WorldMapEdgeSide PickOceanAlongSide(
            float worldX,
            float worldZ,
            Rect bounds,
            WorldMapBoundaryLayout layout)
        {
            WorldMapBoundaryUtility.TryGetOceanEdgeDistance(
                worldX, worldZ, bounds, layout, out var best);
            if (layout.West == WorldMapBoundaryKind.Ocean &&
                Mathf.Approximately(worldX - bounds.xMin, best))
                return WorldMapEdgeSide.West;
            if (layout.East == WorldMapBoundaryKind.Ocean &&
                Mathf.Approximately(bounds.xMax - worldX, best))
                return WorldMapEdgeSide.East;
            if (layout.South == WorldMapBoundaryKind.Ocean &&
                Mathf.Approximately(worldZ - bounds.yMin, best))
                return WorldMapEdgeSide.South;
            return WorldMapEdgeSide.North;
        }
    }
}
