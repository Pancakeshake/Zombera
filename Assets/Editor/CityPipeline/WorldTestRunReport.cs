#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Editor
{
    [Serializable]
    internal sealed class WorldTestStepResult
    {
        public string Name = string.Empty;
        public WorldTestStepStatus Status = WorldTestStepStatus.Skipped;
        public long DurationMs;
        public string Detail = string.Empty;
    }

    [Serializable]
    internal sealed class WorldTestRunReport
    {
        public string RunId = string.Empty;
        public string TestName = "Building Vertical Slice Test";
        public int WorldSeed;
        public string MapSizeTier = string.Empty;
        public int SchemaVersion = WorldStateSchema.CurrentVersion;
        public long StartedUnixMs;
        public long DurationMs;
        public WorldTestStepStatus OverallStatus = WorldTestStepStatus.Skipped;
        public string BaselineHash = string.Empty;
        public string FinalHash = string.Empty;
        public string ReportPath = string.Empty;
        public List<WorldTestStepResult> Steps = new();
        public List<string> Issues = new();
        public List<string> Coverage = new();
        public List<string> Diffs = new();
    }
}
#endif
