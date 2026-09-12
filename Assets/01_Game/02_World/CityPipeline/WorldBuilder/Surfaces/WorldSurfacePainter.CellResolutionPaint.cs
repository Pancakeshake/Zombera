using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Landform-cell coarse paint + bilinear upscale for hub fast iteration.</summary>
    public sealed partial class WorldSurfacePainter
    {
        public static void ComputeCoarsePaintDimensions(
            float tileSizeX,
            float tileSizeZ,
            float landformCellSize,
            int maxResolution,
            out int coarseW,
            out int coarseH)
        {
            if (maxResolution > 0)
            {
                coarseW = maxResolution;
                coarseH = maxResolution;
                return;
            }

            coarseW = Mathf.Max(1, Mathf.CeilToInt(tileSizeX / landformCellSize));
            coarseH = Mathf.Max(1, Mathf.CeilToInt(tileSizeZ / landformCellSize));
        }

        /// <summary>
        /// Soft radius was tuned for full alphamap texels; scale down on coarse grids so
        /// world-space blur stays comparable (radius 2 @ 512 ≈ radius 1 @ 128).
        /// </summary>
        public static int SoftRadiusForGrid(int gridResolution, int referenceAlphamapResolution, int configuredRadius)
        {
            var radius = Mathf.Max(1, configuredRadius);
            if (gridResolution <= 0 || referenceAlphamapResolution <= 0)
                return radius;
            if (gridResolution >= referenceAlphamapResolution)
                return radius;
            return Mathf.Max(1, Mathf.RoundToInt(radius * (gridResolution / (float)referenceAlphamapResolution)));
        }

        private void PaintNaturalSurfaceAtCellResolution(in NaturalSurfacePaintArgs args) =>
            PaintCellResolutionCore(in args, applyAlphamap: true, out _, out _, out _);

        private void PaintCellResolutionCpu(
            in NaturalSurfacePaintArgs args,
            out long loopMs,
            out long softenMs,
            out long upscaleMs) =>
            PaintCellResolutionCore(in args, applyAlphamap: false, out loopMs, out softenMs, out upscaleMs);

        private void PaintAlphamapResolutionCpu(
            in NaturalSurfacePaintArgs args,
            out long loopMs,
            out long softenMs,
            out long upscaleMs) =>
            PaintAlphamapResolutionCore(in args, applyAlphamap: false, out loopMs, out softenMs, out upscaleMs);

        private void PaintCellResolutionCore(
            in NaturalSurfacePaintArgs args,
            bool applyAlphamap,
            out long loopMs,
            out long softenMs,
            out long upscaleMs)
        {
            var coarseRes = Mathf.Max(8, _coarsePaintResolution);
            ComputeCoarsePaintDimensions(
                args.Size.x, args.Size.z, args.Landforms.CellSize, coarseRes,
                out var coarseW, out var coarseH);
            var coarseMap = RentCoarseAlphamapBufferThreadAware(coarseW, coarseH, args.Layers);
            ClearAlphamapBuffer(coarseMap);
            var invCoarseW = 1f / coarseW;
            var invCoarseH = 1f / coarseH;
            var sw = Stopwatch.StartNew();
            using (PaintLoopMarker.Auto())
                PaintCoarseMap(args, coarseMap, coarseW, coarseH, invCoarseW, invCoarseH);

            loopMs = sw.ElapsedMilliseconds;
            sw.Restart();
            using (SoftenMarker.Auto())
                SoftenCoarseMapBeforeUpscale(args, coarseMap, coarseW, coarseH, coarseRes);

            softenMs = sw.ElapsedMilliseconds;
            sw.Restart();
            using (UpscaleMarker.Auto())
                UpscaleCoarseMap(args, coarseMap, coarseW, coarseH);

            upscaleMs = sw.ElapsedMilliseconds;
            sw.Restart();
            using (SoftenMarker.Auto())
            {
                if (SoftenUpscaledMap(args))
                    softenMs += sw.ElapsedMilliseconds;
            }

            if (!applyAlphamap)
                return;

            sw.Restart();
            using (SetAlphamapMarker.Auto())
                args.Data.SetAlphamaps(0, 0, args.Map);
            _paintLoopMs += loopMs;
            _paintSoftenMs += softenMs;
            _paintUpscaleMs += upscaleMs;
            _paintSetAlphamapMs += sw.ElapsedMilliseconds;
            _paintTileCount++;
            MarkOrSyncPaintedTile(args.Terrain, args.Width, args.Height);
        }

        /// <summary>Paints the coarse landform-cell grid, serially on tile workers and in parallel otherwise.</summary>
        private void PaintCoarseMap(
            in NaturalSurfacePaintArgs args,
            float[,,] coarseMap,
            int coarseW,
            int coarseH,
            float invCoarseW,
            float invCoarseH)
        {
            var origin = args.Origin;
            var size = args.Size;
            var layers = args.Layers;
            var landforms = args.Landforms;
            var water = args.Water;
            var biomes = args.Biomes;
            var naturalRecords = args.NaturalRecords;
            var seaLevel = args.SeaLevel;
            Action<int> paintRow = z =>
            {
                var worldZ = origin.z + (z + 0.5f) * invCoarseH * size.z;
                for (var x = 0; x < coarseW; x++)
                {
                    var worldX = origin.x + (x + 0.5f) * invCoarseW * size.x;
                    PaintTexel(new PaintTexelArgs
                    {
                        Target = new AlphamapWriteTarget(coarseMap, z, x, layers),
                        WorldX = worldX,
                        WorldZ = worldZ,
                        Landforms = landforms,
                        Water = water,
                        Biomes = biomes,
                        NaturalRecords = naturalRecords,
                        SeaLevel = seaLevel
                    });
                }
            };
            if (AlphamapSoftBlendUtility.TileWorkerMode)
            {
                for (var z = 0; z < coarseH; z++)
                    paintRow(z);
            }
            else
                Parallel.For(0, coarseH, paintRow);
        }

        /// <summary>Softens the coarse grid before upscale so world-space blur stays comparable at low resolution.</summary>
        private void SoftenCoarseMapBeforeUpscale(
            in NaturalSurfacePaintArgs args,
            float[,,] coarseMap,
            int coarseW,
            int coarseH,
            int coarseRes)
        {
            if (!_softenAlphamapEdges || _skipAlphamapSoften)
                return;

            var softRadiusSrc = _softBeforeUpscaleOnly
                ? _effectivePostSoftRadius
                : _effectiveCoarseSoftRadius;
            var coarseSoftRadius = SoftRadiusForGrid(coarseRes, args.Width, softRadiusSrc);
            if (_softBeforeUpscaleOnly)
                coarseSoftRadius = Mathf.Max(2, coarseSoftRadius);
            var softStrength = _softBeforeUpscaleOnly
                ? _effectivePostSoftStrength
                : _coarseSoftStrength;
            var softPasses = _softBeforeUpscaleOnly
                ? Mathf.Max(1, _effectivePostSoftPasses)
                : 1;
            AlphamapSoftBlendUtility.SoftenNaturalEdgesPasses(
                new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                    coarseMap,
                    RentCoarseAlphamapSoftScratchThreadAware(coarseW, coarseH, args.Layers),
                    coarseW,
                    coarseH,
                    args.Layers,
                    coarseSoftRadius,
                    softStrength),
                softPasses);
        }

        private void UpscaleCoarseMap(
            in NaturalSurfacePaintArgs args,
            float[,,] coarseMap,
            int coarseW,
            int coarseH)
        {
            if (!_skipAlphamapSoften || !UseFastPaintSampling)
            {
                AlphamapSoftBlendUtility.UpscaleBilinearPreserveWeights(
                    coarseMap, coarseW, coarseH, args.Map, args.Width, args.Height, args.Layers);
                return;
            }

            ClearAlphamapBuffer(args.Map);
            AlphamapSoftBlendUtility.UpscaleBlockReplicate(
                coarseMap, coarseW, coarseH, args.Map, args.Width, args.Height, args.Layers);
        }

        /// <summary>Post-upscale soften; returns false when the configuration skips it.</summary>
        private bool SoftenUpscaledMap(in NaturalSurfacePaintArgs args)
        {
            if (_softBeforeUpscaleOnly ||
                !_softenAlphamapEdges || _skipAlphamapSoften ||
                _effectivePostSoftRadius <= 0 || _effectivePostSoftStrength <= 0.001f)
                return false;

            AlphamapSoftBlendUtility.SoftenNaturalEdgesPasses(
                new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                    args.Map,
                    RentAlphamapSoftScratchThreadAware(args.Width, args.Height, args.Layers),
                    args.Width,
                    args.Height,
                    args.Layers,
                    _effectivePostSoftRadius,
                    _effectivePostSoftStrength),
                Mathf.Max(1, _effectivePostSoftPasses));
            return true;
        }

        private void PaintNaturalSurfaceAtAlphamapResolution(in NaturalSurfacePaintArgs args) =>
            PaintAlphamapResolutionCore(in args, applyAlphamap: true, out _, out _, out _);

        private void PaintAlphamapResolutionCore(
            in NaturalSurfacePaintArgs args,
            bool applyAlphamap,
            out long loopMs,
            out long softenMs,
            out long upscaleMs)
        {
            var stride = Mathf.Max(1, _paintTexelStride);
            var invH = 1f / args.Height;
            var invW = 1f / args.Width;
            ClearAlphamapBuffer(args.Map);
            var sw = Stopwatch.StartNew();
            using (PaintLoopMarker.Auto())
            {
                var zCount = (args.Height + stride - 1) / stride;
                var origin = args.Origin;
                var size = args.Size;
                var map = args.Map;
                var width = args.Width;
                var layers = args.Layers;
                var landforms = args.Landforms;
                var water = args.Water;
                var biomes = args.Biomes;
                var naturalRecords = args.NaturalRecords;
                var seaLevel = args.SeaLevel;
                Action<int> paintZi = zi =>
                {
                    var z = zi * stride;
                    var worldZ = origin.z + (z + 0.5f) * invH * size.z;
                    for (var x = 0; x < width; x += stride)
                    {
                        var worldX = origin.x + (x + 0.5f) * invW * size.x;
                        PaintTexel(new PaintTexelArgs
                        {
                            Target = new AlphamapWriteTarget(map, z, x, layers),
                            WorldX = worldX,
                            WorldZ = worldZ,
                            Landforms = landforms,
                            Water = water,
                            Biomes = biomes,
                            NaturalRecords = naturalRecords,
                            SeaLevel = seaLevel
                        });
                    }
                };
                if (AlphamapSoftBlendUtility.TileWorkerMode)
                {
                    for (var zi = 0; zi < zCount; zi++)
                        paintZi(zi);
                }
                else
                    Parallel.For(0, zCount, paintZi);

                if (stride > 1)
                    AlphamapSoftBlendUtility.FillStrideGapsBilinear(
                        args.Map, args.Width, args.Height, args.Layers, stride);
            }

            loopMs = sw.ElapsedMilliseconds;
            upscaleMs = 0;
            sw.Restart();
            using (SoftenMarker.Auto())
            {
                if (_softenAlphamapEdges && !_skipAlphamapSoften &&
                    _effectivePostSoftRadius > 0 && _effectivePostSoftStrength > 0.001f)
                {
                    var softArgs = new AlphamapSoftBlendUtility.SoftenNaturalEdgesArgs(
                        args.Map,
                        RentAlphamapSoftScratchThreadAware(args.Width, args.Height, args.Layers),
                        args.Width,
                        args.Height,
                        args.Layers,
                        _effectivePostSoftRadius,
                        _effectivePostSoftStrength);
                    var passes = Mathf.Max(1, _effectivePostSoftPasses);
                    if (_configuredPaintMode == SurfacePaintQualityMode.Quality &&
                        args.Width >= 256 &&
                        args.Height >= 256)
                        AlphamapSoftBlendUtility.SoftenNaturalEdgesPassesDownsampled(
                            softArgs, passes, divisor: 4);
                    else
                        AlphamapSoftBlendUtility.SoftenNaturalEdgesPasses(softArgs, passes);
                }
            }

            softenMs = sw.ElapsedMilliseconds;
            if (!applyAlphamap)
                return;

            sw.Restart();
            using (SetAlphamapMarker.Auto())
                args.Data.SetAlphamaps(0, 0, args.Map);
            _paintLoopMs += loopMs;
            _paintSoftenMs += softenMs;
            _paintSetAlphamapMs += sw.ElapsedMilliseconds;
            _paintTileCount++;
            MarkOrSyncPaintedTile(args.Terrain, args.Width, args.Height);
        }

        private void MarkOrSyncPaintedTile(Terrain terrain, int width, int height)
        {
            if (SyncBackend != null)
                SyncBackend.MarkDirty(terrain, new RectInt(0, 0, width, height));
            else
                MicroSplatTerrainBinder.SyncControlTexturesOnly(terrain);
        }

        private static void ClearAlphamapBuffer(float[,,] map)
        {
            if (map == null)
                return;
            Array.Clear(map, 0, map.Length);
        }
    }
}
