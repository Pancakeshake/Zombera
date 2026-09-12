namespace Zombera.World.CityPipeline.WorldBuilder
{
    public readonly struct WorldWaterSample
    {
        public readonly WorldWaterClass Class;
        public readonly float SurfaceWorldY;
        public readonly float DepthMeters;
        public readonly float DistanceMeters;

        public WorldWaterSample(WorldWaterClass waterClass, float surfaceWorldY, float depthMeters, float distanceMeters)
        {
            Class = waterClass;
            SurfaceWorldY = surfaceWorldY;
            DepthMeters = depthMeters;
            DistanceMeters = distanceMeters;
        }
    }
}
