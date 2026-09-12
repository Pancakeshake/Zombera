namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Controls terrain fidelity for the procedural city-road mesh stage.</summary>
    public enum RoadBuildQualityMode
    {
        /// <summary>Uses the already baked city pads and post-refinement highway corridors.</summary>
        FastIteration,

        /// <summary>Applies the road-stage terrain conformance pass.</summary>
        FullFidelity
    }

    /// <summary>
    /// Natural alphamap paint fidelity. Independent of <see cref="RoadBuildQualityMode"/> /
    /// Fast Roads headless sync.
    /// </summary>
    public enum SurfacePaintQualityMode
    {
        /// <summary>32×32 coarse + block replicate; skip soften. Hub Fast Build iteration.</summary>
        Fast = 0,

        /// <summary>
        /// 128×128 coarse + bilinear preserve weights + SoftRadiusForGrid soft edges.
        /// Mid-tier when Quality is too slow for Hub iteration.
        /// </summary>
        Balanced = 1,

        /// <summary>Full alphamap stride-2 + multi-pass soft edges. Acceptance / coastal verify.</summary>
        Quality = 2
    }

    /// <summary>Optional flags controlling a world-build run.</summary>
    public sealed class WorldBuildRunOptions
    {
        public bool FastRoads { get; set; }

        /// <summary>
        /// Natural surface paint fidelity. Default <see cref="SurfacePaintQualityMode.Quality"/>
        /// for soft blends; Hub Fast Build sets <see cref="SurfacePaintQualityMode.Fast"/>.
        /// </summary>
        public SurfacePaintQualityMode SurfacePaintQuality { get; set; } = SurfacePaintQualityMode.Quality;

        /// <summary>
        /// Legacy bool bridge: true maps to Fast, false to Quality.
        /// Prefer <see cref="SurfacePaintQuality"/>.
        /// </summary>
        public bool FastSurfacePaint
        {
            get => SurfacePaintQuality == SurfacePaintQualityMode.Fast;
            set => SurfacePaintQuality = value
                ? SurfacePaintQualityMode.Fast
                : SurfacePaintQualityMode.Quality;
        }

        /// <summary>Reduced noise octaves during landform synthesis (editor iteration).</summary>
        public bool FastLandforms { get; set; }

        /// <summary>Reduced biome noise octaves during classification (editor iteration).</summary>
        public bool FastBiomeClassify { get; set; }
        public bool RecordTerrainUndo { get; set; }
        public bool ReuseCachedRoads { get; set; }
        public bool ForceRebuild { get; set; }

        /// <summary>
        ///     Terrain fidelity for city-road meshes. Fast iteration fails closed to full fidelity
        ///     unless the city pads and refined highway-corridor bake are available.
        /// </summary>
        public RoadBuildQualityMode RoadBuildQuality { get; set; } = RoadBuildQualityMode.FastIteration;

        public WorldResetMode ResetMode { get; set; } = WorldResetMode.ContentOnly;
    }
}
