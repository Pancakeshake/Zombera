#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    /// Large-tier hydrology / water verify APIs (no style-match stage cap).
    /// </summary>
    public static partial class WorldBuilderHubPipelineRunner
    {
        /// <summary>Hub screenshot campaign world seed for Large water auto-tune.</summary>
        public const int HydrologyLoopCampaignWorldSeed = 1;

        /// <summary>Hub screenshot Fixed Region Seed for Large water auto-tune.</summary>
        public const int HydrologyLoopCampaignFixedRegionSeed = 1589847769;

        /// <summary>
        /// Hub water verify: Reset → Build Water Surfaces on Large.
        /// Default seed remains the three-oceans fixture (16) for backward compatibility.
        /// </summary>
        public static bool TryRunHydrologyWaterVerifySync(out string error, float timeoutSeconds = 1800f) =>
            TryRunHydrologyWaterVerifySync(
                ThreeOceansOneMountainReferenceSeed,
                fixedRegionSeedOverride: null,
                out error,
                timeoutSeconds);

        /// <summary>
        /// Hub water verify: Reset → Build Water Surfaces on Large with explicit seeds.
        /// </summary>
        public static bool TryRunHydrologyWaterVerifySync(
            int worldSeed,
            int? fixedRegionSeedOverride,
            out string error,
            float timeoutSeconds = 1800f) =>
            TryRunHydrologyStageRangeSync(
                WorldBuildStageId.ResetGeneratedWorld,
                WorldBuildStageId.BuildWaterSurfaces,
                worldSeed,
                fixedRegionSeedOverride,
                out error,
                timeoutSeconds);

        /// <summary>
        /// Large hydrology stage range (no style-match BindWeatherConsumers cap).
        /// Use SolveHydrology→BuildWaterSurfaces for carve iters; BuildOceanSurfaces→BuildWaterSurfaces for Crest-only.
        /// </summary>
        public static bool TryRunHydrologyStageRangeSync(
            WorldBuildStageId firstStageInclusive,
            WorldBuildStageId lastStageInclusive,
            int worldSeed,
            int? fixedRegionSeedOverride,
            out string error,
            float timeoutSeconds = 1800f)
        {
            error = null;
            if (!WorldBuildStageOrder.IsBeforeOrEqual(firstStageInclusive, lastStageInclusive))
            {
                error = "Invalid hydrology stage range: " + firstStageInclusive + " must precede " +
                        lastStageInclusive + " in the registry.";
                return false;
            }

            if (!WorldBuildStageOrder.IsBeforeOrEqual(lastStageInclusive, WorldBuildStageId.BuildWaterSurfaces))
            {
                error = "Hydrology loop cannot run past BuildWaterSurfaces (requested end: " +
                        lastStageInclusive + ").";
                return false;
            }

            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder not found in the open scene(s).";
                return false;
            }

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                window.ApplyHydrologyLoopConfiguration(worldSeed, fixedRegionSeedOverride);
                window.DisableFastIterationMode();

                if (window.EditorMapTier != WorldMapSizeTier.Large)
                {
                    error = "Hydrology stage range refused: map tier must be Large, was " +
                            window.EditorMapTier;
                    return false;
                }

                var resolvedSeed = worldSeed == 0 ? 1 : worldSeed;
                if (window.EditorWorldSeed != resolvedSeed)
                {
                    error = "Hydrology stage range refused: expected seed " + resolvedSeed +
                            ", window has " + window.EditorWorldSeed;
                    return false;
                }

                Debug.Log(
                    "[WorldBuilderHub] Hydrology stage range (Large): seed=" +
                    window.EditorWorldSeed +
                    " fixedRegionSeed=" +
                    builder.FixedRegionSeedOverride +
                    " " +
                    firstStageInclusive +
                    " → " +
                    lastStageInclusive +
                    ".");

                return TryRunHydrologyStageRangeOrFail(
                    window,
                    firstStageInclusive,
                    lastStageInclusive,
                    timeoutSeconds,
                    out error);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static bool TryRunHydrologyStageRangeOrFail(
            CityPipelineRunnerWindow window,
            WorldBuildStageId first,
            WorldBuildStageId last,
            float timeoutSeconds,
            out string error)
        {
            if (window.TryRunPipelineStageRangeSynchronously(first, last, out error, timeoutSeconds))
                return true;

            error = (error ?? "Pipeline failed.") + "\n" + window.GetLastPipelineStageLog();
            return false;
        }
    }
}
#endif
