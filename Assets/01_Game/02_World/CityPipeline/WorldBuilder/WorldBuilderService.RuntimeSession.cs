using System.Collections;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Runtime session bootstrap: initial play-area pipeline after catalog configure.
    /// </summary>
    public sealed partial class WorldBuilderService
    {
        [SerializeField]
        [Tooltip("When true, InitializeSession runs the WorldBuilder stage DAG through FinalizeTerrainTiles for the initial play scope.")]
        private bool _runInitialPlayAreaPipeline = true;

        [SerializeField]
        [Tooltip("Use faster landform/surface/road options for play-mode initial generation.")]
        private bool _fastInitialPlayAreaPipeline = true;

        public IEnumerator InitializeSession(WorldMapSession session, WorldBuildScope initialScope)
        {
            EnsureServices();
            EnsureBuildSession(session, _profile);
            Session = session;

            var loadedWorldState = ApplyQueuedLoadOrLegacyModeReturningLoaded(session);
            _isGenerating = true;
            _progress01 = 0f;

            // EnsureBuildSession already Configure()'s the catalog for this session.

            if (!loadedWorldState && _runInitialPlayAreaPipeline && _profile != null && _tileCatalog != null)
            {
                var generate = RunInitialPlayAreaPipeline(session, initialScope);
                while (generate.MoveNext())
                    yield return generate.Current;
            }
            else
            {
                _ = initialScope;
                _progress01 = 1f;
            }

            if (_tileCatalog != null)
                _tileCatalog.RefreshTileMetrics();

            Zombera.World.StreamedWorldMetrics.RecordInitialPlayAreaComplete();
            _isGenerating = false;
            _progress01 = 1f;
        }

        private bool ApplyQueuedLoadOrLegacyModeReturningLoaded(WorldMapSession session)
        {
            if (_queuedLoadPackage != null)
            {
                ApplyQueuedLoadPackage(session, _queuedLoadPackage);
                _queuedLoadPackage = null;
                return true;
            }

            if (!_queuedLegacyNoWorldState)
                return false;

            _stateManager?.MarkLegacyNoWorldState(_queuedLegacyNoWorldStateReason);
            _queuedLegacyNoWorldState = false;
            _queuedLegacyNoWorldStateReason = string.Empty;
            return false;
        }

        private IEnumerator RunInitialPlayAreaPipeline(WorldMapSession session, WorldBuildScope scope)
        {
            var cityBuilder = ResolveCityBuilder();
            WorldBuilderStackBootstrap.Ensure(this, cityBuilder, _profile);

            var options = new WorldBuildRunOptions
            {
                RoadBuildQuality = RoadBuildQualityMode.FullFidelity,
                FastRoads = _fastInitialPlayAreaPipeline,
                // Shipped play-area look matches Hub acceptance soft paint, not Fast block tiles.
                SurfacePaintQuality = _fastInitialPlayAreaPipeline
                    ? SurfacePaintQualityMode.Balanced
                    : SurfacePaintQualityMode.Quality,
                FastLandforms = _fastInitialPlayAreaPipeline,
                FastBiomeClassify = _fastInitialPlayAreaPipeline,
                ReuseCachedRoads = cityBuilder != null && cityBuilder.ReuseCachedRoadsOnSameSeed,
                ResetMode = WorldResetMode.TerrainAndContent
            };

            var progress = new SessionWorldBuildProgress(this);
            var context = new WorldBuildContext(
                session,
                scope,
                _profile,
                this,
                cityBuilder,
                Artifacts,
                _stateManager,
                StateRecorder,
                options,
                progress,
                new WorldBuildCancellation());

            var run = _runner.RunRange(
                context,
                WorldBuildStageId.ValidateWorldProfile,
                WorldBuildStageId.FinalizeTerrainTiles,
                includeMissingPrerequisites: true);

            while (run.MoveNext())
                yield return run.Current;
        }

        private CityPrefabRoadNetworkBuilder ResolveCityBuilder()
        {
            if (_sessionCityBuilder != null)
                return _sessionCityBuilder;

            return GetComponent<CityPrefabRoadNetworkBuilder>()
                   ?? GetComponentInChildren<CityPrefabRoadNetworkBuilder>(true)
                   ?? FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
        }

        private sealed class SessionWorldBuildProgress : IWorldBuildProgress
        {
            private readonly WorldBuilderService _owner;

            public SessionWorldBuildProgress(WorldBuilderService owner) => _owner = owner;

            public void Report(WorldBuildStageId stageId, float progress01, string message)
            {
                _owner._progress01 = Mathf.Clamp01(progress01);
            }

            public void ReportWarning(WorldBuildStageId stageId, string message)
            {
            }
        }
    }
}
