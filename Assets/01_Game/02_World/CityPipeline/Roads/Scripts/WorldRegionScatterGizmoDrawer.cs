using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Shared wire drawing for world terrain grid bounds and city scatter zones.</summary>
    public static class WorldRegionScatterGizmoDrawer
    {
        public static readonly Color TerrainGridColor = new(0.95f, 0.55f, 0.12f, 0.9f);
        public static readonly Color ScatterZoneColor = new(0.25f, 0.9f, 0.45f, 0.9f);
        public static readonly Color SiteCenterColor = new(0.35f, 0.75f, 1f, 0.95f);
        public static readonly Color SiteFootprintColor = new(0.35f, 0.75f, 1f, 0.55f);

        public static void DrawTerrainBounds(Rect boundsXZ, float groundY)
        {
            if (boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            Gizmos.color = TerrainGridColor;
            DrawRect(boundsXZ, groundY);
        }

        public static void DrawScatterZone(Vector2 centerXZ, float halfExtentMeters, float groundY)
        {
            if (halfExtentMeters < 1f)
                return;

            Gizmos.color = ScatterZoneColor;
            var bounds = new Rect(
                centerXZ.x - halfExtentMeters,
                centerXZ.y - halfExtentMeters,
                halfExtentMeters * 2f,
                halfExtentMeters * 2f);
            DrawRect(bounds, groundY);
        }

        public static void DrawRegionSites(
            CityRegionAsset region,
            float groundY,
            System.Func<CityHubSite, int, Rect> resolveFootprint = null)
        {
            if (region?.sites == null || region.SiteCount == 0)
                return;

            for (var i = 0; i < region.sites.Count; i++)
            {
                var site = region.sites[i];
                if (site == null)
                    continue;

                var footprint = resolveFootprint != null
                    ? resolveFootprint(site, i)
                    : default;
                if (footprint.width <= 0f || footprint.height <= 0f)
                {
                    footprint = new Rect(
                        site.centerXZ.x - site.halfWidthMeters,
                        site.centerXZ.y - site.halfDepthMeters,
                        site.halfWidthMeters * 2f,
                        site.halfDepthMeters * 2f);
                }

                Gizmos.color = SiteFootprintColor;
                DrawRect(footprint, groundY);

                Gizmos.color = SiteCenterColor;
                Gizmos.DrawWireSphere(
                    new Vector3(site.centerXZ.x, groundY + 2f, site.centerXZ.y),
                    Mathf.Max(12f, Mathf.Min(site.halfWidthMeters, site.halfDepthMeters) * 0.08f));
            }
        }

        public static void DrawRegion(
            CityRegionAsset region,
            float groundY,
            bool drawSites = true,
            System.Func<CityHubSite, int, Rect> resolveFootprint = null)
        {
            if (region == null)
                return;

            region.ResolveScatterArea(out var center, out var halfExtent);
            DrawScatterZone(center, halfExtent, groundY);
            if (drawSites)
                DrawRegionSites(region, groundY, resolveFootprint);
        }

        public static void DrawAll(
            Rect terrainBoundsXZ,
            CityRegionAsset region,
            float groundY,
            bool drawSites = true,
            System.Func<CityHubSite, int, Rect> resolveFootprint = null)
        {
            DrawTerrainBounds(terrainBoundsXZ, groundY);
            DrawRegion(region, groundY, drawSites, resolveFootprint);
        }

        public static void DrawPipelineRegion(
            Rect terrainBoundsXZ,
            CityRegionAsset region,
            float groundY,
            bool drawSites = true,
            System.Func<CityHubSite, int, Rect> resolveFootprint = null)
        {
            DrawTerrainBounds(terrainBoundsXZ, groundY);
            if (TryResolveScatterFromBounds(terrainBoundsXZ, out var center, out var halfExtent))
                DrawScatterZone(center, halfExtent, groundY);
            if (drawSites && region != null)
                DrawRegionSites(region, groundY, resolveFootprint);
        }

        public static bool TryResolveScatterFromBounds(
            Rect terrainBoundsXZ,
            out Vector2 center,
            out float halfExtent)
        {
            center = default;
            halfExtent = 0f;
            if (terrainBoundsXZ.width <= 0f || terrainBoundsXZ.height <= 0f)
                return false;

            center = terrainBoundsXZ.center;
            halfExtent = Mathf.Min(terrainBoundsXZ.width, terrainBoundsXZ.height) * 0.5f;
            return halfExtent >= 1f;
        }

        public static bool TryResolveScatterFromBounds(
            Rect terrainBoundsXZ,
            float edgeClearanceMeters,
            out Vector2 center,
            out float halfExtent)
        {
            center = default;
            halfExtent = 0f;
            if (terrainBoundsXZ.width <= 0f || terrainBoundsXZ.height <= 0f)
                return false;

            var inset = Mathf.Max(0f, edgeClearanceMeters);
            var xMin = terrainBoundsXZ.xMin + inset;
            var xMax = terrainBoundsXZ.xMax - inset;
            var yMin = terrainBoundsXZ.yMin + inset;
            var yMax = terrainBoundsXZ.yMax - inset;
            if (xMax <= xMin || yMax <= yMin)
                return TryResolveScatterFromBounds(terrainBoundsXZ, out center, out halfExtent);

            center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            halfExtent = Mathf.Min(xMax - xMin, yMax - yMin) * 0.5f;
            return halfExtent >= 1f;
        }

        public static bool TryResolveScatterFromPlayableBounds(
            Rect playableBoundsXZ,
            out Vector2 center,
            out float halfExtent) =>
            TryResolveScatterFromBounds(playableBoundsXZ, out center, out halfExtent);

        private static void DrawRect(Rect boundsXZ, float groundY)
        {
            DrawRect(boundsXZ.xMin, boundsXZ.xMax, boundsXZ.yMin, boundsXZ.yMax, groundY);
        }

        private static void DrawRect(float xMin, float xMax, float zMin, float zMax, float groundY)
        {
            var a = new Vector3(xMin, groundY, zMin);
            var b = new Vector3(xMax, groundY, zMin);
            var c = new Vector3(xMax, groundY, zMax);
            var d = new Vector3(xMin, groundY, zMax);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
