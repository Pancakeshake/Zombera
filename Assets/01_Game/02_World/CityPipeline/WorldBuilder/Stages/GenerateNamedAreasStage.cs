using System.Collections;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Wraps <see cref="Zombera.World.Roads.CityPrefabRoadNetworkBuilder.GenerateNamedAreas"/>.</summary>
    public sealed class GenerateNamedAreasStage : WorldBuildStageBase
    {
        public GenerateNamedAreasStage() : base(WorldBuildStageId.GenerateNamedAreas)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            var routine = CityBuilderStageUtility.RunSync(
                context,
                Descriptor.Id,
                b =>
                {
                    b.AlignLayoutToWorldSession(
                        context.Session,
                        context.Artifacts?.Sites,
                        context.WorldBuilder?.TileCatalog);
                    b.GenerateNamedAreas();
                    DistrictStateGenerationProjection.ReplaceDistricts(context, b);
                });
            while (routine.MoveNext())
                yield return routine.Current;
        }
    }
}
