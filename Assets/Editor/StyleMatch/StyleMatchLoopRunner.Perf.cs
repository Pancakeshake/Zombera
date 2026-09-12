#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zombera.Editor;

namespace Zombera.Editor.StyleMatch
{
    public sealed partial class StyleMatchLoopRunner
    {
        [Serializable]
        private sealed class PerfBaselineFile
        {
            public long totalMs;
            public StageMs[] stages;
        }

        [Serializable]
        private sealed class StageMs
        {
            public string stageId;
            public long durationMs;
        }

        private PerfBaselineFile _perfBaseline;
        private bool _perfMode;
        private string _lastPerfRegression;

        private void LoadPerfBaseline()
        {
            var path = Path.Combine(OutputDir, "perf-baseline.json");
            if (!File.Exists(path))
            {
                _perfBaseline = null;
                return;
            }

            try
            {
                _perfBaseline = JsonUtility.FromJson<PerfBaselineFile>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StyleMatch] Failed to read perf baseline: " + ex.Message);
                _perfBaseline = null;
            }
        }

        private void SavePerfBaseline(WorldBuilderHubPerfReportFile report)
        {
            if (report?.stages == null || report.stages.Length == 0)
                return;

            var file = new PerfBaselineFile
            {
                totalMs = report.totalMs,
                stages = new StageMs[report.stages.Length]
            };

            for (var i = 0; i < report.stages.Length; i++)
            {
                file.stages[i] = new StageMs
                {
                    stageId = report.stages[i].stageId,
                    durationMs = report.stages[i].durationMs
                };
            }

            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, "perf-baseline.json");
            File.WriteAllText(path, JsonUtility.ToJson(file, prettyPrint: true));
            _perfBaseline = file;
            Debug.Log("[StyleMatch] Saved perf baseline: " + path + " totalMs=" + report.totalMs);
        }

        private bool EvaluatePerfGate(WorldBuilderHubPerfReportFile latest)
        {
            _lastPerfRegression = null;
            if (_perfBaseline?.stages == null || latest?.stages == null)
                return false;

            var baselineById = new Dictionary<string, long>(StringComparer.Ordinal);
            for (var i = 0; i < _perfBaseline.stages.Length; i++)
            {
                var s = _perfBaseline.stages[i];
                if (s != null && !string.IsNullOrEmpty(s.stageId))
                    baselineById[s.stageId] = s.durationMs;
            }

            for (var i = 0; i < latest.stages.Length; i++)
            {
                var stage = latest.stages[i];
                if (stage == null || string.IsNullOrEmpty(stage.stageId))
                    continue;
                if (!baselineById.TryGetValue(stage.stageId, out var baseMs) || baseMs <= 0)
                    continue;

                var ratio = (float)stage.durationMs / baseMs;
                if (ratio < 2f)
                    continue;

                _lastPerfRegression = stage.stageId + "=" + stage.durationMs + "ms (" +
                                      (ratio * 100f).ToString("F0") + "% of baseline " + baseMs + "ms)";
                return true;
            }

            var baselineEnd = _perfBaseline.stages != null && _perfBaseline.stages.Length > 0
                ? _perfBaseline.stages[_perfBaseline.stages.Length - 1].stageId
                : null;
            if (_perfBaseline.totalMs > 0 &&
                !string.IsNullOrEmpty(baselineEnd) &&
                latest.lastStage == baselineEnd &&
                latest.totalMs >= _perfBaseline.totalMs * 2)
            {
                _lastPerfRegression = "totalMs=" + latest.totalMs + " (≥2× baseline " + _perfBaseline.totalMs + ")";
                return true;
            }

            return false;
        }

        private void LogPerfRow(int iter, string mode, WorldBuilderHubPerfReportFile report)
        {
            if (report == null)
                return;

            var line = $"[StyleMatch] PERF iter={iter} mode={mode} totalMs={report.totalMs} last={report.lastStage}";
            if (!string.IsNullOrEmpty(_lastPerfRegression))
                line += " REGRESSION=" + _lastPerfRegression;
            Debug.Log(line);
        }
    }
}
#endif
