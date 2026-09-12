namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Args for <see cref="WorldMapBoundaryUtility.EvaluateOceanCoastHeight"/> (avoids long free-function arity).
    /// </summary>
    public struct OceanCoastHeightParams
    {
        public float LandHeight;
        public float SeaLevel;
        public float EdgeDistanceMeters;
        public float StripDepthMeters;
        public float OffshoreWidthMeters;
        public float ShoreShelfWidthMeters;
        public float BeachWidthMeters;
        public float BeachMaxElevationMeters;
        public float TrenchDepthMeters;
        public float TrenchSteepness;
        public float TrenchNoiseAmplitudeMeters;
        public float AlongEdgeMeters;
        public float WorldX;
        public float WorldZ;
        public float ShelfOuterDepthMeters;
        public float ShelfInnerDepthMeters;
        public float ShelfReefNoiseScaleMeters;
        public float ShelfReefNoiseAmplitudeMeters;
        public float ShelfReefCoverage;
        public DeterministicNoise2D Noise;
    }
}
