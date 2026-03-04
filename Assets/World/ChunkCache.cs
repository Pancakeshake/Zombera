using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Caches unloaded chunk data snapshots for fast reactivation.
    /// </summary>
    public sealed class ChunkCache : MonoBehaviour
    {
        [SerializeField] private int maxCachedChunks = 128;

        private readonly Dictionary<Vector2Int, WorldChunk> cachedChunks = new Dictionary<Vector2Int, WorldChunk>();
        private readonly Queue<Vector2Int> insertionOrder = new Queue<Vector2Int>();

        public int CachedChunkCount => cachedChunks.Count;

        public void StoreChunk(WorldChunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            if (cachedChunks.ContainsKey(chunk.Coordinates))
            {
                cachedChunks[chunk.Coordinates] = chunk;
                return;
            }

            cachedChunks.Add(chunk.Coordinates, chunk);
            insertionOrder.Enqueue(chunk.Coordinates);
            TrimCacheIfNeeded();
        }

        public bool TryGetChunk(Vector2Int coordinates, out WorldChunk chunk)
        {
            return cachedChunks.TryGetValue(coordinates, out chunk);
        }

        public void RemoveChunk(Vector2Int coordinates)
        {
            cachedChunks.Remove(coordinates);
        }

        public void Clear()
        {
            cachedChunks.Clear();
            insertionOrder.Clear();
        }

        private void TrimCacheIfNeeded()
        {
            while (cachedChunks.Count > maxCachedChunks && insertionOrder.Count > 0)
            {
                Vector2Int oldest = insertionOrder.Dequeue();
                cachedChunks.Remove(oldest);
            }
        }

        // TODO: Add cache serialization for world persistence between sessions.
        // TODO: Add LRU policy with chunk activity scoring.
    }
}