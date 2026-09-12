#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>Scene-view overlay for terrain grid and city scatter zones in the Development Hub.</summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private void DrawScatterZoneGizmo(SceneView view)
        {
            if (!showScatterGizmo || builder == null)
                return;

            if (!TryResolveHubScatterGizmoData(out var terrainBounds, out var center, out var halfExtent, out var region))
                return;

            var groundY = builder.SampleGroundHeight(terrainBounds.center) + 3f;
            var labelY = groundY + 4f;

            Handles.zTest = CompareFunction.Always;

            Handles.color = WorldRegionScatterGizmoDrawer.TerrainGridColor;
            DrawHandlesRect(terrainBounds, groundY);
            Handles.Label(
                new Vector3(terrainBounds.xMin, labelY, terrainBounds.yMin),
                "Terrain Grid");

            Handles.color = WorldRegionScatterGizmoDrawer.ScatterZoneColor;
            DrawHandlesScatterSquare(center, halfExtent, groundY);
            Handles.Label(
                new Vector3(center.x - halfExtent, labelY, center.y - halfExtent),
                "Scatter Zone");

            if (region != null)
                DrawHandlesRegionSites(region, groundY, labelY);
        }

        private bool TryResolveHubScatterGizmoData(
            out Rect terrainBounds,
            out Vector2 scatterCenter,
            out float scatterHalfExtent,
            out CityRegionAsset region)
        {
            terrainBounds = default;
            scatterCenter = default;
            scatterHalfExtent = 0f;
            region = builder.ActiveRegionAsset;

            var service = TryGetWorldBuilderService();
            var profile = worldProfile ?? service?.Profile;
            var mapSize = profile?.MapSizeSettings;
            var catalog = service?.TileCatalog;

            WorldMapSession session;
            if (catalog != null && catalog.Session.WorldBoundsXZ.width > 0f)
            {
                session = catalog.Session;
            }
            else
            {
                var tilesPerSide = mapSize != null
                    ? mapSize.GetTilesPerSide(editorMapTier)
                    : editorMapTier switch
                    {
                        WorldMapSizeTier.Small => 4,
                        WorldMapSizeTier.Large => 16,
                        _ => 8
                    };

                session = mapSize != null
                    ? mapSize.CreateSession(
                        editorMapTier,
                        editorWorldSeed == 0 ? 1 : editorWorldSeed,
                        profile != null ? profile.ProfileVersion : 1)
                    : WorldMapSession.Create(
                        editorMapTier,
                        editorWorldSeed == 0 ? 1 : editorWorldSeed,
                        profile != null ? profile.ProfileVersion : 1,
                        Vector2.zero,
                        tilesPerSide,
                        WorldMapSizeSettings.TileSizeMeters);
            }

            terrainBounds = WorldTerrainBoundsResolver.Resolve(
                catalog,
                session,
                service != null ? service.transform : null);

            // Scatter zone matches terrain grid footprint (same center + half-extent).
            if (!WorldRegionScatterGizmoDrawer.TryResolveScatterFromBounds(
                    terrainBounds,
                    out scatterCenter,
                    out scatterHalfExtent))
            {
                scatterCenter = terrainBounds.center;
                scatterHalfExtent = 0f;
            }

            return terrainBounds.width > 0f;
        }

        private static void DrawHandlesRect(Rect boundsXZ, float groundY)
        {
            var a = new Vector3(boundsXZ.xMin, groundY, boundsXZ.yMin);
            var b = new Vector3(boundsXZ.xMax, groundY, boundsXZ.yMin);
            var c = new Vector3(boundsXZ.xMax, groundY, boundsXZ.yMax);
            var d = new Vector3(boundsXZ.xMin, groundY, boundsXZ.yMax);
            Handles.DrawLine(a, b);
            Handles.DrawLine(b, c);
            Handles.DrawLine(c, d);
            Handles.DrawLine(d, a);
        }

        private static void DrawHandlesScatterSquare(Vector2 center, float halfExtent, float groundY)
        {
            DrawHandlesRect(
                new Rect(
                    center.x - halfExtent,
                    center.y - halfExtent,
                    halfExtent * 2f,
                    halfExtent * 2f),
                groundY);
        }

        private void DrawHandlesRegionSites(CityRegionAsset region, float groundY, float labelY)
        {
            if (region.sites == null)
                return;

            Handles.color = WorldRegionScatterGizmoDrawer.SiteFootprintColor;
            for (var i = 0; i < region.sites.Count; i++)
            {
                var site = region.sites[i];
                if (site == null)
                    continue;

                if (!builder.TryResolveSiteFootprintRect(site, i, out var footprint))
                {
                    footprint = new Rect(
                        site.centerXZ.x - site.halfWidthMeters,
                        site.centerXZ.y - site.halfDepthMeters,
                        site.halfWidthMeters * 2f,
                        site.halfDepthMeters * 2f);
                }

                DrawHandlesRect(footprint, groundY);

                if (!string.IsNullOrWhiteSpace(site.displayName))
                {
                    Handles.Label(
                        new Vector3(site.centerXZ.x, labelY, site.centerXZ.y),
                        site.displayName);
                }
            }
        }
    }
}
#endif
