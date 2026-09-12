using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    [AddComponentMenu("Zombera/World/World Building Materializer")]
    [DisallowMultipleComponent]
    public sealed class WorldBuildingMaterializer : MonoBehaviour
    {
        [SerializeField] private WorldStateManager _stateManager;
        [SerializeField] private WorldTileStreamSource _tileStream;
        [SerializeField] private WorldStateViewRegistry _viewRegistry;
        [SerializeField] private MonoBehaviour _archetypeResolverSource;
        [SerializeField] private RuntimePlacedStructureFixer _runtimePlacedStructureFixer;
        [SerializeField] private Transform _viewsRoot;
        [SerializeField] private WorldTileState _minimumTileState = WorldTileState.ContentReady;
        [SerializeField] private bool _logDiagnostics;

        private readonly List<WorldTileInfo> _tileBuffer = new(32);
        private readonly List<WorldTileKey> _loadedTileBuffer = new(32);
        private readonly List<WorldEntityId> _entityIdBuffer = new(64);
        private readonly HashSet<WorldTileKey> _desiredTiles = new();
        private readonly HashSet<string> _loggedMissingArchetypes = new();

        private IBuildingArchetypeResolver _resolver;
        private bool _attemptedFallbackResolve;
        private bool _loggedMissingStateManager;
        private bool _loggedMissingTileStream;
        private bool _loggedMissingRegistry;
        private bool _loggedMissingResolver;
        private bool _hasAppliedStateRevision;
        private long _lastAppliedStateRevision;

        public void Configure(
            WorldStateManager stateManager,
            WorldTileStreamSource tileStream,
            WorldStateViewRegistry registry = null,
            IBuildingArchetypeResolver resolver = null)
        {
            if (stateManager != null)
                _stateManager = stateManager;
            if (tileStream != null)
                _tileStream = tileStream;
            if (registry != null)
                _viewRegistry = registry;
            if (resolver != null)
                SetResolver(resolver);

            _attemptedFallbackResolve = false;
        }

        public int LoadTile(WorldTileKey tile)
        {
            if (!EnsureReady())
                return 0;

            _stateManager.CopyEntityIdsCoveringTile(tile, WorldEntityKind.Building, _entityIdBuffer);
            var loaded = 0;
            for (var i = 0; i < _entityIdBuffer.Count; i++)
                loaded += LoadBuilding(tile, _entityIdBuffer[i]);
            return loaded;
        }

        public bool UnloadTile(WorldTileKey tile)
        {
            return _viewRegistry != null && _viewRegistry.UnregisterLoadedTile(tile, true);
        }

        public void UpdateStreaming() => UpdateStreaming(Vector3.zero);

        public void UpdateStreaming(Vector3 focusPosition)
        {
            _ = focusPosition;
            if (!isActiveAndEnabled || !EnsureReady())
                return;

            if (!_stateManager.HasState)
            {
                DestroyAllViews();
                return;
            }

            ReloadAllViewsIfStateChanged();
            _tileStream.CopyTilesAtOrAbove(_minimumTileState, _tileBuffer);
            BuildDesiredTileSet();
            UnloadUndesiredTiles();
            LoadDesiredTiles();
        }

        public void DestroyAllViews()
        {
            _viewRegistry?.DestroyAllViews();
            _desiredTiles.Clear();
            _loadedTileBuffer.Clear();
            _hasAppliedStateRevision = false;
        }

        public WorldBuildingViewAuditReport AuditLoadedViews()
        {
            var report = WorldBuildingViewAuditor.AuditLoadedViews(_stateManager, _viewRegistry);
            if (_logDiagnostics && report.HasIssues)
                Debug.LogWarning("[WorldBuildingMaterializer] View audit issues: " + report, this);
            return report;
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                DestroyAllViews();
        }

        private int LoadBuilding(WorldTileKey tile, WorldEntityId id)
        {
            if (_viewRegistry.TryGetBuildingView(id, out var existing))
            {
                _viewRegistry.RegisterBuildingView(tile, existing);
                return 0;
            }

            if (!_stateManager.TryCopyBuilding(id, out var building))
                return 0;

            if (!_resolver.TryResolve(building, out var resolution) || !resolution.IsValid)
            {
                LogMissingArchetypeOnce(building);
                return 0;
            }

            var parent = _viewsRoot != null ? _viewsRoot : transform;
            var instance = Instantiate(resolution.Prefab, parent);
            var view = WorldBuildingViewApplier.Apply(
                instance,
                building,
                tile,
                resolution,
                _runtimePlacedStructureFixer,
                _logDiagnostics);
            _viewRegistry.RegisterBuildingView(tile, view);
            return view != null ? 1 : 0;
        }

        private void ReloadAllViewsIfStateChanged()
        {
            var revision = _stateManager.Revision;
            if (_hasAppliedStateRevision && _lastAppliedStateRevision == revision)
                return;

            _viewRegistry.DestroyAllViews();
            _hasAppliedStateRevision = true;
            _lastAppliedStateRevision = revision;
        }

        private void BuildDesiredTileSet()
        {
            _desiredTiles.Clear();
            for (var i = 0; i < _tileBuffer.Count; i++)
                _desiredTiles.Add(WorldTileKey.FromCoord(_tileBuffer[i].Coord));
        }

        private void UnloadUndesiredTiles()
        {
            _viewRegistry.CopyLoadedTiles(_loadedTileBuffer);
            for (var i = 0; i < _loadedTileBuffer.Count; i++)
            {
                if (_desiredTiles.Contains(_loadedTileBuffer[i]))
                    continue;

                UnloadTile(_loadedTileBuffer[i]);
            }
        }

        private void LoadDesiredTiles()
        {
            for (var i = 0; i < _tileBuffer.Count; i++)
            {
                var tile = WorldTileKey.FromCoord(_tileBuffer[i].Coord);
                if (_viewRegistry.IsTileLoaded(tile))
                    continue;

                LoadTile(tile);
            }
        }

        private bool EnsureReady()
        {
            ResolveFallbackReferencesOnce();
            var ready = _stateManager != null && _tileStream != null && _viewRegistry != null && _resolver != null;
            if (!ready)
                LogMissingReferencesOnce();
            return ready;
        }

        private void ResolveFallbackReferencesOnce()
        {
            if (_attemptedFallbackResolve)
                return;

            _attemptedFallbackResolve = true;
            _stateManager ??= GetComponent<WorldStateManager>();
            _stateManager ??= GetComponentInParent<WorldStateManager>();
            _tileStream ??= GetComponent<WorldTileStreamSource>();
            _tileStream ??= GetComponentInParent<WorldTileStreamSource>();
            _viewRegistry ??= GetComponent<WorldStateViewRegistry>();
            _viewRegistry ??= GetComponentInParent<WorldStateViewRegistry>();
            _runtimePlacedStructureFixer ??= GetComponent<RuntimePlacedStructureFixer>();
            _runtimePlacedStructureFixer ??= GetComponentInParent<RuntimePlacedStructureFixer>();
            ResolveResolverSource();
        }

        private void ResolveResolverSource()
        {
            if (_resolver != null)
                return;

            if (_archetypeResolverSource is IBuildingArchetypeResolver serializedResolver)
            {
                _resolver = serializedResolver;
                return;
            }

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IBuildingArchetypeResolver resolver)
                    SetResolver(resolver);
            }
        }

        private void SetResolver(IBuildingArchetypeResolver resolver)
        {
            _resolver = resolver;
            if (resolver is MonoBehaviour behaviour)
                _archetypeResolverSource = behaviour;
        }

        private void LogMissingReferencesOnce()
        {
            LogMissingOnce(_stateManager == null, ref _loggedMissingStateManager, "WorldStateManager");
            LogMissingOnce(_tileStream == null, ref _loggedMissingTileStream, "WorldTileStreamSource");
            LogMissingOnce(_viewRegistry == null, ref _loggedMissingRegistry, "WorldStateViewRegistry");
            LogMissingOnce(_resolver == null, ref _loggedMissingResolver, "IBuildingArchetypeResolver");
        }

        private void LogMissingOnce(bool missing, ref bool logged, string label)
        {
            if (!missing || logged)
                return;

            logged = true;
            Debug.LogWarning("[WorldBuildingMaterializer] Missing " + label + "; building views will not materialize.", this);
        }

        private void LogMissingArchetypeOnce(BuildingState building)
        {
            var archetypeId = building?.archetypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(archetypeId) || !_loggedMissingArchetypes.Add(archetypeId))
                return;

            Debug.LogWarning("[WorldBuildingMaterializer] No building archetype resolved for '" + archetypeId + "'.", this);
        }
    }
}
