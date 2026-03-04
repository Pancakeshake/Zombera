using System.Collections.Generic;
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

        public int ChunkSize => chunkSize;
        public IReadOnlyDictionary<Vector2Int, WorldChunk> LoadedChunks => loadedChunks;

        private readonly Dictionary<Vector2Int, WorldChunk> loadedChunks = new Dictionary<Vector2Int, WorldChunk>();

        public void UpdateStreaming(Vector3 playerPosition, RegionSystem regionSystem, ChunkGenerator chunkGenerator)
        {
            HashSet<Vector2Int> requiredChunks = CalculateRequiredChunkCoordinates(playerPosition);
            List<Vector2Int> currentlyLoaded = new List<Vector2Int>(loadedChunks.Keys);

            for (int i = 0; i < currentlyLoaded.Count; i++)
            {
                Vector2Int loadedCoord = currentlyLoaded[i];

                if (!requiredChunks.Contains(loadedCoord))
                {
                    UnloadChunk(loadedCoord);
                }
            }

            foreach (Vector2Int requiredCoord in requiredChunks)
            {
                if (loadedChunks.ContainsKey(requiredCoord))
                {
                    continue;
                }

                RegionDefinition region = regionSystem != null
                    ? regionSystem.GetRegionAtChunk(requiredCoord)
                    : null;
                LoadChunk(requiredCoord, region, chunkGenerator);
            }

            // TODO: Prioritize loading order by player direction and velocity.
            // TODO: Move generation/unload to background jobs where possible.
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

            if (chunkCache != null && chunkCache.TryGetChunk(coordinates, out WorldChunk cachedChunk))
            {
                chunk = cachedChunk;
                chunkCache.RemoveChunk(coordinates);
            }

            if (chunk == null)
            {
                chunk = chunkGenerator.GenerateChunk(coordinates, region);
            }

            loadedChunks[coordinates] = chunk;

            // TODO: Instantiate visual and nav representations from pooled prefabs.
        }

        private void UnloadChunk(Vector2Int coordinates)
        {
            if (!loadedChunks.TryGetValue(coordinates, out WorldChunk chunk))
            {
                return;
            }

            loadedChunks.Remove(coordinates);
            chunkCache?.StoreChunk(chunk);

            // TODO: Persist dirty chunk state before unload.
            // TODO: Return spawned entities to pools.
            _ = chunk;
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