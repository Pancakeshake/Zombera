#region

using System.Collections.Generic;
using System.Diagnostics;
using System;
using UnityEngine;
using Unity.Profiling;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

#endregion

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable InlineTemporaryVariable
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ConvertIfStatementToNullCoalescingAssignment
// ReSharper disable UnusedMember.Global

namespace Zombera.World
{
    /// <summary>
    ///     Loads and unloads chunks around the player based on configured radius.
    /// </summary>
    public sealed class ChunkLoader : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateStreamingMarker = new("Zombera.ChunkLoader.UpdateStreaming");

        private readonly struct RequiredChunkTrimSummary
        {
            public RequiredChunkTrimSummary(int removedByDistanceClamp, int removedBySetCap)
            {
                RemovedByDistanceClamp = removedByDistanceClamp;
                RemovedBySetCap = removedBySetCap;
            }

            public int RemovedByDistanceClamp { get; }
            public int RemovedBySetCap { get; }
        }

        private readonly struct StreamingPressureSnapshot
        {
            public StreamingPressureSnapshot(
                int requiredChunkCount,
                int totalLoadedBefore,
                int totalLoadedAfter,
                int loadedThisTick,
                int unloadedThisTick,
                RequiredChunkTrimSummary trimSummary,
                float elapsedMs)
            {
                RequiredChunkCount = requiredChunkCount;
                TotalLoadedBefore = totalLoadedBefore;
                TotalLoadedAfter = totalLoadedAfter;
                LoadedThisTick = loadedThisTick;
                UnloadedThisTick = unloadedThisTick;
                TrimSummary = trimSummary;
                ElapsedMs = elapsedMs;
            }

            public int RequiredChunkCount { get; }
            public int TotalLoadedBefore { get; }
            public int TotalLoadedAfter { get; }
            public int LoadedThisTick { get; }
            public int UnloadedThisTick { get; }
            public RequiredChunkTrimSummary TrimSummary { get; }
            public int RemovedByDistanceClamp => TrimSummary.RemovedByDistanceClamp;
            public int RemovedBySetCap => TrimSummary.RemovedBySetCap;
            public float ElapsedMs { get; }
        }

        [SerializeField] private int loadRadiusInChunks = 1;
        [SerializeField] private int chunkSize = 32;
        [SerializeField] private ChunkCache chunkCache;

        [Header("Streaming Budget")]
        [Tooltip("Max number of new chunks to generate/load per UpdateStreaming call.")]
        [SerializeField]
        [Range(1, 32)]
        private int maxChunkLoadsPerFrame = 3;

        [Tooltip("Max number of chunks to unload per UpdateStreaming call.")]
        [SerializeField]
        [Range(1, 64)]
        private int maxChunkUnloadsPerFrame = 8;

        [Header("MapMagic Tile Streaming")]
        [Tooltip(
            "When assigned, required chunks include all gameplay chunks overlapping active MapMagic tiles (plus margin), merged with the player-radius set.")]
        [SerializeField]
        private WorldTileStreamSource mapMagicTileStreamBridge;

        [SerializeField] [Min(0)] private int mapMagicGameplayChunkMargin = 1;

        [Tooltip("Max chunk distance from player for merged MapMagic tile coverage. 0 disables the clamp.")]
        [SerializeField]
        [Min(0)]
        private int maxMapMagicChunkDistanceFromPlayer = 24;

        [Tooltip("Hard cap on required chunk set size per tick. 0 disables the cap.")]
        [SerializeField]
        [Min(0)]
        private int maxRequiredChunksPerTick = 512;

        [Header("Diagnostics")]
        [SerializeField]
        private bool logStreamingPressure;

        [SerializeField] [Min(0)] private int requiredChunkWarningThreshold = 4096;

        [SerializeField] [Min(0f)] private float updateStreamingSpikeWarningMilliseconds = 8f;

        [SerializeField] [Min(0.1f)] private float streamingPressureWarningCooldownSeconds = 1f;

        private float _nextStreamingPressureWarningAt;
        private readonly List<Vector2Int> _currentlyLoadedBuffer = new(64);
        private readonly List<Vector2Int> _requiredChunkTrimBuffer = new(1024);

        private readonly Dictionary<Vector2Int, WorldChunk> _loadedChunks = new();
        private readonly HashSet<Vector2Int> _requiredChunkBuffer = new();
        private Vector2Int _requiredChunkSortCenter;

        public int ChunkSize => chunkSize;
        public IReadOnlyDictionary<Vector2Int, WorldChunk> LoadedChunks => _loadedChunks;

        public event Action<Vector2Int> ChunkLoaded;
        public event Action<Vector2Int> ChunkUnloaded;

        public void SetMapMagicTileStreamBridge(WorldTileStreamSource bridge)
        {
            mapMagicTileStreamBridge = bridge;
        }

