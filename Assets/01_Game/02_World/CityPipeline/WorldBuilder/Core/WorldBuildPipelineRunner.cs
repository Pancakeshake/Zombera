using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Executes world-build stages with prerequisite resolution and invalidation.</summary>
    public sealed class WorldBuildPipelineRunner
    {
        private readonly WorldBuildStageRegistry _registry;
        private readonly Dictionary<WorldBuildStageId, IWorldBuildStage> _stages = new();
        private readonly Dictionary<WorldBuildStageId, WorldBuildStageRecord> _records = new();

        public IReadOnlyDictionary<WorldBuildStageId, WorldBuildStageRecord> Records => _records;

        public WorldBuildPipelineRunner(WorldBuildStageRegistry registry = null)
        {
            _registry = registry ?? WorldBuildStageRegistry.CreateDefault();
            for (var i = 0; i < _registry.Stages.Count; i++)
            {
                var descriptor = _registry.Stages[i];
                _stages[descriptor.Id] = _registry.CreateStage(descriptor.Id);
                _records[descriptor.Id] = new WorldBuildStageRecord(descriptor.Id);
            }
        }

        public IEnumerator RunRange(
            WorldBuildContext context,
            WorldBuildStageId fromInclusive,
            WorldBuildStageId toInclusive,
            bool includeMissingPrerequisites = true)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var from = (byte)fromInclusive;
            var to = (byte)toInclusive;
            if (from > to)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            for (var i = 0; i < _registry.Stages.Count; i++)
            {
                var id = _registry.Stages[i].Id;
                var value = (byte)id;
                if (value < from || value > to) continue;

                var run = RunStage(context, id, includeMissingPrerequisites);
                while (run.MoveNext())
                    yield return run.Current;

                if (!_records.TryGetValue(id, out var record))
                    continue;

                if (record.Status != WorldBuildStageStatus.Failed)
                    continue;

                UnityEngine.Debug.LogError(
                    $"[WorldBuild] Stopping RunRange after failed stage {id}.",
                    context.WorldBuilder);
                yield break;
            }
        }

        public IEnumerator RunStage(
            WorldBuildContext context,
            WorldBuildStageId stageId,
            bool includeMissingPrerequisites = true)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!_stages.TryGetValue(stageId, out var stage))
                throw new ArgumentOutOfRangeException(nameof(stageId), stageId, "Stage not registered.");

            if (includeMissingPrerequisites)
            {
                var prereqRun = RunMissingPrerequisites(context, stage.Descriptor);
                while (prereqRun.MoveNext())
                    yield return prereqRun.Current;

                if (!AreHardPrerequisitesSatisfied(stage.Descriptor))
                {
                    var record = _records[stageId];
                    record.Status = WorldBuildStageStatus.Failed;
                    record.Messages.Add("Hard prerequisites were not satisfied.");
                    UnityEngine.Debug.LogError(
                        $"[WorldBuild] Skipping stage {stageId}; hard prerequisites not satisfied.",
                        context.WorldBuilder);
                    yield break;
                }
            }

            var execute = ExecuteStage(context, stage);
            while (execute.MoveNext())
                yield return execute.Current;
        }

        private bool AreHardPrerequisitesSatisfied(WorldBuildStageDescriptor descriptor)
        {
            var prereqs = descriptor.HardPrerequisites;
            for (var i = 0; i < prereqs.Count; i++)
            {
                if (!IsSatisfied(_records[prereqs[i]]))
                    return false;
            }

            return true;
        }

        public void Invalidate(WorldBuildStageId stageId, string reason)
        {
            if (!_records.TryGetValue(stageId, out var record))
                return;

            MarkStale(record, reason);

            if (!_stages.TryGetValue(stageId, out var stage))
                return;

            var invalidates = stage.Descriptor.Invalidates;
            for (var i = 0; i < invalidates.Length; i++)
                Invalidate(invalidates[i], reason);
        }

        private IEnumerator RunMissingPrerequisites(
            WorldBuildContext context,
            WorldBuildStageDescriptor descriptor)
        {
            var prereqs = descriptor.HardPrerequisites;
            for (var i = 0; i < prereqs.Count; i++)
            {
                var prereqId = prereqs[i];
                if (IsSatisfied(_records[prereqId]))
                    continue;

                var run = RunStage(context, prereqId, includeMissingPrerequisites: true);
                while (run.MoveNext())
                    yield return run.Current;

                if (!IsSatisfied(_records[prereqId]))
                {
                    UnityEngine.Debug.LogError(
                        $"[WorldBuild] Prerequisite {prereqId} did not succeed; skipping dependents of {descriptor.Id}.",
                        context.WorldBuilder);
                    yield break;
                }
            }
        }

        private IEnumerator ExecuteStage(WorldBuildContext context, IWorldBuildStage stage)
        {
            var id = stage.Descriptor.Id;
            var record = _records[id];
            record.Status = WorldBuildStageStatus.Running;
            record.Progress01 = 0f;
            record.InvalidationReason = null;

            var sw = Stopwatch.StartNew();
            WorldStateStageTransaction transaction = null;
            if (context.StateRecorder != null)
            {
                transaction = context.StateRecorder.BeginStage(
                    id,
                    context.Scope,
                    context.StateManager,
                    context.StateManager != null ? context.StateManager.Header : null,
                    context.Session.Seed);
            }

            var enumerator = BeginStage(context, stage, out var beginError);
            if (beginError != null)
            {
                transaction?.Dispose();
                FinishStageTiming(record, sw);
                HandleStageFault(context, record, beginError);
                yield break;
            }

            while (true)
            {
                object current = null;
                var moved = false;
                Exception fault = null;
                try
                {
                    context.Cancellation.ThrowIfRequested();
                    moved = enumerator.MoveNext();
                    if (moved)
                        current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    fault = ex;
                }

                if (fault != null)
                {
                    transaction?.Dispose();
                    FinishStageTiming(record, sw);
                    HandleStageFault(context, record, fault);
                    if (fault is OperationCanceledException)
                        throw fault;
                    yield break;
                }

                if (!moved)
                    break;

                yield return current;
            }

            if (transaction != null)
            {
                if (!transaction.TryCommit(out _, out var report))
                {
                    transaction.Dispose();
                    FinishStageTiming(record, sw);
                    var message = report != null && report.Errors.Count > 0
                        ? string.Join("; ", report.Errors)
                        : "WorldState stage commit failed.";
                    HandleStageFault(context, record, new WorldBuildStageException(id, message));
                    yield break;
                }

                transaction.Dispose();
            }

            if (record.Status == WorldBuildStageStatus.Running)
                record.Status = WorldBuildStageStatus.Succeeded;
            if (record.Progress01 < 1f)
                record.Progress01 = 1f;

            FinishStageTiming(record, sw);
        }

        private static IEnumerator BeginStage(
            WorldBuildContext context,
            IWorldBuildStage stage,
            out Exception error)
        {
            error = null;
            try
            {
                context.Cancellation.ThrowIfRequested();
                return stage.Execute(context);
            }
            catch (Exception ex)
            {
                error = ex;
                return null;
            }
        }

        private static void FinishStageTiming(WorldBuildStageRecord record, Stopwatch sw)
        {
            sw.Stop();
            record.DurationSeconds = (float)sw.Elapsed.TotalSeconds;
        }

        private static void HandleStageFault(
            WorldBuildContext context,
            WorldBuildStageRecord record,
            Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                record.Status = WorldBuildStageStatus.Cancelled;
                record.Messages.Add("Cancelled.");
                return;
            }

            record.Status = WorldBuildStageStatus.Failed;
            record.Messages.Add(ex.Message);
            if (ex.InnerException != null)
                record.Messages.Add("Inner: " + ex.InnerException.Message);

            if (ex is WorldBuildStageException stageEx)
            {
                UnityEngine.Debug.LogError(
                    $"[WorldBuild] Stage {stageEx.Stage} failed: {stageEx.Message}",
                    context.WorldBuilder);
                if (stageEx.InnerException != null)
                    UnityEngine.Debug.LogException(stageEx.InnerException, context.WorldBuilder);
                return;
            }

            UnityEngine.Debug.LogException(ex, context.WorldBuilder);
            if (ex.InnerException != null)
                UnityEngine.Debug.LogException(ex.InnerException, context.WorldBuilder);
        }

        private static bool IsSatisfied(WorldBuildStageRecord record)
        {
            return record.Status is WorldBuildStageStatus.Succeeded
                or WorldBuildStageStatus.Warning
                or WorldBuildStageStatus.SkippedCached;
        }

        private static void MarkStale(WorldBuildStageRecord record, string reason)
        {
            record.Status = WorldBuildStageStatus.Stale;
            record.InvalidationReason = reason ?? "Invalidated";
            record.Progress01 = 0f;
        }
    }
}
