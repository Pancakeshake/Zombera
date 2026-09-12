using System;
using System.Collections;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Shared helpers for stages that wrap CityPrefabRoadNetworkBuilder methods.</summary>
    internal static class CityBuilderStageUtility
    {
        public static CityPrefabRoadNetworkBuilder RequireBuilder(WorldBuildContext context, WorldBuildStageId stageId)
        {
            if (context?.CityBuilder != null)
                return context.CityBuilder;

            throw new WorldBuildStageException(
                stageId,
                "CityPrefabRoadNetworkBuilder is required for this stage.");
        }

        public static IEnumerator RunSync(
            WorldBuildContext context,
            WorldBuildStageId stageId,
            Action<CityPrefabRoadNetworkBuilder> action)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(stageId, 0.1f, "Starting");
            var builder = RequireBuilder(context, stageId);
            action(builder);
            context.Progress?.Report(stageId, 1f, "Complete");
            yield break;
        }
    }
}
