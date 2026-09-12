using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Links Unity Terrain neighbors across the catalog and stitches shared heightmap edges
    ///     so LOD seams do not leave cyan slits (ocean / clear color) between tiles.
    /// </summary>
    public static class WorldTerrainNeighborUtility
    {
        public static void LinkAll(WorldTileCatalog catalog)
        {
            if (catalog?.Session == null)
                return;

            var side = Mathf.Max(1, catalog.Session.TilesPerSide);
            for (var z = 0; z < side; z++)
            {
                for (var x = 0; x < side; x++)
                {
                    if (!TryLive(catalog, x, z, out var terrain))
                        continue;

                    TryLive(catalog, x - 1, z, out var left);
                    TryLive(catalog, x, z + 1, out var top);
                    TryLive(catalog, x + 1, z, out var right);
                    TryLive(catalog, x, z - 1, out var bottom);
                    terrain.SetNeighbors(left, top, right, bottom);
                }
            }
        }

        /// <summary>
        ///     Stitch shared height edges then re-link neighbors. Call after mid-pipeline height writes
        ///     (pads, road stamp, highway beds) so Finalize is not the only seam authority.
        /// </summary>
        public static void StitchAndLink(WorldTileCatalog catalog)
        {
            StitchSharedEdges(catalog);
            LinkAll(catalog);
        }

        /// <summary>
        ///     Copies each tile's east/north edge heights onto the west/south edge of its neighbor.
        ///     Lower-coord tiles own the shared edge so both sides match exactly.
        /// </summary>
        public static void StitchSharedEdges(WorldTileCatalog catalog)
        {
            if (catalog?.Session == null)
                return;

            var side = Mathf.Max(1, catalog.Session.TilesPerSide);
            for (var z = 0; z < side; z++)
            {
                for (var x = 0; x < side; x++)
                {
                    if (!TryLive(catalog, x, z, out var terrain))
                        continue;

                    if (TryLive(catalog, x + 1, z, out var east))
                        StitchEastEdge(terrain, east);
                    if (TryLive(catalog, x, z + 1, out var north))
                        StitchNorthEdge(terrain, north);
                }
            }
        }

        private static bool TryLive(WorldTileCatalog catalog, int x, int z, out Terrain terrain)
        {
            terrain = null;
            if (x < 0 || z < 0)
                return false;
            if (!catalog.TryGetTile(new WorldTileCoord(x, z), out var info))
                return false;
            return WorldTileInfoUtility.TryGetLiveTerrain(info, out terrain);
        }

        private static void StitchEastEdge(Terrain west, Terrain east)
        {
            var westData = west.terrainData;
            var eastData = east.terrainData;
            if (westData == null || eastData == null)
                return;
            if (westData.heightmapResolution != eastData.heightmapResolution)
                return;

            var res = westData.heightmapResolution;
            var edge = westData.GetHeights(res - 1, 0, 1, res);
            eastData.SetHeights(0, 0, edge);
            eastData.SyncHeightmap();
        }

        private static void StitchNorthEdge(Terrain south, Terrain north)
        {
            var southData = south.terrainData;
            var northData = north.terrainData;
            if (southData == null || northData == null)
                return;
            if (southData.heightmapResolution != northData.heightmapResolution)
                return;

            var res = southData.heightmapResolution;
            var edge = southData.GetHeights(0, res - 1, res, 1);
            northData.SetHeights(0, 0, edge);
            northData.SyncHeightmap();
        }
    }
}
