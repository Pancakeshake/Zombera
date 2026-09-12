#if UNITY_EDITOR
using Zombera.Editor;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Configurable skill-mode iteration run (required phases + optional gates).</summary>
    public sealed class StyleMatchSkillIterationOptions
    {
        public const long DefaultMinCaptureBytes = 1_500_000L;

        /// <summary>Logged to session-log and console for attribution.</summary>
        public string HypothesisNote { get; set; }

        /// <summary>
        /// When true (default), iteration fails unless <see cref="MutationConfirmed"/> is set
        /// after applying a change. Enforces change → test, not test-only reruns.
        /// </summary>
        public bool RequireMutation { get; set; } = true;

        /// <summary>Set true only after the iteration's mutation/change has been applied.</summary>
        public bool MutationConfirmed { get; set; }

        /// <summary>Skip hub pipeline; scene must already match style-match end.</summary>
        public bool CaptureOnly { get; set; }

        /// <summary>Do not clear the Unity console before the run (not recommended).</summary>
        public bool SkipClearConsole { get; set; }

        /// <summary>Skip Enviro clear-midday + valley haze before capture.</summary>
        public bool SkipEnviroLighting { get; set; }

        /// <summary>Ensure capture file exists and meets <see cref="MinCaptureBytes"/>.</summary>
        public bool ValidateCapture { get; set; } = true;

        /// <summary>Minimum PNG size when <see cref="ValidateCapture"/> is true.</summary>
        public long MinCaptureBytes { get; set; } = DefaultMinCaptureBytes;

        /// <summary>After pipeline, compare hub perf report to perf-baseline.json.</summary>
        public bool CheckPerfRegression { get; set; }

        /// <summary>When baseline file is missing, seed it from this pipeline run.</summary>
        public bool SavePerfBaselineIfMissing { get; set; }

        /// <summary>Fail iteration if Unity console has errors after the run.</summary>
        public bool FailOnConsoleErrors { get; set; }

        /// <summary>
        /// Coarser surface paint + reduced landform/biome noise (editor iteration). Default on.
        /// </summary>
        public bool UseFastIteration { get; set; } = true;

        /// <summary>
        /// First registry stage to run. Null runs from <see cref="WorldBuildStageId.ResetGeneratedWorld"/>.
        /// </summary>
        public WorldBuildStageId? PipelineStartStage { get; set; }

        /// <summary>Last registry stage inclusive; capped at BindWeatherConsumers for style-match.</summary>
        public WorldBuildStageId PipelineEndStage { get; set; } =
            WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage;

        /// <summary>Fail when <c>iter-NNN.png</c> already exists (blocks MCP retry duplicates).</summary>
        public bool AllowOverwriteExistingCapture { get; set; }

        /// <summary>Skip post-completion cooldown (manual retry only).</summary>
        public bool BypassCompletionCooldown { get; set; }

        public static StyleMatchSkillIterationOptions PipelineRun() => new();

        /// <summary>Pipeline run after a mutation was applied; records the change note.</summary>
        public static StyleMatchSkillIterationOptions PipelineRunAfterChange(string changeNote) =>
            new() { HypothesisNote = changeNote, MutationConfirmed = true };

        public static StyleMatchSkillIterationOptions CaptureOnlyRun() =>
            new() { CaptureOnly = true, RequireMutation = false };

        public static StyleMatchSkillIterationOptions WithPerfTracking() =>
            new() { CheckPerfRegression = true, SavePerfBaselineIfMissing = true };

        /// <summary>Full-fidelity rebuild through style-match end (slow validation pass).</summary>
        public static StyleMatchSkillIterationOptions FullFidelityPipelineRun() =>
            new() { UseFastIteration = false, RequireMutation = false };

        /// <summary>Re-paint surfaces and downstream stages only (scene artifacts must exist).</summary>
        public static StyleMatchSkillIterationOptions SurfacesOnlyAfterChange(string changeNote) =>
            new()
            {
                HypothesisNote = changeNote,
                MutationConfirmed = true,
                PipelineStartStage = WorldBuildStageId.PaintNaturalSurfaces,
                PipelineEndStage = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage,
            };

        /// <summary>Re-classify biomes, re-paint, and run wilderness + Enviro stages.</summary>
        public static StyleMatchSkillIterationOptions BiomesAndSurfacesAfterChange(string changeNote) =>
            new()
            {
                HypothesisNote = changeNote,
                MutationConfirmed = true,
                PipelineStartStage = WorldBuildStageId.ClassifyBiomesAndBuildability,
                PipelineEndStage = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage,
            };

        /// <summary>Re-run Enviro stages only (surfaces/wilderness must already be correct).</summary>
        public static StyleMatchSkillIterationOptions EnvironmentOnlyAfterChange(string changeNote) =>
            new()
            {
                HypothesisNote = changeNote,
                MutationConfirmed = true,
                PipelineStartStage = WorldBuildStageId.ConfigureSkyAndWeather,
                PipelineEndStage = WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage,
            };

        /// <summary>Re-paint surfaces only — skips wilderness and Enviro (fastest paint tweak path).</summary>
        public static StyleMatchSkillIterationOptions PaintOnlyAfterChange(string changeNote) =>
            new()
            {
                HypothesisNote = changeNote,
                MutationConfirmed = true,
                PipelineStartStage = WorldBuildStageId.PaintNaturalSurfaces,
                PipelineEndStage = WorldBuildStageId.PaintNaturalSurfaces,
            };

        public WorldBuildStageId ResolvePipelineStartStage() =>
            PipelineStartStage ?? WorldBuildStageId.ResetGeneratedWorld;
    }

    public sealed class StyleMatchSkillIterationResult
    {
        public int IterationIndex { get; set; }
        public string CapturePath { get; set; }
        public long CaptureBytes { get; set; }
        public bool PipelineRan { get; set; }
        public bool PerfRegression { get; set; }
        public string PerfRegressionDetail { get; set; }
        public int ConsoleErrorCount { get; set; }
        public long CompareReferenceMs { get; set; }
        public float CompareSimilarityScore { get; set; }
        public float CompareMeanAbsoluteError { get; set; }
        public WorldBuildStageId PipelineStartStage { get; set; }
        public WorldBuildStageId PipelineEndStage { get; set; }
        public bool UsedFastIteration { get; set; }
        public long TotalPipelineMs { get; set; }
    }
}
#endif
