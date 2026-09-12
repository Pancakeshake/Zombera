using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Resolves city site-placement / scatter bounds to match the full terrain grid
    ///     (<see cref="WorldMapSession.WorldBoundsXZ"/>), clipped to allocated terrain when present.
    /// </summary>
    public static class WorldSiteBoundsUtility
    {
        public static Rect ResolvePlayableSiteBounds(
            WorldMapSession session,
            WorldGenerationProfile profile,
            WorldTileCatalog catalog)
        {
            _ = profile;
            var mapBounds = session.WorldBoundsXZ;
            if (mapBounds.width <= 0f || mapBounds.height <= 0f)
                return mapBounds;

            var terrainBounds = WorldTerrainBoundsResolver.Resolve(
                catalog,
                session,
                catalog != null && catalog.transform.root != null
                    ? catalog.transform.root
                    : null);

            if (catalog != null &&
                catalog.TryGetWorldBoundsXZ(out var allocated, requireAllocatedTerrain: true) &&
                allocated.width > 0f)
            {
                terrainBounds = allocated;
            }

            if (terrainBounds.width <= 0f || terrainBounds.height <= 0f)
                return mapBounds;

            var clipped = WorldMapBoundaryUtility.IntersectRects(mapBounds, terrainBounds);
            return clipped.width > 0f && clipped.height > 0f
                ? clipped
                : mapBounds;
        }
    }
}
