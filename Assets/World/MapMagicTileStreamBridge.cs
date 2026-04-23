using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Runtime bridge: reads MapMagic's deployed tile grid and maps each tile's world footprint to
    /// gameplay chunk coordinates (<see cref="ChunkLoader"/>). Also records tile-apply events for metrics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapMagicTileStreamBridge : MonoBehaviour
    {
        [SerializeField] private MapMagicObject targetMapMagic;

        /// <summary>Fired on main thread when a terrain tile finishes apply for the bound MapMagic instance.</summary>
        public event System.Action<TerrainTile, TileData> TileAppliedForGameplay;

        public MapMagicObject TargetMapMagic => targetMapMagic;

        public void Bind(MapMagicObject mapMagic)
        {
            targetMapMagic = mapMagic;
        }

        public bool TryGetTarget(out MapMagicObject mapMagic)
        {
            mapMagic = targetMapMagic != null ? targetMapMagic : FindFirstObjectByType<MapMagicObject>();
            if (targetMapMagic == null)
            {
                targetMapMagic = mapMagic;
            }

            return mapMagic != null;
        }

        private void OnEnable()
        {
            TerrainTile.OnTileApplied += HandleTileApplied;
        }

        private void OnDisable()
        {
            TerrainTile.OnTileApplied -= HandleTileApplied;
        }

        private void HandleTileApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (tile == null || targetMapMagic == null || tile.mapMagic != targetMapMagic)
            {
                return;
            }

            StreamedWorldMetrics.RecordMapMagicTileApplied();
            TileAppliedForGameplay?.Invoke(tile, data);
        }

        /// <summary>
        /// Refreshes metrics from the MapMagic tile manager (bounded by MapMagic retention; no unbounded growth).
        /// </summary>
        public void RefreshTileMetrics()
        {
            if (targetMapMagic == null || targetMapMagic.tiles == null)
            {
                StreamedWorldMetrics.SetActiveMapMagicTiles(0);
                return;
            }

            StreamedWorldMetrics.SetActiveMapMagicTiles(targetMapMagic.tiles.grid.Count);
        }

        /// <summary>
        /// Adds every gameplay chunk index overlapping any active MapMagic tile world rect (plus margin in chunk units).
        /// </summary>
        public void AppendChunksCoveringDeployedTiles(HashSet<Vector2Int> chunks, int gameplayChunkSize, int chunkMargin)
        {
            if (chunks == null)
            {
                return;
            }

            if (!TryGetTarget(out MapMagicObject mapMagic) || mapMagic.tiles == null)
            {
                return;
            }

            int cs = Mathf.Max(1, gameplayChunkSize);
            float marginWorld = Mathf.Max(0, chunkMargin) * cs;

            foreach (KeyValuePair<Coord, TerrainTile> kvp in mapMagic.tiles.grid)
            {
                TerrainTile tile = kvp.Value;
                if (tile == null)
                {
                    continue;
                }

                Rect worldRect = tile.WorldRect;
                worldRect.xMin -= marginWorld;
                worldRect.yMin -= marginWorld;
                worldRect.xMax += marginWorld;
                worldRect.yMax += marginWorld;

                AppendChunksForWorldRectXZ(worldRect, cs, chunks);
            }

            RefreshTileMetrics();
        }

        private static void AppendChunksForWorldRectXZ(Rect worldRectXZ, int chunkSize, HashSet<Vector2Int> chunks)
        {
            int minCx = Mathf.FloorToInt(worldRectXZ.xMin / chunkSize);
            int maxCx = Mathf.FloorToInt((worldRectXZ.xMax - 0.001f) / chunkSize);
            int minCz = Mathf.FloorToInt(worldRectXZ.yMin / chunkSize);
            int maxCz = Mathf.FloorToInt((worldRectXZ.yMax - 0.001f) / chunkSize);

            for (int x = minCx; x <= maxCx; x++)
            {
                for (int z = minCz; z <= maxCz; z++)
                {
                    chunks.Add(new Vector2Int(x, z));
                }
            }
        }
    }
}
