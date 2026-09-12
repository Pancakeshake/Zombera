using UnityEngine;

namespace Zombera.World
{
    public sealed partial class MapStateService
    {
        public bool IsChunkDiscovered(Vector2Int chunkCoordinates)
        {
            return _discoveredChunks.Contains(chunkCoordinates);
        }

        public bool DiscoverChunk(Vector2Int chunkCoordinates)
        {
            if (!_discoveredChunks.Add(chunkCoordinates)) return false;

            DiscoveredChunkAdded?.Invoke(chunkCoordinates);
            return true;
        }

        public void ClearDiscoveredChunks()
        {
            _discoveredChunks.Clear();
        }

        private void RevealChunksNearPlayer()
        {
            var radius = Mathf.Max(0, playerDiscoveryRadiusChunks);
            if (radius == 0)
            {
                DiscoverChunk(_currentPlayerChunk);
                return;
            }

            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var chunk = new Vector2Int(_currentPlayerChunk.x + dx, _currentPlayerChunk.y + dz);
                    DiscoverChunk(chunk);
                }
            }
        }
    }
}

