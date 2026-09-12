using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Publishes the current weather snapshot to Crest and other consumers.</summary>
    public sealed class BindWeatherConsumersStage : WorldBuildStageBase
    {
        public BindWeatherConsumersStage() : base(WorldBuildStageId.BindWeatherConsumers)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.2f, "Binding weather consumers");

            var source = context.WorldBuilder?.WeatherSource;
            if (source == null)
            {
                Debug.LogWarning(
                    "[BindWeatherConsumersStage] IWorldWeatherSource not bound.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no weather source)");
                yield break;
            }

            var snapshot = source.Current;
            var consumers = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var applied = 0;
            for (var i = 0; i < consumers.Length; i++)
            {
                if (consumers[i] is not IWorldWeatherConsumer consumer) continue;
                consumer.ApplyWeather(snapshot);
                applied++;
            }

            yield return null;
            context.Progress?.Report(Descriptor.Id, 1f, $"Bound {applied} weather consumer(s)");
        }
    }
}
