using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Selected city and landmark sites for a world build.</summary>
    public sealed class WorldSitePlan
    {
        public IReadOnlyList<WorldCitySite> CitySites { get; }
        public IReadOnlyList<WorldCitySite> LandmarkSites { get; }

        public IReadOnlyList<Vector2> CitySitesXZ { get; }
        public IReadOnlyList<Vector2> LandmarkSitesXZ { get; }

        public WorldSitePlan(
            IReadOnlyList<Vector2> citySitesXZ = null,
            IReadOnlyList<Vector2> landmarkSitesXZ = null)
            : this(FromPoints(citySitesXZ, "City"), FromPoints(landmarkSitesXZ, "Landmark"))
        {
        }

        public WorldSitePlan(
            IReadOnlyList<WorldCitySite> citySites,
            IReadOnlyList<WorldCitySite> landmarkSites = null)
        {
            CitySites = citySites ?? Array.Empty<WorldCitySite>();
            LandmarkSites = landmarkSites ?? Array.Empty<WorldCitySite>();
            CitySitesXZ = ExtractCenters(CitySites);
            LandmarkSitesXZ = ExtractCenters(LandmarkSites);
        }

        private static IReadOnlyList<WorldCitySite> FromPoints(IReadOnlyList<Vector2> points, string prefix)
        {
            if (points == null || points.Count == 0) return Array.Empty<WorldCitySite>();
            var sites = new WorldCitySite[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                sites[i] = new WorldCitySite
                {
                    DisplayName = $"{prefix}_{i}",
                    CenterXZ = points[i],
                    StableId = (ulong)(i + 1)
                };
            }

            return sites;
        }

        private static IReadOnlyList<Vector2> ExtractCenters(IReadOnlyList<WorldCitySite> sites)
        {
            if (sites == null || sites.Count == 0) return Array.Empty<Vector2>();
            var centers = new Vector2[sites.Count];
            for (var i = 0; i < sites.Count; i++)
                centers[i] = sites[i] != null ? sites[i].CenterXZ : Vector2.zero;
            return centers;
        }
    }
}
