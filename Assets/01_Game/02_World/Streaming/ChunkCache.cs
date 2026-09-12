#region

using System.Collections.Generic;
using System.Text;
using UnityEngine;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Caches unloaded chunk data snapshots for fast reactivation.
    /// </summary>
    public sealed class ChunkCache : MonoBehaviour
    {
        [SerializeField] private int maxCachedChunks = 128;

        private readonly Dictionary<Vector2Int, WorldChunk> _cachedChunks = new();
        private readonly Queue<Vector2Int> _insertionOrder = new();

        // ReSharper disable once UnusedMember.Global
        public int CachedChunkCount => _cachedChunks.Count;

        public void StoreChunk(WorldChunk chunk)
        {
            if (chunk == null) return;

            if (!_cachedChunks.TryAdd(chunk.Coordinates, chunk))
            {
                _cachedChunks[chunk.Coordinates] = chunk;
                return;
            }

            _insertionOrder.Enqueue(chunk.Coordinates);
            TrimCacheIfNeeded();
        }

        public bool TryGetChunk(Vector2Int coordinates, out WorldChunk chunk)
        {
            return _cachedChunks.TryGetValue(coordinates, out chunk);
        }

        public void RemoveChunk(Vector2Int coordinates)
        {
            _cachedChunks.Remove(coordinates);
        }

        public void Clear()
        {
            _cachedChunks.Clear();
            _insertionOrder.Clear();
        }

        private void TrimCacheIfNeeded()
        {
            while (_cachedChunks.Count > maxCachedChunks && _insertionOrder.Count > 0)
            {
                var oldest = _insertionOrder.Dequeue();
                _cachedChunks.Remove(oldest);
            }
        }

        /// <summary>Returns all cached chunk data as JSON-compatible string for session persistence.</summary>
        // ReSharper disable once UnusedMember.Global
        public string SerializeToJson()
        {
            var sb = new StringBuilder();
            sb.Append("[");
            var first = true;

            foreach (var entry in _cachedChunks)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append(
                    $"{{\"x\":{entry.Key.x},\"y\":{entry.Key.y},\"seed\":{entry.Value.Seed},\"regionId\":\"{entry.Value.RegionId}\",\"dirty\":{(entry.Value.IsDirty ? "true" : "false")}}}");
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}