namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Optional hook for environment backends that read Enviro / profile references.</summary>
    public interface IWorldEnvironmentProfileBinder
    {
        void BindProfile(WorldEnvironmentProfile environment);
    }
}
