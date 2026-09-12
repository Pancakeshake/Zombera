using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Shared alphamap layer-range helpers for <see cref="AlphamapSoftBlendUtility"/>.</summary>
    internal readonly struct AlphamapNaturalLayerRanges
    {
        private const int CoreNaturalLayerCount = 13;

        public readonly int CoreMax;
        public readonly int ExtMin;
        public readonly int ExtMax;

        public AlphamapNaturalLayerRanges(int layers)
        {
            CoreMax = Mathf.Min(layers, CoreNaturalLayerCount);
            ExtMin = WorldSurfacePalette.ExtendedNaturalMinIndex;
            ExtMax = Mathf.Min(layers - 1, WorldSurfacePalette.ExtendedNaturalMaxIndex);
        }

        public bool HasExtended => ExtMin <= ExtMax;
    }

    /// <summary>Bilinear sample corners and lerp factors.</summary>
    internal readonly struct AlphamapBilinearCorners
    {
        public readonly int X0;
        public readonly int X1;
        public readonly int Z0;
        public readonly int Z1;
        public readonly float Tx;
        public readonly float Tz;

        public AlphamapBilinearCorners(int x0, int x1, int z0, int z1, float tx, float tz)
        {
            X0 = x0;
            X1 = x1;
            Z0 = z0;
            Z1 = z1;
            Tx = tx;
            Tz = tz;
        }
    }

    /// <summary>Source/destination alphamap grids for upscale.</summary>
    internal readonly struct AlphamapUpscaleMaps
    {
        private readonly float[,,] _src;
        private readonly float[,,] _dst;

        public float[,,] Src => _src;
        public float[,,] Dst => _dst;
        public readonly int SrcW;
        public readonly int SrcH;
        public readonly int DstW;
        public readonly int DstH;
        public readonly int Layers;

        public AlphamapUpscaleMaps(
            float[,,] src,
            int srcW,
            int srcH,
            float[,,] dst,
            int dstW,
            int dstH,
            int layers)
        {
            _src = src;
            _dst = dst;
            SrcW = srcW;
            SrcH = srcH;
            DstW = dstW;
            DstH = dstH;
            Layers = layers;
        }
    }

    /// <summary>Destination block bounds for nearest-neighbor upscale.</summary>
    internal readonly struct AlphamapBlockDstRegion
    {
        public readonly int ZStart;
        public readonly int ZEnd;
        public readonly int XStart;
        public readonly int XEnd;

        public AlphamapBlockDstRegion(int zStart, int zEnd, int xStart, int xEnd)
        {
            ZStart = zStart;
            ZEnd = zEnd;
            XStart = xStart;
            XEnd = xEnd;
        }
    }
}
