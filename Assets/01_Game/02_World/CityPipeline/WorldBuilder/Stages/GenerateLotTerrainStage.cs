using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    ///     Lot/district alphamap paint (City section, after PlaceBuildings).
    ///     Skips only if a legacy run set <see cref="WorldBuildArtifacts.LotTerrainComposedDuringPaint"/>
    ///     (Paint Natural Surfaces is natural-only and leaves the flag false).
    /// </summary>
    public sealed class GenerateLotTerrainStage : WorldBuildStageBase
    {
        public GenerateLotTerrainStage() : base(WorldBuildStageId.GenerateLotTerrain)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts != null && context.Artifacts.LotTerrainComposedDuringPaint)
            {
                Debug.Log(
                    "[GenerateLotTerrainStage] Skipped alphamap overwrite — already composed during PaintNaturalSurfaces.");
                context.Progress?.Report(Descriptor.Id, 1f, "Skipped (composed during paint)");
                yield break;
            }

            var routine = CityBuilderStageUtility.RunSync(
                context, Descriptor.Id, b => b.GenerateDistrictLotTerrain());
            while (routine.MoveNext())
                yield return routine.Current;
        }
    }
}
