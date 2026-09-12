#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor
{
    [Serializable]
    public sealed class WorldBuilderHubStageEntry
    {
        public string stageId;
        public string displayName;
        public long durationMs;
    }

    [Serializable]
    public sealed class WorldBuilderHubPerfReportFile
    {
        public long totalMs;
        public int stageCount;
        public string lastStage;
        public long completedUnixMs;
        public WorldBuilderHubStageEntry[] stages;
    }

    /// <summary>Collects and writes hub pipeline stage timings for perf-fix loops.</summary>
    public static class WorldBuilderHubPerfReportWriter
    {
        public const string LatestFileName = "hub-perf-latest.json";

        private static readonly List<WorldBuilderHubStageEntry> _currentRun = new(20);

        public static bool HasActiveRun => _currentRun.Count > 0;

        public static string LatestReportPath =>
            Path.Combine(CityPipelineRunnerWindow.ReportsDirectory, LatestFileName);

        public static void BeginRun()
        {
            _currentRun.Clear();
        }

        public static void RecordStage(WorldBuildStageId stageId, string displayName, long durationMs)
        {
            _currentRun.Add(new WorldBuilderHubStageEntry
            {
                stageId = stageId.ToString(),
                displayName = displayName ?? stageId.ToString(),
                durationMs = durationMs
            });
        }

        public static void WriteCompleted(long totalMs, WorldBuildStageId lastStageInclusive)
        {
            var report = new WorldBuilderHubPerfReportFile
            {
                totalMs = totalMs,
                stageCount = _currentRun.Count,
                lastStage = lastStageInclusive.ToString(),
                completedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                stages = _currentRun.ToArray()
            };

            var path = LatestReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? CityPipelineRunnerWindow.ReportsDirectory);
            File.WriteAllText(path, JsonUtility.ToJson(report, prettyPrint: true));
            Debug.Log("[WorldBuilderHub] perf report written: " + path + " totalMs=" + totalMs);
        }

        public static bool TryReadLatest(out WorldBuilderHubPerfReportFile report)
        {
            report = null;
            var path = LatestReportPath;
            if (!File.Exists(path))
                return false;

            try
            {
                report = JsonUtility.FromJson<WorldBuilderHubPerfReportFile>(File.ReadAllText(path));
                return report != null && report.stages != null && report.stages.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WorldBuilderHub] Failed to read perf report: " + ex.Message);
                return false;
            }
        }
    }
}
#endif
