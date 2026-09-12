#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>Hub pipeline start/stop range control for <see cref="CityPipelineRunnerWindow"/>.</summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private void StartRun()
        {
            if (!TryResolveBuilder())
                return;

            EnsureStepsFresh();
            StartRange(0, _steps.Count - 1, false);
        }

        private void RunNextStep()
        {
            if (!TryResolveBuilder())
                return;

            EnsureStepsFresh();
            var next = FindNextStepToRun();
            if (next < 0)
            {
                _sessionLog.Add("No pending steps — pipeline complete (Reset Steps to start over).");
                Repaint();
                return;
            }

            // One step only — reuse WorldBuilderService session / artifacts already built.
            StartRange(next, next, true);
        }

        private void RunSection(string section)
        {
            if (!TryResolveBuilder())
                return;

            EnsureStepsFresh();
            var from = _steps.FindIndex(s => s.Section == section);
            if (from < 0)
                return;

            var to = from;
            for (var i = from + 1; i < _steps.Count; i++)
            {
                if (_steps[i].Section != section)
                    break;
                to = i;
            }

            // Prefer the first incomplete step in the section; do not rewind to
            // already-Done work earlier in the same section.
            var start = from;
            for (var i = from; i <= to; i++)
            {
                var status = _steps[i].Status;
                if (status != StepStatus.Done && status != StepStatus.Warning)
                {
                    start = i;
                    break;
                }
            }

            if (start > to)
            {
                _sessionLog.Add("Section '" + section + "' already complete.");
                Repaint();
                return;
            }

            StartRange(start, to, false);
        }

        private void RunSingle(int stepIndex)
        {
            if (!TryResolveBuilder())
                return;

            EnsureStepsFresh();
            if (stepIndex < 0 || stepIndex >= _steps.Count)
                return;

            // Already complete → redo only this step.
            if (IsStepComplete(_steps[stepIndex]))
            {
                StartRange(stepIndex, stepIndex, true);
                return;
            }

            // Jump ahead (e.g. step 0 → step 10): run first incomplete through the
            // clicked step. Clicking the immediate next incomplete step is from==to.
            var from = ResolveRunStartThrough(stepIndex);
            StartRange(from, stepIndex, singleStep: from == stepIndex);
        }

        /// <summary>
        ///     First incomplete step at or before <paramref name="stepIndex"/>.
        ///     Completed steps before the target are left alone (existing session data).
        /// </summary>
        private int ResolveRunStartThrough(int stepIndex)
        {
            var last = Mathf.Min(stepIndex, _steps.Count - 1);
            for (var i = 0; i <= last; i++)
            {
                if (!IsStepComplete(_steps[i]))
                    return i;
            }

            return stepIndex;
        }

        /// <summary>
        ///     Next incomplete step in queue order. Prefers <see cref="_stepIndex"/>
        ///     when that slot still needs work; otherwise the first non-Done step.
        ///     Returns -1 when every step is Done/Warning.
        /// </summary>
        private int FindNextStepToRun()
        {
            if (_stepIndex >= 0 && _stepIndex < _steps.Count &&
                !IsStepComplete(_steps[_stepIndex]))
                return _stepIndex;

            for (var i = 0; i < _steps.Count; i++)
            {
                if (!IsStepComplete(_steps[i]))
                    return i;
            }

            return -1;
        }

        private void StartRange(int from, int to, bool singleStep)
        {
            _stepIndex = from;
            _stopAfterIndex = to;
            _runSingleStep = singleStep;
            _stopRequested = false;
            _running = true;
            _nextStepAt = EditorApplication.timeSinceStartup;
            _runCancellation = new WorldBuildCancellation();

            // Only a complete Run All pass counts as a "full run" for the total.
            _trackingFullRun = !singleStep && from == 0 && to == _steps.Count - 1;
            _runStopwatch = _trackingFullRun ? Stopwatch.StartNew() : null;

            CityPipelineFailureMarkers.Clear();
            EditorApplication.update += EditorTick;
            Repaint();
        }

        private void StopRun()
        {
            _stopRequested = true;
            _runCancellation?.Request();
            _running = false;
            EditorApplication.update -= EditorTick;
            Repaint();
        }
    }
}
#endif
