using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.BuildParks"/>.</summary>
    public sealed class BuildParksStage : WorldBuildStageBase
    {
        public BuildParksStage() : base(WorldBuildStageId.BuildParks)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.BuildParks());
    }
}
