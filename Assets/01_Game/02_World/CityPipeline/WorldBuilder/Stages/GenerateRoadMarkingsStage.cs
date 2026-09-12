using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateRoadDecals"/>.</summary>
    public sealed class GenerateRoadMarkingsStage : WorldBuildStageBase
    {
        public GenerateRoadMarkingsStage() : base(WorldBuildStageId.GenerateRoadMarkings)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateRoadDecals());
    }
}
