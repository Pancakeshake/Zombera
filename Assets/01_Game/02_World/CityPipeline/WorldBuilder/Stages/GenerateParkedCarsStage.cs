using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateParkedCars"/>.</summary>
    public sealed class GenerateParkedCarsStage : WorldBuildStageBase
    {
        public GenerateParkedCarsStage() : base(WorldBuildStageId.GenerateParkedCars)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context) =>
            CityBuilderStageUtility.RunSync(context, Descriptor.Id, b => b.GenerateParkedCars());
    }
}
