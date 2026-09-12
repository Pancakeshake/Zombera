using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Finite tile stream contract consumed by WorldManager, roads, cities and NavMesh.</summary>
    public interface IWorldTileStream
    {
        event Action<WorldTileTransition> TileStateChanged;
        event Action<WorldTileInfo> BeforeTileReset;
        event Action<WorldTileBatchResult> BatchCompleted;

        WorldMapSession Session { get; }
        Rect WorldBoundsXZ { get; }

        bool TryGetTile(WorldTileCoord coord, out WorldTileInfo info);
        void CopyTilesAtOrAbove(WorldTileState state, List<WorldTileInfo> buffer);
        void AppendChunksCoveringTilesAtOrAbove(
            WorldTileState minimumState,
            HashSet<Vector2Int> chunks,
            int gameplayChunkSize,
            int chunkMargin,
            Vector2Int? focusChunk,
            int maxChunkDistanceFromFocus);
    }
}
