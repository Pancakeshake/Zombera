#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>
    /// Skill-mode helpers: one pipeline+capture per agent iteration (no blind mutation cycling).
    /// Session plan lives in <see cref="SessionPlanPath"/>; agent updates it each iteration.
    /// </summary>
    public sealed partial class StyleMatchLoopRunner
    {
        public const int DefaultSkillMaxIterations = 200;

        public static string SessionPlanPath => Path.Combine(OutputDir, "session.md");
        public static string SessionLogPath => Path.Combine(OutputDir, "session-log.md");

        [MenuItem("Tools/World/Style Match/Run Next Skill Iteration")]
        public static void MenuRunNextSkillIteration() =>
            MenuRunSkillIteration(StyleMatchSkillIterationOptions.PipelineRun());

        [MenuItem("Tools/World/Style Match/Run Next Skill Iteration (Capture Only)")]
        public static void MenuRunNextSkillIterationCaptureOnly() =>
            MenuRunSkillIteration(StyleMatchSkillIterationOptions.CaptureOnlyRun());

        [MenuItem("Tools/World/Style Match/Run Next Skill Iteration (With Perf Gate)")]
        public static void MenuRunNextSkillIterationWithPerf() =>
            MenuRunSkillIteration(StyleMatchSkillIterationOptions.WithPerfTracking());

        private static void MenuRunSkillIteration(StyleMatchSkillIterationOptions options)
        {
            var next = FindLatestCapturedIteration() + 1;
            if (next > DefaultSkillMaxIterations)
            {
                Debug.LogWarning("[StyleMatch] Max skill iterations (" + DefaultSkillMaxIterations + ") reached.");
                return;
            }

            if (options.RequireMutation && !options.MutationConfirmed)
            {
                Debug.LogError(
                    "[StyleMatch] Skill iter " + next + " blocked: apply a change first, then call " +
                    "TryRunSkillIteration with PipelineRunAfterChange(note) or set MutationConfirmed.");
                return;
            }

            if (!TryRunSkillIteration(next, options, out var result, out var error))
                Debug.LogError("[StyleMatch] Skill iter " + next + " failed: " + error);
            else
                Debug.Log("[StyleMatch] Skill iter " + next + " complete — " + result.CaptureBytes + " bytes");
        }

        /// <summary>Push rock/snow elevation band defaults onto the scene WorldSurfacePainter.</summary>
        public static bool ApplyMountainElevationBandDefaults() =>
            StyleMatchPainterMutations.ApplyMountainElevationBandDefaults();

        public static bool BoostValleyForestDensity(float multiplier) =>
            StyleMatchProfileMutations.BoostBiomeNatureDensity(
                new[] { "Forest", "Hills", "Plains" }, multiplier);

        public static bool AddSurfacePainterFloat(string field, float delta, float min, float max) =>
            StyleMatchPainterMutations.AddFloat(field, delta, min, max);

        public static bool SetSurfacePainterFloat(string field, float value, float min, float max) =>
            StyleMatchPainterMutations.SetFloat(field, value, min, max);

        public static bool SetGrassYellowScale(float scale) =>
            StyleMatchPainterMutations.SetFloat("_mainGrassYellowScale", scale, 0f, 1f);

        public static int FindLatestCapturedIteration()
        {
            Directory.CreateDirectory(OutputDir);
            var latest = 0;
            foreach (var file in Directory.GetFiles(OutputDir, "iter*.png"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var dash = name.IndexOf('-');
                if (dash < 0 || dash >= name.Length - 1)
                    continue;

                if (!int.TryParse(name.Substring(dash + 1), out var index))
                    continue;

                if (index > latest)
                    latest = index;
            }

            return latest;
        }

        [MenuItem("Tools/World/Style Match/Force Clear Iteration Lock")]
        public static void MenuForceClearIterationLock()
        {
            ForceClearIterationLock();
            Debug.Log("[StyleMatch] Iteration lock cleared.");
        }

        /// <summary>Poll this path instead of re-invoking script-execute while an iteration runs.</summary>
        public static bool TryGetAgentIterationLog(out string text) =>
            StyleMatchIterationLock.TryGetAgentLog(out text);

        public static bool IsSkillIterationLocked => StyleMatchIterationLock.IsLocked;

        public static void ForceClearIterationLock() => StyleMatchIterationLock.ForceClear();

        [MenuItem("Tools/World/Style Match/Stop Batch Loop")]
        public static void MenuStopBatchLoop()
        {
            if (!Stop(out var error))
                Debug.LogWarning("[StyleMatch] " + error);
        }

        /// <summary>Apply style-match lighting and capture (pipeline must already be complete).</summary>
        public static bool TryCaptureStyleMatchIteration(int iterationIndex, out string error) =>
            TryRunSkillIteration(
                iterationIndex,
                StyleMatchSkillIterationOptions.CaptureOnlyRun(),
                out _,
                out error);

        /// <summary>Capture only (pipeline must already match style-match end).</summary>
        public static void CaptureIteration(int iterationIndex)
        {
            if (!TryCaptureStyleMatchIteration(iterationIndex, out var error))
                Debug.LogWarning("[StyleMatch] " + error);
        }

        /// <summary>Run hub pipeline through style-match end, then capture. Legacy — prefer change → test.</summary>
        public static bool TryRunPipelineAndCapture(int iterationIndex, out string error) =>
            TryRunSkillIteration(
                iterationIndex,
                new StyleMatchSkillIterationOptions { RequireMutation = false },
                out _,
                out error);

        public static void EnsureSkillSessionFiles()
        {
            Directory.CreateDirectory(OutputDir);
            if (!File.Exists(SessionPlanPath))
                File.WriteAllText(SessionPlanPath, SkillSessionTemplate);

            if (!File.Exists(SessionLogPath))
            {
                File.WriteAllText(SessionLogPath,
                    "# Style match session log\n\n| Iter | Step | Result |\n|------|------|--------|\n");
            }
        }

        private static void AppendSessionLog(int iter, string step, string result)
        {
            EnsureSkillSessionFiles();
            File.AppendAllText(SessionLogPath,
                "| " + iter + " | " + step + " | " + result + " |\n");
        }

        private const string SkillSessionTemplate =
@"# Style match session (skill mode)

- Reference: reference.png
- Max iterations: 200
- Pipeline end: BindWeatherConsumers (no city)
- Fast iteration: true (set false for validation pass)
- Current iteration: 0

## Per-iteration flow (change then test)

1. Change - code edit, profile tweak, or batch mutation (one hypothesis per iter)
2. Test - TryRunSkillIteration with PipelineRunAfterChange (or SurfacesOnlyAfterChange / BiomesAndSurfacesAfterChange)

UseFastIteration defaults to true. Run FullFidelityPipelineRun once before keeping a change.

RequireMutation is on by default: iterations without MutationConfirmed and HypothesisNote are rejected.

## Planned steps (agent maintains)

- [ ] s1 | code | Example step | V | pending

## Score table (fill after each capture)

| Iter | T | S | V | W | A | L | Overall | Notes |
|------|---|---|---|---|---|---|---------|-------|
";
    }
}
#endif
