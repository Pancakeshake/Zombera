using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldTileOwnership
    {
        public static bool TryResolveOwnerTile(
            WorldStateHeader header,
            Vector3 worldPosition,
            out WorldTileKey tile,
            bool clamp = false)
        {
            return TryResolveOwnerTile(
                header,
                new Vector2(worldPosition.x, worldPosition.z),
                out tile,
                clamp);
        }

        public static bool TryResolveOwnerTile(
            WorldStateHeader header,
            Vector2 worldPositionXZ,
            out WorldTileKey tile,
            bool clamp = false)
        {
            tile = default;
            if (!TryResolveTileIndex(header, worldPositionXZ.x, header?.worldOriginXZ.x ?? 0f, out var x, clamp))
                return false;

            if (!TryResolveTileIndex(header, worldPositionXZ.y, header.worldOriginXZ.y, out var z, clamp))
                return false;

            tile = new WorldTileKey(x, z);
            return true;
        }

        public static void CopyCoveredTiles(
            WorldStateHeader header,
            Rect boundsXZ,
            List<WorldTileKey> results,
            bool clamp = false)
        {
            if (results == null)
                return;

            results.Clear();
            if (!TryGetTileRange(header, boundsXZ, out var minX, out var minZ, out var maxX, out var maxZ, clamp))
                return;

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                    results.Add(new WorldTileKey(x, z));
            }
        }

        public static WorldTileKey ResolveOwnerTile(
            WorldStateHeader header,
            Vector2 worldPositionXZ,
            bool clamp = false)
        {
            return TryResolveOwnerTile(header, worldPositionXZ, out var tile, clamp)
                ? tile
                : default;
        }

        public static WorldTileKey ResolveOwnerTile(
            WorldStateHeader header,
            Vector3 worldPosition,
            bool clamp = false)
        {
            return TryResolveOwnerTile(header, worldPosition, out var tile, clamp)
                ? tile
                : default;
        }

        private static bool TryGetTileRange(
            WorldStateHeader header,
            Rect boundsXZ,
            out int minX,
            out int minZ,
            out int maxX,
            out int maxZ,
            bool clamp)
        {
            minX = minZ = maxX = maxZ = 0;
            var min = boundsXZ.min;
            var max = boundsXZ.max;
            if (boundsXZ.width <= 0f && boundsXZ.height <= 0f)
                max = min;

            if (!TryResolveTileIndex(header, min.x, header?.worldOriginXZ.x ?? 0f, out minX, clamp))
                return false;
            if (!TryResolveTileIndex(header, min.y, header.worldOriginXZ.y, out minZ, clamp))
                return false;
            if (!TryResolveTileIndex(header, max.x, header.worldOriginXZ.x, out maxX, true))
                return false;
            if (!TryResolveTileIndex(header, max.y, header.worldOriginXZ.y, out maxZ, true))
                return false;

            if (maxX < minX || maxZ < minZ)
                return false;

            return true;
        }

        private static bool TryResolveTileIndex(
            WorldStateHeader header,
            float position,
            float origin,
            out int index,
            bool clamp)
        {
            index = 0;
            if (!HasUsableGrid(header))
                return false;

            var local = position - origin;
            var sideLength = header.tilesPerSide * header.tileSizeMeters;
            if (!ClampOrReject(local, sideLength, ref local, clamp))
                return false;

            index = Mathf.FloorToInt(local / header.tileSizeMeters);
            index = Mathf.Clamp(index, 0, header.tilesPerSide - 1);
            return true;
        }

        private static bool ClampOrReject(
            float local,
            float sideLength,
            ref float result,
            bool clamp)
        {
            if (clamp)
            {
                result = Mathf.Clamp(local, 0f, sideLength);
                return true;
            }

            if (local < 0f || local > sideLength)
                return false;

            result = local;
            return true;
        }

        private static bool HasUsableGrid(WorldStateHeader header) =>
            header != null &&
            header.tilesPerSide > 0 &&
            header.tileSizeMeters > 0f;
    }
}
