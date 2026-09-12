using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateDistrictLots"/>.</summary>
    public sealed class GenerateDistrictLotsStage : WorldBuildStageBase
    {
        public GenerateDistrictLotsStage() : base(WorldBuildStageId.GenerateDistrictLots)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            var routine = CityBuilderStageUtility.RunSync(
                context,
                Descriptor.Id,
                b =>
                {
                    b.GenerateDistrictLots();
                    LotStateGenerationProjection.ReplaceLots(context, b);
                });
            while (routine.MoveNext())
                yield return routine.Current;
        }
    }
}
