#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     Programmatic entry points for the World Builder Development Hub pipeline.
    ///     Used by Unity MCP script-execute, automated tests, and menu shortcuts.
    /// </summary>
    public static partial class WorldBuilderHubPipelineRunner
    {
        /// <summary>Default hub world seed for interior landform variation.</summary>
        public const int ThreeOceansOneMountainReferenceSeed = 16;

        [MenuItem("Tools/World/Run Hub Pipeline Up To Road Meshes")]
        public static void MenuRunUpToRoadMeshes()
        {
            if (!TryRunUpToRoadMeshes(
                    Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include),
                    out var error))
            {
                Debug.LogError("[WorldBuilderHubPipelineRunner] " + error);
                return;
            }

            Debug.Log("[WorldBuilderHubPipelineRunner] Pipeline finished through procedural road meshes.");
        }

        [MenuItem("Tools/World/Run Hub Pipeline Up To Easy Roads", false, 1000)]
        public static void MenuRunUpToEasyRoadsDeprecated() => MenuRunUpToRoadMeshes();

        [MenuItem("Tools/World/Run Hub Pipeline Up To Paint Natural Surfaces")]
        public static void MenuRunUpToPaintNaturalSurfaces()
        {
            if (!TryRunUpToPaintNaturalSurfaces(
                    Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include),
                    out var error))
            {
                Debug.LogError("[WorldBuilderHubPipelineRunner] " + error);
                return;
            }

            Debug.Log("[WorldBuilderHubPipelineRunner] Pipeline finished through Paint Natural Surfaces.");
        }

        [MenuItem("Tools/World/Run Hub Pipeline Reset To Carve (Acceptance)")]
        public static void MenuRunResetToCarveAcceptance()
        {
            if (!TryRunAcceptanceRange(
                    WorldBuildStageId.CarveWaterFeatures,
                    out var error))
            {
                Debug.LogError("[WorldBuilderHubPipelineRunner] " + error);
                return;
            }

            Debug.Log("[WorldBuilderHubPipelineRunner] Acceptance pipeline finished through Carve Water Features.");
        }

        [MenuItem("Tools/World/Run Hub Pipeline Reset To Build Ocean Surfaces (Acceptance)")]
        public static void MenuRunResetToOceanAcceptance()
        {
            if (!TryRunAcceptanceRange(
                    WorldBuildStageId.BuildOceanSurfaces,
                    out var error))
            {
                Debug.LogError("[WorldBuilderHubPipelineRunner] " + error);
                return;
            }

            Debug.Log("[WorldBuilderHubPipelineRunner] Acceptance pipeline finished through Build Ocean Surfaces.");
        }

        /// <summary>
        ///     Reset-to-stage acceptance run. Deliberately does NOT go through
        ///     <see cref="TryValidateStyleMatchStageRange"/>: acceptance targets such as
        ///     CarveWaterFeatures and BuildOceanSurfaces sit past the style-match end cap,
        ///     so that validator rejected them and these menus could never run.
        /// </summary>
        public static bool TryRunAcceptanceRange(
            WorldBuildStageId lastStageInclusive,
            out string error,
            float timeoutSeconds = 900f)
        {
            error = null;
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
                window.ApplyRoadsInfrastructureAcceptanceConfiguration();
                if (!window.TryRunPipelineStageRangeSynchronously(
                        WorldBuildStageId.ResetGeneratedWorld,
                        lastStageInclusive,
                        out error,
                        timeoutSeconds))
                {
                    error = (error ?? "Pipeline failed.") + "\n" + window.GetLastPipelineStageLog();
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        public static bool TryRunUpToPaintNaturalSurfaces(
            CityPrefabRoadNetworkBuilder builder,
            out string error,
            float timeoutSeconds = 900f)
        {
            error = null;
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder not found in the open scene(s).";
                return false;
            }

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                window.ApplyVerticalSliceTestConfiguration();
                window.EnableFastIterationMode();
                if (!window.TryRunPipelineUpToStageSynchronously(
                        WorldBuildStageId.PaintNaturalSurfaces,
                        out error,
                        timeoutSeconds))
                {
                    error = (error ?? "Pipeline failed.") + "\n" + window.GetLastPipelineStageLog();
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        ///     Sync hub pipeline for a registry stage range. Used by style-match skill iterations.
        /// </summary>
        public static bool TryRunStageRangeSync(
            WorldBuildStageId firstStageInclusive,
            WorldBuildStageId lastStageInclusive,
            bool useFastIteration,
            out string error,
            float timeoutSeconds = 900f)
        {
            error = null;
            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder not found in the open scene(s).";
                return false;
            }

            if (!TryValidateStyleMatchStageRange(firstStageInclusive, lastStageInclusive, out error))
                return false;

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                window.ApplyVerticalSliceTestConfiguration();
                firstStageInclusive = StyleMatchPartialPipelineBootstrap.Resolve(
                    firstStageInclusive,
                    window.TryGetWorldBuilderService());

                if (useFastIteration)
                    window.EnableFastIterationMode();
                else
                    window.DisableFastIterationMode();

                if (!window.TryRunPipelineStageRangeSynchronously(
                        firstStageInclusive,
                        lastStageInclusive,
                        out error,
                        timeoutSeconds))
                {
                    error = (error ?? "Pipeline failed.") + "\n" + window.GetLastPipelineStageLog();
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        /// <summary>Style-match cap: no city/roads stages beyond <see cref="StyleMatchPipelineEndStage"/>.</summary>
        public static bool TryValidateStyleMatchStageRange(
            WorldBuildStageId firstStageInclusive,
            WorldBuildStageId lastStageInclusive,
            out string error)
        {
            error = null;
            if (!WorldBuildStageOrder.IsBeforeOrEqual(firstStageInclusive, lastStageInclusive))
            {
                error = "Invalid stage range: " + firstStageInclusive + " must precede " + lastStageInclusive +
                        " in the registry.";
                return false;
            }

            if (!WorldBuildStageOrder.IsBeforeOrEqual(lastStageInclusive, StyleMatchPipelineEndStage))
            {
                error = "Style-match pipeline cannot run past " + StyleMatchPipelineEndStage +
                        " (requested end: " + lastStageInclusive + ").";
                return false;
            }

            // No separate "first stage" check: first <= last (above) and last <= cap mean
            // first <= cap already, so such a check could never fire.

            return true;
        }

        public static WorldBuildStageId ResolvePartialPipelineStart(
            WorldBuildStageId requested,
            CityPrefabRoadNetworkBuilder builder) =>
            StyleMatchPartialPipelineBootstrap.Resolve(requested, builder);

        public static bool TryGetSceneReadiness(CityPrefabRoadNetworkBuilder builder, out string detail)
        {
            detail = null;
            if (builder == null)
            {
                detail = "builder missing";
                return false;
            }

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                return StyleMatchPartialPipelineBootstrap.IsSceneReadyForStyleMatchCapture(
                    window.TryGetWorldBuilderService(),
                    out detail);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// Cold hub run through procedural road meshes (seed 16 / Medium).
        /// Pass <paramref name="useFastIteration"/> true for the Fast Iteration road-stage path.
        /// </summary>
        public static bool TryRunUpToRoadMeshes(
            CityPrefabRoadNetworkBuilder builder,
            out string error,
            float timeoutSeconds = 900f,
            bool useFastIteration = false)
        {
            error = null;
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder not found in the open scene(s).";
                return false;
            }

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                window.ApplyVerticalSliceTestConfiguration();
                if (useFastIteration)
                    window.EnableFastIterationMode();
                else
                    window.DisableFastIterationMode();
                if (!window.TryRunPipelineUpToStageSynchronously(
                        WorldBuildStageId.BuildEasyRoadsMeshes,
                        out error,
                        timeoutSeconds))
                {
                    error = (error ?? "Pipeline failed.") + "\n" + window.GetLastPipelineStageLog();
                    return false;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        // Hydrology Large-tier APIs live in WorldBuilderHubPipelineRunner.Hydrology.cs
    }
}
#endif
