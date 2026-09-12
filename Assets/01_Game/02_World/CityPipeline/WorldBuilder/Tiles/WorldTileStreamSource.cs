using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Abstract MonoBehaviour tile stream. Serialized owners reference this base type
    ///     so first-party and Legacy MapMagic adapters remain assignable.
    /// </summary>
    public abstract class WorldTileStreamSource : MonoBehaviour, IWorldTileStream
    {
        public abstract event Action<WorldTileTransition> TileStateChanged;
        public abstract event Action<WorldTileInfo> BeforeTileReset;
        public abstract event Action<WorldTileBatchResult> BatchCompleted;

        public abstract WorldMapSession Session { get; }
        public abstract Rect WorldBoundsXZ { get; }

        public abstract bool TryGetTile(WorldTileCoord coord, out WorldTileInfo info);

        public abstract void CopyTilesAtOrAbove(WorldTileState state, List<WorldTileInfo> buffer);

        public abstract void AppendChunksCoveringTilesAtOrAbove(
            WorldTileState minimumState,
            HashSet<Vector2Int> chunks,
            int gameplayChunkSize,
            int chunkMargin,
            Vector2Int? focusChunk,
            int maxChunkDistanceFromFocus);

        /// <summary>
        ///     Optional generation-target bind (e.g. MapMagicObject). Default is a no-op.
        /// </summary>
        public virtual void BindGenerationTarget(UnityEngine.Object target)
        {
        }

        /// <summary>
        ///     Optional generation-target lookup. Default returns false.
        /// </summary>
        public virtual bool TryGetGenerationTarget(out UnityEngine.Object target)
        {
            target = null;
            return false;
        }

        /// <summary>Optional metrics refresh for streamed backends. Default no-op.</summary>
        public virtual void RefreshTileMetrics()
        {
        }
    }
}
