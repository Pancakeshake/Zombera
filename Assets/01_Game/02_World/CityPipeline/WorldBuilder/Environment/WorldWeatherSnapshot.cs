namespace Zombera.World.CityPipeline.WorldBuilder
{
    public readonly struct WorldWeatherSnapshot
    {
        public readonly string WeatherId;
        public readonly float Transition01;
        public readonly float WindStrength01;
        public readonly float Precipitation01;
        public readonly float TemperatureCelsius;

        public WorldWeatherSnapshot(
            string weatherId,
            float transition01,
            float windStrength01,
            float precipitation01,
            float temperatureCelsius)
        {
            WeatherId = weatherId ?? "Clear";
            Transition01 = transition01;
            WindStrength01 = windStrength01;
            Precipitation01 = precipitation01;
            TemperatureCelsius = temperatureCelsius;
        }
    }
}
