using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Shared per-site layout cloning and arterial flatten bounds for region cities.</summary>
    public static class CityRegionSiteLayoutUtility
    {
        public static CityMathRoadLayout ApplySiteToLayout(CityMathRoadLayout template, CityHubSite site)
        {
            var clone = JsonUtility.FromJson<CityMathRoadLayout>(JsonUtility.ToJson(template)) ?? new CityMathRoadLayout();
            clone.centerXZ = site.centerXZ;
            clone.layoutSeed = site.layoutSeed;
            clone.generateStreetGrid = site.generateStreetGrid;
            clone.generateArterialRing = site.generateArterialRing;
            clone.generateHighwayExits = site.generateHighwayExits;
            clone.guaranteedHighwayExitCount = Mathf.Max(2, site.guaranteedHighwayExitCount);
            clone.randomizeCityExtents = false;
            clone.cityRadiusMeters = Mathf.Max(site.halfWidthMeters, site.halfDepthMeters);
            clone.cityHalfWidthMinMeters = site.halfWidthMeters;
            clone.cityHalfWidthMaxMeters = site.halfWidthMeters;
            clone.cityHalfDepthMinMeters = site.halfDepthMeters;
            clone.cityHalfDepthMaxMeters = site.halfDepthMeters;
            return clone;
        }

        public static int ResolveSiteSeed(CityHubSite site, int regionSeed, int siteIndex)
        {
            if (site.layoutSeed != 0)
                return site.layoutSeed;

            var mixed = regionSeed ^ ((siteIndex + 1) * unchecked((int)0x9E3779B9));
            return mixed == 0 ? 12345 : mixed;
        }

        /// <summary>
        ///     City footprint plateau (full layout bounds). Falloff is applied outward by the flattener.
        /// </summary>
        public static bool TryResolveCityPadPlateauBounds(
            CityMathRoadLayout template,
            CityHubSite site,
            int siteSeed,
            out Rect plateauBoundsXZ)
        {
            plateauBoundsXZ = default;
            if (template == null || site == null)
                return false;

            var siteLayout = ApplySiteToLayout(template, site);
            siteLayout.GetTerrainFlattenBounds(site.centerXZ, siteSeed, out _, out plateauBoundsXZ);
            return plateauBoundsXZ.width > 0f && plateauBoundsXZ.height > 0f;
        }

        /// <summary>
        ///     Arterial pad with a tight outer fade band — not the full city footprint.
        /// </summary>
        public static bool TryResolveArterialPadBounds(
            CityMathRoadLayout template,
            CityHubSite site,
            int siteSeed,
            float outerMarginMeters,
            out Rect innerBoundsXZ,
            out Rect outerBoundsXZ)
        {
            innerBoundsXZ = default;
            outerBoundsXZ = default;
            if (template == null || site == null)
                return false;

            var siteLayout = ApplySiteToLayout(template, site);
            siteLayout.GetTerrainFlattenBounds(site.centerXZ, siteSeed, out innerBoundsXZ, out _);
            if (innerBoundsXZ.width <= 0f || innerBoundsXZ.height <= 0f)
                return false;

            outerBoundsXZ = ExpandRect(innerBoundsXZ, Mathf.Max(1f, outerMarginMeters));
            return true;
        }

        public static Rect ExpandRect(Rect rect, float meters) =>
            Rect.MinMaxRect(rect.xMin - meters, rect.yMin - meters, rect.xMax + meters, rect.yMax + meters);
    }
}
