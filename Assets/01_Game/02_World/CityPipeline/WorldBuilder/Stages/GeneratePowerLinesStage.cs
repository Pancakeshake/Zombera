using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GeneratePowerLines"/>.</summary>
    public sealed class GeneratePowerLinesStage : WorldBuildStageBase
    {
        public GeneratePowerLinesStage() : base(WorldBuildStageId.GeneratePowerLines)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GeneratePowerLines());
    }
}
