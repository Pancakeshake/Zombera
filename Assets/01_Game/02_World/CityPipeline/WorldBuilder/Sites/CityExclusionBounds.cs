using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Shared city pad/site exclusion for wilderness content
    ///     (nature, POIs). Rects match reserved arterial+margin cores
    ///     from ReserveCityPads / ApplyCityPads (arterial + flat margin).
    /// </summary>
    public static class CityExclusionBounds
    {
        public const float DefaultMarginMeters = 16f;

        public static Rect SitePlateauRect(WorldCitySite site)
        {
            if (site == null)
                return default;

            var halfW = Mathf.Max(40f, site.HalfWidthMeters);
            var halfD = Mathf.Max(40f, site.HalfDepthMeters);
            return Rect.MinMaxRect(
                site.CenterXZ.x - halfW,
                site.CenterXZ.y - halfD,
                site.CenterXZ.x + halfW,
                site.CenterXZ.y + halfD);
        }

        public static bool ContainsAny(
            WorldSitePlan sites,
            Vector2 xz,
            float marginMeters = DefaultMarginMeters)
        {
            if (sites?.CitySites == null || sites.CitySites.Count == 0)
                return false;

            return ContainsAny(sites.CitySites, xz, marginMeters);
        }

        public static bool ContainsAny(
            IReadOnlyList<WorldCitySite> citySites,
            Vector2 xz,
            float marginMeters = DefaultMarginMeters)
        {
            if (citySites == null || citySites.Count == 0)
                return false;

            var margin = Mathf.Max(0f, marginMeters);
            for (var i = 0; i < citySites.Count; i++)
            {
                var site = citySites[i];
                if (site == null)
                    continue;

                var rect = SitePlateauRect(site);
                if (rect.width <= 0f || rect.height <= 0f)
                    continue;

                if (ContainsInclusive(rect, xz, margin))
                    return true;
            }

            return false;
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point, float margin)
        {
            return point.x >= rect.xMin - margin &&
                   point.x <= rect.xMax + margin &&
                   point.y >= rect.yMin - margin &&
                   point.y <= rect.yMax + margin;
        }
    }
}
