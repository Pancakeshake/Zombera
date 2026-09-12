using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    internal static class WorldStateStageCaptureUtility
    {
        public static bool TryGetActiveStage(
            WorldBuildContext context,
            WorldBuildStageId stageId,
            out WorldStateStageTransaction stage)
        {
            stage = context?.StateRecorder?.CurrentStage;
            if (context?.StateManager != null && context.StateManager.HasState && stage == null)
            {
                throw new WorldBuildStageException(
                    stageId,
                    "WorldState stage transaction is required when WorldState is active.");
            }

            return stage != null && context?.StateManager != null && context.StateManager.HasState;
        }
    }
}
