using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Resolves the authoritative XZ footprint for world generation: prefers
    ///     live allocated terrains, then catalog tiles, then the session plan bounds.
    /// </summary>
    public static class WorldTerrainBoundsResolver
    {
        public static Rect Resolve(
            WorldTileCatalog catalog,
            WorldMapSession session,
            Transform terrainScopeRoot = null)
        {
            if (terrainScopeRoot != null &&
                WorldTileInfoUtility.TryGetTerrainBoundsUnderRoot(terrainScopeRoot, out var scopedBounds))
            {
                return scopedBounds;
            }

            if (WorldTileInfoUtility.TryGetWorldTerrainGridBounds(out var sceneBounds))
                return sceneBounds;

            if (catalog != null &&
                catalog.TryGetWorldBoundsXZ(out var allocated, requireAllocatedTerrain: true))
            {
                return allocated;
            }

            if (catalog != null &&
                catalog.TryGetWorldBoundsXZ(out var catalogBounds, requireAllocatedTerrain: false))
            {
                return catalogBounds;
            }

            return session.WorldBoundsXZ;
        }
    }
}
