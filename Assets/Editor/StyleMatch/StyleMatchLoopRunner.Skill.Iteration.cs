#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zombera.Editor;
using Zombera.World.Roads;

namespace Zombera.Editor.StyleMatch
{
    public sealed partial class StyleMatchLoopRunner
    {
        public static bool TryRunSkillIteration(
            int iterationIndex,
            StyleMatchSkillIterationOptions options,
            out StyleMatchSkillIterationResult result,
            out string error)
        {
            result = new StyleMatchSkillIterationResult { IterationIndex = iterationIndex };
            error = null;
            options ??= StyleMatchSkillIterationOptions.PipelineRun();
            var lockAcquired = false;

            if (IsRunning)
            {
                error = "Batch loop is running; stop it first.";
                return false;
            }

            if (IsSkillBatchRunning)
            {
                error = "Skill batch is running; stop it first.";
                return false;
            }

            if (IsAgentMarathonRunning)
            {
                error = "Agent marathon is running; stop it first.";
                return false;
            }

            var capturePath = Path.Combine(OutputDir, $"iter-{iterationIndex:D3}.png");
            if (!options.AllowOverwriteExistingCapture && File.Exists(capturePath))
            {
                error = "Capture already exists: iter-" + iterationIndex.ToString("D3") +
                        ".png — use the next index.";
                LogPhase(iterationIndex, "slot-gate", error);
                return false;
            }

            if (!StyleMatchIterationLock.TryAcquire(
                    iterationIndex,
                    options.HypothesisNote,
                    out error,
                    options.BypassCompletionCooldown))
                return false;

            lockAcquired = true;

            try
            {
                Directory.CreateDirectory(OutputDir);
                LogPhase(iterationIndex, "start", options.HypothesisNote ?? "skill iteration");

                if (!TryValidateMutationGate(iterationIndex, options, out error))
                    return false;

                if (!TryPhaseWaitForCompile(iterationIndex, out error))
                    return false;

                if (!options.SkipClearConsole)
                    TryPhaseClearConsole(iterationIndex);

            if (!TryPhaseOpenScene(iterationIndex, out error))
                return false;

            if (options.CaptureOnly && !StyleMatchPerfCampaign.IsSceneReadyForCapture(out var readyDetail))
            {
                error = "Capture-only blocked: " + readyDetail;
                LogPhase(iterationIndex, "capture-ready", error);
                return false;
            }

            var runner = new StyleMatchLoopRunner();
                if (!options.CaptureOnly)
                {
                    if (!TryPhaseRunPipeline(iterationIndex, options, result, out error))
                        return false;

                    result.PipelineRan = true;
                    TryPhasePerf(iterationIndex, options, runner, result);
                }

                if (!options.SkipEnviroLighting)
                    TryPhaseEnviroLighting(iterationIndex);

                if (!TryPhaseCapture(iterationIndex, runner, result, out error))
                    return false;

                if (options.ValidateCapture && !TryPhaseValidateCapture(iterationIndex, options, result, out error))
                    return false;

                TryPhaseCompareReference(iterationIndex, result);

                result.ConsoleErrorCount = GetConsoleErrorCount();
                if (options.FailOnConsoleErrors && result.ConsoleErrorCount > 0)
                {
                    error = "Console has " + result.ConsoleErrorCount + " error(s) after iteration.";
                    LogPhase(iterationIndex, "console-errors", error);
                    return false;
                }

                AppendSessionLog(iterationIndex, BuildSessionStep(options, result), "ok");
                var completeDetail = result.CapturePath + " (" + result.CaptureBytes + " bytes) compare=" +
                                     result.CompareReferenceMs + "ms sim=" +
                                     result.CompareSimilarityScore.ToString("F3");
                LogPhase(iterationIndex, "complete", completeDetail);
                StyleMatchIterationLock.Release(iterationIndex, true, "compare=" + result.CompareReferenceMs +
                    "ms sim=" + result.CompareSimilarityScore.ToString("F3"));
                lockAcquired = false;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    StyleMatchIterationLock.Release(iterationIndex, false, error ?? "aborted");
            }
        }

        private static bool TryValidateMutationGate(
            int iter,
            StyleMatchSkillIterationOptions options,
            out string error)
        {
            error = null;
            if (!options.RequireMutation)
                return true;

            if (!options.MutationConfirmed)
            {
                error = "No mutation applied. Each iteration must change something before testing " +
                        "(set MutationConfirmed after apply, or use PipelineRunAfterChange).";
                LogPhase(iter, "mutation-gate", error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.HypothesisNote))
            {
                error = "HypothesisNote is required — describe what changed this iteration.";
                LogPhase(iter, "mutation-gate", error);
                return false;
            }

            LogPhase(iter, "mutation-gate", "ok: " + options.HypothesisNote);
            return true;
        }

