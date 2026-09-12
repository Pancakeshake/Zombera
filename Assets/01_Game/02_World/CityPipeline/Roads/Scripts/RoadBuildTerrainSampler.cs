using System.Diagnostics;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Caches active terrain bounds and a coarse XZ grid for one procedural road mesh build.</summary>
    internal sealed class RoadBuildTerrainSampler
    {
        private const float GridCellSizeMeters = 128f;

        private readonly Entry[] _entries;
        private readonly int[] _grid;
        private readonly float _originX;
        private readonly float _originZ;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private int _lastEntryIndex = -1;
        private readonly Stopwatch _sampleWatch = new();

        public int SampleCount { get; private set; }
        public int CachedTerrainHitCount { get; private set; }
        public int FallbackCount { get; private set; }
        public long SampleElapsedMs => _sampleWatch.ElapsedMilliseconds;

        public RoadBuildTerrainSampler()
        {
            var terrains = Terrain.activeTerrains;
            _entries = terrains == null ? System.Array.Empty<Entry>() : new Entry[terrains.Length];
            for (var i = 0; i < _entries.Length; i++)
                _entries[i] = new Entry(terrains[i]);

            if (!TryComputeUnionBounds(out var minX, out var minZ, out var maxX, out var maxZ))
            {
                _grid = System.Array.Empty<int>();
                _originX = _originZ = 0f;
                _gridWidth = _gridHeight = 0;
                return;
            }

            _originX = minX;
            _originZ = minZ;
            _gridWidth = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / GridCellSizeMeters));
            _gridHeight = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / GridCellSizeMeters));
            _grid = new int[_gridWidth * _gridHeight];
            for (var i = 0; i < _grid.Length; i++)
                _grid[i] = -1;

            // First-wins to match linear scan order on Terrain.activeTerrains.
            for (var i = 0; i < _entries.Length; i++)
                StampEntry(i);
        }

        public float Sample(Vector2 worldXZ, System.Func<Vector2, float> fallback)
        {
            _sampleWatch.Start();
            SampleCount++;
            float height;
            var hit = TrySample(worldXZ, out height);
            if (hit)
                CachedTerrainHitCount++;
            else
            {
                FallbackCount++;
                height = fallback(worldXZ);
            }

            _sampleWatch.Stop();
            return height;
        }

        private bool TrySample(Vector2 worldXZ, out float worldY)
        {
            if (TrySampleEntry(_lastEntryIndex, worldXZ, out worldY))
                return true;

            if (TrySampleFromGrid(worldXZ, out worldY))
                return true;

            for (var i = 0; i < _entries.Length; i++)
            {
                if (!TrySampleEntry(i, worldXZ, out worldY))
                    continue;

                _lastEntryIndex = i;
                return true;
            }

            worldY = 0f;
            return false;
        }

        private bool TrySampleFromGrid(Vector2 worldXZ, out float worldY)
        {
            worldY = 0f;
            if (_grid.Length == 0)
                return false;

            var gx = Mathf.FloorToInt((worldXZ.x - _originX) / GridCellSizeMeters);
            var gz = Mathf.FloorToInt((worldXZ.y - _originZ) / GridCellSizeMeters);
            if (gx < 0 || gz < 0 || gx >= _gridWidth || gz >= _gridHeight)
                return false;

            var index = _grid[gz * _gridWidth + gx];
            if (!TrySampleEntry(index, worldXZ, out worldY))
                return false;

            _lastEntryIndex = index;
            return true;
        }

        private bool TrySampleEntry(int index, Vector2 worldXZ, out float worldY)
        {
            worldY = 0f;
            if (index < 0 || index >= _entries.Length)
                return false;

            var entry = _entries[index];
            if (entry.Terrain == null || !entry.Bounds.Contains(worldXZ))
                return false;

            worldY = entry.Terrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) + entry.PositionY;
            return true;
        }

        private bool TryComputeUnionBounds(
            out float minX,
            out float minZ,
            out float maxX,
            out float maxZ)
        {
            minX = minZ = float.PositiveInfinity;
            maxX = maxZ = float.NegativeInfinity;
            var found = false;
            for (var i = 0; i < _entries.Length; i++)
            {
                var bounds = _entries[i].Bounds;
                if (bounds.width <= 0f || bounds.height <= 0f)
                    continue;

                minX = Mathf.Min(minX, bounds.xMin);
                minZ = Mathf.Min(minZ, bounds.yMin);
                maxX = Mathf.Max(maxX, bounds.xMax);
                maxZ = Mathf.Max(maxZ, bounds.yMax);
                found = true;
            }

            return found &&
                   !(float.IsNaN(minX) || float.IsNaN(minZ) || float.IsNaN(maxX) || float.IsNaN(maxZ) ||
                     float.IsInfinity(minX) || float.IsInfinity(minZ) || float.IsInfinity(maxX) ||
                     float.IsInfinity(maxZ));
        }

        private void StampEntry(int entryIndex)
        {
            var bounds = _entries[entryIndex].Bounds;
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            var x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.xMin - _originX) / GridCellSizeMeters), 0, _gridWidth - 1);
            var x1 = Mathf.Clamp(Mathf.FloorToInt((bounds.xMax - _originX) / GridCellSizeMeters), 0, _gridWidth - 1);
            var z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.yMin - _originZ) / GridCellSizeMeters), 0, _gridHeight - 1);
            var z1 = Mathf.Clamp(Mathf.FloorToInt((bounds.yMax - _originZ) / GridCellSizeMeters), 0, _gridHeight - 1);
            for (var z = z0; z <= z1; z++)
            {
                var row = z * _gridWidth;
                for (var x = x0; x <= x1; x++)
                {
                    var cell = row + x;
                    if (_grid[cell] < 0)
                        _grid[cell] = entryIndex;
                }
            }
        }

        private readonly struct Entry
        {
            public readonly Terrain Terrain;
            public readonly Rect Bounds;
            public readonly float PositionY;

            public Entry(Terrain terrain)
            {
                Terrain = terrain;
                if (terrain == null || terrain.terrainData == null)
                {
                    Bounds = default;
                    PositionY = 0f;
                    return;
                }

                var position = terrain.transform.position;
                var size = terrain.terrainData.size;
                Bounds = Rect.MinMaxRect(position.x, position.z, position.x + size.x, position.z + size.z);
                PositionY = position.y;
            }
        }
    }
}
