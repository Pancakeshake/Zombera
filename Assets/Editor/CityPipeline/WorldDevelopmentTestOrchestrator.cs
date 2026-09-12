#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Simulation;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Views;

namespace Zombera.Editor
{
    /// <summary>
    ///     Runs Milestone 7 Building Vertical Slice Test steps in Edit Mode where feasible.
    /// </summary>
    internal sealed class WorldDevelopmentTestOrchestrator
    {
        private const string TestName = "Building Vertical Slice Test";
        private const int TestSeed = CityPipelineRunnerWindow.ThreeOceansOneMountainReferenceSeed;

        private readonly CityPipelineRunnerWindow _window;

        public WorldDevelopmentTestOrchestrator(CityPipelineRunnerWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public WorldTestRunReport RunBuildingVerticalSliceTest()
        {
            var report = CreateReport();
            var stopwatch = Stopwatch.StartNew();

            RunStep(report, "Verify prerequisites", StepVerifyPrerequisites);
            RunStep(report, "Configure Medium map + seed 16", StepConfigureTestSettings);
            RunStep(report, "Run full world-build pipeline", StepRunFullPipeline);
            RunStep(report, "Validate WorldState", StepValidateState);
            RunStep(report, "Capture baseline hash", StepCaptureBaseline);
            RunStep(report, "Encode payload", StepEncodePayload);
            RunStep(report, "Decode payload round-trip", StepDecodePayload);
            RunStep(report, "Diff unchanged snapshot", StepDiffUnchanged);
            RunStep(report, "Fire first building event", StepFireBuilding);
            RunStep(report, "Advance simulation +1 hour", StepAdvanceSimulation);
            RunStep(report, "Diff post-simulation state", StepDiffPostSimulation);
            RunStep(report, "Replay decoded payload", StepReplayPayload);
            RunStep(report, "Audit materialized building views", StepAuditViews);
            MarkNotCovered(report, "Play Mode spawn and NavMesh streaming");
            MarkNotCovered(report, "Runtime chunk streaming tick");

            stopwatch.Stop();
            report.DurationMs = stopwatch.ElapsedMilliseconds;
            report.OverallStatus = ComputeOverallStatus(report);
            WriteReport(report);
            return report;
        }

        private WorldTestRunReport CreateReport()
        {
            return new WorldTestRunReport
            {
                RunId = Guid.NewGuid().ToString("N"),
                TestName = TestName,
                WorldSeed = TestSeed,
                MapSizeTier = WorldMapSizeTier.Small.ToString(),
                StartedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Steps = new List<WorldTestStepResult>(),
                Issues = new List<string>(),
                Coverage = new List<string>(),
                Diffs = new List<string>()
            };
        }

        private void RunStep(
            WorldTestRunReport report,
            string name,
            Func<WorldTestRunReport, WorldTestStepResult> action)
        {
            var stepWatch = Stopwatch.StartNew();
            WorldTestStepResult result;
            try
            {
                result = action(report);
            }
            catch (Exception ex)
            {
                result = new WorldTestStepResult
                {
                    Name = name,
                    Status = WorldTestStepStatus.Failed,
                    Detail = ex.Message
                };
                report.Issues.Add(name + ": " + ex.Message);
            }

            stepWatch.Stop();
            result.Name = name;
            result.DurationMs = stepWatch.ElapsedMilliseconds;
            report.Steps.Add(result);
        }

        private WorldTestStepResult StepVerifyPrerequisites(WorldTestRunReport report)
        {
            var service = _window.ResolveWorldBuilderService();
            if (service == null)
                return Fail("CityPrefabRoadNetworkBuilder or WorldBuilderService is missing.");

            if (service.StateManager == null)
                return Fail("WorldStateManager is not provisioned.");

            if (service.Profile == null)
                return Pass("World Builder stack present; profile optional for editor test.");

            return Pass("Prerequisites satisfied.");
        }

        private WorldTestStepResult StepConfigureTestSettings(WorldTestRunReport report)
        {
            _window.ApplyVerticalSliceTestConfiguration();
            report.WorldSeed = TestSeed;
            report.MapSizeTier = WorldMapSizeTier.Small.ToString();
            return Pass("Medium map, seed 16, road cache reuse disabled.");
        }

        private WorldTestStepResult StepRunFullPipeline(WorldTestRunReport report)
        {
            if (!_window.TryRunFullPipelineSynchronously(out var error))
                return Fail(error ?? "Pipeline run failed.");

            var manager = _window.ResolveStateManager();
            var counts = CityPipelineRunnerWindow.CountEntities(manager);
            if (counts.Buildings <= 0)
                return Fail("Pipeline finished but no buildings were placed.");

            return Pass("Pipeline completed with " + counts.Buildings + " building(s).");
        }

        private WorldTestStepResult StepValidateState(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            if (manager == null || !manager.HasState)
                return Fail("WorldState is not available.");

            var state = manager.CaptureCanonicalCopy();
            var validation = WorldStateValidator.Validate(state);
            if (!validation.IsValid)
            {
                AppendValidationIssues(report, validation);
                return Fail("WorldState validation failed.");
            }

            return Pass("WorldState is valid.");
        }

        private WorldTestStepResult StepCaptureBaseline(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            if (manager == null || !manager.HasState)
                return Fail("WorldState is not available.");

            report.BaselineHash = WorldStateHasher.ComputeHash(manager.CaptureCanonicalCopy());
            return Pass("Baseline hash " + ShortHash(report.BaselineHash) + ".");
        }

        private WorldTestStepResult StepEncodePayload(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            var payload = WorldStatePayloadCodec.Encode(manager.CaptureCanonicalCopy());
            var json = JsonUtility.ToJson(payload, true);
            File.WriteAllText(CityPipelineRunnerWindow.TestPayloadPath, json);
            if (!string.Equals(payload.hashSha256, report.BaselineHash, StringComparison.OrdinalIgnoreCase))
                return Fail("Encoded payload hash does not match baseline.");

            return Pass("Payload saved to " + CityPipelineRunnerWindow.TestPayloadPath + ".");
        }

        private WorldTestStepResult StepDecodePayload(WorldTestRunReport report)
        {
            if (!File.Exists(CityPipelineRunnerWindow.TestPayloadPath))
                return Fail("Payload file missing.");

            var json = File.ReadAllText(CityPipelineRunnerWindow.TestPayloadPath);
            var payload = JsonUtility.FromJson<WorldStatePayload>(json);
            var decode = WorldStatePayloadCodec.Decode(
                payload.payloadBase64,
                expectedHashSha256: report.BaselineHash);
            if (!decode.IsValid || decode.state == null)
            {
                AppendValidationIssues(report, decode.report);
                return Fail("Payload decode failed.");
            }

            _decodedState = decode.state;
            return Pass("Decode matched baseline hash.");
        }

        private WorldTestStepResult StepDiffUnchanged(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            if (_decodedState == null)
                return Fail("Decoded snapshot not available.");

            var diff = WorldStateDiffer.Diff(_decodedState, manager.CaptureCanonicalCopy());
            if (diff.HasChanges)
            {
                AppendDiffSummary(report, diff, maxLines: 8);
                return Fail("Unexpected diff entries before simulation (" + diff.Entries.Count + ").");
            }

            return Pass("No diff entries before simulation.");
        }

        private WorldTestStepResult StepFireBuilding(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            if (!CityPipelineRunnerWindow.TryFindFirstBuilding(manager, out var buildingId))
                return Fail("No building available for fire event.");

            var service = new WorldStateSimulationService(manager);
            if (!service.FireBuildingNow(buildingId, 1f, out _, out var fireReport))
            {
                AppendValidationIssues(report, fireReport);
                return Fail("Fire event failed for " + buildingId + ".");
            }

            return Pass("Fired building " + buildingId + ".");
        }

        private WorldTestStepResult StepAdvanceSimulation(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            var service = new WorldStateSimulationService(manager);
            if (!service.AdvanceHours(1, out _, out var advanceReport))
            {
                AppendValidationIssues(report, advanceReport);
                return Fail("Simulation advance failed.");
            }

            return Pass("Advanced +1 hour.");
        }

        private WorldTestStepResult StepDiffPostSimulation(WorldTestRunReport report)
        {
            var manager = _window.ResolveStateManager();
            if (string.IsNullOrWhiteSpace(report.BaselineHash))
                return Fail("Baseline hash missing.");

            report.FinalHash = WorldStateHasher.ComputeHash(manager.CaptureCanonicalCopy());
            if (string.Equals(report.BaselineHash, report.FinalHash, StringComparison.OrdinalIgnoreCase))
                return Fail("Simulation did not change WorldState hash.");

            if (_decodedState == null)
                return Fail("Baseline snapshot missing for diff.");

            var diff = WorldStateDiffer.Diff(_decodedState, manager.CaptureCanonicalCopy());
            AppendDiffSummary(report, diff, maxLines: 16);
            return Pass("Post-simulation diff has " + diff.Entries.Count + " entr(y/ies).");
        }

        private WorldTestStepResult StepReplayPayload(WorldTestRunReport report)
        {
            if (_decodedState == null)
                return Fail("Decoded snapshot not available.");

            var manager = _window.ResolveStateManager();
            if (!manager.TryLoad(_decodedState, out var loadReport))
            {
                AppendValidationIssues(report, loadReport);
                return Fail("Replay load failed.");
            }

            var replayHash = WorldStateHasher.ComputeHash(manager.CaptureCanonicalCopy());
            if (!string.Equals(report.BaselineHash, replayHash, StringComparison.OrdinalIgnoreCase))
                return Fail("Replay hash does not match baseline.");

            return Pass("Replay restored baseline hash.");
        }

        private WorldTestStepResult StepAuditViews(WorldTestRunReport report)
        {
            var materializer = _window.ResolveMaterializer();
            if (materializer == null)
                return Skipped("WorldBuildingMaterializer not found; view audit skipped.");

            var manager = _window.ResolveStateManager();
            if (!CityPipelineRunnerWindow.TryFindFirstBuilding(manager, out var buildingId))
                return Skipped("No building to load for view audit.");

            var tile = ResolveBuildingTile(manager, buildingId);
            if (tile.x < 0)
                return Skipped("Could not resolve owner tile for " + buildingId + ".");

            materializer.DestroyAllViews();
            var loaded = materializer.LoadTile(tile);
            var audit = materializer.AuditLoadedViews();
            if (audit.HasIssues)
            {
                for (var i = 0; i < audit.Issues.Count; i++)
                    report.Issues.Add("View audit: " + audit.Issues[i]);
                return Fail("View audit reported " + audit.Issues.Count + " issue(s); loaded " + loaded + " view(s).");
            }

            return Pass("Loaded " + loaded + " view(s); audit clean.");
        }

        private void MarkNotCovered(WorldTestRunReport report, string domain)
        {
            report.Coverage.Add(domain);
            report.Steps.Add(new WorldTestStepResult
            {
                Name = domain,
                Status = WorldTestStepStatus.NotCovered,
                Detail = "Requires Play Mode or runtime systems."
            });
        }

        private static WorldTestStepStatus ComputeOverallStatus(WorldTestRunReport report)
        {
            var sawFailed = false;
            var sawPassed = false;
            for (var i = 0; i < report.Steps.Count; i++)
            {
                switch (report.Steps[i].Status)
                {
                    case WorldTestStepStatus.Failed:
                        sawFailed = true;
                        break;
                    case WorldTestStepStatus.Passed:
                        sawPassed = true;
                        break;
                }
            }

            if (sawFailed)
                return WorldTestStepStatus.Failed;
            return sawPassed ? WorldTestStepStatus.Passed : WorldTestStepStatus.Skipped;
        }

        private static void WriteReport(WorldTestRunReport report)
        {
            Directory.CreateDirectory(CityPipelineRunnerWindow.ReportsDirectory);
            var fileName = "building-vertical-slice-" + report.RunId + ".json";
            var path = Path.Combine(CityPipelineRunnerWindow.ReportsDirectory, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            report.ReportPath = path;
        }

        private static void AppendValidationIssues(WorldTestRunReport report, WorldValidationReport validation)
        {
            if (validation?.Errors == null)
                return;

            for (var i = 0; i < validation.Errors.Count; i++)
                report.Issues.Add(validation.Errors[i]);
        }

        private static void AppendDiffSummary(WorldTestRunReport report, WorldStateDiff diff, int maxLines)
        {
            if (diff?.Entries == null)
                return;

            var count = Math.Min(maxLines, diff.Entries.Count);
            for (var i = 0; i < count; i++)
            {
                var entry = diff.Entries[i];
                report.Diffs.Add(entry.Kind + " " + entry.Path);
            }

            if (diff.Entries.Count > maxLines)
                report.Diffs.Add("… +" + (diff.Entries.Count - maxLines) + " more");
        }

        private static WorldTileKey ResolveBuildingTile(WorldStateManager manager, WorldEntityId buildingId)
        {
            if (manager == null || !manager.TryCopyBuilding(buildingId, out var building))
                return new WorldTileKey(-1, -1);

            var header = manager.Header;
            if (header == null)
                return new WorldTileKey(-1, -1);

            if (WorldTileOwnership.TryResolveOwnerTile(header, building.footprintXZ.center, out var tile, true))
                return tile;

            return new WorldTileKey(-1, -1);
        }

        private static WorldTestStepResult Pass(string detail) =>
            new() { Status = WorldTestStepStatus.Passed, Detail = detail };

        private static WorldTestStepResult Fail(string detail) =>
            new() { Status = WorldTestStepStatus.Failed, Detail = detail };

        private static WorldTestStepResult Skipped(string detail) =>
            new() { Status = WorldTestStepStatus.Skipped, Detail = detail };

        private static string ShortHash(string hash) =>
            string.IsNullOrEmpty(hash) || hash.Length <= 12 ? hash ?? string.Empty : hash.Substring(0, 12) + "…";

        private WorldState _decodedState;
    }
}
#endif
