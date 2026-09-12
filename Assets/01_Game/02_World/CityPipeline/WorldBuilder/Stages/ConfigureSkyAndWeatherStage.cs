using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Configures Enviro sky/weather from the world environment profile.</summary>
    public sealed class ConfigureSkyAndWeatherStage : WorldBuildStageBase
    {
        public ConfigureSkyAndWeatherStage() : base(WorldBuildStageId.ConfigureSkyAndWeather)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.2f, "Configuring sky and weather");

            var profile = context.Profile?.Environment;
            if (profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldEnvironmentProfile is required.");

            var backend = context.WorldBuilder?.EnvironmentBackend;
            if (backend == null)
            {
                Debug.LogWarning(
                    "[ConfigureSkyAndWeatherStage] IWorldEnvironmentBackend not bound; skipping Enviro configure.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no environment backend)");
                yield break;
            }

            if (backend is IWorldEnvironmentProfileBinder binder)
                binder.BindProfile(profile);

            if (!backend.Validate(profile, out var error))
            {
                Debug.LogWarning(
                    "[ConfigureSkyAndWeatherStage] Environment validate: " + error,
                    context.WorldBuilder);
            }

            var envContext = new WorldEnvironmentContext
            {
                Session = context.Session,
                Profile = profile,
                Rng = new DeterministicRng(
                    WorldSubsystemSeeds.Derive(context.Session.Seed, context.Session.ProfileVersion,
                        WorldSubsystemSeeds.Weather))
            };
            backend.Configure(envContext);
            context.WorldBuilder?.OceanWaterRenderer?.RebindPrimaryLight();

            var state = backend.Capture();
            Debug.Log(
                $"[ConfigureSkyAndWeatherStage] Ready — weather={state.ActiveWeatherId}, " +
                $"tod={state.TimeOfDayHours:0.##}, season={state.Season}, " +
                $"fog={profile.EnableFog}, volumetricClouds={profile.EnableVolumetricClouds}, " +
                $"quality={profile.EnviroQualityName}",
                context.WorldBuilder);

            yield return null;
            context.Progress?.Report(Descriptor.Id, 1f, "Sky and weather configured");
        }
    }
}
