using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.PlaceTrees"/>.</summary>
    public sealed class PlaceCityTreesStage : WorldBuildStageBase
    {
        public PlaceCityTreesStage() : base(WorldBuildStageId.PlaceCityTrees)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.PlaceTrees());
    }
}
