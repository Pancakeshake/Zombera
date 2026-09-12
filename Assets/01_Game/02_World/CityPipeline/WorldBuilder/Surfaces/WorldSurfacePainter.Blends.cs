using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Mountain elevation rock/snow bands and inland grass for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private DeterministicNoise2D MountainBlendNoise =>
            _mountainBlendNoise ??= new DeterministicNoise2D(_mountainBlendNoiseSeed);
        private DeterministicNoise2D MainGrassNoise =>
            _mainGrassNoise ??= new DeterministicNoise2D(_mainGrassNoiseSeed);
        private void SuppressVegetationForElevationBand(float[,,] map, int z, int x, float strength)
        {
            var keep = 1f - Mathf.Clamp01(strength);
            if (keep >= 0.999f)
                return;
            if (_layerGrassGreen >= 0)
                map[z, x, _layerGrassGreen] *= keep;
            if (_layerGrassYellow >= 0)
                map[z, x, _layerGrassYellow] *= keep;
            if (_layerGrass >= 0)
                map[z, x, _layerGrass] *= keep;
            if (_layerSparseGrass >= 0)
                map[z, x, _layerSparseGrass] *= keep;
            if (_layerDirt >= 0)
                map[z, x, _layerDirt] *= keep;
        }
        private void ApplyValleyForestFloor(in ValleyForestFloorArgs args)
        {
            if (args.DominantId != "Forest" && args.DominantId != "Hills" && args.DominantId != "Wetland")
                return;
            if (args.ElevAbove > _rockMinElevationMeters - 30f || args.Slope > 24f)
                return;
            var valley = Mathf.InverseLerp(_rockMinElevationMeters - 30f, 80f, args.ElevAbove);
            var flat = Mathf.InverseLerp(22f, 8f, args.Slope);
            var wet = Mathf.Lerp(0.65f, 1f, args.Moisture);
            var amount = valley * flat * wet * 0.5f;
            if (amount <= 0.03f)
                return;
            var target = args.Target;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerDirt, amount);
            if (_layerGrassGreen >= 0)
                target.Map[target.Z, target.X, _layerGrassGreen] *= 1f - amount * 0.35f;
        }
        private void ApplyElevationDirtBand(
            float[,,] map,
            int z,
            int x,
            int layers,
            float slope,
            float elevAbove)
        {
            if (elevAbove < _dirtBandMinElevationMeters || elevAbove >= _rockMinElevationMeters + 40f)
                return;
            var elevDirt = Mathf.InverseLerp(_dirtBandMinElevationMeters, _dirtBandFullElevationMeters, elevAbove);
            var slopeDamp = Mathf.InverseLerp(38f, 16f, slope);
            var amount = elevDirt * slopeDamp * 0.45f;
            if (amount <= 0.03f)
                return;
            AddLayerByIndex(map, z, x, layers, _layerDirt, amount);
        }
        private float ApplyElevationRockBand(in ElevationRockBandArgs args)
        {
            var elevRock = args.ElevAbove <= _rockMinElevationMeters
                ? 0f
                : Mathf.InverseLerp(_rockMinElevationMeters, _rockFullElevationMeters, args.ElevAbove);
            var slopeRock = Mathf.InverseLerp(20f, 48f, args.Slope);
            var slopeExposure = Mathf.InverseLerp(14f, 32f, args.Slope);
            var combined = Mathf.Max(elevRock * slopeExposure, slopeRock * 0.5f);
            if (!IsRockyBiome(args.DominantId) && !IsSnowyBiome(args.DominantId))
            {
                combined *= Mathf.InverseLerp(12f, 24f, args.Slope);
                combined *= Mathf.Lerp(0.4f, 1f, args.SnowBiomeWeight + elevRock);
            }
            if (combined <= 0.03f)
                return 0f;
            var blend = EvaluateMountainSurfaceWeights(
                args.WorldX,
                args.WorldZ,
                args.Slope,
                args.ElevAbove,
                _rockMinElevationMeters,
                _rockFullElevationMeters,
                args.SnowMin,
                args.SnowFull);
            var exposedRock = blend.x + blend.y;
            if (exposedRock <= 0.01f)
                return 0f;
            var target = args.Target;
            SuppressVegetationForElevationBand(
                target.Map, target.Z, target.X, combined * Mathf.Lerp(0.45f, 0.8f, exposedRock));
            AddLayerByIndex(
                target.Map, target.Z, target.X, target.Layers, _layerCliffDark, combined * blend.x);
            AddLayerByIndex(
                target.Map, target.Z, target.X, target.Layers, _layerSnowRock, combined * blend.y);
            return combined * exposedRock;
        }
        private void ApplyElevationSnowBand(in ElevationSnowBandArgs args)
        {
            if (args.ElevAbove < args.SnowMin)
                return;
            var elevFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(args.SnowMin, args.SnowFull, args.ElevAbove));
            if (elevFactor <= 0.02f)
                return;
            var blend = EvaluateMountainSurfaceWeights(
                args.WorldX,
                args.WorldZ,
                args.Slope,
                args.ElevAbove,
                _rockMinElevationMeters,
                _rockFullElevationMeters,
                args.SnowMin,
                args.SnowFull);
            var snowAmount = blend.z * Mathf.Lerp(0.7f, 1.2f, elevFactor);
            if (args.SnowBiomeWeight > 0.12f)
            {
                snowAmount = Mathf.Max(
                    snowAmount,
                    blend.z * args.SnowBiomeWeight * (_cliffSnowMaxWeight + 0.4f));
            }
            if (snowAmount <= 0.02f)
                return;
            var suppress = Mathf.Max(snowAmount * 0.75f, args.RockStrength * 0.35f, elevFactor * 0.55f);
            var target = args.Target;
            SuppressVegetationForElevationBand(target.Map, target.Z, target.X, suppress);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSnow, snowAmount);
        }
        /// <summary>
        ///     Keeps meadow / sparse grass on higher gentle slopes until snow/rock take over.
        /// </summary>
        private void ApplyHighlandGrass(in HighlandGrassArgs args)
        {
            if (args.ElevAbove < 25f || args.ElevAbove >= args.SnowFull)
                return;
            if (args.Slope > 42f)
                return;
            var climb = Mathf.InverseLerp(25f, Mathf.Max(90f, args.SnowMin - 20f), args.ElevAbove);
            var beforeSnow = 1f - Mathf.InverseLerp(args.SnowMin - 50f, args.SnowFull, args.ElevAbove);
            var gentle = Mathf.InverseLerp(42f, 6f, args.Slope);
            var amount = climb * beforeSnow * Mathf.Lerp(0.7f, 1.2f, gentle);
            if (amount <= 0.02f)
                return;
            var noise = MainGrassNoise;
            var scale = Mathf.Max(10f, _mainGrassNoiseScaleMeters * 1.15f);
            var patch = noise.Fbm(args.WorldX / scale, args.WorldZ / scale, 2);
            amount *= Mathf.Lerp(1.05f, 1.55f, patch);
            var target = args.Target;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrassGreen, amount * 0.95f);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerMeadowGrass, amount * 0.65f);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSparseGrass, amount * 0.5f);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrassYellow, amount * 0.3f);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrass, amount * 0.28f);
        }
        private static bool IsRockyBiome(string stableId) =>
            stableId == "Hills" ||
            stableId == "Mountains" ||
            stableId == "Badlands" ||
            stableId == "Ashlands";
        private static bool IsSnowyBiome(string stableId) =>
            stableId == "Snow" || stableId == "AlpineSnow" || stableId == "Mountains";
        private static bool IsAridBiome(string stableId) =>
            stableId == "Desert" ||
            stableId == "Dunes" ||
            stableId == "Badlands" ||
            stableId == "Savanna" ||
            stableId == "Scrubland";
        private void ApplyMainGrassMix(in MainGrassMixArgs args)
        {
            if (args.Scale <= 0f)
                return;
            var noiseScale = Mathf.Max(8f, _mainGrassNoiseScaleMeters);
            var noise = MainGrassNoise;
            var macro = noise.Fbm(args.WorldX / noiseScale, args.WorldZ / noiseScale, 1);
            var detail = noise.Fbm(
                args.WorldX / (noiseScale * 0.45f) + 19f,
                args.WorldZ / (noiseScale * 0.45f) + 53f,
                2);
            var target = args.Target;
            if (TryApplySpecialMainGrass(in args, macro, detail))
                return;
            var grassGreen = Mathf.Lerp(0.35f, 0.55f, macro) * args.Scale;
            var grassYellow = Mathf.Lerp(0.28f, 0.45f, detail) * args.Scale * _mainGrassYellowScale;
            var grassAlt = Mathf.Lerp(0.12f, 0.28f, 1f - macro) * args.Scale *
                           Mathf.Lerp(0.7f, 1.1f, args.Moisture);
            var sparse = Mathf.Lerp(0.08f, 0.22f, detail) * args.Scale * (1f - args.Moisture * 0.5f);
            var meadow = args.Moisture > 0.55f
                ? Mathf.Lerp(0.08f, 0.22f, args.Moisture) * args.Scale * Mathf.Lerp(0.4f, 1f, macro)
                : 0f;
            var dryFloor = args.Moisture < 0.35f && args.DominantId is "Hills" or "Plains" or "Forest"
                ? Mathf.Lerp(0.05f, 0.16f, 1f - args.Moisture) * args.Scale * detail
                : 0f;
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrassGreen, grassGreen);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrassYellow, grassYellow);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerGrass, grassAlt);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSparseGrass, sparse);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerMeadowGrass, meadow);
            AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerDryForestFloor, dryFloor);
        }
        private bool TryApplySpecialMainGrass(in MainGrassMixArgs args, float macro, float detail)
        {
            var target = args.Target;
            switch (args.DominantId)
            {
                case "Savanna":
                case "Scrubland":
                    AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerMeadowGrass,
                        Mathf.Lerp(0.35f, 0.55f, macro) * args.Scale);
                    AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSparseGrass,
                        Mathf.Lerp(0.3f, 0.5f, detail) * args.Scale);
                    return true;
                case "Taiga":
                    AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerDryForestFloor,
                        Mathf.Lerp(0.4f, 0.6f, macro) * args.Scale);
                    AddLayerByIndex(target.Map, target.Z, target.X, target.Layers, _layerSparseGrass,
                        Mathf.Lerp(0.25f, 0.4f, detail) * args.Scale);
                    return true;
                default:
                    return false;
            }
        }
        private const float MainGrassBeachCutoff = CoastalSurfaceBlendUtility.BeachSoftMin;
        private void ApplyExplicitSurfaceWeights(
            float[,,] map,
            int z,
            int x,
            int layers,
            WorldBiomeRecord record,
            float scale)
        {
            if (scale <= 0f) return;
            if (record?.SurfaceWeights == null || record.SurfaceWeights.Count == 0)
            {
                AddLayer(map, z, x, layers, DefaultSemantic(record), scale);
                return;
            }
            for (var i = 0; i < record.SurfaceWeights.Count; i++)
            {
                var entry = record.SurfaceWeights[i];
                if (entry == null) continue;
                AddLayer(map, z, x, layers, entry.SurfaceSemantic, scale * entry.Weight);
            }
        }
        private void ApplyDominantSpecialtySurfaces(
            float[,,] map,
            int z,
            int x,
            int layers,
            List<WorldBiomeRecord> records,
            string dominantId)
        {
            var record = FindBiomeRecord(records, dominantId);
            ApplyExplicitSurfaceWeights(map, z, x, layers, record, 1f);
        }
        private Dictionary<string, WorldBiomeRecord> _biomeRecordByStableId;
        private List<WorldBiomeRecord> _cachedBiomeRecordList;
        private WorldBiomeRecord FindBiomeRecord(List<WorldBiomeRecord> records, string stableId)
        {
            if (records == null || string.IsNullOrEmpty(stableId))
                return null;
            if (records != _cachedBiomeRecordList || _biomeRecordByStableId == null)
            {
                _cachedBiomeRecordList = records;
                _biomeRecordByStableId = new Dictionary<string, WorldBiomeRecord>(records.Count, StringComparer.Ordinal);
                for (var i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    if (record == null || string.IsNullOrEmpty(record.StableId))
                        continue;
                    _biomeRecordByStableId[record.StableId] = record;
                }
            }
            return _biomeRecordByStableId.TryGetValue(stableId, out var found) ? found : null;
        }
        private static bool ShouldUseMainGrassBase(
            WorldWaterClass waterClass,
            float coastalT,
            string dominantId,
            bool nearOceanCoast,
            float moisture)
        {
            if (waterClass != WorldWaterClass.None)
                return false;
            if (coastalT > MainGrassBeachCutoff)
                return false;
            if (nearOceanCoast && coastalT > 0.12f)
                return false;
            return dominantId switch
            {
                "Ocean" => false,
                "Shore" when nearOceanCoast => false,
                "Mountains" or "Snow" or "AlpineSnow" => false,
                "Desert" or "Dunes" or "Badlands" or "Ashlands" => moisture > 0.36f,
                "Wetland" or "Swamp" => false,
                "Savanna" or "Scrubland" => false,
                _ => true
            };
        }
        private static string DefaultSemantic(WorldBiomeRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.StableId)) return "GrassGreen";
            return record.StableId switch
            {
                "Ocean" => "Sand",
                "Shore" => "Sand",
                "Mountains" => "CliffBright",
                "Badlands" or "Ashlands" => "CliffRed",
                "Desert" or "Dunes" => "Sand",
                "Snow" or "AlpineSnow" => "Snow",
                "Wetland" or "Swamp" => "Dirt",
                "Savanna" or "Scrubland" => "SparseGrass",
                "Taiga" => "Dirt",
                _ => "GrassGreen"
            };
        }
    }
}

