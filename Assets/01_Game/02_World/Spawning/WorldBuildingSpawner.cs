#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

#endregion

namespace Zombera.World.Spawning
{
    /// <summary>
    ///     Spawns world buildings as terrain tiles stream in.
    ///     MapMagic spline extraction lives in Legacy; this World path no-ops that source.
    /// </summary>
    [AddComponentMenu("Zombera/World/World Building Spawner")]
    [DisallowMultipleComponent]
    public sealed partial class WorldBuildingSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The tile stream this spawner should react to.")]
        [SerializeField]
        private WorldTileStreamSource tileStreamBridge;

        [Header("Road Placement")]
        [SerializeField] [Min(1f)] private float roadSetbackMeters = 9f;
        [SerializeField] [Min(2f)] private float lotSpacingMeters = 14f;
        [SerializeField] private bool spawnBothSides = true;
        [SerializeField] [Min(0f)] private float positionJitterMeters = 1.2f;
        [SerializeField] [Range(0f, 45f)] private float yawJitterDegrees = 10f;

        [Header("Spline Sampling")]
        [SerializeField] [Range(0.02f, 1f)] private float splineResPerMeter = 0.15f;
        [SerializeField] [Min(2)] private int splineMinSamples = 3;
        [SerializeField] [Min(2)] private int splineMaxSamples = 32;

        [Header("Entries")]
        [SerializeField] private List<BuildingSpawnEntry> entries = new();

        [Header("Spawn Post-Processing")]
        [SerializeField] private RuntimePlacedStructureFixer runtimePlacedStructureFixer;
        [SerializeField] private bool autoCreateRuntimePlacedStructureFixer = true;
        [SerializeField] private bool disableEasyBuildCollapseForSpawnedBuildings = true;

        [Header("Runtime Budget")]
        [SerializeField] [Range(1, 8)] private int maxTileUpdatesPerFrame = 1;
        [SerializeField] [Range(0f, 2f)] private float minSecondsBetweenTileUpdates = 0.3f;
        [SerializeField] [Range(1, 64)] private int maxBuildingSpawnsPerFrame = 4;

        [Header("Debug")]
        [SerializeField] private bool logSpawns;

        private readonly Dictionary<(int x, int z), GameObject> _tileRoots = new();
        private readonly Queue<TileRequest> _pendingQueue = new();
        private readonly Dictionary<(int x, int z), TileRequest> _pendingByCoord = new();
        private readonly Queue<PendingSpawn> _spawnQueue = new(256);
        private readonly Dictionary<GameObject, Queue<GameObject>> _buildingPools = new();
        private Transform _poolRoot;
        private float _nextAllowedTime;

        private void OnEnable()
        {
            if (tileStreamBridge == null) tileStreamBridge = WorldTileStreamSourceUtility.FindBridge();
            WorldTileStreamSourceUtility.SubscribeTileApplied(tileStreamBridge, HandleTileApplied);
            if (tileStreamBridge != null)
                tileStreamBridge.BeforeTileReset += HandleBeforeTileReset;

            ResolveRuntimePlacedStructureFixer();
        }

        private void OnDisable()
        {
            WorldTileStreamSourceUtility.UnsubscribeTileApplied(tileStreamBridge, HandleTileApplied);
            if (tileStreamBridge != null)
                tileStreamBridge.BeforeTileReset -= HandleBeforeTileReset;

            FlushSpawnQueueImmediate();
            _pendingQueue.Clear();
            _pendingByCoord.Clear();
        }

        private void Update()
        {
            ProcessQueue();
            DrainSpawnQueue();
        }

        private void ProcessQueue()
        {
            if (_pendingQueue.Count == 0) return;
            if (Time.unscaledTime < _nextAllowedTime) return;

            var budget = Mathf.Clamp(maxTileUpdatesPerFrame, 1, 8);
            var processed = 0;

            while (processed < budget && _pendingQueue.Count > 0)
            {
                if (Time.unscaledTime < _nextAllowedTime) break;

                var request = _pendingQueue.Dequeue();
                var key = (request.Tile.Coord.X, request.Tile.Coord.Z);
                if (!_pendingByCoord.TryGetValue(key, out var latest)
                    || latest.Tile.Coord != request.Tile.Coord)
                    continue;

                _pendingByCoord.Remove(key);
                ProcessTile(request.Tile);
                processed++;
                _nextAllowedTime = Time.unscaledTime + Mathf.Max(0f, minSecondsBetweenTileUpdates);
            }
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            var key = (tile.Coord.X, tile.Coord.Z);
            var request = new TileRequest(tile);
            _pendingByCoord[key] = request;
            _pendingQueue.Enqueue(request);
        }

        private void HandleBeforeTileReset(WorldTileInfo tile)
        {
            var key = (tile.Coord.X, tile.Coord.Z);
            if (!_tileRoots.TryGetValue(key, out var root) || root == null)
            {
                _tileRoots.Remove(key);
                return;
            }

            ReclaimTileRoot(root);
            _tileRoots.Remove(key);
        }
    }
}
