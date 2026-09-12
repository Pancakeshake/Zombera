using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Softens hard alphamap squares from fast-paint stride fill and binary surface picks.
    /// Operates only on natural layers (core 0–12 and extended 17–31).
    /// </summary>
    public static partial class AlphamapSoftBlendUtility
    {
        /// <summary>Shared soften inputs for single- or multi-pass edge blur.</summary>
        public readonly struct SoftenNaturalEdgesArgs
        {
            private readonly float[,,] _map;
            private readonly float[,,] _scratch;

            public float[,,] Map => _map;
            public float[,,] Scratch => _scratch;
            public readonly int Width;
            public readonly int Height;
            public readonly int Layers;
            public readonly int Radius;
            public readonly float Strength;

            public SoftenNaturalEdgesArgs(
                float[,,] map,
                float[,,] scratch,
                int width,
                int height,
                int layers,
                int radius,
                float strength)
            {
                _map = map;
                _scratch = scratch;
                Width = width;
                Height = height;
                Layers = layers;
                Radius = radius;
                Strength = strength;
            }
        }

        /// <summary>
        /// Sliding-window separable box blur on active natural layers, then lerp + renormalize.
        /// </summary>
        public static void SoftenNaturalEdges(
            float[,,] map,
            float[,,] scratch,
            int width,
            int height,
            int layers,
            int radius,
            float strength)
        {
            SoftenNaturalEdgesPasses(
                new SoftenNaturalEdgesArgs(map, scratch, width, height, layers, radius, strength),
                1);
        }

        /// <summary>Repeated soften passes for stair-step snow/rock edges after coarse upscale.</summary>
        public static void SoftenNaturalEdgesPasses(in SoftenNaturalEdgesArgs args, int passes)
        {
            if (args.Map == null || args.Scratch == null ||
                args.Width < 1 || args.Height < 1 || args.Layers < 1)
                return;

            var radius = Mathf.Max(1, args.Radius);
            var strength = Mathf.Clamp01(args.Strength);
            if (strength <= 0.001f)
                return;

            var ranges = new AlphamapNaturalLayerRanges(args.Layers);
            var activeBuf = ActiveLayersForThread();
            var activeCount = CollectActiveNaturalLayers(
                args.Map, args.Width, args.Height, ranges, activeBuf);
            if (activeCount == 0)
                return;

            passes = Mathf.Clamp(passes, 1, 4);
            for (var pass = 0; pass < passes; pass++)
            {
                // Later passes tighten slightly so we don't turn mountains into fog.
                var passRadius = pass == 0 ? radius : Mathf.Max(1, radius - pass);
                var passStrength = pass == 0
                    ? strength
                    : Mathf.Clamp01(strength * (1f - pass * 0.12f));
                SoftenNaturalEdgesOnce(
                    args.Map,
                    args.Width,
                    args.Height,
                    ranges,
                    activeBuf,
                    activeCount,
                    passRadius,
                    passStrength);
            }
        }
    }
}
