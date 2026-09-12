using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>First-party finite tile catalog and stream source.</summary>
    [AddComponentMenu("Zombera/World/World Tile Catalog")]
    [DisallowMultipleComponent]
    public sealed class WorldTileCatalog : WorldTileStreamSource, IWorldTileGameplayEvents
    {
        private readonly Dictionary<WorldTileCoord, WorldTileInfo> _tiles = new();
        private readonly List<WorldTileInfo> _scratch = new();
        private WorldMapSession _session;

        public override event Action<WorldTileTransition> TileStateChanged;
        public override event Action<WorldTileInfo> BeforeTileReset;
        public override event Action<WorldTileBatchResult> BatchCompleted;
        public event Action<WorldTileInfo> TileAppliedForGameplay;

        public override WorldMapSession Session => _session;
        public override Rect WorldBoundsXZ => _session.WorldBoundsXZ;
        public int Count => _tiles.Count;

        public void Configure(WorldMapSession session)
        {
            _session = session;
            _tiles.Clear();

            var tiles = Mathf.Max(1, session.TilesPerSide);
            var tileSize = session.TileSizeMeters > 0f ? session.TileSizeMeters : 1000f;
            var origin = session.WorldOriginXZ;

            for (var z = 0; z < tiles; z++)
            {
                for (var x = 0; x < tiles; x++)
                {
                    var coord = new WorldTileCoord(x, z);
                    var rect = new Rect(origin.x + x * tileSize, origin.y + z * tileSize, tileSize, tileSize);
                    _tiles[coord] = new WorldTileInfo(coord, rect, null, WorldTileState.None);
                }
            }
        }

        public override bool TryGetTile(WorldTileCoord coord, out WorldTileInfo info) =>
            _tiles.TryGetValue(coord, out info);

        public bool Contains(WorldTileCoord coord) => _tiles.ContainsKey(coord);

        public void CopyTilesIntersecting(Rect boundsXZ, List<WorldTileInfo> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            foreach (var pair in _tiles)
            {
                if (!boundsXZ.Overlaps(pair.Value.WorldRectXZ)) continue;
                buffer.Add(pair.Value);
            }
        }

        public override void CopyTilesAtOrAbove(WorldTileState state, List<WorldTileInfo> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            foreach (var pair in _tiles)
            {
                if (pair.Value.State < state) continue;
                buffer.Add(pair.Value);
            }
        }

        public override void AppendChunksCoveringTilesAtOrAbove(
            WorldTileState minimumState,
            HashSet<Vector2Int> chunks,
            int gameplayChunkSize,
            int chunkMargin,
            Vector2Int? focusChunk,
            int maxChunkDistanceFromFocus)
        {
            if (chunks == null || gameplayChunkSize <= 0) return;

            var clamp = focusChunk.HasValue && maxChunkDistanceFromFocus > 0;
            var focus = focusChunk.GetValueOrDefault();
            var minX = focus.x - maxChunkDistanceFromFocus;
            var maxX = focus.x + maxChunkDistanceFromFocus;
            var minZ = focus.y - maxChunkDistanceFromFocus;
            var maxZ = focus.y + maxChunkDistanceFromFocus;

            foreach (var pair in _tiles)
            {
                var info = pair.Value;
                if (info.State < minimumState) continue;

                var rect = info.WorldRectXZ;
                var x0 = Mathf.FloorToInt(rect.xMin / gameplayChunkSize) - chunkMargin;
                var x1 = Mathf.FloorToInt((rect.xMax - 0.001f) / gameplayChunkSize) + chunkMargin;
                var z0 = Mathf.FloorToInt(rect.yMin / gameplayChunkSize) - chunkMargin;
                var z1 = Mathf.FloorToInt((rect.yMax - 0.001f) / gameplayChunkSize) + chunkMargin;

                for (var z = z0; z <= z1; z++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        if (clamp && (x < minX || x > maxX || z < minZ || z > maxZ))
                            continue;
                        chunks.Add(new Vector2Int(x, z));
                    }
                }
            }
        }

        public void SetTerrain(WorldTileCoord coord, Terrain terrain)
        {
            if (!_tiles.TryGetValue(coord, out var info)) return;
            _tiles[coord] = info.WithTerrain(terrain);
        }

        public bool TryTransition(WorldTileCoord coord, WorldTileState expected, WorldTileState next)
        {
            if (!_tiles.TryGetValue(coord, out var info)) return false;
            if (info.State != expected) return false;

            var updated = info.WithState(next);
            _tiles[coord] = updated;
            TileStateChanged?.Invoke(new WorldTileTransition(updated, expected, next));
            RaiseGameplayApplyIfNeeded(updated, expected, next);
            RefreshTileMetrics();
            return true;
        }

        public override void RefreshTileMetrics()
        {
            var contentReady = 0;
            var allocated = 0;
            foreach (var pair in _tiles)
            {
                if (pair.Value.State >= WorldTileState.Allocated)
                    allocated++;
                if (pair.Value.State >= WorldTileState.ContentReady)
                    contentReady++;
            }

            StreamedWorldMetrics.SetActiveStreamedTiles(allocated);
            StreamedWorldMetrics.SetContentReadyTiles(contentReady);
        }

        private void RaiseGameplayApplyIfNeeded(
            WorldTileInfo updated,
            WorldTileState previous,
            WorldTileState next)
        {
            if (next < WorldTileState.ContentReady) return;
            if (previous >= WorldTileState.ContentReady) return;
            TileAppliedForGameplay?.Invoke(updated);
            StreamedWorldMetrics.RecordTileAppliedForGameplay();
        }

        public void SanitizeDestroyedTerrains()
        {
            var coords = new List<WorldTileCoord>(_tiles.Count);
            foreach (var pair in _tiles)
                coords.Add(pair.Key);

            for (var i = 0; i < coords.Count; i++)
            {
                var coord = coords[i];
                if (!_tiles.TryGetValue(coord, out var info))
                    continue;

                if (WorldTileInfoUtility.TryGetLiveTerrain(info, out _))
                    continue;

                _tiles[coord] = info.WithTerrain(null).WithState(WorldTileState.None);
            }
        }

        public void Invalidate(WorldTileCoord coord) =>
            InvalidateTo(coord, WorldTileState.None);

        internal void InvalidateTo(WorldTileCoord coord, WorldTileState state)
        {
            if (!_tiles.TryGetValue(coord, out var info)) return;
            if (info.State == state) return;

            BeforeTileReset?.Invoke(info);
            var previous = info.State;
            var updated = info.WithTerrain(null).WithState(state);
            _tiles[coord] = updated;
            TileStateChanged?.Invoke(new WorldTileTransition(updated, previous, state));
        }

        public void RequestTiles(IReadOnlyList<WorldTileCoord> coords)
        {
            if (coords == null) return;
            for (var i = 0; i < coords.Count; i++)
            {
                var coord = coords[i];
                if (_tiles.ContainsKey(coord)) continue;
                if (_session.TilesPerSide <= 0) continue;
                if (coord.X < 0 || coord.Z < 0 || coord.X >= _session.TilesPerSide ||
                    coord.Z >= _session.TilesPerSide)
                    continue;

                var tileSize = _session.TileSizeMeters;
                var origin = _session.WorldOriginXZ;
                var rect = new Rect(
                    origin.x + coord.X * tileSize,
                    origin.y + coord.Z * tileSize,
                    tileSize,
                    tileSize);
                _tiles[coord] = new WorldTileInfo(coord, rect, null, WorldTileState.None);
            }
        }

        public void RaiseBatchCompleted(WorldTileBatchResult result) =>
            BatchCompleted?.Invoke(result);

        public void Clear()
        {
            _scratch.Clear();
            foreach (var pair in _tiles)
                BeforeTileReset?.Invoke(pair.Value);
            _tiles.Clear();
        }

        /// <summary>
        ///     Union of tile rects in the catalog. When
        ///     <paramref name="requireAllocatedTerrain"/> is true, only tiles with
        ///     a live <see cref="Terrain"/> are included (post Allocate Terrain Grid).
        /// </summary>
        public bool TryGetWorldBoundsXZ(out Rect boundsXZ, bool requireAllocatedTerrain = false)
        {
            boundsXZ = default;
            var found = false;

            foreach (var pair in _tiles)
            {
                var info = pair.Value;
                if (requireAllocatedTerrain && info.Terrain == null)
                    continue;

                var rect = ResolveTileBoundsXZ(info);
                if (rect.width <= 0f || rect.height <= 0f)
                    continue;

                boundsXZ = found ? UnionRect(boundsXZ, rect) : rect;
                found = true;
            }

            return found && boundsXZ.width > 0f && boundsXZ.height > 0f;
        }

        private static Rect ResolveTileBoundsXZ(WorldTileInfo info)
        {
            if (info.Terrain != null && info.Terrain.terrainData != null)
            {
                var pos = info.Terrain.transform.position;
                var size = info.Terrain.terrainData.size;
                return new Rect(pos.x, pos.z, size.x, size.z);
            }

            return info.WorldRectXZ;
        }

        private static Rect UnionRect(Rect a, Rect b)
        {
            if (a.width <= 0f || a.height <= 0f)
                return b;
            if (b.width <= 0f || b.height <= 0f)
                return a;

            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