        public void UpdateStreaming(Vector3 playerPosition, RegionSystem regionSystem, ChunkGenerator chunkGenerator)
        {
            UpdateStreaming(playerPosition, regionSystem, chunkGenerator, mapMagicTileStreamBridge);
        }

        public void UpdateStreaming(
            Vector3 playerPosition,
            RegionSystem regionSystem,
            ChunkGenerator chunkGenerator,
            WorldTileStreamSource tileBridgeOverride)
        {
            var startedAt = Time.realtimeSinceStartup;
            var totalLoadedBefore = _loadedChunks.Count;

            var requiredChunks = _requiredChunkBuffer;
            var unloadedThisTick = 0;
            var loadedThisTick = 0;
            var removedByDistanceClamp = 0;
            var removedBySetCap = 0;

            using (UpdateStreamingMarker.Auto())
            {
                var playerChunk = WorldToChunk(playerPosition);
                FillRequiredChunkCoordinates(requiredChunks, playerChunk);

                var bridge = tileBridgeOverride ?? mapMagicTileStreamBridge;
                if (bridge != null)
                    bridge.AppendChunksCoveringTilesAtOrAbove(
                        WorldTileState.TerrainReady,
                        requiredChunks,
                        chunkSize,
                        mapMagicGameplayChunkMargin,
                        playerChunk,
                        Mathf.Max(0, maxMapMagicChunkDistanceFromPlayer));

                ApplyRequiredChunkGuards(
                    requiredChunks,
                    playerChunk,
                    out removedByDistanceClamp,
                    out removedBySetCap);

                var currentlyLoaded = _currentlyLoadedBuffer;
                currentlyLoaded.Clear();
                foreach (var kvp in _loadedChunks) currentlyLoaded.Add(kvp.Key);

                var unloadBudget = Mathf.Clamp(maxChunkUnloadsPerFrame, 1, 256);
                for (var i = 0; i < currentlyLoaded.Count; i++)
                {
                    if (unloadedThisTick >= unloadBudget) break;

                    var loadedCoord = currentlyLoaded[i];

                    if (!requiredChunks.Contains(loadedCoord))
                    {
                        UnloadChunk(loadedCoord);
                        unloadedThisTick++;
                    }
                }

                var loadBudget = Mathf.Clamp(maxChunkLoadsPerFrame, 1, 64);

                foreach (var requiredCoord in requiredChunks)
                {
                    if (_loadedChunks.ContainsKey(requiredCoord)) continue;

                    if (loadedThisTick >= loadBudget) break;

                    var region = regionSystem?.GetRegionAtChunk(requiredCoord);
                    LoadChunk(requiredCoord, region, chunkGenerator);
                    loadedThisTick++;
                }
            }

            MaybeLogStreamingPressure(new StreamingPressureSnapshot(
                requiredChunks.Count,
                totalLoadedBefore,
                _loadedChunks.Count,
                loadedThisTick,
                unloadedThisTick,
                new RequiredChunkTrimSummary(removedByDistanceClamp, removedBySetCap),
                (Time.realtimeSinceStartup - startedAt) * 1000f));
        }

        private void MaybeLogStreamingPressure(StreamingPressureSnapshot snapshot)
        {
            if (!logStreamingPressure) return;

            var warnByCount = requiredChunkWarningThreshold > 0
                              && snapshot.RequiredChunkCount >= requiredChunkWarningThreshold;
            var warnByTime = updateStreamingSpikeWarningMilliseconds > 0f
                             && snapshot.ElapsedMs >= updateStreamingSpikeWarningMilliseconds;
            if (!warnByCount && !warnByTime) return;

            if (Time.unscaledTime < _nextStreamingPressureWarningAt) return;
            _nextStreamingPressureWarningAt = Time.unscaledTime +
                                              Mathf.Max(0.1f, streamingPressureWarningCooldownSeconds);

            UnityEngine.Debug.LogWarning(
                "[ChunkLoader] Streaming pressure spike: required=" + snapshot.RequiredChunkCount +
                ", loadedBefore=" + snapshot.TotalLoadedBefore +
                ", loadedAfter=" + snapshot.TotalLoadedAfter +
                ", loadedThisTick=" + snapshot.LoadedThisTick +
                ", unloadedThisTick=" + snapshot.UnloadedThisTick +
                ", trimmedByDistance=" + snapshot.RemovedByDistanceClamp +
                ", trimmedByCap=" + snapshot.RemovedBySetCap +
                ", elapsedMs=" + snapshot.ElapsedMs.ToString("0.00") + ".",
                this);
        }

        private void ApplyRequiredChunkGuards(
            HashSet<Vector2Int> required,
            Vector2Int playerChunk,
            out int removedByDistanceClamp,
            out int removedBySetCap)
        {
            removedByDistanceClamp = 0;
            removedBySetCap = 0;
            if (required == null || required.Count == 0) return;

            var distanceClamp = Mathf.Max(0, maxMapMagicChunkDistanceFromPlayer);
            if (distanceClamp > 0)
                removedByDistanceClamp = TrimRequiredChunksByDistance(required, playerChunk, distanceClamp);

            var requiredSetCap = Mathf.Max(0, maxRequiredChunksPerTick);
            if (requiredSetCap > 0 && required.Count > requiredSetCap)
                removedBySetCap = TrimRequiredChunksByCap(required, playerChunk, requiredSetCap);
        }

