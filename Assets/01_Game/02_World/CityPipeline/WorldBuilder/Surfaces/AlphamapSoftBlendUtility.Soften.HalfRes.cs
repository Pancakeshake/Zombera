using System.Threading.Tasks;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Downsampled soften for Quality: box downsample → multi-pass soft → bilinear upscale.
    /// Same pass/strength policy; fewer soft pixels (÷4 at half, ÷16 at quarter vs full-res).
    /// </summary>
    public static partial class AlphamapSoftBlendUtility
    {
        private static float[,,] _downMap;
        private static float[,,] _downScratch;
        private static int _downW;
        private static int _downH;
        private static int _downLayers;

        /// <summary>Soften at half alphamap resolution (legacy entry).</summary>
        public static void SoftenNaturalEdgesPassesHalfRes(
            in SoftenNaturalEdgesArgs args,
            int passes) =>
            SoftenNaturalEdgesPassesDownsampled(args, passes, 2);

        /// <summary>
        /// Soften at alphamapResolution/divisor. Divisor 4 on 512 → 128 working grid.
        /// </summary>
        public static void SoftenNaturalEdgesPassesDownsampled(
            in SoftenNaturalEdgesArgs args,
            int passes,
            int divisor)
        {
            if (args.Map == null || args.Width < 4 || args.Height < 4 || args.Layers < 1)
            {
                SoftenNaturalEdgesPasses(args, passes);
                return;
            }

            divisor = Mathf.Clamp(divisor, 2, 8);
            var workW = args.Width / divisor;
            var workH = args.Height / divisor;
            if (workW < 2 || workH < 2)
            {
                SoftenNaturalEdgesPasses(args, passes);
                return;
            }

            EnsureDownBuffers(workW, workH, args.Layers);
            BoxDownsampleNatural(
                args.Map, args.Width, args.Height, _downMap, workW, workH, args.Layers, divisor);

            // Keep working radius ≥2 so quarter-res still carries world-space soft width.
            var workRadius = Mathf.Max(
                2,
                Mathf.RoundToInt(Mathf.Max(1, args.Radius) * (workW / (float)args.Width)));
            SoftenNaturalEdgesPasses(
                new SoftenNaturalEdgesArgs(
                    _downMap,
                    _downScratch,
                    workW,
                    workH,
                    args.Layers,
                    workRadius,
                    args.Strength),
                passes);

            UpscaleBilinearPreserveWeights(
                _downMap, workW, workH, args.Map, args.Width, args.Height, args.Layers);
        }

        private static void EnsureDownBuffers(int workW, int workH, int layers)
        {
            if (_downMap != null &&
                _downW == workW &&
                _downH == workH &&
                _downLayers == layers &&
                _downScratch != null)
                return;

            _downMap = new float[workH, workW, layers];
            _downScratch = new float[workH, workW, layers];
            _downW = workW;
            _downH = workH;
            _downLayers = layers;
        }

        private static void BoxDownsampleNatural(
            float[,,] src,
            int srcW,
            int srcH,
            float[,,] dst,
            int dstW,
            int dstH,
            int layers,
            int divisor)
        {
            var ranges = new AlphamapNaturalLayerRanges(layers);
            ForRows(dstH, z =>
            {
                var z0 = z * divisor;
                var z1 = Mathf.Min(srcH - 1, z0 + divisor - 1);
                for (var x = 0; x < dstW; x++)
                {
                    var x0 = x * divisor;
                    var x1 = Mathf.Min(srcW - 1, x0 + divisor - 1);
                    AverageNaturalBlock(src, dst, z, x, z0, z1, x0, x1, ranges);
                }
            });
        }

        private static void AverageNaturalBlock(
            float[,,] src,
            float[,,] dst,
            int dstZ,
            int dstX,
            int z0,
            int z1,
            int x0,
            int x1,
            in AlphamapNaturalLayerRanges ranges)
        {
            ClearNaturalCell(dst, dstZ, dstX, ranges);
            var cells = (z1 - z0 + 1) * (x1 - x0 + 1);
            var inv = 1f / cells;
            AverageRange(src, dst, dstZ, dstX, z0, z1, x0, x1, 0, ranges.CoreMax - 1, inv);
            if (ranges.HasExtended)
                AverageRange(
                    src, dst, dstZ, dstX, z0, z1, x0, x1, ranges.ExtMin, ranges.ExtMax, inv);
            NormalizeNaturalCell(dst, dstZ, dstX, ranges);
        }

        private static void ClearNaturalCell(
            float[,,] map,
            int z,
            int x,
            in AlphamapNaturalLayerRanges ranges)
        {
            for (var layer = 0; layer < ranges.CoreMax; layer++)
                map[z, x, layer] = 0f;
            if (!ranges.HasExtended)
                return;
            for (var layer = ranges.ExtMin; layer <= ranges.ExtMax; layer++)
                map[z, x, layer] = 0f;
        }

        private static void AverageRange(
            float[,,] src,
            float[,,] dst,
            int dstZ,
            int dstX,
            int z0,
            int z1,
            int x0,
            int x1,
            int layerMin,
            int layerMax,
            float inv)
        {
            for (var layer = layerMin; layer <= layerMax; layer++)
            {
                var sum = 0f;
                for (var z = z0; z <= z1; z++)
                {
                    for (var x = x0; x <= x1; x++)
                        sum += src[z, x, layer];
                }

                dst[dstZ, dstX, layer] = sum * inv;
            }
        }

        private static void NormalizeNaturalCell(
            float[,,] map,
            int z,
            int x,
            in AlphamapNaturalLayerRanges ranges)
        {
            var sum = 0f;
            for (var layer = 0; layer < ranges.CoreMax; layer++)
                sum += map[z, x, layer];
            if (ranges.HasExtended)
            {
                for (var layer = ranges.ExtMin; layer <= ranges.ExtMax; layer++)
                    sum += map[z, x, layer];
            }

            if (sum <= 1e-5f)
            {
                if (ranges.CoreMax > 0)
                    map[z, x, 0] = 1f;
                return;
            }

            var scale = 1f / sum;
            for (var layer = 0; layer < ranges.CoreMax; layer++)
                map[z, x, layer] *= scale;
            if (!ranges.HasExtended)
                return;
            for (var layer = ranges.ExtMin; layer <= ranges.ExtMax; layer++)
                map[z, x, layer] *= scale;
        }
    }
}
