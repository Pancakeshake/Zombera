namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldEnvironmentBackend
    {
        bool Validate(WorldEnvironmentProfile profile, out string error);
        void Configure(WorldEnvironmentContext context);
        void Restore(WorldEnvironmentState state);
        WorldEnvironmentState Capture();
    }
}
