using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    /// Ensures required World Builder stack components and bindings exist before profile validation.
    /// </summary>
    public sealed class EnsureWorldBuilderStackStage : WorldBuildStageBase
    {
        public EnsureWorldBuilderStackStage() : base(WorldBuildStageId.EnsureWorldBuilderStack)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Ensuring World Builder stack");

            var service = context.WorldBuilder;
            if (service == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldBuilderService is required.");

            if (!WorldBuilderStackBootstrap.IsComplete(service))
                WorldBuilderStackBootstrap.Ensure(service, context.CityBuilder, context.Profile);
            else
                service.BindPipelineReferences(context.Profile);

            context.Progress?.Report(Descriptor.Id, 1f, "World Builder stack ready");
            yield break;
        }
    }
}
