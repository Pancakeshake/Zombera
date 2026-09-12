using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Plans and publishes wilderness POIs through <see cref="IWorldPoiSink"/>.</summary>
    public sealed class PlaceWildernessPoisStage : WorldBuildStageBase
    {
        public PlaceWildernessPoisStage() : base(WorldBuildStageId.PlaceWildernessPois)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Placing wilderness POIs");

            if (context.Profile?.Pois == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldPoiProfile is required.");

            var terrain = context.WorldBuilder?.TerrainQuery;
            if (terrain == null)
            {
                Debug.LogWarning(
                    "[PlaceWildernessPoisStage] Terrain query unavailable; skipping POIs.",
                    context.WorldBuilder);
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (no terrain query)");
                yield break;
            }

            var poiSeed = WorldSubsystemSeeds.Derive(
                context.Session.Seed, context.Session.ProfileVersion, WorldSubsystemSeeds.Pois);
            var planner = new WorldPoiPlanner();
            var records = planner.Plan(
                context.Session,
                context.Profile,
                terrain,
                new DeterministicRng(poiSeed),
                context.Artifacts?.Sites);
            yield return null;

            PublishPois(context, records);
            var sink = context.WorldBuilder?.PoiSink;
            if (sink == null)
            {
                Debug.LogWarning(
                    "[PlaceWildernessPoisStage] IWorldPoiSink not bound; planned " + records.Count + " POIs.",
                    context.WorldBuilder);
            }
            else
            {
                sink.Clear(context.Scope);
                sink.Publish(records);
            }

            context.Progress?.Report(Descriptor.Id, 1f, $"POIs: {records.Count}");
        }

        private void PublishPois(
            WorldBuildContext context,
            System.Collections.Generic.IReadOnlyList<WorldPoiRecord> records)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            stage.ReplacePois(PoiStateGenerationProjection.CreatePois(context.Session, records));
        }
    }
}
