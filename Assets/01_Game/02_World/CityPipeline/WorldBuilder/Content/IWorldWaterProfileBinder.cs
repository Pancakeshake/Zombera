namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Optional hook for water renderers that read Crest / profile sea-level settings.</summary>
    public interface IWorldWaterProfileBinder
    {
        void BindProfile(WorldWaterProfile water, HydrologyProfile hydrology);
    }
}
