using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Places wilderness vegetation/rocks via <see cref="IWorldNaturePlacer"/>.</summary>
    public sealed class PlaceWildernessNatureStage : WorldBuildStageBase
    {
        public PlaceWildernessNatureStage() : base(WorldBuildStageId.PlaceWildernessNature)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Placing wilderness nature");

            if (context.Profile?.Nature == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldNatureProfile is required.");

            var placer = context.WorldBuilder?.NaturePlacer;
            if (placer == null)
            {
                Debug.LogWarning(
                    "[PlaceWildernessNatureStage] IWorldNaturePlacer not bound; skipping nature.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no nature placer)");
                yield break;
            }

            placer.Clear(context.Scope);
            var place = placer.Place(context);
            while (place.MoveNext())
            {
                context.Cancellation.ThrowIfRequested();
                yield return place.Current;
            }

            context.Progress?.Report(Descriptor.Id, 1f, "Wilderness nature complete");
        }
    }
}
