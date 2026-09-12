using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Seeds or restores deterministic weather state for the session.</summary>
    public sealed class InitializeEnvironmentStateStage : WorldBuildStageBase
    {
        public InitializeEnvironmentStateStage() : base(WorldBuildStageId.InitializeEnvironmentState)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.2f, "Initializing environment state");

            var profile = context.Profile?.Environment;
            if (profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldEnvironmentProfile is required.");

            var weatherSeed = WorldSubsystemSeeds.Derive(
                context.Session.Seed, context.Session.ProfileVersion, WorldSubsystemSeeds.Weather);
            var rng = new DeterministicRng(weatherSeed);

            var weather = context.WorldBuilder?.WeatherSource as WorldWeatherDirector;
            var envContext = new WorldEnvironmentContext
            {
                Session = context.Session,
                Profile = profile,
                Rng = rng
            };

            if (weather != null)
                weather.Configure(envContext);
            else
                Debug.LogWarning(
                    "[InitializeEnvironmentStateStage] WorldWeatherDirector not bound.",
                    context.WorldBuilder);

            var backend = context.WorldBuilder?.EnvironmentBackend;
            backend?.Configure(envContext);
            context.WorldBuilder?.OceanWaterRenderer?.RebindPrimaryLight();

            yield return null;
            context.Progress?.Report(Descriptor.Id, 1f, "Environment state ready");
        }
    }
}