        private int TrimRequiredChunksByDistance(HashSet<Vector2Int> required, Vector2Int playerChunk,
            int distanceClamp)
        {
            _requiredChunkTrimBuffer.Clear();

            foreach (var coord in required)
            {
                var dx = Mathf.Abs(coord.x - playerChunk.x);
                var dz = Mathf.Abs(coord.y - playerChunk.y);
                if (dx > distanceClamp || dz > distanceClamp)
                    _requiredChunkTrimBuffer.Add(coord);
            }

            foreach (var coord in _requiredChunkTrimBuffer)
                required.Remove(coord);

            return _requiredChunkTrimBuffer.Count;
        }

        private int TrimRequiredChunksByCap(HashSet<Vector2Int> required, Vector2Int playerChunk, int requiredSetCap)
        {
            _requiredChunkTrimBuffer.Clear();
            foreach (var coord in required) _requiredChunkTrimBuffer.Add(coord);

            _requiredChunkSortCenter = playerChunk;
            _requiredChunkTrimBuffer.Sort(CompareRequiredChunkDistance);

            var removedCount = 0;
            for (var i = requiredSetCap; i < _requiredChunkTrimBuffer.Count; i++)
            {
                required.Remove(_requiredChunkTrimBuffer[i]);
                removedCount++;
            }

            return removedCount;
        }

        private int CompareRequiredChunkDistance(Vector2Int a, Vector2Int b)
        {
            var ax = a.x - _requiredChunkSortCenter.x;
            var az = a.y - _requiredChunkSortCenter.y;
            var bx = b.x - _requiredChunkSortCenter.x;
            var bz = b.y - _requiredChunkSortCenter.y;

            var aDistanceSq = ax * ax + az * az;
            var bDistanceSq = bx * bx + bz * bz;
            if (aDistanceSq != bDistanceSq) return aDistanceSq.CompareTo(bDistanceSq);

            if (a.x != b.x) return a.x.CompareTo(b.x);
            return a.y.CompareTo(b.y);
        }

        public bool IsChunkLoaded(Vector2Int coordinates)
        {
            return _loadedChunks.ContainsKey(coordinates);
        }

        public WorldChunk GetChunk(Vector2Int coordinates)
        {
            _loadedChunks.TryGetValue(coordinates, out var chunk);
            return chunk;
        }

        private void LoadChunk(Vector2Int coordinates, RegionDefinition region, ChunkGenerator chunkGenerator)
        {
            WorldChunk chunk = null;

            var sw = Stopwatch.StartNew();
            if (chunkCache != null && chunkCache.TryGetChunk(coordinates, out var cachedChunk))
            {
                chunk = cachedChunk;
                chunkCache.RemoveChunk(coordinates);
            }

            if (chunk == null) chunk = chunkGenerator.GenerateChunk(coordinates, region);

            StreamedWorldChunkState.TryApplyToChunk(chunk);

            sw.Stop();
            StreamedWorldMetrics.RecordChunkLoaded(sw.Elapsed);

            _loadedChunks[coordinates] = chunk;
            ChunkLoaded?.Invoke(coordinates);

            // Visual and NavMesh representations will be instantiated from pools by the ChunkMeshBuilder
            // subsystem once it is implemented, avoiding repeated alloc/destroy cycles.
        }

        private void UnloadChunk(Vector2Int coordinates)
        {
            if (!_loadedChunks.TryGetValue(coordinates, out var chunk)) return;

            if (chunk.IsDirty) StreamedWorldChunkState.CaptureChunkState(chunk, "unload");

            _loadedChunks.Remove(coordinates);
            StreamedWorldMetrics.RecordChunkUnloaded();
            chunkCache?.StoreChunk(chunk);
            ChunkUnloaded?.Invoke(coordinates);

            // Persist dirty state so changes survive across load/unload cycles.
            if (chunk.IsDirty) chunkCache?.StoreChunk(chunk); // re-store to update cached version

            // Return spawned entity IDs to the spawn pool for re-use.
            chunk.SpawnedEntityIds.Clear();
        }

        private void FillRequiredChunkCoordinates(HashSet<Vector2Int> required, Vector2Int center)
        {
            if (required == null) return;

            required.Clear();

            for (var x = -loadRadiusInChunks; x <= loadRadiusInChunks; x++)
            {
                for (var y = -loadRadiusInChunks; y <= loadRadiusInChunks; y++)
                    required.Add(new Vector2Int(center.x + x, center.y + y));
            }
        }

        private Vector2Int WorldToChunk(Vector3 worldPosition)
        {
            var chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
            var chunkY = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(chunkX, chunkY);
        }
    }
}