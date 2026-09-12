#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    public static partial class WorldBuilderHubPipelineRunner
    {
        private static CityPipelineRunnerWindow _asyncWindow;
        private static IEnumerator _asyncRoutine;
        private static string _asyncError;

        public static bool IsPipelineRunning => _asyncRoutine != null;

        [MenuItem("Tools/World/Run Hub Pipeline Up To Paint Natural Surfaces (Async)")]
        public static void MenuRunUpToPaintNaturalSurfacesAsync()
        {
            if (!StartUpToPaintNaturalSurfacesAsync(out var error))
                Debug.LogError("[WorldBuilderHub] " + error);
        }

        public static bool StartUpToPaintNaturalSurfacesAsync(out string error) =>
            StartUpToStageAsync(WorldBuildStageId.PaintNaturalSurfaces, out error, useFastIteration: true);

        /// <summary>
        ///     Style-match pipeline end: wilderness + Enviro, no roads/city/infrastructure.
        /// </summary>
        public const WorldBuildStageId StyleMatchPipelineEndStage = WorldBuildStageId.BindWeatherConsumers;

        [MenuItem("Tools/World/Run Hub Pipeline Up To Style Match End (Async)")]
        public static void MenuRunUpToStyleMatchEndAsync()
        {
            if (!StartUpToStyleMatchEndAsync(out var error))
                Debug.LogError("[WorldBuilderHub] " + error);
        }

        public static bool StartUpToStyleMatchEndAsync(out string error) =>
            StartUpToStageAsync(StyleMatchPipelineEndStage, out error, useFastIteration: true);

        [MenuItem("Tools/World/Run Hub Pipeline Up To Road Meshes (Async)")]
        public static void MenuRunUpToRoadMeshesAsync()
        {
            if (!StartUpToRoadMeshesAsync(out var error))
                Debug.LogError("[WorldBuilderHub] " + error);
        }

        /// <summary>
        /// Cold full-fidelity async baseline through procedural road meshes (no FastIteration).
        /// </summary>
        public static bool StartUpToRoadMeshesAsync(out string error) =>
            StartUpToStageAsync(
                WorldBuildStageId.BuildEasyRoadsMeshes,
                out error,
                useFastIteration: false);

        /// <summary>Async hub pipeline through any registry stage.</summary>
        public static bool StartUpToStageAsync(WorldBuildStageId lastStageInclusive, out string error) =>
            StartUpToStageAsync(lastStageInclusive, out error, useFastIteration: true);

        /// <summary>Async hub pipeline through any registry stage.</summary>
        public static bool StartUpToStageAsync(
            WorldBuildStageId lastStageInclusive,
            out string error,
            bool useFastIteration)
        {
            error = null;
            if (_asyncRoutine != null)
            {
                error = "Hub pipeline is already running.";
                return false;
            }

            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder not found in the open scene(s).";
                return false;
            }

            _asyncWindow = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            _asyncWindow.SetBuilder(builder);
            _asyncWindow.ApplyVerticalSliceTestConfiguration();
            if (useFastIteration)
                _asyncWindow.EnableFastIterationMode();
            else
                _asyncWindow.DisableFastIterationMode();

            WorldBuilderHubPerfReportWriter.BeginRun();
            Zombera.World.CityPipeline.WorldBuilder.LandformHeightmapBaker.ResetBakeCounters();
            _asyncRoutine = _asyncWindow.EnumeratePipelineUpTo(lastStageInclusive);
            _asyncError = null;
            EditorApplication.update += PumpAsyncPipeline;
            Debug.Log(
                "[WorldBuilderHub] Async pipeline started (Reset → " + lastStageInclusive +
                ", fastIteration=" + useFastIteration + ").");
            return true;
        }

        private static void PumpAsyncPipeline()
        {
            if (_asyncRoutine == null)
                return;

            try
            {
                if (_asyncRoutine.MoveNext())
                    return;
            }
            catch (System.Exception ex)
            {
                _asyncError = ex.Message;
            }

            FinishAsyncPipeline(_asyncError == null, _asyncError);
        }

        private static void FinishAsyncPipeline(bool success, string error)
        {
            EditorApplication.update -= PumpAsyncPipeline;
            _asyncRoutine = null;

            if (_asyncWindow != null)
            {
                if (!success)
                    Debug.LogError("[WorldBuilderHub] Async pipeline failed: " + (error ?? "unknown error"));

                Object.DestroyImmediate(_asyncWindow);
                _asyncWindow = null;
            }

            if (success)
            {
                Debug.Log(
                    "[WorldBuilderHub] Async pipeline finished. Report: " +
                    WorldBuilderHubPerfReportWriter.LatestReportPath +
                    " setHeights=" +
                    Zombera.World.CityPipeline.WorldBuilder.LandformHeightmapBaker.SetHeightsCallCount +
                    " tilesBaked=" +
                    Zombera.World.CityPipeline.WorldBuilder.LandformHeightmapBaker.TilesBakedCount);
            }
        }
    }
}
#endif
