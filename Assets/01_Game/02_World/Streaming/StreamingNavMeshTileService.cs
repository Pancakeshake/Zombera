#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Per-tile NavMesh builds driven by <see cref="WorldTileStreamSource" />:
    ///     each tile gets its own <see cref="NavMeshData" /> instances (per agent type),
    ///     updated when content is ready and removed before tile reset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class StreamingNavMeshTileService : MonoBehaviour, INavMeshTileReadiness
    {
        private const float SceneObjectScanMinIntervalSeconds = 0.90f;
        private const float NavMeshTriangleCacheIntervalSeconds = 0.4f;

        [Header("Authority")]
        [Tooltip("When enabled, PlayerSpawner defers runtime NavMesh building to this service.")]
        [SerializeField]
        private bool driveRuntimeNavMesh = true;

        [SerializeField] private PlayerSpawner playerSpawner;
        [SerializeField] private ProceduralRoadSystem roadSystem;

        [Header("Tile Source Scope")]
        [Tooltip("When enabled, each tile NavMesh bake uses only the tile's active terrain as source to avoid multi-second Recast spikes.")]
        [SerializeField]
        private bool useTileLocalTerrainSourcesOnly = true;

        [Tooltip("Allowlisted enterable TunnelNavFloor MeshColliders intersecting the tile (policy v4). Terrain remains primary.")]
        [SerializeField]
        private bool includeAllowlistedTunnelFloorSources = true;

        private static StreamingNavMeshTileService _runtimeInstance;

        [Header("Bake Budget")]
        [Tooltip("Max tiles to bake per frame during a full rebuild (initial world entry).")]
        [SerializeField]
        [Range(1, 32)]
        private int rebuildTilesPerFrame = 1;

        [Tooltip("Max tile-applied navmesh bakes processed each frame.")]
        [SerializeField]
        [Range(1, 8)]
        private int tileApplyBakesPerFrame = 1;

        [Tooltip("Minimum unscaled seconds between tile-applied navmesh bakes to avoid recast storms.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float minSecondsBetweenTileApplyBakes = 0.75f;

        [Header("State-Based Bake Budget")]
        [Tooltip("When enabled, tile bake throughput is higher during LoadingWorld and lower during normal gameplay.")]
        [SerializeField]
        private bool useStateBasedBakeBudget = true;

        [Tooltip("Max tiles to bake per frame during full rebuild while LoadingWorld.")]
        [SerializeField]
        [Range(1, 32)]
        private int loadingRebuildTilesPerFrame = 6;

        [Tooltip("Max tile-applied bakes started per frame while LoadingWorld.")]
        [SerializeField]
        [Range(1, 8)]
        private int loadingTileApplyBakesPerFrame = 4;

        [Tooltip("Minimum seconds between tile-applied bakes while LoadingWorld.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float loadingMinSecondsBetweenTileApplyBakes = 0.05f;

        [Tooltip("Max tile-applied bakes started per frame during gameplay.")]
        [SerializeField]
        [Range(1, 8)]
        private int worldTileApplyBakesPerFrame = 1;

        [Tooltip("Minimum seconds between tile-applied bakes during gameplay.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float worldMinSecondsBetweenTileApplyBakes = 0.75f;

        [Header("Async Baking (Advanced)")]
        [Tooltip("Use NavMeshBuilder.UpdateNavMeshDataAsync for streamed tile bakes. Falls back to synchronous baking when async cannot start.")]
        [SerializeField]
        private bool useAsyncTileBaking = true;

        [Tooltip("Max concurrent async tile bakes while LoadingWorld.")]
        [SerializeField]
        [Range(1, 16)]
        private int loadingMaxConcurrentAsyncTileBakes = 6;

        [Tooltip("Max concurrent async tile bakes during gameplay.")]
        [SerializeField]
        [Range(1, 8)]
        private int worldMaxConcurrentAsyncTileBakes = 2;

        [Tooltip("Minimum unscaled seconds between stale-tile prune passes while tiles are streaming.")]
        [SerializeField]
        [Min(0.1f)]
        private float pruneCooldownSeconds = 4f;

        [Tooltip("Allow startup fallback to full cached-tile rebuild when no tile-applied bakes are queued. Can be expensive.")]
        [SerializeField]
        private bool allowBootstrapFullRebuildFallback;

        [Header("Agent Type Budget")]
        [Tooltip("Bake only a single agent type per streamed tile to reduce Recast cost.")]
        [SerializeField]
        private bool usePrimaryAgentTypeOnly = true;

        [Tooltip("Agent type ID used when primary-only mode is enabled (Unity default humanoid is usually 0).")]
        [SerializeField]
        private int primaryAgentTypeId;

        [Header("First-Party World Builder")]
        [SerializeField]
        private WorldTileStreamSource worldTileStream;

        [Header("Diagnostics")]
        [Tooltip("Logs focused runtime diagnostics (owner scene, baked tile count, NavMesh triangle count).")]
        [SerializeField]
        private bool logFocusedNavMeshDiagnostics;

        [Tooltip("Minimum seconds between repeated scene-mismatch warnings while tiles are streaming.")]
        [SerializeField]
        [Min(0.1f)]
        private float sceneMismatchLogCooldownSeconds = 2f;

        [Tooltip("Warn when a single tile bake exceeds this duration in milliseconds. Set to 0 to disable.")]
        [SerializeField]
        [Min(0f)]
        private float tileBakeSpikeWarningMilliseconds = 120f;

        [Tooltip("Warn when an async tile bake takes this long in wall-clock milliseconds. This is not a main-thread hitch metric.")]
        [SerializeField]
        [Min(0f)]
        private float asyncTileBakeWallClockWarningMilliseconds = 1200f;

        [Tooltip("Minimum seconds between slow tile-bake warnings.")]
        [SerializeField]
        [Min(0.1f)]
        private float tileBakeWarningCooldownSeconds = 3f;

        [Tooltip("Warn when a single BuildNavMeshData call exceeds this duration in milliseconds. Set to 0 to disable.")]
        [SerializeField]
        [Min(0f)]
        private float perAgentBakeSpikeWarningMilliseconds = 80f;

        [Tooltip("Minimum seconds between slow per-agent BuildNavMeshData warnings.")]
        [SerializeField]
        [Min(0.1f)]
        private float perAgentBakeWarningCooldownSeconds = 3f;

        private readonly HashSet<int> _agentTypeIdBuffer = new();
        private readonly HashSet<Terrain> _includedTerrainBuffer = new();
        private readonly HashSet<WorldTileCoord> _liveCoordBuffer = new();
        private readonly HashSet<WorldTileCoord> _pendingTileBakeSet = new();
        private readonly List<NavMeshBuildSource> _navMeshSourceBuffer = new(256);
        private readonly List<WorldTileCoord> _staleCoordBuffer = new(64);
        private readonly Queue<WorldTileCoord> _pendingTileBakeQueue = new();
        private readonly Dictionary<WorldTileCoord, WorldTileInfo> _pendingTileByCoord = new();
        private readonly Dictionary<WorldTileCoord, PendingAsyncTileBake> _pendingAsyncTileBakes = new();
        private readonly Dictionary<WorldTileCoord, int> _tileBakeVersions = new();
        private readonly List<WorldTileCoord> _completedAsyncBakeCoords = new(32);
        private readonly Dictionary<WorldTileCoord, TileNavInstances> _tiles = new();
        private readonly List<WorldTileInfo> _streamTileScratch = new(64);

        private IWorldGenerationBackend _worldGenerationBackend;
        private bool _subscribedWorldTileStream;
        private NavMeshAgent[] _cachedNavMeshAgents = Array.Empty<NavMeshAgent>();
        private Terrain[] _cachedTerrains = Array.Empty<Terrain>();
        private Scene _ownerScene;
        private Coroutine _rebuildAllCoroutine;
        private Scene _sceneObjectScanOwner;

        private float _nextAllowedTileApplyBakeTime;
        private float _nextAllowedPruneTime;
        private float _nextNavMeshTriangleSampleAt = -1000f;
        private float _nextSceneMismatchLogTime;
        private float _nextSlowAgentBakeWarningAt;
        private float _nextSlowTileBakeWarningAt;
        private int _nextTileBakeVersion = 1;
        private int _cachedNavMeshTriangleCount;
        private float _sceneObjectScanTime = -1000f;

        public bool IsDrivingRuntimeNavMesh => driveRuntimeNavMesh && enabled;
        public bool LastBootstrapHadTriangles { get; private set; }
        public bool HasAnyBakedTiles => _tiles.Count > 0;
        public bool IsBootstrapRebuildInProgress => _rebuildAllCoroutine != null;
        public int PendingTileBakeCount => _pendingTileBakeQueue.Count;

        private enum TileBakeOutcome
        {
            Success,
            MissingTerrain,
            NoSources,
            NoInstances,
            InProgress
        }

        private struct TileBakePassDiagnostics
        {
            public int TileEntriesVisited;
            public int NullTiles;
            public int MissingTerrain;
            public int NoSources;
            public int NoInstances;
            public int Successes;
        }

        private sealed class TileNavInstances
        {
            public readonly List<NavMeshData> Datas = new(4);
            public readonly List<NavMeshDataInstance> Instances = new(4);
        }

        private sealed class PendingAsyncTileBake
        {
            public WorldTileCoord Coord;
            public int Version;
            public float StartedAtRealtime;
            public int SourceCount;
            public int AgentTypeCount;
            public List<NavMeshBuildSource> Sources;
            public TileNavInstances Entry = new();
            public readonly List<AsyncOperation> Operations = new(4);
        }
    }
}
