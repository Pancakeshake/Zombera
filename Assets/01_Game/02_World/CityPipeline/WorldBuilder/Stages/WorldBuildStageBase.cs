using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Shared stub stage base: reports progress 1.0 then completes.</summary>
    public abstract class WorldBuildStageBase : IWorldBuildStage
    {
        protected WorldBuildStageBase(WorldBuildStageId stageId)
        {
            Descriptor = WorldBuildStageRegistry.Describe(stageId);
        }

        public WorldBuildStageDescriptor Descriptor { get; }

        public virtual IEnumerator Execute(WorldBuildContext context)
        {
            context?.Cancellation?.ThrowIfRequested();
            throw new WorldBuildStageException(
                Descriptor.Id,
                $"Not Implemented: {Descriptor.DisplayName} ({Descriptor.Id}).");
        }
    }
}
