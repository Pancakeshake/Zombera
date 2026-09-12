using System.Diagnostics;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        [ContextMenu("Reset City Terrain Paint (Step 1)")]
        public void ResetCityTerrainFootprint()
        {
            CitySurfaceLayoutStore.Clear();

            var bounds = ResolveCityPaintBounds();
            bounds = UnionRect(bounds, ResolveSingleCityPaintBounds());
            if (RegionModeActive)
                bounds = UnionRect(bounds,
                    CollectRegionPaintBounds(ActiveRegionAsset, Layout, ResolveBuiltRegionSeed()));

            if (bounds.width > 0f && bounds.height > 0f)
                bounds = ExpandRect(bounds, 80f, 80f);

            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] No city bounds — terrain paint reset skipped.",
                    this);
                return;
            }

            var watch = Stopwatch.StartNew();
            CityLotTerrainPainter.HardWipeControlsInBounds(bounds);
            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Reset city terrain paint within " + bounds +
                " in " + watch.ElapsedMilliseconds +
                "ms. MapMagic Refresh is owned by Legacy authoring tools.", this);
        }

        private Rect ResolveCityPaintBounds()
        {
            var bounds = _lastGeneratedBounds;
            if (bounds.width > 0f && bounds.height > 0f)
                return bounds;

            if (!RegionModeActive)
                return ResolveSingleCityPaintBounds();

            return CollectRegionPaintBounds(ActiveRegionAsset, Layout, ResolveBuiltRegionSeed());
        }

        private Rect ResolveSingleCityPaintBounds()
        {
            var cityLayout = Layout;
            if (cityLayout == null)
                return default;

            cityLayout.GetTerrainFlattenBounds(ResolveLayoutCenterXZ(), cityLayout.layoutSeed, out _, out var outerRect);
            return outerRect.width > 0f && outerRect.height > 0f ? outerRect : default;
        }

        private static Rect CollectRegionPaintBounds(CityRegionAsset region, CityMathRoadLayout template, int regionSeed)
        {
            var union = default(Rect);
            if (region == null || template == null)
                return union;

            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                if (site == null)
                    continue;

                var siteLayout = ApplySiteToLayout(template, site);
                if (siteLayout == null)
                    continue;

                var seed = ResolveSiteSeed(site, regionSeed, i);
                siteLayout.GetTerrainFlattenBounds(site.centerXZ, seed, out _, out var outer);
                if (outer.width > 0f && outer.height > 0f)
                    union = UnionRect(union, outer);
            }

            return union;
        }
    }
}
