using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateDistrictFences"/>.</summary>
    public sealed class GenerateFencesStage : WorldBuildStageBase
    {
        public GenerateFencesStage() : base(WorldBuildStageId.GenerateFences)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateDistrictFences());
    }
}
