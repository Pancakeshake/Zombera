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

        // Activity scoring for LRU promotion: higher score = more recently accessed.
        private readonly Dictionary<Vector2Int, float> accessTimestamps = new Dictionary<Vector2Int, float>();

        public WorldChunk GetChunkWithLRU(Vector2Int coordinates)
        {
            if (!cachedChunks.TryGetValue(coordinates, out WorldChunk chunk))
            {
                return null;
            }

            accessTimestamps[coordinates] = UnityEngine.Time.realtimeSinceStartup;
            return chunk;
        }

        /// <summary>Returns all cached chunk data as JSON-compatible string for session persistence.</summary>
        public string SerializeToJson()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[");
            bool first = true;

            foreach (System.Collections.Generic.KeyValuePair<Vector2Int, WorldChunk> entry in cachedChunks)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"{{\"x\":{entry.Key.x},\"y\":{entry.Key.y},\"seed\":{entry.Value.Seed},\"regionId\":\"{entry.Value.RegionId}\",\"dirty\":{(entry.Value.IsDirty ? "true" : "false")}}}");
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}