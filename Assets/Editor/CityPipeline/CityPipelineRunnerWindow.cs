#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     World Builder — Development Hub. Dockable runner for the city/world-build
    ///     pipeline plus state, simulation, events, streaming, and automated tests.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow : EditorWindow
    {
        private enum StepStatus
        {
            Pending,
            Running,
            Done,
            Warning,
            Failed
        }

        // ── Serialized state ──────────────────────────────────────

        [SerializeField] private CityPrefabRoadNetworkBuilder builder;
        [SerializeField] private WorldGenerationProfile worldProfile;
        [SerializeField] private WorldMapSizeTier editorMapTier = WorldMapSizeTier.Medium;
        [SerializeField] private int editorWorldSeed = ThreeOceansOneMountainReferenceSeed;
        [SerializeField] private bool pauseAfterStep;
        [SerializeField] private float stepDelaySeconds = 0.5f;
        [SerializeField] private bool fastRoadsMode;
        [SerializeField] private RoadBuildQualityMode roadBuildQuality = RoadBuildQualityMode.FastIteration;
        [SerializeField] private SurfacePaintQualityMode hubSurfacePaintQuality = SurfacePaintQualityMode.Quality;
        [SerializeField] private bool fastBuildFoldout = true;

        [Tooltip(
            "Hide Enviro atmosphere fog, built-in RenderSettings fog and Scene view fog while a run " +
            "builds the world, and restore the previous state when the run ends or fails. The authored " +
            "environment profile is untouched: its fog density feeds the world-state profile fingerprint.")]
        [SerializeField] private bool suppressFogWhileGenerating = true;
        [SerializeField] private bool showFailureMarkers = true;
        [SerializeField] private bool showScatterGizmo = true;
        [SerializeField] private List<string> collapsedSections = new();
        // ── Development Hub fields ────────────────────────────────

        [SerializeField] private WorldDevelopmentHubTab hubTab;
        [SerializeField] private string baselineHash = string.Empty;
        [SerializeField] private string selectedBuildingId = string.Empty;
        [SerializeField] private int streamTileX;
        [SerializeField] private int streamTileZ;
        [SerializeField] private float streamRadius = 1f;

        [NonSerialized] private WorldTestRunReport _latestTestReport;
        [NonSerialized] private WorldDevelopmentTestOrchestrator _testOrchestrator;

        private readonly List<PipelineStage> _steps = new();
        private readonly List<string> _sections = new();
        private readonly List<string> _sessionLog = new();
        private readonly List<string> _capturedLogs = new();
        private int _stepIndex;
        private int _stopAfterIndex;
        private bool _running;
        private bool _stopRequested;
        private bool _runSingleStep;
        private string _runningStepName = string.Empty;
        private double _nextStepAt;
        private Vector2 _scrollSteps;
        private Vector2 _scrollLog;
        private CityPrefabRoadNetworkBuilder _builtForBuilder;

        private PipelineStage _activeStep;
        private IEnumerator _stepRoutine;
        private Stopwatch _stepStopwatch;

        // Full-run tracking: wall time of a complete Run All pass, reported at
        // the bottom of the window when the run finishes.
        private Stopwatch _runStopwatch;
        private bool _trackingFullRun;
        private long _lastFullRunMs = -1;

        private static readonly Dictionary<StepStatus, GUIStyle> StepStyles = new();

        [MenuItem("Tools/World/World Builder — Development Hub")]
        public static void ShowWindow()
        {
            var window = GetWindow<CityPipelineRunnerWindow>("World Builder — Development Hub");
            window.minSize = new Vector2(460f, 560f);
            window.Show();
        }

        public static void ShowFor(CityPrefabRoadNetworkBuilder target)
        {
            var window = GetWindow<CityPipelineRunnerWindow>("World Builder — Development Hub");
            window.minSize = new Vector2(460f, 560f);
            window.builder = target;
            window.Show();
        }

        public void SetBuilder(CityPrefabRoadNetworkBuilder target) => builder = target;

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGui;
            EnsureHubSurfacePaintQualityDefault();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGui;
            StopRun();
        }

        // ── GUI drawing lives in CityPipelineRunnerWindow.Gui.cs ──
        // ── Run start/stop lives in CityPipelineRunnerWindow.RunControl.cs ──

        private void EditorTick()
        {
            if (!_running)
                return;

            if (_activeStep != null)
            {
                PumpActiveStep();
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextStepAt)
                return;

            if (_stepIndex > _stopAfterIndex)
            {
                RecordFullRunTotal();
                StopRun();
                return;
            }

            // Section / ranged continues skip Done steps; Run All re-executes everything.
            if (!_trackingFullRun)
            {
                while (_stepIndex <= _stopAfterIndex && IsStepComplete(_steps[_stepIndex]))
                    _stepIndex++;

                if (_stepIndex > _stopAfterIndex)
                {
                    StopRun();
                    return;
                }
            }

            BeginStep(_steps[_stepIndex]);
        }

        private static bool IsStepComplete(PipelineStage step) =>
            step.Status is StepStatus.Done or StepStatus.Warning;

        private void BeginStep(PipelineStage step)
        {
            _runningStepName = step.Name;
            step.Status = StepStatus.Running;
            step.DurationMs = 0;
            step.HasErrors = false;
            step.HasWarnings = false;
            step.Logs.Clear();
            _capturedLogs.Clear();

            _activeStep = step;
            _stepRoutine = step.Start();
            _stepStopwatch = Stopwatch.StartNew();
            Application.logMessageReceived += CaptureLog;
            Undo.IncrementCurrentGroup();
            Repaint();
        }

        /// <summary>Max editor time spent advancing a step coroutine per tick.</summary>
        private const float PumpTimeBudgetSeconds = 0.012f;

        /// <summary>Scene view repaints at most this often while a step runs.</summary>
        private const float SceneRepaintIntervalSeconds = 0.1f;

        private readonly Stopwatch _pumpStopwatch = new();
        private double _lastSceneRepaintAt;

        private void PumpActiveStep()
        {
            // Time-sliced pump: advance the step coroutine as many times as fit in
            // a small per-tick budget, then repaint the Scene view at most a few
            // times per second. The old per-yield repaint turned every coroutine
            // yield into a full scene redraw and dominated wall time on big cities.
            _pumpStopwatch.Restart();
            try
            {
                var hasMore = _stepRoutine.MoveNext();
                while (hasMore && _pumpStopwatch.Elapsed.TotalSeconds < PumpTimeBudgetSeconds)
                    hasMore = _stepRoutine.MoveNext();

                if (hasMore)
                {
                    if (EditorApplication.timeSinceStartup - _lastSceneRepaintAt >= SceneRepaintIntervalSeconds)
                    {
                        SceneView.RepaintAll();
                        _lastSceneRepaintAt = EditorApplication.timeSinceStartup;
                    }
                    Repaint();
                    return;
                }
            }
            catch (Exception e)
            {
                _activeStep.HasErrors = true;
                _activeStep.Logs.Add("EXCEPTION: " + e.Message);
                UnityEngine.Debug.LogException(e);
            }

            FinishStep(_activeStep);
            var stepFailed = _activeStep.Status == StepStatus.Failed;
            _stepIndex++;
            _activeStep = null;
            _stepRoutine = null;
            _stepStopwatch = null;

            if (_stepIndex > _stopAfterIndex && !_stopRequested)
                RecordFullRunTotal();

            if (_stopRequested || _runSingleStep || pauseAfterStep || _stepIndex > _stopAfterIndex || stepFailed)
                StopRun();
            else
                _nextStepAt = EditorApplication.timeSinceStartup + stepDelaySeconds;
        }

        private void FinishStep(PipelineStage step)
        {
            Application.logMessageReceived -= CaptureLog;

            var collapseWatch = Stopwatch.StartNew();
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            collapseWatch.Stop();

            _stepStopwatch.Stop();
            step.DurationMs = _stepStopwatch.ElapsedMilliseconds;

            ClassifyCapturedLogs(step);
            step.Status = step.HasErrors ? StepStatus.Failed : step.HasWarnings ? StepStatus.Warning : StepStatus.Done;

            _sessionLog.Add($"{step.Name} — {step.Status} in {step.DurationMs} ms"
                + (step.Logs.Count > 0 ? $" ({step.Logs.Count} log line(s))" : string.Empty));

            UnityEngine.Debug.Log(
                "[CityPipelineRunnerWindow] FinishStep '" + step.Name + "': undoCollapse=" +
                collapseWatch.ElapsedMilliseconds + "ms, durationMs=" + step.DurationMs + "ms");

            EditorUtility.SetDirty(builder);
            SceneView.RepaintAll();
            Repaint();
        }

        private void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
                return;
            _capturedLogs.Add($"[{type}] {condition}");
        }

        /// <summary>
        ///     Stamps the elapsed wall time when a complete Run All pass finishes.
        ///     Guarded so a stopped or paused run never records a partial total.
        /// </summary>
        private void RecordFullRunTotal()
        {
            if (!_trackingFullRun || _runStopwatch == null)
                return;

            _trackingFullRun = false;
            _runStopwatch.Stop();
            _lastFullRunMs = _runStopwatch.ElapsedMilliseconds;

            _sessionLog.Add("Full run finished — total " + FormatRunMs(_lastFullRunMs));
            UnityEngine.Debug.Log(
                "[CityPipelineRunnerWindow] Full run finished — total=" + _lastFullRunMs + "ms");
            Repaint();
        }

        private static string FormatRunMs(long ms) =>
            $"{ms:N0} ms ({(ms / 1000.0):F1} s)";

        private void ClassifyCapturedLogs(PipelineStage step)
        {
            for (var i = 0; i < _capturedLogs.Count; i++)
            {
                var line = _capturedLogs[i];
                if (line.StartsWith("[Error]") || line.StartsWith("[Exception]") || line.StartsWith("[Assert]"))
                    step.HasErrors = true;
                else if (line.StartsWith("[Warning]"))
                    step.HasWarnings = true;
                step.Logs.Add(line);
            }
        }

        // ── Builder resolution + section state ────────────────────

        private bool TryResolveBuilder()
        {
            if (builder == null)
                builder = FindFirstObjectByType<CityPrefabRoadNetworkBuilder>();
            if (builder != null)
                return true;

            EditorGUILayout.HelpBox("Assign a CityPrefabRoadNetworkBuilder to run the pipeline.", MessageType.Warning);
            return false;
        }

        private void EnsureStepsFresh()
        {
            if (_steps.Count == 0 || _builtForBuilder != builder)
                RebuildSteps();
        }

        private bool IsSectionExpanded(string section) => !collapsedSections.Contains(section);

        private void ToggleSectionExpanded(string section)
        {
            if (!collapsedSections.Remove(section))
                collapsedSections.Add(section);
            Repaint();
        }

        // ── Scene view overlays ───────────────────────────────────

        private void DrawSceneGui(SceneView view)
        {
            DrawSceneMarkers(view);
            DrawScatterZoneGizmo(view);
        }

        private void DrawSceneMarkers(SceneView view)
        {
            if (!showFailureMarkers)
                return;

            var markers = CityPipelineFailureMarkers.All;
            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                Handles.color = new Color(1f, 0.22f, 0.18f, 0.9f);
                Handles.DrawWireDisc(marker.Position, Vector3.up, 0.9f);
                Handles.Label(marker.Position + Vector3.up * 0.6f, marker.Reason);
            }
        }

        // ── Small helpers ─────────────────────────────────────────

        private static string StatusPrefix(StepStatus status) => status switch
        {
            StepStatus.Running => "▶ ",
            StepStatus.Done => "✓ ",
            StepStatus.Warning => "⚠ ",
            StepStatus.Failed => "✕ ",
            _ => "· "
        };

        private static string DurationSuffix(PipelineStage step) =>
            step.DurationMs > 0 ? $"   ({step.DurationMs} ms)" : string.Empty;

        private static GUIStyle StyleForStatus(StepStatus status)
        {
            if (StepStyles.TryGetValue(status, out var existing))
                return existing;

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = status switch
            {
                StepStatus.Running => new Color(0.95f, 0.60f, 0.15f),
                StepStatus.Done => new Color(0.30f, 0.85f, 0.35f),
                StepStatus.Warning => new Color(1f, 0.85f, 0.25f),
                StepStatus.Failed => new Color(1f, 0.30f, 0.25f),
                _ => Color.gray
            };
            StepStyles[status] = style;
            return style;
        }
    }
}
#endif
