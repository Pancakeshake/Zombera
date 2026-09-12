using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Noise-driven coastal / shore blending for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private bool _loggedCoastalBlendPath;
        private DeterministicNoise2D BeachBlendNoise =>
            _beachBlendNoise ??= new DeterministicNoise2D(_beachBlendNoiseSeed);
        private float ComputeBeachStrength(in BeachStrengthQueryArgs query) =>
            CoastalSurfaceBlendUtility.ComputeBeachStrength(new CoastalSurfaceBlendUtility.BeachStrengthArgs
            {
                ShoreWeight = query.ShoreWeight,
                Elev = query.Elev,
                SeaLevel = query.SeaLevel,
                WaterDist = query.WaterDist,
                WaterClass = query.WaterClass,
                DominantId = query.DominantId,
                NearOceanCoast = query.NearOceanCoast,
                OceanBeachMaxWaterDistanceMeters = _oceanBeachMaxWaterDistanceMeters,
                BeachMaxElevationAboveSea = EffectiveBeachMaxElevationMeters,
                BeachInlandBlendMeters = _beachInlandBlendMeters,
                EdgeDistMeters = query.WarpedOceanEdgeDistMeters,
                OceanCoastStripWidthMeters = EffectiveBeachOuterMeters
            });
        private float EffectiveBeachOuterMeters
        {
            get
            {
                if (_cachedBeachOuterMeters > 0f)
                    return _cachedBeachOuterMeters;
                if (_paintLandformProfile != null)
                    return _paintLandformProfile.OceanCoastStripWidthMeters + BeachOuterMarginMeters;
                return 230f + BeachOuterMarginMeters;
            }
        }
        private float EffectiveBeachMaxElevationMeters
        {
            get
            {
                var profileMax = _paintLandformProfile != null
                    ? _paintLandformProfile.OceanBeachMaxElevationMeters
                    : Mathf.Max(0.5f, _beachMaxElevationAboveSea);
                return profileMax + 1.5f;
            }
        }
        private bool UseLegacyCoastalBlend =>
            _paintLandformProfile != null && _paintLandformProfile.UseLegacyCoastalBlend;
        private void LogCoastalBlendPathOnce()
        {
            if (_loggedCoastalBlendPath)
                return;
            lock (_cellSnapshotLock)
            {
                if (_loggedCoastalBlendPath)
                    return;
                _loggedCoastalBlendPath = true;
            }

            Debug.Log(
                "[WorldSurfacePainter] Coastal blend path=" +
                (UseLegacyCoastalBlend ? "legacy" : "coastal-v3"),
                this);
        }

        private bool IsNearOceanCoast(HydrologyPlan water, LandformField landforms, int cx, int cz)
        {
            if (_paintContextReady && landforms != null)
            {
                var center = landforms.CellCenterXZ(cx, cz);
                var bounds = _paintSession.WorldBoundsXZ;
                var layout = _paintBoundaryLayout;
                if (!_paintBoundaryReady)
                    return IsNearOceanCoastHydrology(water, cx, cz);

                var stripDepth = _paintLandformProfile.EdgeBarrierDepthMeters;
                if (!WorldMapBoundaryUtility.TryGetOceanCoastDistance(
                        center.x,
                        center.y,
                        bounds,
                        stripDepth,
                        layout,
                        out var edgeDist))
                    return false;

                var beachOuter = _cachedBeachOuterMeters > 0f
                    ? _cachedBeachOuterMeters
                    : _paintLandformProfile.OceanCoastStripWidthMeters + BeachOuterMarginMeters;
                return edgeDist <= beachOuter;
            }

            return IsNearOceanCoastHydrology(water, cx, cz);
        }
        private static bool IsNearOceanCoastHydrology(HydrologyPlan water, int cx, int cz, int radiusCells = 2)
        {
            if (water?.WaterClass == null || water.Width <= 0 || water.Height <= 0)
                return false;
            for (var dz = -radiusCells; dz <= radiusCells; dz++)
            {
                for (var dx = -radiusCells; dx <= radiusCells; dx++)
                {
                    var x = cx + dx;
                    var z = cz + dz;
                    if (x < 0 || z < 0 || x >= water.Width || z >= water.Height)
                        continue;
                    if (water.WaterClass[water.Index(x, z)] == WorldWaterClass.Ocean)
                        return true;
                }
            }
            return false;
        }
        private void ApplyCoastalBlend(in CoastalBlendApplyArgs args)
        {
            if (args.CoastalT <= 0.02f)
                return;
            LogCoastalBlendPathOnce();
            var indices = BuildCoastalLayerIndices();
            var noise = BeachBlendNoise;
            var macroScale = Mathf.Max(8f, _beachNoiseScaleMeters);
            var detailScale = Mathf.Max(4f, _beachDetailNoiseScaleMeters);
            var macroOctaves = UseFastPaintSampling ? 2 : 3;
            var macro = noise.Fbm(args.WorldX / macroScale, args.WorldZ / macroScale, macroOctaves);
            var detail = noise.Fbm(
                args.WorldX / detailScale + 31f, args.WorldZ / detailScale + 67f, 2);
            var target = args.Target;
            if (UseLegacyCoastalBlend)
            {
                CoastalSurfaceBlendUtility.AccumulateLegacyBeachLayers(
                    target.Map, target.Z, target.X, target.Layers, indices,
                    new CoastalSurfaceBlendUtility.LegacyBeachAccumulateArgs
                    {
                        BeachStrength = args.CoastalT,
                        WaterDist = args.WaterDist,
                        WaterClass = args.WaterClass,
                        PreferWetSand = args.PreferWetSand,
                        Macro = macro,
                        Detail = detail,
                        InlandWetSandFadeMeters = _inlandWetSandFadeMeters
                    });
                return;
            }
            CoastalSurfaceBlendUtility.SuppressCompetingLayers(
                target.Map, target.Z, target.X, indices, Mathf.Clamp01(args.CoastalT * 1.15f));
            var blend = new CoastalSurfaceBlendUtility.CoastalBlendParams
            {
                CoastalT = args.CoastalT,
                Slope = args.Slope,
                WaterDist = args.WaterDist,
                EdgeDist = args.EdgeDist,
                ElevAbove = args.ElevAbove,
                Moisture = args.Moisture,
                MacroNoise = macro,
                DetailNoise = detail,
                PreferWet = args.PreferWetSand || args.WaterClass == WorldWaterClass.Ocean,
                PreferBlackSand = IsBlackSandCoastBiome(args.DominantId),
                PreferRedCliff = args.DominantId is "Badlands" or "Ashlands",
                PreferDryDesert = args.DominantId is "Desert" or "Dunes"
            };
            CoastalSurfaceBlendUtility.AccumulateCoastalLayers(
                target.Map, target.Z, target.X, target.Layers, indices, blend);
        }
        private static bool IsBlackSandCoastBiome(string dominantId) =>
            dominantId is "Shore" or "Badlands" or "Ashlands" or "Desert" or "Ocean";
        private float SampleBiomeWeightBilinear(
            BiomeField biomes,
            float worldX,
            float worldZ,
            LandformField landforms,
            string stableId)
        {
            if (biomes == null || landforms == null || string.IsNullOrEmpty(stableId))
                return 0f;
            var biomeIndex = ResolveBiomeIndex(biomes, stableId);
            return biomeIndex < 0
                ? 0f
                : SampleBiomeWeightBilinear(biomes, worldX, worldZ, landforms, biomeIndex);
        }
        private static float SampleBiomeWeightBilinear(
            BiomeField biomes,
            float worldX,
            float worldZ,
            LandformField landforms,
            int biomeIndex)
        {
            if (biomes == null || landforms == null || biomeIndex < 0)
                return 0f;
            var invCell = 1f / landforms.CellSize;
            var fx = (worldX - landforms.OriginXZ.x) * invCell - 0.5f;
            var fz = (worldZ - landforms.OriginXZ.y) * invCell - 0.5f;
            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var tx = fx - x0;
            var tz = fz - z0;
            var w00 = SampleBiomeWeightClamped(biomes, x0, z0, biomeIndex);
            var w10 = SampleBiomeWeightClamped(biomes, x0 + 1, z0, biomeIndex);
            var w01 = SampleBiomeWeightClamped(biomes, x0, z0 + 1, biomeIndex);
            var w11 = SampleBiomeWeightClamped(biomes, x0 + 1, z0 + 1, biomeIndex);
            var wx0 = Mathf.Lerp(w00, w10, tx);
            var wx1 = Mathf.Lerp(w01, w11, tx);
            return Mathf.Lerp(wx0, wx1, tz);
        }
        private static float SampleBiomeWeightClamped(BiomeField biomes, int x, int z, int biomeIndex)
        {
            x = Mathf.Clamp(x, 0, biomes.Width - 1);
            z = Mathf.Clamp(z, 0, biomes.Height - 1);
            return biomes.Weights[biomes.WeightIndex(z * biomes.Width + x, biomeIndex)];
        }
        private static float SamplePlanningFieldBilinear(in PlanningFieldSampleArgs args)
        {
            if (args.Values == null || args.Values.Length == 0 || args.Width <= 0 || args.Height <= 0)
                return args.Fallback;
            var fx = (args.WorldX - args.Origin.x) / args.CellSize - 0.5f;
            var fz = (args.WorldZ - args.Origin.y) / args.CellSize - 0.5f;
            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var tx = fx - x0;
            var tz = fz - z0;
            var v00 = SamplePlanningFieldClamped(args.Values, args.Width, args.Height, x0, z0, args.Fallback);
            var v10 = SamplePlanningFieldClamped(args.Values, args.Width, args.Height, x0 + 1, z0, args.Fallback);
            var v01 = SamplePlanningFieldClamped(args.Values, args.Width, args.Height, x0, z0 + 1, args.Fallback);
            var v11 = SamplePlanningFieldClamped(args.Values, args.Width, args.Height, x0 + 1, z0 + 1, args.Fallback);
            var vx0 = Mathf.Lerp(v00, v10, tx);
            var vx1 = Mathf.Lerp(v01, v11, tx);
            return Mathf.Lerp(vx0, vx1, tz);
        }
        private static float SamplePlanningFieldClamped(
            float[] values,
            int width,
            int height,
            int x,
            int z,
            float fallback)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            z = Mathf.Clamp(z, 0, height - 1);
            var index = z * width + x;
            if (index < 0 || index >= values.Length)
                return fallback;
            return values[index];
        }
    }
}

