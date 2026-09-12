#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Editor;
using Zombera.World.Roads;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Editor loop: mutation → style-match hub pipeline → capture.</summary>
    public sealed partial class StyleMatchLoopRunner
    {
        /// <summary>Single authoring scene for style-match (never use City Prefab Creation Hub).</summary>
        public const string WorldGenerationScenePath =
            "Assets/00_Scenes/02_System Dev Scenes/World Generation.unity";

        private const string WorldGenerationScene = WorldGenerationScenePath;

        public const int DefaultShortRunCount = 20;
        public const int DefaultLongRunCount = 100;

        private const float PipelineTimeoutSeconds = 900f;

        internal static string OutputDir => Path.Combine("Library", "StyleMatch");

        private static StyleMatchLoopRunner _active;

        private LoopPhase _phase = LoopPhase.Idle;
        private int _planIndex;
        private int _targetCount = DefaultShortRunCount;
        private string _lastSummary = string.Empty;
        private double _pipelineWaitStart;
        private bool _pipelineStartRequested;

        private enum LoopPhase
        {
            Idle,
            Mutation,
            PipelineStart,
            PipelineWait,
            Capture,
            Done
        }

        public static bool IsRunning =>
            _active != null && _active._phase != LoopPhase.Idle && _active._phase != LoopPhase.Done;

        [MenuItem("Tools/World/Style Match/Run 20 Iterations (Batch Tuning — Deprecated)")]
        public static void MenuRunTwentyIterations()
        {
            Debug.LogWarning("[StyleMatch] Batch tuning is deprecated. Use skill mode (50 agent iterations) — see Library/StyleMatch/session.md");
            if (!Start(0, DefaultShortRunCount, out var error))
                Debug.LogError("[StyleMatch] " + error);
        }

        [MenuItem("Tools/World/Style Match/Run 100 Iterations (Batch Tuning — Deprecated)")]
        public static void MenuRunHundredIterations()
        {
            Debug.LogWarning("[StyleMatch] Batch tuning is deprecated. Use skill mode (50 agent iterations) — see Library/StyleMatch/session.md");
            if (!Start(0, DefaultLongRunCount, out var error))
                Debug.LogError("[StyleMatch] " + error);
        }

        public static bool Start(int fromPlanIndex, out string error) =>
            Start(fromPlanIndex, DefaultShortRunCount, out error);

        public static bool Start(int fromPlanIndex, int iterationCount, out string error)
        {
            error = null;
            if (IsRunning)
            {
                error = "Style-match loop is already running.";
                return false;
            }

            if (iterationCount <= 0)
            {
                error = "iterationCount must be positive.";
                return false;
            }

            if (fromPlanIndex < 0 || fromPlanIndex >= iterationCount)
            {
                error = "fromPlanIndex must be in [0, " + (iterationCount - 1) + "].";
                return false;
            }

            _active = new StyleMatchLoopRunner();
            _active._targetCount = iterationCount;
            _active.Begin(fromPlanIndex);
            return true;
        }

        private void Begin(int fromPlanIndex)
        {
            Directory.CreateDirectory(OutputDir);
            LoadPerfBaseline();

            if (!OpenWorldGenerationScene())
            {
                Shutdown("failed to open World Generation scene");
                return;
            }

            _planIndex = fromPlanIndex;
            _phase = LoopPhase.Mutation;
            WriteStatus("starting planIndex=" + fromPlanIndex);
            EditorApplication.update += Tick;
            Debug.Log("[StyleMatch] Loop started at plan index " + fromPlanIndex + " / " + _targetCount);
        }

        private static bool OpenWorldGenerationScene()
        {
            return OpenWorldGenerationSceneForSetup();
        }

        /// <summary>Opens the canonical World Generation scene (used by menus and skill iterations).</summary>
        public static bool OpenWorldGenerationSceneForSetup()
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(WorldGenerationScene)))
            {
                Debug.LogError("[StyleMatch] Scene asset missing: " + WorldGenerationScene);
                return false;
            }

            var scene = EditorSceneManager.OpenScene(WorldGenerationScene, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[StyleMatch] Failed to open: " + WorldGenerationScene);
                return false;
            }

            if (FindHubBuilder() == null)
            {
                Debug.LogError(
                    "[StyleMatch] CityPrefabRoadNetworkBuilder not found in World Generation scene.");
                return false;
            }

            Debug.Log("[StyleMatch] Opened authoring scene: " + WorldGenerationScene);
            return true;
        }

        private static CityPrefabRoadNetworkBuilder FindHubBuilder() =>
            UnityEngine.Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);

        private void Tick()
        {
            try
            {
                switch (_phase)
                {
                    case LoopPhase.Mutation:
                        RunMutation();
                        break;
                    case LoopPhase.PipelineStart:
                        TryStartPipeline();
                        break;
                    case LoopPhase.PipelineWait:
                        WaitForPipeline();
                        break;
                    case LoopPhase.Capture:
                        RunCapture();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[StyleMatch] Loop error at plan " + _planIndex + ": " + ex);
                Shutdown("error: " + ex.Message);
            }
        }

        private void RunMutation()
        {
            _lastSummary = "none";
            if (!ApplyIterationMutation(_planIndex, _planIndex, ref _lastSummary))
                Debug.LogWarning("[StyleMatch] plan=" + _planIndex + " mutation returned false: " + _lastSummary);

            Debug.Log("[StyleMatch] plan=" + _planIndex + " mutation: " + _lastSummary);
            _pipelineStartRequested = false;
            _phase = LoopPhase.PipelineStart;
        }

        private void TryStartPipeline()
        {
            if (WorldBuilderHubPipelineRunner.IsPipelineRunning)
            {
                _pipelineWaitStart = EditorApplication.timeSinceStartup;
                _phase = LoopPhase.PipelineWait;
                return;
            }

            if (_pipelineStartRequested)
                return;

            if (!WorldBuilderHubPipelineRunner.StartUpToStyleMatchEndAsync(out var error))
            {
                Debug.LogError("[StyleMatch] Pipeline start failed: " + error);
                Shutdown("pipeline start failed: " + error);
                return;
            }

            _pipelineStartRequested = true;
            _pipelineWaitStart = EditorApplication.timeSinceStartup;
            _phase = LoopPhase.PipelineWait;
        }

        private void WaitForPipeline()
        {
            if (WorldBuilderHubPipelineRunner.IsPipelineRunning)
            {
                if (EditorApplication.timeSinceStartup - _pipelineWaitStart > PipelineTimeoutSeconds)
                    Shutdown("pipeline timeout");
                return;
            }

            if (WorldBuilderHubPerfReportWriter.TryReadLatest(out var report))
            {
                if (_planIndex == 0 && _perfBaseline == null)
                    SavePerfBaseline(report);

                var mode = EvaluatePerfGate(report) ? "perf" : "style";
                LogPerfRow(_planIndex, mode, report);
            }

            _phase = LoopPhase.Capture;
        }

        private void RunCapture()
        {
            try
            {
                CaptureIterationScreenshot(_planIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StyleMatch] Capture failed plan=" + _planIndex + ": " + ex.Message);
            }

            WriteStatus("plan " + _planIndex + " done: " + _lastSummary);

            _planIndex++;
            if (_planIndex >= _targetCount)
            {
                Shutdown("completed " + _targetCount + " iterations");
                return;
            }

            _phase = LoopPhase.Mutation;
        }

        private void Shutdown(string reason)
        {
            EditorApplication.update -= Tick;
            _phase = LoopPhase.Done;
            WriteStatus(reason);
            Debug.Log("[StyleMatch] Loop finished: " + reason);
            _active = null;
        }

        public static bool Stop(out string error)
        {
            error = null;
            if (!IsRunning)
            {
                error = "No style-match batch loop is running.";
                return false;
            }

            _active.Shutdown("stopped by user");
            return true;
        }

        private static void WriteStatus(string line)
        {
            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, "status.txt");
            File.WriteAllText(path, DateTime.UtcNow.ToString("o") + " " + line + System.Environment.NewLine);
        }
    }
}
#endif
