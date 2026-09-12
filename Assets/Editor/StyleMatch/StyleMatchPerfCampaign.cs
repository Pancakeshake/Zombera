#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Editor;
using Zombera.World.Roads;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Perf-fix-loop runner for style-match pipeline speed (ms, not visual sim).</summary>
    public static class StyleMatchPerfCampaign
    {
        public const string SessionPerfPath = "Library/StyleMatch/session-perf.md";

        public enum BenchmarkKind
        {
            FullFastReset,
            SurfacesOnlyFast,
            BiomesAndSurfacesFast,
            PaintOnlyFast,
        }

        [MenuItem("Tools/World/Style Match/Run Perf Baseline (3 benchmarks)")]
        public static void MenuRunPerfBaseline()
        {
            if (!RunAllBaselines(out var summary, out var error))
                Debug.LogError("[StyleMatch] Perf baseline failed: " + error);
            else
                Debug.Log("[StyleMatch] Perf baseline complete:\n" + summary);
        }

        public static bool RunAllBaselines(out string summary, out string error)
        {
            summary = null;
            error = null;
            var log = new StringBuilder();
            log.AppendLine("# Perf baseline " + System.DateTime.UtcNow.ToString("o"));
            log.AppendLine();
            log.AppendLine("| Benchmark | totalMs | lastStage |");
            log.AppendLine("|-----------|---------|-----------|");

            var kinds = new[]
            {
                BenchmarkKind.FullFastReset,
                BenchmarkKind.BiomesAndSurfacesFast,
                BenchmarkKind.SurfacesOnlyFast,
                BenchmarkKind.PaintOnlyFast,
            };

            for (var i = 0; i < kinds.Length; i++)
            {
                if (!RunBenchmark(kinds[i], out var totalMs, out error))
                    return false;

                WorldBuilderHubPerfReportWriter.TryReadLatest(out var report);
                log.AppendLine("| " + kinds[i] + " | " + totalMs + " | " + (report?.lastStage ?? "?") + " |");
            }

            summary = log.ToString();
            AppendSessionPerf(summary);
            return true;
        }

        public static bool RunBenchmark(BenchmarkKind kind, out long totalMs, out string error)
        {
            totalMs = 0;
            error = null;
            if (!EnsureWorldGenerationScene(out error))
                return false;

            ResolveStageRange(kind, out var start, out var end);

            if (!WorldBuilderHubPipelineRunner.TryRunStageRangeSync(start, end, useFastIteration: true, out error))
                return false;

            if (!WorldBuilderHubPerfReportWriter.TryReadLatest(out var report) || report == null)
            {
                error = "Perf report missing after benchmark " + kind;
                return false;
            }

            totalMs = report.totalMs;
            AppendSessionPerf(
                "- **" + kind + "**: " + totalMs + "ms (" + start + ".." + end + ", " +
                report.stageCount + " stages)");
            Debug.Log("[StyleMatch] PERF benchmark " + kind + " totalMs=" + totalMs);
            return true;
        }

        /// <summary>Single campaign iteration with explicit index (no FindLatest+1).</summary>
        public static bool RunCampaignIteration(
            int iterationIndex,
            StyleMatchSkillIterationOptions options,
            out StyleMatchSkillIterationResult result,
            out string error)
        {
            result = null;
            error = null;
            if (StyleMatchLoopRunner.IsSkillIterationLocked)
            {
                error = "Iteration locked — poll Library/StyleMatch/agent-iter.log";
                return false;
            }

            if (!EnsureWorldGenerationScene(out error))
                return false;

            return StyleMatchLoopRunner.TryRunSkillIteration(iterationIndex, options, out result, out error);
        }

        public static bool IsSceneReadyForCapture(out string detail)
        {
            detail = null;
            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                detail = "CityPrefabRoadNetworkBuilder missing";
                return false;
            }

            return WorldBuilderHubPipelineRunner.TryGetSceneReadiness(builder, out detail);
        }

        private static void ResolveStageRange(
            BenchmarkKind kind,
            out WorldBuildStageId start,
            out WorldBuildStageId end)
        {
            switch (kind)
            {
                case BenchmarkKind.SurfacesOnlyFast:
                    start = WorldBuildStageId.PaintNaturalSurfaces;
                    end = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage;
                    break;
                case BenchmarkKind.BiomesAndSurfacesFast:
                    start = WorldBuildStageId.ClassifyBiomesAndBuildability;
                    end = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage;
                    break;
                case BenchmarkKind.PaintOnlyFast:
                    start = WorldBuildStageId.PaintNaturalSurfaces;
                    end = WorldBuildStageId.PaintNaturalSurfaces;
                    break;
                default:
                    start = WorldBuildStageId.ResetGeneratedWorld;
                    end = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage;
                    break;
            }
        }

        private static bool EnsureWorldGenerationScene(out string error)
        {
            error = null;
            var scenePath = StyleMatchLoopRunner.WorldGenerationScenePath;
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!StyleMatchLoopRunner.OpenWorldGenerationSceneForSetup())
                {
                    error = "Failed to open World Generation scene.";
                    return false;
                }
            }

            return true;
        }

        public static void AppendSessionPerf(string line)
        {
            Directory.CreateDirectory(StyleMatchLoopRunner.OutputDir);
            if (!File.Exists(SessionPerfPath))
            {
                File.WriteAllText(SessionPerfPath,
                    "# Style match perf sprint\n\n| P# | Change | Before ms | After ms | Delta |\n" +
                    "|----|--------|-----------|----------|-------|\n");
            }

            File.AppendAllText(SessionPerfPath, line.TrimEnd() + "\n");
        }
    }
}
#endif
