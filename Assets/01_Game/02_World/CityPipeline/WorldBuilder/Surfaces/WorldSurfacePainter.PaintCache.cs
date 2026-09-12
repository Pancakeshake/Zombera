using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Scratch buffers, per-cell sampling cache, and paint-mode stride for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private float[,,] _alphamapScratch;
        private float[,,] _alphamapSoftScratch;
        private float[,,] _coarseAlphamapScratch;
        private float[,,] _coarseAlphamapSoftScratch;
        private int _scratchWidth;
        private int _scratchHeight;
        private int _scratchLayers;
        private int _coarseScratchWidth;
        private int _coarseScratchHeight;
        private int _coarseScratchLayers;
        private int _paintTexelStride = 1;
        private bool _skipAlphamapSoften;
        private bool _useCellResolutionPaint;
        private bool _fastPaintSampling;
        private int _coarsePaintResolution = FastPaintCoarseResolution;
        private SurfacePaintQualityMode _configuredPaintMode = SurfacePaintQualityMode.Quality;
        private int _effectiveCoarseSoftRadius = 1;
        private int _effectivePostSoftRadius = 1;
        private float _effectivePostSoftStrength = 0.75f;
        private int _effectivePostSoftPasses = 1;
        /// <summary>When true, Quality soft floors run on the coarse grid before upscale (no full-res post).</summary>
        private bool _softBeforeUpscaleOnly;
        private readonly Dictionary<int, PaintCellSnapshot> _cellSnapshots = new(256);
        private readonly object _cellSnapshotLock = new();

        private struct PaintCellSnapshot
        {
            public string DominantId;
            public bool NearOceanCoast;
            public bool InOceanBarrierStrip;
            public WorldWaterClass WaterClass;
            public float Moisture;
            public float WaterDist;
            public float ShoreWeight;
            public float EdgeDistMeters;
            public float InteriorMask;
        }

        private const int FastPaintCoarseResolution = 32;
        private const int BalancedPaintCoarseResolution = 128;
        private const int QualityPaintCoarseResolution = 256;

        /// <summary>Legacy bool bridge: true → Fast, false → Quality.</summary>
        public void ConfigurePaintIteration(bool fastSurfacePaint)
        {
            ConfigurePaintIteration(
                fastSurfacePaint
                    ? SurfacePaintQualityMode.Fast
                    : SurfacePaintQualityMode.Quality);
        }

        public void ConfigurePaintIteration(SurfacePaintQualityMode mode)
        {
            _configuredPaintMode = mode;
            _paintTexelStride = 1;
            ResolveEffectiveSoftSettings(
                out _effectiveCoarseSoftRadius,
                out _effectivePostSoftRadius,
                out _effectivePostSoftStrength,
                out _effectivePostSoftPasses);

            switch (mode)
            {
                case SurfacePaintQualityMode.Fast:
                    ConfigureFastPaintIteration();
                    break;
                case SurfacePaintQualityMode.Balanced:
                    ConfigureBalancedPaintIteration();
                    break;
                default:
                    ConfigureQualityPaintIteration();
                    break;
            }

            Debug.Log(
                "[WorldSurfacePainter] ConfigurePaintIteration mode=" + mode +
                " cellPaint=" + _useCellResolutionPaint +
                " coarseRes=" + _coarsePaintResolution +
                " fastSampling=" + _fastPaintSampling +
                " stride=" + _paintTexelStride +
                " coarseSoftRadius=" + (_skipAlphamapSoften ? 0 : _effectiveCoarseSoftRadius) +
                " softRadius=" + (_skipAlphamapSoften ? 0 : _effectivePostSoftRadius) +
                " softPasses=" + (_skipAlphamapSoften ? 0 : _effectivePostSoftPasses) +
                " skipSoften=" + _skipAlphamapSoften);
        }

        private void ConfigureFastPaintIteration()
        {
            _useCellResolutionPaint = true;
            _coarsePaintResolution = FastPaintCoarseResolution;
            _fastPaintSampling = true;
            _skipAlphamapSoften = true;
            _softBeforeUpscaleOnly = false;
        }

        private void ConfigureBalancedPaintIteration()
        {
            _useCellResolutionPaint = true;
            _coarsePaintResolution = BalancedPaintCoarseResolution;
            _fastPaintSampling = false;
            _skipAlphamapSoften = false;
            _softBeforeUpscaleOnly = false;
            // Lighter soften than Quality — do not ratchet serialized fields.
            _effectivePostSoftPasses = Mathf.Min(_effectivePostSoftPasses, 1);
            _effectivePostSoftStrength = Mathf.Min(_effectivePostSoftStrength, 0.85f);
        }

        private void ConfigureQualityPaintIteration()
        {
            // Paint+soften at 256 (same ~sample count as stride-2@512), one bilinear upscale.
            // Avoids 512→128 downsample + 128→512 upscale round-trip that dominated soften ms.
            _useCellResolutionPaint = true;
            _coarsePaintResolution = QualityPaintCoarseResolution;
            _fastPaintSampling = false;
            _skipAlphamapSoften = false;
            _paintTexelStride = 1;
            _softBeforeUpscaleOnly = true;
            ApplyMinSoftSettingsLocal(
                3,
                0.92f,
                1,
                ref _effectivePostSoftRadius,
                ref _effectivePostSoftStrength,
                ref _effectivePostSoftPasses);
            _effectiveCoarseSoftRadius = _effectivePostSoftRadius;
        }

        private void ResolveEffectiveSoftSettings(
            out int coarseSoftRadius,
            out int postSoftRadius,
            out float postSoftStrength,
            out int postSoftPasses)
        {
            coarseSoftRadius = Mathf.Max(1, _coarseSoftRadius);
            postSoftRadius = Mathf.Max(1, _postUpscaleSoftRadius);
            postSoftStrength = _postUpscaleSoftStrength;
            postSoftPasses = Mathf.Max(1, _postUpscaleSoftPasses);
            if (_alphamapSoftRadius > postSoftRadius)
                postSoftRadius = _alphamapSoftRadius;
            if (_alphamapSoftStrength > postSoftStrength)
                postSoftStrength = _alphamapSoftStrength;
        }

        private static void ApplyMinSoftSettingsLocal(
            int minRadius,
            float minStrength,
            int minPasses,
            ref int postSoftRadius,
            ref float postSoftStrength,
            ref int postSoftPasses)
        {
            if (postSoftRadius < minRadius)
                postSoftRadius = minRadius;
            if (postSoftStrength < minStrength)
                postSoftStrength = minStrength;
            if (postSoftPasses < minPasses)
                postSoftPasses = minPasses;
        }

        public int PaintTexelStride => _paintTexelStride;
        public bool UseCellResolutionPaint => _useCellResolutionPaint;
        public SurfacePaintQualityMode ConfiguredPaintMode => _configuredPaintMode;
        public int CoarsePaintResolution => _coarsePaintResolution;
        public bool SkipAlphamapSoften => _skipAlphamapSoften;
        public bool ConfiguredFastPaintSampling => _fastPaintSampling;
        public int EffectiveCoarseSoftRadius => _effectiveCoarseSoftRadius;
        public int EffectivePostSoftRadius => _effectivePostSoftRadius;
        private bool UseFastPaintSampling => _fastPaintSampling;

        private BiomeField _cachedBiomeFieldForLookup;
        private readonly Dictionary<string, int> _biomeIndexByStableId = new(16);

        private int ResolveBiomeIndex(BiomeField biomes, string stableId)
        {
            if (biomes == null || string.IsNullOrEmpty(stableId))
                return -1;

            if (biomes != _cachedBiomeFieldForLookup)
            {
                _cachedBiomeFieldForLookup = biomes;
                _biomeIndexByStableId.Clear();
                if (biomes.BiomeStableIds != null)
                {
                    for (var i = 0; i < biomes.BiomeStableIds.Length; i++)
                        _biomeIndexByStableId[biomes.BiomeStableIds[i]] = i;
                }
            }

            return _biomeIndexByStableId.TryGetValue(stableId, out var index) ? index : -1;
        }

        private float[,,] RentAlphamapBuffer(int width, int height, int layers)
        {
            if (_alphamapScratch == null ||
                _scratchWidth != width ||
                _scratchHeight != height ||
                _scratchLayers != layers)
            {
                _alphamapScratch = new float[height, width, layers];
                _alphamapSoftScratch = new float[height, width, layers];
                _scratchWidth = width;
                _scratchHeight = height;
                _scratchLayers = layers;
            }

            return _alphamapScratch;
        }

        private float[,,] RentAlphamapSoftScratch(int width, int height, int layers)
        {
            RentAlphamapBuffer(width, height, layers);
            return _alphamapSoftScratch;
        }

        private float[,,] RentCoarseAlphamapBuffer(int width, int height, int layers)
        {
            if (_coarseAlphamapScratch == null ||
                _coarseScratchWidth != width ||
                _coarseScratchHeight != height ||
                _coarseScratchLayers != layers)
            {
                _coarseAlphamapScratch = new float[height, width, layers];
                _coarseAlphamapSoftScratch = new float[height, width, layers];
                _coarseScratchWidth = width;
                _coarseScratchHeight = height;
                _coarseScratchLayers = layers;
            }

            return _coarseAlphamapScratch;
        }

        private float[,,] RentCoarseAlphamapSoftScratch(int width, int height, int layers)
        {
            RentCoarseAlphamapBuffer(width, height, layers);
            return _coarseAlphamapSoftScratch;
        }

        private void ClearCellSnapshots() => _cellSnapshots.Clear();

        private PaintCellSnapshot GetCellSnapshot(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes,
            int cx,
            int cz)
        {
            var key = landforms.Index(
                Mathf.Clamp(cx, 0, landforms.Width - 1),
                Mathf.Clamp(cz, 0, landforms.Height - 1));
            lock (_cellSnapshotLock)
            {
                if (_cellSnapshots.TryGetValue(key, out var cached))
                    return cached;
            }

            var built = BuildCellSnapshot(landforms, water, biomes, cx, cz, key);
            lock (_cellSnapshotLock)
            {
                if (_cellSnapshots.TryGetValue(key, out var raced))
                    return raced;
                _cellSnapshots[key] = built;
                return built;
            }
        }

        private PaintCellSnapshot BuildCellSnapshot(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes,
            int cx,
            int cz,
            int cell)
        {
            biomes.TryGetDominantStableId(cell, out var dominantId);

            var snapshot = new PaintCellSnapshot
            {
                DominantId = dominantId,
                WaterClass = SampleWaterClass(water, cx, cz),
                Moisture = cell < biomes.Moisture.Length ? biomes.Moisture[cell] : 0.5f,
                WaterDist = water != null && cell < water.DistanceToWaterMeters.Length
                    ? water.DistanceToWaterMeters[cell]
                    : 9999f,
                EdgeDistMeters = 9999f,
                InteriorMask = 1f
            };

            // Always apply coast data — dry-shoulder cells (WaterClass.None, waterDist >= 40)
            // are still coastal by map boundary and must not fall back to grass.
            if (_paintSessionGridsReady)
            {
                TryApplySessionCoastData(ref snapshot, cell);
            }
            else
            {
                snapshot.NearOceanCoast = IsNearOceanCoast(water, landforms, cx, cz);
                snapshot.ShoreWeight = SampleBiomeWeightAtCell(biomes, landforms, cx, cz, "Shore");
                FillLiveCoastSnapshotFields(ref snapshot, landforms, cx, cz);
            }

            return snapshot;
        }

        private void FillLiveCoastSnapshotFields(
            ref PaintCellSnapshot snapshot,
            LandformField landforms,
            int cx,
            int cz)
        {
            if (!_paintContextReady || !_paintBoundaryReady || landforms == null || _paintLandformProfile == null)
                return;

            var center = landforms.CellCenterXZ(cx, cz);
            var bounds = _paintSession.WorldBoundsXZ;
            var layout = _paintBoundaryLayout;
            if (WorldMapBoundaryUtility.TryGetSideAwareOceanEdgeDistance(
                    center.x, center.y, bounds, layout, out var edgeDist))
            {
                snapshot.EdgeDistMeters = edgeDist;
                snapshot.InOceanBarrierStrip = edgeDist < _paintLandformProfile.EdgeBarrierDepthMeters;
            }

            snapshot.InteriorMask = InteriorLandformRelief.EvaluateInteriorMask(
                center.x, center.y, bounds, layout, _paintLandformProfile);
        }

        private static WorldWaterClass SampleWaterClass(HydrologyPlan water, int cx, int cz)
        {
            if (water == null || cx < 0 || cz < 0 || cx >= water.Width || cz >= water.Height)
                return WorldWaterClass.None;
            return water.WaterClass[water.Index(cx, cz)];
        }

        private float SampleBiomeWeightAtCell(
            BiomeField biomes,
            LandformField landforms,
            int cx,
            int cz,
            string stableId)
        {
            if (biomes == null || landforms == null || string.IsNullOrEmpty(stableId))
                return 0f;

            var biomeIndex = ResolveBiomeIndex(biomes, stableId);
            if (biomeIndex < 0)
                return 0f;

            var center = landforms.CellCenterXZ(cx, cz);
            return SampleBiomeWeightBilinearFast(biomes, center.x, center.y, biomeIndex);
        }

    }
}
