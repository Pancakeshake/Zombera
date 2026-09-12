using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Resolves tile coords covered by a <see cref="WorldBuildScope"/>.</summary>
    public static class WorldBuildScopeUtility
    {
        public static void CollectTiles(
            WorldBuildScope scope,
            WorldMapSession session,
            List<WorldTileCoord> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();

            var tilesPerSide = Mathf.Max(1, session.TilesPerSide);
            if (scope.Kind == WorldBuildScopeKind.TileSet && scope.Tiles != null && scope.Tiles.Count > 0)
            {
                for (var i = 0; i < scope.Tiles.Count; i++)
                {
                    var coord = scope.Tiles[i];
                    if (coord.X < 0 || coord.Z < 0 || coord.X >= tilesPerSide || coord.Z >= tilesPerSide)
                        continue;
                    buffer.Add(coord);
                }

                return;
            }

            if (scope.Kind == WorldBuildScopeKind.FullMap || scope.BoundsXZ.width <= 0f || scope.BoundsXZ.height <= 0f)
            {
                for (var z = 0; z < tilesPerSide; z++)
                {
                    for (var x = 0; x < tilesPerSide; x++)
                        buffer.Add(new WorldTileCoord(x, z));
                }

                return;
            }

            CollectIntersecting(scope.BoundsXZ, session, buffer);
        }

        public static void CollectTilesOverlappingRects(
            IReadOnlyList<Rect> rects,
            WorldMapSession session,
            List<WorldTileCoord> buffer)
        {
            buffer.Clear();
            if (rects == null || rects.Count == 0)
                return;

            var seen = new HashSet<WorldTileCoord>();
            var scratch = new List<WorldTileCoord>(32);
            for (var i = 0; i < rects.Count; i++)
            {
                scratch.Clear();
                CollectIntersecting(rects[i], session, scratch);
                for (var t = 0; t < scratch.Count; t++)
                {
                    if (!seen.Add(scratch[t]))
                        continue;
                    buffer.Add(scratch[t]);
                }
            }
        }

        public static void CollectIntersecting(
            Rect boundsXZ,
            WorldMapSession session,
            List<WorldTileCoord> buffer)
        {
            if (buffer == null) return;

            var tilesPerSide = Mathf.Max(1, session.TilesPerSide);
            var tileSize = session.TileSizeMeters > 0f ? session.TileSizeMeters : 1000f;
            var origin = session.WorldOriginXZ;

            for (var z = 0; z < tilesPerSide; z++)
            {
                for (var x = 0; x < tilesPerSide; x++)
                {
                    var rect = new Rect(origin.x + x * tileSize, origin.y + z * tileSize, tileSize, tileSize);
                    if (!boundsXZ.Overlaps(rect)) continue;
                    buffer.Add(new WorldTileCoord(x, z));
                }
            }
        }
    }
}
