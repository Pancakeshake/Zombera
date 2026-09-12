using System;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldWeatherSource
    {
        event Action<WorldWeatherSnapshot> WeatherChanged;
        WorldWeatherSnapshot Current { get; }
    }
}
