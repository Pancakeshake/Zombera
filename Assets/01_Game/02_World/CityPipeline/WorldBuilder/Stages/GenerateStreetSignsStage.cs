using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateStreetSigns"/>.</summary>
    public sealed class GenerateStreetSignsStage : WorldBuildStageBase
    {
        public GenerateStreetSignsStage() : base(WorldBuildStageId.GenerateStreetSigns)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateStreetSigns());
    }
}
