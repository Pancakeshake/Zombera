using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{

    /// <summary>
    ///     ScriptableObject describing a multi-city region for the City Prefab Hub.
    ///     Pipeline step '1. Roads' generates every site's road layout plus inter-city
    ///     highways; steps 2-19 operate on the builder's selected active site.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/City Region", fileName = "CityRegion.asset")]
    public sealed class CityRegionAsset : ScriptableObject
    {
        public string displayName = "Region";

        [Tooltip("0 = fall back to the hub layout's seed.")]
        public int regionSeed;

        [Tooltip("When true, sites are linked with RoadClass.Highway roads (minimum spanning tree).")]
        public bool generateRegionHighways = true;

        [Min(50f)]
        [Tooltip("Sites closer than this are never linked directly (avoids highways through neighbouring cities).")]
        public float highwayMinLinkDistanceMeters = 600f;

        public List<CityHubSite> sites = new();

        [Header("Site Scatter")]
        [Min(1)]
        [Tooltip("Square map size in MapMagic tiles (1 tile = 1000m). The scatter zone is derived from this.")]
        public int mapTilesPerSide = 10;

        [Tooltip("When true, every region build re-scatters all sites inside the tile zone before generating roads.")]
        public bool autoScatterOnBuild = true;

        [Tooltip("When true, every build rolls a fresh region seed — new city positions and layouts each time.")]
        public bool randomizeRegionSeedPerBuild = true;

        [Min(1)]
        [Tooltip("Target site count when scattering runs. Scatter never removes sites — the " +
                 "effective target is max(this, the current site count).")]
        public int scatterSiteCount = 3;

        [Tooltip("When true, scatter assigns each city a random name from the CityNames list.")]
        public bool randomizeCityNames = true;

        [Min(0f)]
        [Tooltip("Square half-extent of the scatter zone. 0 = auto-derive from Map Tiles Per Side.")]
        public float scatterHalfExtentMeters;

        [Tooltip("Center of the scatter zone.")]
        public Vector2 scatterCenterXZ;

        [Min(0f)]
        [Tooltip("Extra gap required between two city footprints, on top of each city's own radius.")]
        public float minSiteClearanceMeters = 200f;

        [Tooltip("0 = use Region Seed.")]
        public int scatterSeed;

        /// <summary>
        ///     Configures scatter so cities use <paramref name="worldBoundsXZ"/>
        ///     (full terrain / session bounds; optional extra edge clearance).
        /// </summary>
        public void ConfigureScatterForWorldBounds(Rect worldBoundsXZ, float edgeClearanceMeters = 0f)
        {
            if (worldBoundsXZ.width <= 0f || worldBoundsXZ.height <= 0f)
                return;

            var inset = Mathf.Max(0f, edgeClearanceMeters);
            var xMin = worldBoundsXZ.xMin + inset;
            var xMax = worldBoundsXZ.xMax - inset;
            var yMin = worldBoundsXZ.yMin + inset;
            var yMax = worldBoundsXZ.yMax - inset;
            if (xMax <= xMin || yMax <= yMin)
            {
                scatterCenterXZ = worldBoundsXZ.center;
                scatterHalfExtentMeters = Mathf.Min(worldBoundsXZ.width, worldBoundsXZ.height) * 0.5f;
            }
            else
            {
                scatterCenterXZ = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
                scatterHalfExtentMeters = Mathf.Min(xMax - xMin, yMax - yMin) * 0.5f;
            }

            mapTilesPerSide = Mathf.Max(
                1,
                Mathf.CeilToInt(Mathf.Max(worldBoundsXZ.width, worldBoundsXZ.height) / 1000f));
        }

        /// <summary>
        ///     Binds scatter zone and city target to a world-build session using the full
        ///     terrain grid (<see cref="WorldMapSession.WorldBoundsXZ"/>).
        /// </summary>
        public void ConfigureScatterForSession(
            WorldMapSession session,
            WorldMapSizeSettings mapSizeSettings = null,
            LandformProfile landformProfile = null)
        {
            _ = landformProfile;
            if (session.WorldBoundsXZ.width <= 0f || session.WorldBoundsXZ.height <= 0f)
                return;

            var siteBounds = WorldSiteBoundsUtility.ResolvePlayableSiteBounds(
                session,
                profile: null,
                catalog: null);
            ConfigureScatterForWorldBounds(siteBounds);
            mapTilesPerSide = Mathf.Max(1, session.TilesPerSide);

            if (mapSizeSettings == null)
                return;

            var cityTarget = mapSizeSettings.GetCitySiteTarget(session.Tier, session.Seed);
            if (cityTarget > 0)
                scatterSiteCount = cityTarget;
        }

        /// <summary>True when every site footprint lies inside <paramref name="worldBoundsXZ"/>.</summary>
        public bool AllSiteFootprintsInside(Rect worldBoundsXZ)
        {
            if (sites == null || sites.Count == 0)
                return false;

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                    continue;
                if (!IsSiteFootprintInside(worldBoundsXZ, site))
                    return false;
            }

            return true;
        }

        /// <summary>True when every site center and footprint fit inside the scatter zone.</summary>
        public bool AllSitesInsideScatterZone(Rect? terrainBounds = null)
        {
            if (sites == null || sites.Count == 0)
                return false;

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                    continue;
                if (!IsSiteInsideScatterZone(site))
                    return false;
                if (terrainBounds.HasValue && !IsSiteFootprintInside(terrainBounds.Value, site))
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Relocates sites that fall outside the scatter zone or terrain footprint.
        ///     Returns how many sites were moved.
        /// </summary>
        public int ConstrainSitesToScatterZone(int seedOverride = 0, Rect? terrainBounds = null)
        {
            if (SiteCount == 0)
                return 0;

            ResolveScatterArea(out var areaCenter, out var halfExtent);
            var rng = new System.Random(ResolveScatterSeed(seedOverride) ^ unchecked((int)0x5CA77A01));
            var placed = new List<CityHubSite>(SiteCount);
            var kept = new List<CityHubSite>(SiteCount);
            var relocated = 0;

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                    continue;

                if (IsSiteInsideScatterZone(site) &&
                    (!terrainBounds.HasValue || IsSiteFootprintInside(terrainBounds.Value, site)) &&
                    HasScatterClearance(site, placed))
                {
                    placed.Add(site);
                    kept.Add(site);
                    continue;
                }

                var moved = TryRelocateSite(rng, site, placed, areaCenter, halfExtent);
                if (moved == null)
                    continue;

                placed.Add(moved);
                kept.Add(moved);
                relocated++;
            }

            sites = kept;
            return relocated;
        }

        public static bool IsSiteFootprintInside(Rect bounds, CityHubSite site)
        {
            if (site == null)
                return false;

            var radius = ResolveFootprintRadius(site);
            return site.centerXZ.x - radius >= bounds.xMin &&
                   site.centerXZ.x + radius <= bounds.xMax &&
                   site.centerXZ.y - radius >= bounds.yMin &&
                   site.centerXZ.y + radius <= bounds.yMax;
        }

        public bool IsSiteInsideScatterZone(CityHubSite site)
        {
            if (site == null)
                return false;

            ResolveScatterArea(out var center, out var halfExtent);
            var placementHalf = ResolvePlacementHalfExtent(halfExtent, ResolveFootprintRadius(site));
            return Mathf.Abs(site.centerXZ.x - center.x) <= placementHalf &&
                   Mathf.Abs(site.centerXZ.y - center.y) <= placementHalf;
        }

        /// <summary>True when every site center lies inside <paramref name="worldBoundsXZ"/>.</summary>
        public bool AllSiteCentersInside(Rect worldBoundsXZ)
        {
            if (sites == null || sites.Count == 0)
                return false;

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                    continue;
                if (!worldBoundsXZ.Contains(site.centerXZ))
                    return false;
            }

            return true;
        }

        public int SiteCount => sites?.Count ?? 0;

        /// <summary>
        ///     Scatter target that never trims: adding sites in the inspector grows the
        ///     scatter, it never shrinks it back down to <see cref="scatterSiteCount"/>.
        /// </summary>
        public int EffectiveScatterSiteCount => Mathf.Max(scatterSiteCount, SiteCount);

        public CityHubSite GetSite(int index) =>
            sites != null && index >= 0 && index < sites.Count ? sites[index] : null;

        /// <summary>
        ///     The square zone cities scatter into: either the manual half-extent or,
        ///     when 0, the whole tile grid (Map Tiles Per Side x 1000m / 2).
        /// </summary>
        public void ResolveScatterArea(out Vector2 center, out float halfExtent)
        {
            center = scatterCenterXZ;
            if (scatterHalfExtentMeters >= 100f)
            {
                halfExtent = scatterHalfExtentMeters;
                return;
            }

            halfExtent = Mathf.Max(100f, mapTilesPerSide * 500f);
        }

        /// <summary>
        ///     Replaces every site with a deterministic scatter across the tile zone:
        ///     each city is dart-thrown and only accepted when its footprint keeps
        ///     <see cref="minSiteClearanceMeters"/> away from every already-placed
        ///     footprint. Existing sites are reused as templates, so per-site settings
        ///     (district, extents, toggles) survive re-scatters; only positions change.
        /// </summary>
        public int RandomizeSites(int seedOverride = 0)
        {
            var rng = new System.Random(ResolveScatterSeed(seedOverride));
            ResolveScatterArea(out var areaCenter, out var halfExtent);
            var targetCount = EffectiveScatterSiteCount;
            var placed = new List<CityHubSite>(Mathf.Max(1, targetCount));
            var usedNames = new HashSet<string>();

            for (var i = 0; i < targetCount; i++)
            {
                var template = i < sites.Count ? sites[i] : null;
                var site = TryPlaceScatteredSite(rng, placed, template, areaCenter, halfExtent);
                if (site == null)
                    continue;

                site.displayName = ResolveScatterSiteName(rng, usedNames, template);
                placed.Add(site);
            }

            sites = placed;
            return placed.Count;
        }

        private string ResolveScatterSiteName(System.Random rng, HashSet<string> usedNames, CityHubSite template)
        {
            if (!randomizeCityNames && template != null && !string.IsNullOrWhiteSpace(template.displayName))
                return template.displayName;

            return CityNames.Pick(rng, usedNames);
        }

        /// <summary>
        ///     Guarantees the site guard in place: any site whose footprint overlaps a
        ///     previously-accepted site is relocated (dart-thrown, deterministic from
        ///     the scatter seed) until it clears. Valid sites stay exactly where they
        ///     are. Call before building the region so cities can never sit on top of
        ///     each other — even when sites are added by hand at (0,0).
        /// </summary>
        public int EnsureSpacedSites(int seedOverride = 0)
        {
            if (SiteCount < 2)
                return 0;

            var rng = new System.Random(ResolveScatterSeed(seedOverride));
            ResolveScatterArea(out var areaCenter, out var halfExtent);
            var placed = new List<CityHubSite>(SiteCount);
            var relocated = 0;

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                    continue;

                if (!HasScatterClearance(site, placed))
                {
                    var moved = TryRelocateSite(rng, site, placed, areaCenter, halfExtent);
                    if (moved != null)
                    {
                        sites[i] = moved;
                        placed.Add(moved);
                        relocated++;
                        continue;
                    }
                }

                placed.Add(site);
            }

            return relocated;
        }

        /// <summary>Number of site pairs whose footprints sit closer than the guard allows.</summary>
        public int CountOverlappingSitePairs()
        {
            if (SiteCount < 2)
                return 0;

            var count = 0;
            for (var i = 0; i < sites.Count; i++)
            {
                var a = sites[i];
                if (a == null)
                    continue;

                for (var j = i + 1; j < sites.Count; j++)
                {
                    var b = sites[j];
                    if (b == null)
                        continue;

                    var minDistance = ResolveFootprintRadius(a) + ResolveFootprintRadius(b) + minSiteClearanceMeters;
                    if ((a.centerXZ - b.centerXZ).sqrMagnitude < minDistance * minDistance)
                        count++;
                }
            }

            return count;
        }

        private CityHubSite TryPlaceScatteredSite(
            System.Random rng, List<CityHubSite> placed, CityHubSite template, Vector2 areaCenter, float halfExtent)
        {
            const int maxAttempts = 48;
            var placementHalf = ResolvePlacementHalfExtent(
                halfExtent,
                template != null ? ResolveFootprintRadius(template) : 280f);
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = BuildScatteredSite(template, rng, areaCenter, placementHalf);
                if (HasScatterClearance(candidate, placed))
                    return candidate;
            }

            return null;
        }

        private CityHubSite TryRelocateSite(
            System.Random rng, CityHubSite template, List<CityHubSite> placed, Vector2 areaCenter, float halfExtent)
        {
            const int maxAttempts = 48;
            var placementHalf = ResolvePlacementHalfExtent(halfExtent, ResolveFootprintRadius(template));
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = BuildScatteredSite(template, rng, areaCenter, placementHalf);
                if (HasScatterClearance(candidate, placed))
                    return candidate;
            }

            return null;
        }

        private static CityHubSite BuildScatteredSite(
            CityHubSite template, System.Random rng, Vector2 areaCenter, float halfExtent)
        {
            var source = template ?? new CityHubSite();
            return new CityHubSite
            {
                displayName = source.displayName,
                siteType = source.siteType,
                centerXZ = new Vector2(
                    areaCenter.x + (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent,
                    areaCenter.y + (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent),
                districtMix = source.districtMix != null
                    ? new List<CityDistrictWeight>(source.districtMix)
                    : new List<CityDistrictWeight>(),
                layoutSeed = source.layoutSeed,
                halfWidthMeters = source.halfWidthMeters,
                halfDepthMeters = source.halfDepthMeters,
                generateStreetGrid = source.generateStreetGrid,
                generateArterialRing = source.generateArterialRing,
                generateHighwayExits = source.generateHighwayExits,
                guaranteedHighwayExitCount = source.guaranteedHighwayExitCount,
                connectToRegionHighways = source.connectToRegionHighways
            };
        }

        private int ResolveScatterSeed(int seedOverride)
        {
            if (seedOverride != 0)
                return seedOverride;
            if (scatterSeed != 0)
                return scatterSeed;
            if (regionSeed != 0)
                return regionSeed;
            return 12345;
        }

        private bool HasScatterClearance(CityHubSite candidate, List<CityHubSite> placed)
        {
            var radius = ResolveFootprintRadius(candidate);
            for (var i = 0; i < placed.Count; i++)
            {
                var minDistance = radius + ResolveFootprintRadius(placed[i]) + minSiteClearanceMeters;
                if ((candidate.centerXZ - placed[i].centerXZ).sqrMagnitude < minDistance * minDistance)
                    return false;
            }

            return true;
        }

        private static float ResolveFootprintRadius(CityHubSite site) =>
            Mathf.Max(site.halfWidthMeters, site.halfDepthMeters);

        private static float ResolvePlacementHalfExtent(float zoneHalfExtent, float footprintRadius) =>
            Mathf.Max(50f, zoneHalfExtent - footprintRadius);
    }
}
