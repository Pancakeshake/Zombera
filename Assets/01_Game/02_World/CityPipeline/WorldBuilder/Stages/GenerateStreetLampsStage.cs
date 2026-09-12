using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateStreetLamps"/>.</summary>
    public sealed class GenerateStreetLampsStage : WorldBuildStageBase
    {
        public GenerateStreetLampsStage() : base(WorldBuildStageId.GenerateStreetLamps)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateStreetLamps());
    }
}