        private static bool TryPhaseWaitForCompile(int iter, out string error)
        {
            error = null;
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                LogPhase(iter, "compile-ready", "ok");
                return true;
            }

            error = "Unity is still compiling; wait and retry.";
            LogPhase(iter, "compile-ready", error);
            return false;
        }

        private static void TryPhaseClearConsole(int iter)
        {
            try
            {
                var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)
                    ?.Invoke(null, null);
                LogPhase(iter, "clear-console", "ok");
            }
            catch (Exception ex)
            {
                LogPhase(iter, "clear-console", "skipped: " + ex.Message);
            }
        }

        private static bool TryPhaseOpenScene(int iter, out string error)
        {
            if (!IsWorldGenerationSceneActive())
            {
                if (!OpenWorldGenerationScene())
                {
                    error = "Failed to open World Generation scene (see Assets/00_Scenes/02_System Dev Scenes/World Generation.unity).";
                    LogPhase(iter, "open-scene", error);
                    return false;
                }
            }
            else
            {
                LogPhase(iter, "open-scene", "already active");
            }

            var rigOk = StyleMatchCaptureRig.EnsureSceneRig();
            LogPhase(iter, "open-scene", rigOk ? "ok" : "ok (camera rig pending terrain)");
            error = null;
            return true;
        }

        private static bool IsWorldGenerationSceneActive()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.IsValid() &&
                   string.Equals(scene.path, WorldGenerationScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryPhaseRunPipeline(
            int iter,
            StyleMatchSkillIterationOptions options,
            StyleMatchSkillIterationResult result,
            out string error)
        {
            error = null;
            var start = options.ResolvePipelineStartStage();
            var end = options.PipelineEndStage;
            result.PipelineStartStage = start;
            result.PipelineEndStage = end;
            result.UsedFastIteration = options.UseFastIteration;

            var builder = UnityEngine.Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(
                UnityEngine.FindObjectsInactive.Include);
            if (builder != null)
                start = WorldBuilderHubPipelineRunner.ResolvePartialPipelineStart(start, builder);

            result.PipelineStartStage = start;

            if (!WorldBuilderHubPipelineRunner.TryValidateStyleMatchStageRange(start, end, out error))
            {
                LogPhase(iter, "pipeline", error ?? "invalid stage range");
                return false;
            }

            if (WorldBuilderHubPipelineRunner.TryRunStageRangeSync(
                    start,
                    end,
                    options.UseFastIteration,
                    out error))
            {
                var mode = options.UseFastIteration ? "fast" : "full";
                if (WorldBuilderHubPerfReportWriter.TryReadLatest(out var report) && report != null)
                    result.TotalPipelineMs = report.totalMs;

                LogPhase(iter, "pipeline", start + ".." + end + " (" + mode + ") " + result.TotalPipelineMs + "ms");
                return true;
            }

            LogPhase(iter, "pipeline", error ?? "failed");
            return false;
        }

        private static void TryPhaseEnviroLighting(int iter)
        {
            var ok = StyleMatchEnviroSetup.EnsureInActiveScene() &&
                     StyleMatchEnviroSetup.SetClearMidday();
            LogPhase(iter, "enviro-lighting", ok ? "ok" : "partial");
        }

        private static bool TryPhaseCapture(
            int iter,
            StyleMatchLoopRunner runner,
            StyleMatchSkillIterationResult result,
            out string error)
        {
            error = null;
            if (!runner.TryCaptureIterationScreenshot(iter, out error))
            {
                LogPhase(iter, "capture", error ?? "failed");
                return false;
            }

            result.CapturePath = Path.Combine(OutputDir, $"iter-{iter:D3}.png");
            result.CaptureBytes = File.Exists(result.CapturePath) ? new FileInfo(result.CapturePath).Length : 0;
            LogPhase(iter, "capture", result.CapturePath);
            return true;
        }

        private static bool TryPhaseValidateCapture(
            int iter,
            StyleMatchSkillIterationOptions options,
            StyleMatchSkillIterationResult result,
            out string error)
        {
            error = null;
            if (!File.Exists(result.CapturePath))
            {
                error = "Capture file missing: " + result.CapturePath;
                LogPhase(iter, "validate-capture", error);
                return false;
            }

            if (result.CaptureBytes < options.MinCaptureBytes)
            {
                error = "Capture too small (" + result.CaptureBytes + " < " + options.MinCaptureBytes +
                        "); likely wrong camera framing or empty scene.";
                LogPhase(iter, "validate-capture", error);
                return false;
            }

            LogPhase(iter, "validate-capture", "ok");
            return true;
        }

        private static void TryPhasePerf(
            int iter,
            StyleMatchSkillIterationOptions options,
            StyleMatchLoopRunner runner,
            StyleMatchSkillIterationResult result)
        {
            if (!options.CheckPerfRegression && !options.SavePerfBaselineIfMissing)
                return;

            runner.LoadPerfBaseline();
            if (!WorldBuilderHubPerfReportWriter.TryReadLatest(out var report))
            {
                LogPhase(iter, "perf", "no report");
                return;
            }

            if (options.SavePerfBaselineIfMissing && runner._perfBaseline == null)
                runner.SavePerfBaseline(report);

            if (!options.CheckPerfRegression)
            {
                LogPhase(iter, "perf", "saved-baseline-only");
                return;
            }

            result.PerfRegression = runner.EvaluatePerfGate(report);
            result.PerfRegressionDetail = runner._lastPerfRegression;
            runner.LogPerfRow(iter, result.PerfRegression ? "perf" : "style", report);
            LogPhase(iter, "perf", result.PerfRegression ? "regression: " + result.PerfRegressionDetail : "ok");
        }

        private static void TryPhaseCompareReference(int iter, StyleMatchSkillIterationResult result)
        {
            if (string.IsNullOrEmpty(result.CapturePath) ||
                !StyleMatchReferenceCompare.TryCompare(
                    result.CapturePath,
                    StyleMatchReferenceCompare.ResolveDefaultReferencePath(),
                    out var compare))
            {
                LogPhase(iter, "compare-reference", "skipped");
                return;
            }

            result.CompareReferenceMs = compare.CompareMs;
            result.CompareSimilarityScore = compare.SimilarityScore;
            result.CompareMeanAbsoluteError = compare.MeanAbsoluteError;
            LogPhase(
                iter,
                "compare-reference",
                compare.CompareMs + "ms mae=" + compare.MeanAbsoluteError.ToString("F1") +
                " sim=" + compare.SimilarityScore.ToString("F3"));
        }

        private static string BuildSessionStep(
            StyleMatchSkillIterationOptions options,
            StyleMatchSkillIterationResult result)
        {
            var change = string.IsNullOrWhiteSpace(options.HypothesisNote)
                ? "(no change recorded)"
                : options.HypothesisNote.Trim();
            var test = options.CaptureOnly
                ? "capture-only"
                : BuildPipelineTestLabel(options, result);
            var compare = result.CompareReferenceMs > 0
                ? " | compare=" + result.CompareReferenceMs + "ms sim=" +
                  result.CompareSimilarityScore.ToString("F3")
                : string.Empty;
            var perf = result.TotalPipelineMs > 0 ? " | pipelineMs=" + result.TotalPipelineMs : string.Empty;
            return "change: " + change + " | test: " + test + perf + compare;
        }

        private static string BuildPipelineTestLabel(
            StyleMatchSkillIterationOptions options,
            StyleMatchSkillIterationResult result)
        {
            var range = result.PipelineStartStage + ".." + result.PipelineEndStage;
            var mode = result.UsedFastIteration ? "fast" : "full";
            var label = "pipeline(" + range + "," + mode + ")+capture";
            if (options.CheckPerfRegression)
                label += "+perf";
            return label;
        }

        private static string BuildSessionStep(StyleMatchSkillIterationOptions options)
        {
            var change = string.IsNullOrWhiteSpace(options.HypothesisNote)
                ? "(no change recorded)"
                : options.HypothesisNote.Trim();
            if (options.CaptureOnly)
                return "change: " + change + " | test: capture-only";

            var start = options.ResolvePipelineStartStage();
            var mode = options.UseFastIteration ? "fast" : "full";
            var test = "pipeline(" + start + ".." + options.PipelineEndStage + "," + mode + ")+capture";
            if (options.CheckPerfRegression)
                test += "+perf";
            return "change: " + change + " | test: " + test;
        }

        private static void LogPhase(int iter, string phase, string detail) =>
            Debug.Log("[StyleMatch] PHASE iter=" + iter + " " + phase + ": " + detail);

        private static int GetConsoleErrorCount()
        {
            try
            {
                var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                var method = logEntries?.GetMethod(
                    "GetCountsByType",
                    BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                    return 0;

                var args = new object[] { 0, 0, 0 };
                method.Invoke(null, args);
                return (int)args[0];
            }
            catch
            {
                return 0;
            }
        }
    }
}
#endif
