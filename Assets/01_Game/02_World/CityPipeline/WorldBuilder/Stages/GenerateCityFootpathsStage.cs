using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateFootpaths"/>.</summary>
    public sealed class GenerateCityFootpathsStage : WorldBuildStageBase
    {
        public GenerateCityFootpathsStage() : base(WorldBuildStageId.GenerateCityFootpaths)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            var routine = CityBuilderStageUtility.RunSync(
                context,
                Descriptor.Id,
                b => b.GenerateFootpaths());
            while (routine.MoveNext())
                yield return routine.Current;
        }
    }
}
