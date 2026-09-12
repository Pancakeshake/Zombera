using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Shared highway corridor landform carve → Unity heightmap bake used by Plan/Refine stages.
    /// </summary>
    public static class HighwayCorridorTerrainBake
    {
        private static readonly List<WorldTileCoord> Tiles = new(64);
        private static readonly List<Rect> CorridorBounds = new(16);
        private static readonly List<CityFlattenPad> PadScratch = new(16);

        public static void CollectCorridorBakeBounds(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            List<Rect> bounds)
        {
            bounds.Clear();
            if (roads == null || settings == null)
                return;

            var margin = Mathf.Max(
                settings.highwayTerrainShoulderMeters + settings.highwayCoarseCorridorBlendMeters,
                64f);

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.roadClass != RoadClass.Highway)
                    continue;

                var rect = road.BoundsXZ;
                if (rect.width <= 0f || rect.height <= 0f)
                    continue;

                bounds.Add(Rect.MinMaxRect(
                    rect.xMin - margin,
                    rect.yMin - margin,
                    rect.xMax + margin,
                    rect.yMax + margin));
            }
        }

        /// <summary>
        ///     Re-carves refined highways into the landform field and bakes overlapping Unity tiles.
        /// </summary>
        public static int CarveAndBake(WorldBuildContext context, int detailSeedXor)
        {
            if (context?.Artifacts?.Landforms == null ||
                context.Artifacts.Roads?.Roads == null ||
                context.Profile?.RoadNetworkSettings == null)
                return 0;

            var settings = context.Profile.RoadNetworkSettings;
            var roads = context.Artifacts.Roads.Roads;
            BuildPadAnchorsFromSites(context.Artifacts.Sites, PadScratch);

            var carved = HighwayCorridorCarver.Carve(
                context.Artifacts.Landforms,
                roads,
                settings,
                new HighwayCorridorCarver.HighwayCarveOptions
                {
                    Hydrology = context.Artifacts.Hydrology,
                    PadAnchors = PadScratch.Count > 0 ? PadScratch : null,
                    MaxReclaimDepthMeters = CityPadReclaimPolicy.MaxReclaimDepthMeters(
                        context.Profile.Landforms),
                    Tunnels = context.Artifacts.Tunnels
                });

            var catalog = context.WorldBuilder?.TileCatalog;
            if (catalog == null || context.Profile.TerrainGrid == null)
            {
                context.Artifacts.SetHighwayCorridorsBakedPostRefine(carved >= 0);
                return carved;
            }

            CollectCorridorBakeBounds(roads, settings, CorridorBounds);
            if (CorridorBounds.Count > 0)
            {
                WorldBuildScopeUtility.CollectTilesOverlappingRects(
                    CorridorBounds,
                    context.Session,
                    Tiles);
            }
            else
            {
                WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, Tiles);
            }

            LandformHeightmapBaker.BakeScopedTiles(
                context.Artifacts.Landforms,
                context.Profile.TerrainGrid,
                context.Profile.Hydrology,
                catalog,
                Tiles,
                context.Session.Seed ^ detailSeedXor,
                applyDetailNoise: false);
            SyncScopedHeightmaps(catalog, Tiles);
            context.Artifacts.SetHighwayCorridorsBakedPostRefine(true);
            return carved;
        }

        public static void SyncScopedHeightmaps(WorldTileCatalog catalog, List<WorldTileCoord> tiles)
        {
            if (catalog == null || tiles == null)
                return;

            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain) ||
                    terrain.terrainData == null)
                    continue;

                terrain.terrainData.SyncHeightmap();
            }

            Physics.SyncTransforms();
        }

        private static void BuildPadAnchorsFromSites(WorldSitePlan sites, List<CityFlattenPad> pads)
        {
            pads.Clear();
            if (sites?.CitySites == null)
                return;

            for (var i = 0; i < sites.CitySites.Count; i++)
            {
                var site = sites.CitySites[i];
                if (site == null)
                    continue;

                var halfW = Mathf.Max(40f, site.HalfWidthMeters);
                var halfD = Mathf.Max(40f, site.HalfDepthMeters);
                var bounds = Rect.MinMaxRect(
                    site.CenterXZ.x - halfW,
                    site.CenterXZ.y - halfD,
                    site.CenterXZ.x + halfW,
                    site.CenterXZ.y + halfD);
                pads.Add(new CityFlattenPad(bounds, site.PadHeightWorldY, falloffMeters: 32f));
            }
        }
    }
}
