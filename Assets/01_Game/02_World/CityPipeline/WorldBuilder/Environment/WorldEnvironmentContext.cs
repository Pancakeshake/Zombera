namespace Zombera.World.CityPipeline.WorldBuilder
{
    public sealed class WorldEnvironmentContext
    {
        public WorldMapSession Session;
        public WorldEnvironmentProfile Profile;
        public string RegionOrBiomeFilter;
        public DeterministicRng Rng;
    }
}
