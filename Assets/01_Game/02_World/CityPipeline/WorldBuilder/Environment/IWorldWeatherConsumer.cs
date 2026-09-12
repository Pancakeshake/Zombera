namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldWeatherConsumer
    {
        void ApplyWeather(in WorldWeatherSnapshot weather);
    }
}
