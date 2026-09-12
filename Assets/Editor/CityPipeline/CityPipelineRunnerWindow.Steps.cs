#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    /// Pipeline definition for <see cref="CityPipelineRunnerWindow" />.
    /// Stage list comes from <see cref="WorldBuildStageRegistry"/> (dependency-band sections).
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow : EditorWindow
    {
        /// <summary>One executable pipeline stage, grouped under a section.</summary>
        private sealed class PipelineStage
        {
            public string Section;
            public string Name;
            public WorldBuildStageId StageId;
            public Func<IEnumerator> Start;
            public StepStatus Status;
            public long DurationMs;
            public bool HasErrors;
            public bool HasWarnings;
            public readonly List<string> Logs = new();
        }

        private WorldBuildPipelineRunner _pipelineRunner;
        private WorldBuildCancellation _runCancellation;
        private string _lastPipelineStageLog = string.Empty;

        internal string GetLastPipelineStageLog() => _lastPipelineStageLog ?? string.Empty;

        internal IEnumerator EnumeratePipelineUpTo(WorldBuildStageId lastStageInclusive)
        {
            if (!TryResolveBuilder())
                throw new InvalidOperationException("CityPrefabRoadNetworkBuilder is not assigned.");

            EnsureStepsFresh();
            _ = BuildEditorContext();
            _pipelineRunner ??= new WorldBuildPipelineRunner(WorldBuildStageRegistry.Default);
            return RunRegistryStagesUpTo(lastStageInclusive, fastRoadsMode);
        }

        private void RebuildSteps()
        {
            _steps.Clear();
            _sections.Clear();
            _sessionLog.Add("— pipeline steps reset —");
            _builtForBuilder = builder;
            _pipelineRunner = new WorldBuildPipelineRunner(WorldBuildStageRegistry.Default);
            _runCancellation = null;

            var registry = WorldBuildStageRegistry.Default;
            for (var i = 0; i < registry.Stages.Count; i++)
            {
                var descriptor = registry.Stages[i];
                var stageId = descriptor.Id;
                AddStep(descriptor.Section, FormatStepName(descriptor), stageId, () => RunRegistryStage(stageId));
            }

            _stepIndex = 0;
            _stopAfterIndex = _steps.Count - 1;
        }

        private string FormatStepName(WorldBuildStageDescriptor descriptor)
        {
            var name = descriptor.DisplayName;
            if (builder != null && builder.RegionModeActive &&
                (descriptor.Id == WorldBuildStageId.GenerateNamedAreas ||
                 descriptor.Id == WorldBuildStageId.GenerateCityFootpaths ||
                 descriptor.Id == WorldBuildStageId.GenerateDistrictLots ||
                 descriptor.Id == WorldBuildStageId.PlaceBuildings))
            {
                var site = builder.ActiveSite;
                if (site != null && !string.IsNullOrWhiteSpace(site.displayName))
                    name += " (" + site.displayName + ")";
            }

            return name;
        }

        private IEnumerator RunRegistryStage(WorldBuildStageId stageId)
        {
            if (stageId == WorldBuildStageId.ResetGeneratedWorld)
            {
                var prep = EditorPreResetRoutine(builder);
                while (prep.MoveNext())
                    yield return prep.Current;
            }

            var context = BuildEditorContext();
            _pipelineRunner ??= new WorldBuildPipelineRunner(WorldBuildStageRegistry.Default);
            var run = _pipelineRunner.RunStage(context, stageId, includeMissingPrerequisites: false);
            while (run.MoveNext())
                yield return run.Current;

            if (_pipelineRunner.Records.TryGetValue(stageId, out var record) &&
                record.Status == WorldBuildStageStatus.Failed)
            {
                throw new WorldBuildStageException(stageId, string.Join("; ", record.Messages));
            }
        }

        private WorldBuildContext BuildEditorContext()
        {
            var service = WorldBuilderStackProvisioner.EnsureForBuilder(builder);
            if (service == null)
                throw new InvalidOperationException("WorldBuilderService could not be provisioned for the city builder.");

            var profile = worldProfile != null
                ? worldProfile
                : service.Profile;

            var mapSize = profile != null ? profile.MapSizeSettings : null;
            var tilesPerSide = mapSize != null
                ? mapSize.GetTilesPerSide(editorMapTier)
                : editorMapTier switch
                {
                    WorldMapSizeTier.Small => 4,
                    WorldMapSizeTier.Large => 16,
                    _ => 8
                };

            var tileSize = WorldMapSizeSettings.TileSizeMeters;
            WorldMapSession session;
            if (mapSize != null)
            {
                session = mapSize.CreateSession(
                    editorMapTier,
                    editorWorldSeed == 0 ? 1 : editorWorldSeed,
                    profile != null ? profile.ProfileVersion : 1);
            }
            else
            {
                session = WorldMapSession.CreateWithOceanRing(
                    editorMapTier,
                    editorWorldSeed == 0 ? 1 : editorWorldSeed,
                    profile != null ? profile.ProfileVersion : 1,
                    Vector2.zero,
                    editorMapTier == WorldMapSizeTier.Large ? 10 : tilesPerSide,
                    editorMapTier == WorldMapSizeTier.Large ? 3 : 0,
                    tileSize);
            }

            service.EnsureBuildSession(session, profile);
            _runCancellation ??= new WorldBuildCancellation();

            var options = new WorldBuildRunOptions
            {
                RoadBuildQuality = roadBuildQuality,
                FastRoads = fastRoadsMode,
                // Paint mode is independent of Fast Roads headless sync.
                SurfacePaintQuality = hubSurfacePaintQuality,
                FastLandforms = false,
                FastBiomeClassify = false,
                ReuseCachedRoads = builder != null && builder.ReuseCachedRoadsOnSameSeed,
                // Hub Reset must destroy WorldTerrainGrid tiles + water, not only city props.
                ResetMode = WorldResetMode.TerrainAndContent
            };

            return new WorldBuildContext(
                session,
                WorldBuildScope.FullMap(session.WorldBoundsXZ),
                profile,
                service,
                builder,
                service.Artifacts,
                service.StateManager,
                service.StateRecorder,
                options,
                HubWorldBuildProgress.Instance,
                _runCancellation);
        }

        internal WorldBuildContext CreateHubBuildContext() => BuildEditorContext();

        /// <summary>
        ///     Runs registry-ordered hub stages from Reset through <paramref name="lastStageInclusive"/>.
        ///     Safe for Unity MCP script-execute and automated editor tests.
        /// </summary>
        public bool TryRunPipelineUpToStageSynchronously(
            WorldBuildStageId lastStageInclusive,
            out string error,
            float timeoutSeconds = 900f) =>
            TryRunPipelineStageRangeSynchronously(
                WorldBuildStageId.ResetGeneratedWorld,
                lastStageInclusive,
                out error,
                timeoutSeconds);

        /// <summary>
        /// Roads/bridges/tunnels acceptance: FullFidelity, Fast*=off, cache off, locked seed,
        /// Reset through <see cref="WorldBuildStageId.SyncInfrastructureSurfaces"/>.
        /// </summary>
        public bool TryRunRoadsInfrastructureAcceptanceSynchronously(
            out string error,
            float timeoutSeconds = 900f)
        {
            error = null;
            if (!TryResolveBuilder())
            {
                error = "CityPrefabRoadNetworkBuilder is not assigned.";
                return false;
            }

            EnsureStepsFresh();
            ApplyRoadsInfrastructureAcceptanceConfiguration();
            return TryRunPipelineUpToStageSynchronously(
                WorldBuildStageId.SyncInfrastructureSurfaces,
                out error,
                timeoutSeconds);
        }

        /// <summary>
        ///     Runs registry-ordered hub stages from <paramref name="firstStageInclusive"/>
        ///     through <paramref name="lastStageInclusive"/>.
        /// </summary>
        public bool TryRunPipelineStageRangeSynchronously(
            WorldBuildStageId firstStageInclusive,
            WorldBuildStageId lastStageInclusive,
            out string error,
            float timeoutSeconds = 900f)
        {
            error = null;
            if (!TryResolveBuilder())
            {
                error = "CityPrefabRoadNetworkBuilder is not assigned.";
                return false;
            }

            if (!WorldBuildStageOrder.IsBeforeOrEqual(firstStageInclusive, lastStageInclusive))
            {
                error = "Invalid stage range: " + firstStageInclusive + " must precede " + lastStageInclusive +
                        " in the registry.";
                return false;
            }

            EnsureStepsFresh();
            _ = BuildEditorContext();
            _pipelineRunner ??= new WorldBuildPipelineRunner(WorldBuildStageRegistry.Default);
            var routine = RunRegistryStagesFromTo(firstStageInclusive, lastStageInclusive, fastRoadsMode);
            return TryPumpCoroutineSynchronously(routine, timeoutSeconds, out error);
        }

        private IEnumerator RunRegistryStagesUpTo(
            WorldBuildStageId lastStageInclusive,
            bool fastRoads) =>
            RunRegistryStagesFromTo(WorldBuildStageId.ResetGeneratedWorld, lastStageInclusive, fastRoads);

        private IEnumerator RunRegistryStagesFromTo(
            WorldBuildStageId firstStageInclusive,
            WorldBuildStageId lastStageInclusive,
            bool fastRoads)
        {
            var registry = WorldBuildStageRegistry.Default;
            var log = new System.Text.StringBuilder();
            var pipelineWatch = Stopwatch.StartNew();
            var stageCount = 0;
            var started = false;
            WorldBuilderHubPerfReportWriter.BeginRun();
            for (var i = 0; i < registry.Stages.Count; i++)
            {
                var descriptor = registry.Stages[i];
                var stageId = descriptor.Id;
                if (!started)
                {
                    if (stageId != firstStageInclusive)
                        continue;

                    started = true;
                }

                var stageWatch = Stopwatch.StartNew();
                log.AppendLine(stageId + ": start");
                _lastPipelineStageLog = log.ToString();
                var run = RunRegistryStage(stageId);

                if (fastRoads)
                {
                    while (run.MoveNext())
                    {
                    }
                }
                else
                {
                    while (run.MoveNext())
                        yield return run.Current;
                }

                stageWatch.Stop();
                stageCount++;
                var durationMs = stageWatch.ElapsedMilliseconds;
                log.AppendLine(stageId + ": ok " + stageWatch.Elapsed.TotalSeconds.ToString("F1") + "s");
                _lastPipelineStageLog = log.ToString();
                WorldBuilderHubPerfReportWriter.RecordStage(stageId, descriptor.DisplayName, durationMs);
                Debug.Log(
                    "[WorldBuilderHub] stage=" + stageId +
                    " name=\"" + descriptor.DisplayName + "\"" +
                    " durationMs=" + durationMs);

                if (stageId == lastStageInclusive)
                {
                    pipelineWatch.Stop();
                    WorldBuilderHubPerfReportWriter.WriteCompleted(
                        pipelineWatch.ElapsedMilliseconds,
                        lastStageInclusive);
                    Debug.Log(
                        "[WorldBuilderHub] pipeline=" + firstStageInclusive +
                        ".." + lastStageInclusive +
                        " totalMs=" + pipelineWatch.ElapsedMilliseconds +
                        " stageCount=" + stageCount);
                    yield break;
                }
            }

            _lastPipelineStageLog = log.ToString();
            if (!started)
            {
                throw new InvalidOperationException(
                    "Stage '" + firstStageInclusive + "' was not found in the world-build registry.");
            }

            throw new InvalidOperationException(
                "Stage '" + lastStageInclusive + "' was not found after '" + firstStageInclusive + "'.");
        }

        internal bool TryRunFullPipelineSynchronously(out string error)
        {
            error = null;
            if (!TryResolveBuilder())
            {
                error = "CityPrefabRoadNetworkBuilder is not assigned.";
                return false;
            }

            EnsureStepsFresh();
            ApplyVerticalSliceTestConfiguration();
            EnableFastIterationMode();
            var context = BuildEditorContext();
            _pipelineRunner ??= new WorldBuildPipelineRunner(WorldBuildStageRegistry.Default);
            var routine = _pipelineRunner.RunRange(
                context,
                WorldBuildStageId.ResetGeneratedWorld,
                WorldBuildStageId.ValidateAndPublish,
                includeMissingPrerequisites: true);
            return TryPumpCoroutineSynchronously(routine, 900f, out error);
        }

        /// <summary>
        /// Editor-only terrain/MapMagic wait before the registered reset stage.
        /// Does not clear city content — that remains owned by <see cref="ResetGeneratedWorldStage"/>.
        /// </summary>
        private static IEnumerator EditorPreResetRoutine(CityPrefabRoadNetworkBuilder target)
        {
            if (target == null)
                yield break;

            var stepWatch = Stopwatch.StartNew();

            var terrainWatch = Stopwatch.StartNew();
            target.ResetCityTerrainFootprint();
            var paintMs = terrainWatch.ElapsedMilliseconds;

            var mapMagic = UnityEngine.Object.FindFirstObjectByType<MapMagic.Core.MapMagicObject>();
            var waitMs = 0L;
            if (mapMagic != null)
            {
                var waitWatch = Stopwatch.StartNew();
                var deadline = Time.realtimeSinceStartup + 180f;
                var sawGenerating = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (mapMagic.IsGenerating())
                    {
                        sawGenerating = true;
                        yield return null;
                        continue;
                    }

                    if (sawGenerating || waitWatch.ElapsedMilliseconds > 250)
                        break;

                    yield return null;
                }

                waitMs = waitWatch.ElapsedMilliseconds;
            }

            Debug.Log(
                "[CityPipelineRunnerWindow] Reset pre-step: paintClear=" + paintMs +
                "ms mapMagicWait=" + waitMs +
                "ms, wall=" + stepWatch.ElapsedMilliseconds + "ms");
        }

        private void AddStep(string section, string name, WorldBuildStageId stageId, Func<IEnumerator> start)
        {
            if (!_sections.Contains(section))
                _sections.Add(section);
            _steps.Add(new PipelineStage
            {
                Section = section,
                Name = name,
                StageId = stageId,
                Start = start
            });
        }
    }
}
#endif
