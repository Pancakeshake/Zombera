using System.Threading.Tasks;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Stride-gap fill, block replicate, and bilinear upscale for alphamaps.</summary>
    public static partial class AlphamapSoftBlendUtility
    {
        /// <summary>
        /// Fills unpainted stride texels with bilinear interpolation between painted samples.
        /// </summary>
        public static void FillStrideGapsBilinear(
            float[,,] map,
            int width,
            int height,
            int layers,
            int stride)
        {
            if (map == null || stride <= 1 || width < 1 || height < 1 || layers < 1)
                return;

            var ranges = new AlphamapNaturalLayerRanges(layers);
            var maxSampleX = ((width - 1) / stride) * stride;
            var maxSampleZ = ((height - 1) / stride) * stride;

            ForRows(height, z =>
                FillStrideGapsRow(map, width, z, stride, maxSampleX, maxSampleZ, ranges));
        }

        /// <summary>
        /// Nearest-neighbor block upscale — much faster than bilinear for hub fast iteration.
        /// Coarse texels must already be normalized.
        /// </summary>
        public static void UpscaleBlockReplicate(
            float[,,] src,
            int srcW,
            int srcH,
            float[,,] dst,
            int dstW,
            int dstH,
            int layers)
        {
            if (src == null || dst == null || srcW < 1 || srcH < 1 || dstW < 1 || dstH < 1 || layers < 1)
                return;

            var ranges = new AlphamapNaturalLayerRanges(layers);
            for (var srcZ = 0; srcZ < srcH; srcZ++)
            {
                var dstZStart = srcZ * dstH / srcH;
                var dstZEnd = (srcZ + 1) * dstH / srcH;
                for (var srcX = 0; srcX < srcW; srcX++)
                {
                    var region = new AlphamapBlockDstRegion(
                        dstZStart,
                        dstZEnd,
                        srcX * dstW / srcW,
                        (srcX + 1) * dstW / srcW);
                    ReplicateBlockLayers(src, dst, srcZ, srcX, region, ranges);
                }
            }
        }

        /// <summary>
        /// Bilinear upsample from a coarse painted grid to full alphamap resolution.
        /// Renormalizes natural layer weights per texel.
        /// </summary>
        public static void UpscaleBilinear(
            float[,,] src,
            int srcW,
            int srcH,
            float[,,] dst,
            int dstW,
            int dstH,
            int layers) =>
            UpscaleBilinearCore(
                new AlphamapUpscaleMaps(src, srcW, srcH, dst, dstW, dstH, layers),
                renormalizeNatural: true);

        /// <summary>
        /// Bilinear upsample without per-texel normalize
        /// (safe when source texels are already normalized).
        /// Writes only active natural layers after clearing destination.
        /// </summary>
        public static void UpscaleBilinearPreserveWeights(
            float[,,] src,
            int srcW,
            int srcH,
            float[,,] dst,
            int dstW,
            int dstH,
            int layers)
        {
            if (src == null || dst == null ||
                srcW < 1 || srcH < 1 || dstW < 1 || dstH < 1 || layers < 1)
                return;

            System.Array.Clear(dst, 0, dst.Length);
            var ranges = new AlphamapNaturalLayerRanges(layers);
            var activeBuf = UpscaleActiveLayersForThread();
            var activeCount = CollectActiveNaturalLayers(
                src, srcW, srcH, ranges, activeBuf);
            if (activeCount == 0)
            {
                if (ranges.CoreMax > 0)
                {
                    ForRows(dstH, z =>
                    {
                        for (var x = 0; x < dstW; x++)
                            dst[z, x, 0] = 1f;
                    });
                }

                return;
            }

            var active = activeBuf;
            ForRows(dstH, z =>
            {
                SampleSrcAxis(z, srcH, dstH, out var z0, out var z1, out var tz);
                for (var x = 0; x < dstW; x++)
                {
                    SampleSrcAxis(x, srcW, dstW, out var x0, out var x1, out var tx);
                    var corners = new AlphamapBilinearCorners(x0, x1, z0, z1, tx, tz);
                    for (var i = 0; i < activeCount; i++)
                    {
                        var layer = active[i];
                        var v00 = src[corners.Z0, corners.X0, layer];
                        var v10 = src[corners.Z0, corners.X1, layer];
                        var v01 = src[corners.Z1, corners.X0, layer];
                        var v11 = src[corners.Z1, corners.X1, layer];
                        dst[z, x, layer] = Mathf.Lerp(
                            Mathf.Lerp(v00, v10, corners.Tx),
                            Mathf.Lerp(v01, v11, corners.Tx),
                            corners.Tz);
                    }
                }
            });
        }

        private static readonly int[] UpscaleActiveLayerBuffer = new int[32];
        [System.ThreadStatic] private static int[] _tlsUpscaleActiveLayers;

        private static int[] UpscaleActiveLayersForThread()
        {
            if (!_tileWorkerMode)
                return UpscaleActiveLayerBuffer;
            return _tlsUpscaleActiveLayers ??= new int[32];
        }

        private static void FillStrideGapsRow(
            float[,,] map,
            int width,
            int z,
            int stride,
            int maxSampleX,
            int maxSampleZ,
            in AlphamapNaturalLayerRanges ranges)
        {
            var z0 = (z / stride) * stride;
            var z1 = Mathf.Min(z0 + stride, maxSampleZ);
            var tz = z0 == z1 ? 0f : (z - z0) / (float)(z1 - z0);

            for (var x = 0; x < width; x++)
            {
                if (IsPaintedStrideSample(x, z, stride, maxSampleX, maxSampleZ))
                    continue;

                var x0 = (x / stride) * stride;
                var x1 = Mathf.Min(x0 + stride, maxSampleX);
                var tx = x0 == x1 ? 0f : (x - x0) / (float)(x1 - x0);
                var corners = new AlphamapBilinearCorners(x0, x1, z0, z1, tx, tz);
                WriteBilinearRange(map, z, x, 0, ranges.CoreMax - 1, corners);
                if (ranges.HasExtended)
                    WriteBilinearRange(map, z, x, ranges.ExtMin, ranges.ExtMax, corners);
                NormalizeNaturalRanges(map, z, x, ranges);
            }
        }

        private static bool IsPaintedStrideSample(
            int x, int z, int stride, int maxSampleX, int maxSampleZ) =>
            x % stride == 0 && z % stride == 0 && x <= maxSampleX && z <= maxSampleZ;

        private static void ReplicateBlockLayers(
            float[,,] src,
            float[,,] dst,
            int srcZ,
            int srcX,
            in AlphamapBlockDstRegion region,
            in AlphamapNaturalLayerRanges ranges)
        {
            ReplicateLayerRange(src, dst, srcZ, srcX, region, 0, ranges.CoreMax - 1);
            if (!ranges.HasExtended)
                return;

            ReplicateLayerRange(src, dst, srcZ, srcX, region, ranges.ExtMin, ranges.ExtMax);
        }

        private static void ReplicateLayerRange(
            float[,,] src,
            float[,,] dst,
            int srcZ,
            int srcX,
            in AlphamapBlockDstRegion region,
            int layerMin,
            int layerMax)
        {
            for (var layer = layerMin; layer <= layerMax; layer++)
            {
                var value = src[srcZ, srcX, layer];
                for (var dz = region.ZStart; dz < region.ZEnd; dz++)
                {
                    for (var dx = region.XStart; dx < region.XEnd; dx++)
                        dst[dz, dx, layer] = value;
                }
            }
        }

        private static void UpscaleBilinearCore(in AlphamapUpscaleMaps maps, bool renormalizeNatural)
        {
            if (maps.Src == null || maps.Dst == null ||
                maps.SrcW < 1 || maps.SrcH < 1 || maps.DstW < 1 || maps.DstH < 1 || maps.Layers < 1)
                return;

            var ranges = new AlphamapNaturalLayerRanges(maps.Layers);
            var infraMin = WorldSurfacePalette.InfrastructureMinIndex;
            var infraMax = Mathf.Min(maps.Layers - 1, WorldSurfacePalette.InfrastructureMaxIndex);
            var src = maps.Src;
            var dst = maps.Dst;
            var srcW = maps.SrcW;
            var srcH = maps.SrcH;
            var dstW = maps.DstW;
            var dstH = maps.DstH;

            ForRows(dstH, z =>
            {
                SampleSrcAxis(z, srcH, dstH, out var z0, out var z1, out var tz);
                for (var x = 0; x < dstW; x++)
                {
                    SampleSrcAxis(x, srcW, dstW, out var x0, out var x1, out var tx);
                    var corners = new AlphamapBilinearCorners(x0, x1, z0, z1, tx, tz);
                    WriteUpscaledNatural(src, dst, z, x, ranges, corners, renormalizeNatural);
                    ClearInfrastructureLayers(dst, z, x, infraMin, infraMax);
                }
            });
        }

        private static void SampleSrcAxis(
            int dstCoord,
            int srcSize,
            int dstSize,
            out int i0,
            out int i1,
            out float t)
        {
            var srcF = (dstCoord + 0.5f) * srcSize / dstSize - 0.5f;
            i0 = Mathf.Clamp(Mathf.FloorToInt(srcF), 0, srcSize - 1);
            i1 = Mathf.Min(i0 + 1, srcSize - 1);
            t = i1 == i0 ? 0f : srcF - i0;
        }

        private static void WriteUpscaledNatural(
            float[,,] src,
            float[,,] dst,
            int z,
            int x,
            in AlphamapNaturalLayerRanges ranges,
            in AlphamapBilinearCorners corners,
            bool renormalizeNatural)
        {
            WriteBilinearSample(src, dst, z, x, 0, ranges.CoreMax - 1, corners);
            if (ranges.HasExtended)
                WriteBilinearSample(src, dst, z, x, ranges.ExtMin, ranges.ExtMax, corners);

            if (renormalizeNatural)
                NormalizeNaturalRanges(dst, z, x, ranges);
        }

        private static void ClearInfrastructureLayers(
            float[,,] dst,
            int z,
            int x,
            int infraMin,
            int infraMax)
        {
            if (infraMin > infraMax)
                return;

            for (var layer = infraMin; layer <= infraMax; layer++)
                dst[z, x, layer] = 0f;
        }

        private static void WriteBilinearRange(
            float[,,] map,
            int z,
            int x,
            int layerMin,
            int layerMax,
            in AlphamapBilinearCorners corners)
        {
            WriteBilinearSample(map, map, z, x, layerMin, layerMax, corners);
        }

        private static void WriteBilinearSample(
            float[,,] src,
            float[,,] dst,
            int dstZ,
            int dstX,
            int layerMin,
            int layerMax,
            in AlphamapBilinearCorners corners)
        {
            for (var layer = layerMin; layer <= layerMax; layer++)
            {
                var v00 = src[corners.Z0, corners.X0, layer];
                var v10 = src[corners.Z0, corners.X1, layer];
                var v01 = src[corners.Z1, corners.X0, layer];
                var v11 = src[corners.Z1, corners.X1, layer];
                dst[dstZ, dstX, layer] = Mathf.Lerp(
                    Mathf.Lerp(v00, v10, corners.Tx),
                    Mathf.Lerp(v01, v11, corners.Tx),
                    corners.Tz);
            }
        }
    }
}
