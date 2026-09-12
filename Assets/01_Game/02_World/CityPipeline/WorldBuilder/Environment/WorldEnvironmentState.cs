using System;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldEnvironmentState
    {
        public string ActiveWeatherId = "Cloudy";
        public float Transition01;
        public float HoursUntilNextChange = 5f;
        public float WindStrength01;
        public float Precipitation01;
        public float TemperatureCelsius = 18f;
        public float TimeOfDayHours = 10f;
        public WorldEnvironmentSeason Season = WorldEnvironmentSeason.Autumn;
    }
}
