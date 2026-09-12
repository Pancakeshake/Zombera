using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateTrafficLights"/>.</summary>
    public sealed class GenerateTrafficLightsStage : WorldBuildStageBase
    {
        public GenerateTrafficLightsStage() : base(WorldBuildStageId.GenerateTrafficLights)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateTrafficLights());
    }
}
