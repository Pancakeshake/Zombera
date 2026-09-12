using System.Threading.Tasks;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Planar cache-friendly soften: same sliding-box + lerp + normalize math as the
    /// prior float[,,] path, with active-layer reuse across passes and Parallel.For rows/cols.
    /// </summary>
    public static partial class AlphamapSoftBlendUtility
    {
        private const int MaxTrackedActiveLayers = 32;
        private static readonly int[] ActiveLayerBuffer = new int[MaxTrackedActiveLayers];
        private static float[] _planeOriginal;
        private static float[] _planeBlurred;
        private static readonly object SoftenPlaneLock = new();

        [System.ThreadStatic] private static int[] _tlsActiveLayers;
        [System.ThreadStatic] private static float[] _tlsPlaneOriginal;
        [System.ThreadStatic] private static float[] _tlsPlaneBlurred;

        private static int[] ActiveLayersForThread()
        {
            if (!_tileWorkerMode)
                return ActiveLayerBuffer;
            return _tlsActiveLayers ??= new int[MaxTrackedActiveLayers];
        }

        private static void SoftenNaturalEdgesOnce(
            float[,,] map,
            int width,
            int height,
            in AlphamapNaturalLayerRanges ranges,
            int[] activeLayers,
            int activeCount,
            int radius,
            float strength)
        {
            var planeLen = width * height;
            EnsureSoftenPlanes(planeLen);

            for (var i = 0; i < activeCount; i++)
                SoftenActiveLayerPlanar(
                    map, width, height, activeLayers[i], radius, strength);

            NormalizeNaturalRangesParallel(map, width, height, ranges);
        }

        private static void SoftenActiveLayerPlanar(
            float[,,] map,
            int width,
            int height,
            int layer,
            int radius,
            float strength)
        {
            var original = _tileWorkerMode ? _tlsPlaneOriginal : _planeOriginal;
            var blurred = _tileWorkerMode ? _tlsPlaneBlurred : _planeBlurred;
            CopyLayerToPlane(map, width, height, layer, original);
            BlurHorizontalPlanarParallel(original, blurred, width, height, radius);
            BlurVerticalPlanarLerpParallel(original, blurred, map, width, height, layer, radius, strength);
        }

        private static void EnsureSoftenPlanes(int planeLen)
        {
            if (_tileWorkerMode)
            {
                if (_tlsPlaneOriginal == null || _tlsPlaneOriginal.Length < planeLen)
                    _tlsPlaneOriginal = new float[planeLen];
                if (_tlsPlaneBlurred == null || _tlsPlaneBlurred.Length < planeLen)
                    _tlsPlaneBlurred = new float[planeLen];
                return;
            }

            if (_planeOriginal != null && _planeOriginal.Length >= planeLen &&
                _planeBlurred != null && _planeBlurred.Length >= planeLen)
                return;

            lock (SoftenPlaneLock)
            {
                if (_planeOriginal == null || _planeOriginal.Length < planeLen)
                    _planeOriginal = new float[planeLen];
                if (_planeBlurred == null || _planeBlurred.Length < planeLen)
                    _planeBlurred = new float[planeLen];
            }
        }

        private static void CopyLayerToPlane(
            float[,,] map,
            int width,
            int height,
            int layer,
            float[] plane)
        {
            for (var z = 0; z < height; z++)
            {
                var row = z * width;
                for (var x = 0; x < width; x++)
                    plane[row + x] = map[z, x, layer];
            }
        }

        private static void BlurHorizontalPlanarParallel(
            float[] source,
            float[] dest,
            int width,
            int height,
            int radius)
        {
            ForRows(height, z => BlurRowHorizontalPlanar(source, dest, z, width, radius));
        }

        private static void BlurVerticalPlanarLerpParallel(
            float[] original,
            float[] horizontalBlurred,
            float[,,] map,
            int width,
            int height,
            int layer,
            int radius,
            float strength)
        {
            ForRows(
                width,
                x => BlurColumnVerticalPlanarLerp(
                    original, horizontalBlurred, map, x, width, height, layer, radius, strength));
        }

        private static void BlurRowHorizontalPlanar(
            float[] source,
            float[] dest,
            int z,
            int width,
            int radius)
        {
            var row = z * width;
            var sum = 0f;
            var rightInit = Mathf.Min(width - 1, radius);
            for (var i = 0; i <= rightInit; i++)
                sum += source[row + i];

            for (var x = 0; x < width; x++)
            {
                var xMin = Mathf.Max(0, x - radius);
                var xMax = Mathf.Min(width - 1, x + radius);
                if (x > 0)
                {
                    var prevMin = Mathf.Max(0, x - 1 - radius);
                    if (prevMin < xMin)
                        sum -= source[row + prevMin];
                    var prevMax = Mathf.Min(width - 1, x - 1 + radius);
                    if (xMax > prevMax)
                        sum += source[row + xMax];
                }

                dest[row + x] = sum / (xMax - xMin + 1);
            }
        }

        private static void BlurColumnVerticalPlanarLerp(
            float[] original,
            float[] horizontalBlurred,
            float[,,] map,
            int x,
            int width,
            int height,
            int layer,
            int radius,
            float strength)
        {
            var sum = 0f;
            var bottomInit = Mathf.Min(height - 1, radius);
            for (var i = 0; i <= bottomInit; i++)
                sum += horizontalBlurred[i * width + x];

            for (var z = 0; z < height; z++)
            {
                var zMin = Mathf.Max(0, z - radius);
                var zMax = Mathf.Min(height - 1, z + radius);
                if (z > 0)
                {
                    var prevMin = Mathf.Max(0, z - 1 - radius);
                    if (prevMin < zMin)
                        sum -= horizontalBlurred[prevMin * width + x];
                    var prevMax = Mathf.Min(height - 1, z - 1 + radius);
                    if (zMax > prevMax)
                        sum += horizontalBlurred[zMax * width + x];
                }

                var blurred = sum / (zMax - zMin + 1);
                map[z, x, layer] = Mathf.Lerp(original[z * width + x], blurred, strength);
            }
        }

        private static void NormalizeNaturalRangesParallel(
            float[,,] map,
            int width,
            int height,
            in AlphamapNaturalLayerRanges ranges)
        {
            var coreMax = ranges.CoreMax;
            var hasExtended = ranges.HasExtended;
            var extMin = ranges.ExtMin;
            var extMax = ranges.ExtMax;
            ForRows(height, z =>
            {
                for (var x = 0; x < width; x++)
                    NormalizeNaturalRangesAt(map, z, x, coreMax, hasExtended, extMin, extMax);
            });
        }

        private static void NormalizeNaturalRanges(
            float[,,] map,
            int z,
            int x,
            in AlphamapNaturalLayerRanges ranges)
        {
            NormalizeNaturalRangesAt(
                map, z, x, ranges.CoreMax, ranges.HasExtended, ranges.ExtMin, ranges.ExtMax);
        }

        private static void NormalizeNaturalRangesAt(
            float[,,] map,
            int z,
            int x,
            int coreMax,
            bool hasExtended,
            int extMin,
            int extMax)
        {
            var sum = SumRange(map, z, x, 0, coreMax - 1);
            if (hasExtended)
                sum += SumRange(map, z, x, extMin, extMax);

            if (sum <= 1e-5f)
            {
                if (coreMax > 0)
                    map[z, x, 0] = 1f;
                return;
            }

            var inv = 1f / sum;
            ScaleRange(map, z, x, 0, coreMax - 1, inv);
            if (hasExtended)
                ScaleRange(map, z, x, extMin, extMax, inv);
        }

        private static float SumRange(float[,,] map, int z, int x, int layerMin, int layerMax)
        {
            var sum = 0f;
            for (var layer = layerMin; layer <= layerMax; layer++)
                sum += map[z, x, layer];
            return sum;
        }

        private static void ScaleRange(
            float[,,] map,
            int z,
            int x,
            int layerMin,
            int layerMax,
            float scale)
        {
            for (var layer = layerMin; layer <= layerMax; layer++)
                map[z, x, layer] *= scale;
        }

        private static int CollectActiveNaturalLayers(
            float[,,] map,
            int width,
            int height,
            in AlphamapNaturalLayerRanges ranges,
            int[] dst)
        {
            var count = 0;
            count = AppendActiveLayersInRange(map, width, height, 0, ranges.CoreMax - 1, dst, count);
            if (ranges.HasExtended)
            {
                count = AppendActiveLayersInRange(
                    map, width, height, ranges.ExtMin, ranges.ExtMax, dst, count);
            }

            return count;
        }

        private static int AppendActiveLayersInRange(
            float[,,] map,
            int width,
            int height,
            int layerMin,
            int layerMax,
            int[] dst,
            int count)
        {
            for (var layer = layerMin; layer <= layerMax; layer++)
            {
                if (count >= dst.Length)
                    break;
                if (!LayerHasWeight(map, width, height, layer))
                    continue;
                dst[count++] = layer;
            }

            return count;
        }

        private static bool LayerHasWeight(float[,,] map, int width, int height, int layer)
        {
            var stepZ = height < 8 ? 1 : 4;
            var stepX = width < 8 ? 1 : 4;
            for (var z = 0; z < height; z += stepZ)
            {
                for (var x = 0; x < width; x += stepX)
                {
                    if (map[z, x, layer] > 1e-4f)
                        return true;
                }
            }

            return false;
        }
    }
}
