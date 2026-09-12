using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        /// <summary>
        /// True when NavMesh is sampled near the position and no tile bake is pending for that location.
        /// </summary>
        public bool IsNavMeshReadyNear(Vector3 worldPosition)
        {
            if (!IsDrivingRuntimeNavMesh) return true;
            if (!float.IsFinite(worldPosition.x) || !float.IsFinite(worldPosition.z)) return false;

            if (TryFindTileAtWorldPosition(worldPosition, out _, out var coord)
                && IsTileNavMeshWorkPending(coord))
                return false;

            if (TryFindTileAtWorldPosition(worldPosition, out _, out coord)
                && !_tiles.ContainsKey(coord))
                return false;

            return UnitNavUtils.IsNavMeshReadyAt(worldPosition);
        }

        private bool IsTileNavMeshWorkPending(WorldTileCoord coord)
        {
            return _pendingTileBakeSet.Contains(coord)
                   || _pendingAsyncTileBakes.ContainsKey(coord)
                   || _pendingTileByCoord.ContainsKey(coord);
        }

        private bool TryFindTileAtWorldPosition(
            Vector3 worldPosition,
            out WorldTileInfo tile,
            out WorldTileCoord coord)
        {
            tile = default;
            coord = default;
            var xz = new Vector2(worldPosition.x, worldPosition.z);

            foreach (var kvp in _pendingTileByCoord)
            {
                var candidate = kvp.Value;
                if (!candidate.WorldRectXZ.Contains(xz)) continue;

                tile = candidate;
                coord = kvp.Key;
                return true;
            }

            if (worldTileStream == null) return false;

            _streamTileScratch.Clear();
            worldTileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, _streamTileScratch);
            for (var i = 0; i < _streamTileScratch.Count; i++)
            {
                var candidate = _streamTileScratch[i];
                if (!candidate.WorldRectXZ.Contains(xz)) continue;

                tile = candidate;
                coord = candidate.Coord;
                return true;
            }

            return false;
        }
    }
}
