using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Loads and unloads chunks around the player based on configured radius.
    /// </summary>
    public sealed class ChunkLoader : MonoBehaviour
    {
        [SerializeField] private int loadRadiusInChunks = 1;
        [SerializeField] private int chunkSize = 32;
        [SerializeField] private ChunkCache chunkCache;
        [Header("Streaming Budget")]
        [Tooltip("Max number of new chunks to generate/load per UpdateStreaming call.")]
        [SerializeField, Range(1, 32)] private int maxChunkLoadsPerFrame = 3;

        [Header("MapMagic Tile Streaming")]
        [Tooltip("When assigned, required chunks include all gameplay chunks overlapping active MapMagic tiles (plus margin), merged with the player-radius set.")]
        [SerializeField] private MapMagicTileStreamBridge mapMagicTileStreamBridge;
        [SerializeField, Min(0)] private int mapMagicGameplayChunkMargin = 1;

        public int ChunkSize => chunkSize;
        public IReadOnlyDictionary<Vector2Int, WorldChunk> LoadedChunks => loadedChunks;

        private readonly Dictionary<Vector2Int, WorldChunk> loadedChunks = new Dictionary<Vector2Int, WorldChunk>();

        public void SetMapMagicTileStreamBridge(MapMagicTileStreamBridge bridge)
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
            MapMagicTileStreamBridge tileBridgeOverride)
        {
            HashSet<Vector2Int> requiredChunks = CalculateRequiredChunkCoordinates(playerPosition);

            MapMagicTileStreamBridge bridge = tileBridgeOverride ?? mapMagicTileStreamBridge;
            if (bridge != null)
            {
                bridge.AppendChunksCoveringDeployedTiles(requiredChunks, chunkSize, mapMagicGameplayChunkMargin);
            }

            List<Vector2Int> currentlyLoaded = new List<Vector2Int>(loadedChunks.Keys);

            for (int i = 0; i < currentlyLoaded.Count; i++)
            {
                Vector2Int loadedCoord = currentlyLoaded[i];

                if (!requiredChunks.Contains(loadedCoord))
                {
                    UnloadChunk(loadedCoord);
                }
            }

            int loadBudget = Mathf.Clamp(maxChunkLoadsPerFrame, 1, 64);
            int loadedThisTick = 0;

            foreach (Vector2Int requiredCoord in requiredChunks)
            {
                if (loadedChunks.ContainsKey(requiredCoord))
                {
                    continue;
                }

                if (loadedThisTick >= loadBudget)
                {
                    break;
                }

                RegionDefinition region = regionSystem?.GetRegionAtChunk(requiredCoord);
                LoadChunk(requiredCoord, region, chunkGenerator);
                loadedThisTick++;
            }

            // Sort load order by distance ahead of the player's facing direction.
            SortPendingLoadsByPriority(playerPosition);
        }

        private readonly System.Collections.Generic.List<Vector2Int> pendingLoadQueue
            = new System.Collections.Generic.List<Vector2Int>();

        private void SortPendingLoadsByPriority(Vector3 playerPosition)
        {
            // Background job scheduling is deferred to a future streaming thread;
            // for now we record the priority-sorted list for the next UpdateStreaming call.
            pendingLoadQueue.Clear();
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            forward.y = 0f;

            foreach (System.Collections.Generic.KeyValuePair<Vector2Int, WorldChunk> entry in loadedChunks)
            {
                Vector3 chunkWorld = new Vector3(entry.Key.x * chunkSize, 0f, entry.Key.y * chunkSize);
                float dot = Vector3.Dot((chunkWorld - playerPosition).normalized, forward.normalized);

                if (dot > 0f)
                {
                    pendingLoadQueue.Add(entry.Key);
                }
            }
        }

        public bool IsChunkLoaded(Vector2Int coordinates)
        {
            return loadedChunks.ContainsKey(coordinates);
        }

        public WorldChunk GetChunk(Vector2Int coordinates)
        {
            loadedChunks.TryGetValue(coordinates, out WorldChunk chunk);
            return chunk;
        }

        private void LoadChunk(Vector2Int coordinates, RegionDefinition region, ChunkGenerator chunkGenerator)
        {
            WorldChunk chunk = null;

            Stopwatch sw = Stopwatch.StartNew();
            if (chunkCache != null && chunkCache.TryGetChunk(coordinates, out WorldChunk cachedChunk))
            {
                chunk = cachedChunk;
                chunkCache.RemoveChunk(coordinates);
            }

            if (chunk == null)
            {
                chunk = chunkGenerator.GenerateChunk(coordinates, region);
            }

            StreamedWorldChunkState.TryApplyToChunk(chunk);

            sw.Stop();
            StreamedWorldMetrics.RecordChunkLoaded(sw.Elapsed);

            loadedChunks[coordinates] = chunk;

            // Visual and NavMesh representations will be instantiated from pools by the ChunkMeshBuilder
            // subsystem once it is implemented, avoiding repeated alloc/destroy cycles.
        }

        private void UnloadChunk(Vector2Int coordinates)
        {
            if (!loadedChunks.TryGetValue(coordinates, out WorldChunk chunk))
            {
                return;
            }

            if (chunk.IsDirty)
            {
                StreamedWorldChunkState.CaptureChunkState(chunk, "unload");
            }

            loadedChunks.Remove(coordinates);
            StreamedWorldMetrics.RecordChunkUnloaded();
            chunkCache?.StoreChunk(chunk);

            // Persist dirty state so changes survive across load/unload cycles.
            if (chunk.IsDirty)
            {
                chunkCache?.StoreChunk(chunk); // re-store to update cached version
            }

            // Return spawned entity IDs to the spawn pool for re-use.
            chunk.SpawnedEntityIds.Clear();
        }

        private HashSet<Vector2Int> CalculateRequiredChunkCoordinates(Vector3 playerPosition)
        {
            HashSet<Vector2Int> required = new HashSet<Vector2Int>();
            Vector2Int center = WorldToChunk(playerPosition);

            for (int x = -loadRadiusInChunks; x <= loadRadiusInChunks; x++)
            {
                for (int y = -loadRadiusInChunks; y <= loadRadiusInChunks; y++)
                {
                    required.Add(new Vector2Int(center.x + x, center.y + y));
                }
            }

            return required;
        }

        private Vector2Int WorldToChunk(Vector3 worldPosition)
        {
            int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
            int chunkY = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(chunkX, chunkY);
        }
    }
}