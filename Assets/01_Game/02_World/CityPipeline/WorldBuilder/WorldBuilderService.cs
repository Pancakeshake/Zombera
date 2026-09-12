using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Scene-visible World Builder facade: profile wiring, terrain/hydrology services,
    /// pipeline runner, and <see cref="IWorldGenerationBackend"/>.
    /// </summary>
    [AddComponentMenu("Zombera/World/World Builder Service")]
    [DisallowMultipleComponent]
    public sealed partial class WorldBuilderService : MonoBehaviour, IWorldGenerationBackend
    {
        [SerializeField] private WorldGenerationProfile _profile;
        [SerializeField] private WorldTileCatalog _tileCatalog;
        [SerializeField] private WorldSurfacePainter _surfacePainter;
        [SerializeField] private WorldStateManager _stateManager;
        [SerializeField] private MonoBehaviour _oceanWaterRendererSource;
        [SerializeField] private MonoBehaviour _waterRendererSource;
        [SerializeField] private MonoBehaviour _naturePlacerSource;
        [SerializeField] private MonoBehaviour _poiSinkSource;
        [SerializeField] private MonoBehaviour _environmentBackendSource;
        [SerializeField] private MonoBehaviour _weatherSource;

        private IOceanWaterRenderer _oceanWaterRenderer;
        private IWorldWaterRenderer _waterRenderer;
        private IWorldNaturePlacer _naturePlacer;
        private IWorldPoiSink _poiSink;
        private IWorldEnvironmentBackend _environmentBackend;
        private IWorldWeatherSource _weather;

        private WorldTerrainQueryService _terrainQuery;
        private WorldTerrainMutationService _terrainMutation;
        private WorldHydrologyQueryService _hydrologyQuery;
        private WorldBuildPipelineRunner _runner;
        private WorldBuildArtifacts _artifacts;
        private WorldStateGenerationRecorder _stateRecorder;
        private bool _isGenerating;
        private float _progress01;
        private bool _hasBuildSession;
        private ulong _sessionProfileFingerprint;
        private WorldStateLoadPackage _queuedLoadPackage;
        private bool _queuedLegacyNoWorldState;
        private string _queuedLegacyNoWorldStateReason = string.Empty;

        private CityRegionAsset _sessionCityRegion;
        private CityPrefabRoadNetworkBuilder _sessionCityBuilder;
        private bool _hasCityPlanSnapshot;
        private bool _savedRegionModeEnabled;
        private int _savedFixedRegionSeedOverride;
        private bool _savedReuseCachedRoadsOnSameSeed;

        public WorldGenerationProfile Profile => _profile;
        public WorldTileCatalog TileCatalog => _tileCatalog;
        public WorldSurfacePainter SurfacePainter => _surfacePainter;
        public WorldStateManager StateManager => _stateManager;
        public WorldStateGenerationRecorder StateRecorder
        {
            get
            {
                if (_stateRecorder == null)
                    _stateRecorder = new WorldStateGenerationRecorder(_stateManager);
                return _stateRecorder;
            }
        }

        public IOceanWaterRenderer OceanWaterRenderer => _oceanWaterRenderer;
        public IWorldWaterRenderer WaterRenderer => _waterRenderer;
        public IWorldNaturePlacer NaturePlacer => _naturePlacer;
        public IWorldPoiSink PoiSink => _poiSink;
        public IWorldEnvironmentBackend EnvironmentBackend => _environmentBackend;
        public IWorldWeatherSource WeatherSource => _weather;

        public IWorldTerrainQuery TerrainQuery => _terrainQuery;
        public IWorldTerrainMutation TerrainMutation => _terrainMutation;
        public IWorldHydrologyQuery HydrologyQuery => _hydrologyQuery;

        public WorldBuildPipelineRunner Runner => _runner;
        public WorldMapSession Session { get; private set; }
        public WorldBuildArtifacts Artifacts => _artifacts ??= new WorldBuildArtifacts();
        public IReadOnlyDictionary<WorldBuildStageId, WorldBuildStageRecord> LastReport =>
            _runner != null ? _runner.Records : null;

        public WorldTileStreamSource TileStream => _tileCatalog;
        public bool IsGenerating => _isGenerating;
        public float Progress01 => _progress01;

        private void Awake()
        {
            EnsureServices();
            ValidateBindings(log: true);
        }

        private void OnValidate() => ValidateBindings(log: false);

        public void SetSession(WorldMapSession session) => Session = session;

        public void SetProfile(WorldGenerationProfile generationProfile) => _profile = generationProfile;

        public void SetStateManager(WorldStateManager stateManager) => _stateManager = stateManager;

        /// <summary>
        /// Ensures one artifact bag and fresh WorldState for the build session.
        /// Same session key is a no-op; a changed key clears the prior session plan/artifacts/state.
        /// </summary>
        public void EnsureBuildSession(WorldMapSession session, WorldGenerationProfile profile)
        {
            EnsureServices();
            if (profile != null)
                _profile = profile;

            WorldBuilderStackBootstrap.Ensure(this, _sessionCityBuilder, _profile);

            var fingerprint = WorldProfileFingerprints.Compute(profile);

            if (_hasBuildSession &&
                SameSessionKey(Session, session) &&
                _sessionProfileFingerprint == fingerprint)
            {
                TileCatalog?.SanitizeDestroyedTerrains();
                return;
            }

            if (_hasBuildSession)
            {
                ClearSessionCityPlan();
                ResetArtifacts(WorldResetMode.TerrainAndContent);
                _stateManager?.Clear(WorldStateClearReason.SessionReset);
            }

            Session = session;
            _hasBuildSession = true;
            _sessionProfileFingerprint = fingerprint;
            TileCatalog?.Configure(session);
            _ = Artifacts;
            _ = StateRecorder;
            if (_queuedLoadPackage != null || _queuedLegacyNoWorldState)
                return;
            CreateFreshStateForSession(session, _profile);
        }

        public void QueueLoadedWorldState(WorldStateLoadPackage package)
        {
            _queuedLoadPackage = package ?? throw new System.ArgumentNullException(nameof(package));
            _queuedLegacyNoWorldState = false;
            _queuedLegacyNoWorldStateReason = string.Empty;
        }

        public void QueueLegacyNoWorldState(string reason)
        {
            _queuedLoadPackage = null;
            _queuedLegacyNoWorldState = true;
            _queuedLegacyNoWorldStateReason = reason ?? string.Empty;
        }

        private void CreateFreshStateForSession(WorldMapSession session, WorldGenerationProfile profile)
        {
            if (_stateManager == null)
                return;

            var fingerprint = profile != null
                ? profile.ComputeFingerprint().ToString("x16")
                : string.Empty;

            var header = new WorldStateHeader
            {
                schemaVersion = WorldStateSchema.CurrentVersion,
                canonicalFormatVersion = WorldStateSchema.CanonicalFormatVersion,
                idAlgorithmVersion = WorldStateSchema.IdAlgorithmVersion,
                generatorId = "WorldBuilder",
                generatorVersion = 1,
                worldSeed = session.Seed,
                mapSizeTier = session.Tier,
                profileVersion = profile != null ? profile.ProfileVersion : session.ProfileVersion,
                profileFingerprint = fingerprint,
                planFingerprint = string.Empty,
                worldOriginXZ = session.WorldOriginXZ,
                tilesPerSide = session.TilesPerSide,
                tileSizeMeters = session.TileSizeMeters,
                worldBoundsXZ = session.WorldBoundsXZ
            };

            if (!_stateManager.TryCreateFresh(header, out var report) &&
                report != null &&
                report.Errors.Count > 0)
            {
                Debug.LogError(
                    "[WorldBuilderService] Failed to create fresh WorldState: " +
                    string.Join("; ", report.Errors),
                    this);
            }
        }

        public void ResetArtifacts(WorldResetMode mode)
        {
            if (_artifacts == null)
            {
                _artifacts = new WorldBuildArtifacts();
                return;
            }

            _artifacts.SetLandforms(null);
            _artifacts.SetOrogen(null);
            _artifacts.SetHydrology(null);
            _artifacts.SetBiomes(null);
            _artifacts.SetSites(null);
            _artifacts.SetCityPads(null);
            _artifacts.SetRoads(null);
            _artifacts.SetCrossings(null);
            _artifacts.SetTunnels(null);
            _artifacts.SetCityGeneratedRoads(null);
            if (mode == WorldResetMode.TerrainAndContent)
                _artifacts.SetPlan(null);
        }

        /// <summary>
        ///     Binds a session CityRegion onto <paramref name="builder"/> without replacing
        ///     the authored inspector SO. Template prefers builder.RegionAsset, then profile.
        ///     Empty plans fall back to scatter into the current world session bounds.
        /// </summary>
        public void ApplyCitySitePlan(
            CityPrefabRoadNetworkBuilder builder,
            WorldSitePlan plan,
            Rect? siteBoundsOverride = null)
        {
            if (builder == null)
                throw new System.ArgumentNullException(nameof(builder));
            if (plan == null)
                throw new System.ArgumentNullException(nameof(plan));

            var template = builder.RegionAsset != null ? builder.RegionAsset : _profile?.CityRegion;
            if (template == null)
            {
                throw new System.InvalidOperationException(
                    "CityPrefabRoadNetworkBuilder.RegionAsset or WorldGenerationProfile.CityRegion is required.");
            }

            if (!_hasBuildSession)
                throw new System.InvalidOperationException(
                    "EnsureBuildSession must run before ApplyCitySitePlan.");

            ClearSessionCityPlan();

            var planCount = plan.CitySites != null ? plan.CitySites.Count : 0;
            CityRegionAsset sessionRegion;
            int roadsSeed;

            if (planCount > 0)
            {
                var constrainBounds = siteBoundsOverride ?? Session.WorldBoundsXZ;
                sessionRegion = WorldSitePlanCityRegionAdapter.CreateSessionRegion(
                    template,
                    plan,
                    Session,
                    _profile?.MapSizeSettings,
                    constrainBounds,
                    out roadsSeed,
                    _profile?.Landforms);
            }
            else
            {
                roadsSeed = WorldSubsystemSeeds.Derive(
                    Session.Seed, Session.ProfileVersion, WorldSubsystemSeeds.Roads);
                sessionRegion = Object.Instantiate(template);
                sessionRegion.name = template.name + "_Session";
                sessionRegion.hideFlags = HideFlags.HideAndDontSave;
                sessionRegion.ConfigureScatterForSession(Session, _profile?.MapSizeSettings, _profile?.Landforms);
                sessionRegion.autoScatterOnBuild = true;
                sessionRegion.regionSeed = roadsSeed;
                sessionRegion.scatterSeed = roadsSeed;
                sessionRegion.randomizeRegionSeedPerBuild = false;
                if (sessionRegion.scatterSiteCount < 1)
                    sessionRegion.scatterSiteCount = Mathf.Max(1, sessionRegion.SiteCount);
            }

            _savedRegionModeEnabled = builder.RegionModeEnabled;
            _savedFixedRegionSeedOverride = builder.FixedRegionSeedOverride;
            _savedReuseCachedRoadsOnSameSeed = builder.ReuseCachedRoadsOnSameSeed;
            _hasCityPlanSnapshot = true;
            _sessionCityBuilder = builder;
            _sessionCityRegion = sessionRegion;

            // Keep authored RegionAsset in the inspector; bind session via override.
            builder.SetSessionRegionOverride(sessionRegion);
            builder.RegionModeEnabled = true;
            builder.FixedRegionSeedOverride = roadsSeed;
            builder.ReuseCachedRoadsOnSameSeed = false;
        }

        /// <summary>
        /// Restores the builder's prior region toggles and destroys the transient session override.
        /// </summary>
        public void ClearSessionCityPlan()
        {
            if (_sessionCityBuilder != null && _hasCityPlanSnapshot)
            {
                _sessionCityBuilder.DetachSessionRegionOverride();
                _sessionCityBuilder.RegionModeEnabled = _savedRegionModeEnabled;
                _sessionCityBuilder.FixedRegionSeedOverride = _savedFixedRegionSeedOverride;
                _sessionCityBuilder.ReuseCachedRoadsOnSameSeed = _savedReuseCachedRoadsOnSameSeed;
            }

            _hasCityPlanSnapshot = false;
            _sessionCityBuilder = null;

            if (_sessionCityRegion != null)
            {
                WorldSitePlanCityRegionAdapter.DestroySessionRegion(_sessionCityRegion);
                _sessionCityRegion = null;
            }
        }

        public void BindArtifactFields(LandformField landforms, BiomeField biomes, HydrologyPlan hydrology)
        {
            EnsureServices();
            _terrainQuery.Bind(landforms, biomes, hydrology, _profile);
            _hydrologyQuery.Bind(hydrology);
            _terrainMutation.BindSurfacePainter(_surfacePainter);
        }

        private void ApplyQueuedLoadOrLegacyMode(WorldMapSession session)
        {
            ApplyQueuedLoadOrLegacyModeReturningLoaded(session);
        }

        private void ApplyQueuedLoadPackage(WorldMapSession session, WorldStateLoadPackage package)
        {
            if (_stateManager == null)
                throw new System.InvalidOperationException("WorldStateManager is required to load WorldState.");
            ValidateQueuedLoadPackage(session, package);
            if (!_stateManager.TryLoad(package.CreateStateCopy(), out var report))
            {
                throw new System.InvalidOperationException(
                    "WorldState load package failed validation: " +
                    string.Join("; ", report.Errors));
            }
        }

        private void ValidateQueuedLoadPackage(WorldMapSession session, WorldStateLoadPackage package)
        {
            if (package.WorldSeed != session.Seed)
                throw new System.InvalidOperationException("Queued WorldState seed does not match the session.");
            if (package.Tier != session.Tier)
                throw new System.InvalidOperationException("Queued WorldState tier does not match the session.");
            if (package.ProfileVersion != session.ProfileVersion)
                throw new System.InvalidOperationException("Queued WorldState profile version does not match the session.");
            if (package.GraphVersion != Zombera.World.ProceduralWorldSession.FirstPartyGraphVersion)
                throw new System.InvalidOperationException("Queued WorldState graph version is not first-party.");
            ValidateQueuedProfileFingerprint(package);
            ValidateQueuedPlanFingerprint(package);
        }

        private void ValidateQueuedProfileFingerprint(WorldStateLoadPackage package)
        {
            if (_profile == null || string.IsNullOrEmpty(package.ProfileFingerprint))
                return;

            var current = _profile.ComputeFingerprint().ToString("x16");
            if (!string.Equals(package.ProfileFingerprint, current, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Queued WorldState profile fingerprint does not match.");
        }

        private static void ValidateQueuedPlanFingerprint(WorldStateLoadPackage package)
        {
            if (string.IsNullOrEmpty(package.PlanFingerprint) ||
                Zombera.World.ProceduralWorldSession.PlanFingerprint == 0UL)
            {
                return;
            }

            var current = Zombera.World.ProceduralWorldSession.PlanFingerprint.ToString("x16");
            if (!string.Equals(package.PlanFingerprint, current, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Queued WorldState plan fingerprint does not match.");
        }

        public IEnumerator ResetScope(WorldBuildScope scope, WorldResetMode mode)
        {
            _isGenerating = true;
            EnsureServices();
            ValidateBindings(log: false);

            // Water TearDown already handled by ResetGeneratedWorldStage for TerrainAndContent.
            if (mode == WorldResetMode.ContentOnly)
            {
                _waterRenderer?.Clear(scope);
                _oceanWaterRenderer?.ClearOcean(scope);
            }

            _naturePlacer?.Clear(scope);
            _poiSink?.Clear(scope);

            _isGenerating = false;
            yield break;
        }

        private static bool SameSessionKey(WorldMapSession a, WorldMapSession b)
        {
            return a.Seed == b.Seed
                   && a.Tier == b.Tier
                   && a.ProfileVersion == b.ProfileVersion
                   && a.TilesPerSide == b.TilesPerSide
                   && Mathf.Approximately(a.TileSizeMeters, b.TileSizeMeters)
                   && Mathf.Approximately(a.WorldOriginXZ.x, b.WorldOriginXZ.x)
                   && Mathf.Approximately(a.WorldOriginXZ.y, b.WorldOriginXZ.y);
        }

        public void RequestTiles(IReadOnlyList<WorldTileCoord> coords)
        {
            _tileCatalog?.RequestTiles(coords);
        }

        public void ReleaseTiles(IReadOnlyList<WorldTileCoord> coords)
        {
            if (_tileCatalog == null || coords == null) return;
            for (var i = 0; i < coords.Count; i++)
                _tileCatalog.Invalidate(coords[i]);
        }

        public void ReportNavigationResult(WorldTileCoord coord, bool succeeded)
        {
            if (_tileCatalog == null || !succeeded) return;
            _tileCatalog.TryTransition(coord, WorldTileState.ContentReady, WorldTileState.NavigationReady);
        }

        public void ShutdownSession()
        {
            ClearSessionCityPlan();
            ResetArtifacts(WorldResetMode.TerrainAndContent);
            _stateManager?.Clear(WorldStateClearReason.Shutdown);
            _hasBuildSession = false;
            _sessionProfileFingerprint = 0;
            _isGenerating = false;
            _progress01 = 0f;
            Session = default;
        }

        private void EnsureServices()
        {
            _terrainQuery ??= new WorldTerrainQueryService();
            _terrainMutation ??= new WorldTerrainMutationService();
            _hydrologyQuery ??= new WorldHydrologyQueryService();
            _runner ??= new WorldBuildPipelineRunner();
            if (_stateRecorder == null)
                _stateRecorder = new WorldStateGenerationRecorder(_stateManager);
            _terrainMutation.BindSurfacePainter(_surfacePainter);
        }

        private void ValidateBindings(bool log)
        {
            _oceanWaterRenderer = CastSource<IOceanWaterRenderer>(
                _oceanWaterRendererSource, nameof(_oceanWaterRendererSource), log);
            _waterRenderer = CastSource<IWorldWaterRenderer>(_waterRendererSource, nameof(_waterRendererSource), log);
            _naturePlacer = CastSource<IWorldNaturePlacer>(_naturePlacerSource, nameof(_naturePlacerSource), log);
            _poiSink = CastSource<IWorldPoiSink>(_poiSinkSource, nameof(_poiSinkSource), log);
            _environmentBackend = CastSource<IWorldEnvironmentBackend>(
                _environmentBackendSource, nameof(_environmentBackendSource), log);
            _weather = CastSource<IWorldWeatherSource>(_weatherSource, nameof(_weatherSource), log);

            if (!log) return;
            if (_profile == null)
                Debug.LogWarning("[WorldBuilderService] Profile is not assigned.", this);
            if (_tileCatalog == null)
                Debug.LogWarning("[WorldBuilderService] Tile catalog is not assigned.", this);
            if (_surfacePainter == null)
                Debug.LogWarning("[WorldBuilderService] Surface painter is not assigned.", this);
            if (_stateManager == null)
                Debug.LogWarning("[WorldBuilderService] State manager is not assigned.", this);
        }

        private T CastSource<T>(MonoBehaviour source, string fieldName, bool log) where T : class
        {
            if (source == null) return null;
            if (source is T typed) return typed;
            if (log)
            {
                Debug.LogError(
                    $"[WorldBuilderService] '{source.name}' assigned to {fieldName} does not implement {typeof(T).Name}.",
                    this);
            }

            return null;
        }
    }
}
