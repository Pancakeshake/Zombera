using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    ///     Runtime state backing both minimap and full world map.
    ///     Owns discovered chunk set, waypoint state, and current view state (zoom/pan).
    /// </summary>
    public sealed partial class MapStateService : MonoBehaviour, IMapStateService
    {
        [Header("Discovery (runtime bootstrap)")]
        [Tooltip("When enabled, marks chunks as discovered as soon as ChunkLoader loads them.")]
        [SerializeField]
        private bool discoverChunksOnChunkLoad = true;

        [Tooltip("Chunks revealed around the player each simulation tick.")]
        [SerializeField] [Min(0)]
        private int playerDiscoveryRadiusChunks = 1;

        [SerializeField] [Min(1)] private int fallbackChunkSize = 32;

        private readonly HashSet<Vector2Int> _discoveredChunks = new();
        private readonly HashSet<Vector2Int> _loadedChunks = new();

        private ChunkLoader _chunkLoader;
        private RegionSystem _regionSystem;
        private FogOfWarTextureBuilder _fogTextureBuilder;

        private Vector2Int _currentPlayerChunk;
        private Vector3 _currentPlayerWorldPosition;
        private RegionDefinition _currentRegion;

        private bool _hasWaypoint;
        private Vector3 _waypointWorldPosition;

        private float _mapZoom = 1f;
        private Vector2 _mapPan;

        public Vector2Int CurrentPlayerChunk => _currentPlayerChunk;
        public Vector3 CurrentPlayerWorldPosition => _currentPlayerWorldPosition;
        public RegionDefinition CurrentRegion => _currentRegion;

        public bool HasWaypoint => _hasWaypoint;
        public Vector3 WaypointWorldPosition => _waypointWorldPosition;

        public float MapZoom => _mapZoom;
        public Vector2 MapPan => _mapPan;
        public RenderTexture FogTexture => _fogTextureBuilder != null ? _fogTextureBuilder.FogTexture : null;

        public IReadOnlyCollection<Vector2Int> DiscoveredChunks => _discoveredChunks;

        public event Action<Vector2Int> DiscoveredChunkAdded;

        public void Configure(
            ChunkLoader chunkLoader,
            RegionSystem regionSystem,
            FogOfWarTextureBuilder fogTextureBuilder = null)
        {
            if (fogTextureBuilder != null) _fogTextureBuilder = fogTextureBuilder;

            if (_chunkLoader == chunkLoader && _regionSystem == regionSystem) return;

            UnbindChunkLoaderEvents();

            _chunkLoader = chunkLoader;
            _regionSystem = regionSystem;

            BindChunkLoaderEvents();
        }

        public void TickPlayer(Vector3 playerWorldPosition)
        {
            _currentPlayerWorldPosition = playerWorldPosition;

            var chunkSize = ResolveChunkSize();
            _currentPlayerChunk = WorldToChunk(playerWorldPosition, chunkSize);

            _currentRegion = _regionSystem != null
                ? _regionSystem.GetRegionAtWorldPosition(playerWorldPosition)
                : null;

            RevealChunksNearPlayer();
        }

        private void OnDestroy()
        {
            UnbindChunkLoaderEvents();
        }

        public void SetWaypoint(Vector3 worldPosition)
        {
            _hasWaypoint = true;
            _waypointWorldPosition = worldPosition;
        }

        public void ClearWaypoint()
        {
            _hasWaypoint = false;
            _waypointWorldPosition = default;
        }

        public void SetView(float zoom, Vector2 pan)
        {
            _mapZoom = Mathf.Max(0.01f, zoom);
            _mapPan = pan;
        }

        private int ResolveChunkSize()
        {
            if (_chunkLoader != null) return Mathf.Max(1, _chunkLoader.ChunkSize);
            return Mathf.Max(1, fallbackChunkSize);
        }

        private static Vector2Int WorldToChunk(Vector3 worldPosition, int chunkSize)
        {
            var chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
            var chunkY = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(chunkX, chunkY);
        }

        private void BindChunkLoaderEvents()
        {
            if (_chunkLoader == null) return;

            _chunkLoader.ChunkLoaded -= HandleChunkLoaded;
            _chunkLoader.ChunkLoaded += HandleChunkLoaded;
            _chunkLoader.ChunkUnloaded -= HandleChunkUnloaded;
            _chunkLoader.ChunkUnloaded += HandleChunkUnloaded;
        }

        private void UnbindChunkLoaderEvents()
        {
            if (_chunkLoader == null) return;

            _chunkLoader.ChunkLoaded -= HandleChunkLoaded;
            _chunkLoader.ChunkUnloaded -= HandleChunkUnloaded;
        }

        private void HandleChunkLoaded(Vector2Int coordinates)
        {
            _loadedChunks.Add(coordinates);

            if (!discoverChunksOnChunkLoad) return;
            DiscoverChunk(coordinates);
        }

        private void HandleChunkUnloaded(Vector2Int coordinates)
        {
            _loadedChunks.Remove(coordinates);
        }
    }
}

