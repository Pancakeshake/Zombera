using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    /// <summary>
    /// Builds a transient <see cref="CityRegionAsset"/> from an authoritative <see cref="WorldSitePlan"/>.
    /// Never mutates the profile's project asset.
    /// </summary>
    public static class WorldSitePlanCityRegionAdapter
    {
        public static CityRegionAsset CreateSessionRegion(
            CityRegionAsset template,
            WorldSitePlan plan,
            WorldMapSession session,
            WorldMapSizeSettings mapSizeSettings,
            Rect terrainBoundsForConstraint,
            out int roadsSeed,
            LandformProfile landformProfile = null)
        {
            if (template == null)
                throw new System.ArgumentNullException(nameof(template));
            if (plan == null)
                throw new System.ArgumentNullException(nameof(plan));

            roadsSeed = WorldSubsystemSeeds.Derive(
                session.Seed, session.ProfileVersion, WorldSubsystemSeeds.Roads);

            var clone = Object.Instantiate(template);
            clone.name = template.name + "_Session";
            clone.hideFlags = HideFlags.HideAndDontSave;

            var citySites = plan.CitySites ?? System.Array.Empty<WorldCitySite>();
            var templateSites = template.sites;
            var templateCount = templateSites != null ? templateSites.Count : 0;
            var sessionSites = new List<CityHubSite>(citySites.Count);

            for (var i = 0; i < citySites.Count; i++)
            {
                var worldSite = citySites[i];
                if (worldSite == null)
                    continue;

                var templateSite = templateCount > 0
                    ? templateSites[i % templateCount]
                    : null;
                sessionSites.Add(BuildHubSite(worldSite, templateSite));
            }

            clone.sites = sessionSites;
            clone.regionSeed = roadsSeed;
            clone.scatterSeed = roadsSeed;
            clone.randomizeRegionSeedPerBuild = false;
            clone.autoScatterOnBuild = false;
            clone.ConfigureScatterForSession(session, mapSizeSettings, landformProfile);
            var constrainBounds = terrainBoundsForConstraint.width > 0f && terrainBoundsForConstraint.height > 0f
                ? terrainBoundsForConstraint
                : session.WorldBoundsXZ;
            clone.ConstrainSitesToScatterZone(roadsSeed, constrainBounds);
            return clone;
        }

        public static void DestroySessionRegion(CityRegionAsset sessionRegion)
        {
            if (sessionRegion == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(sessionRegion);
            else
                Object.DestroyImmediate(sessionRegion);
        }

        private static CityHubSite BuildHubSite(WorldCitySite worldSite, CityHubSite templateSite)
        {
            // Plan site type is authoritative; template only supplies feature toggles / mix fallback.
            var siteType = worldSite.SiteType;
            var useTemplateMix = templateSite?.districtMix != null &&
                                 templateSite.districtMix.Count > 0 &&
                                 templateSite.siteType == siteType;
            return new CityHubSite
            {
                displayName = string.IsNullOrWhiteSpace(worldSite.DisplayName)
                    ? WorldSettlementHierarchy.DisplayNamePrefix(siteType)
                    : worldSite.DisplayName,
                siteType = siteType,
                centerXZ = worldSite.CenterXZ,
                halfWidthMeters = worldSite.HalfWidthMeters > 0f
                    ? worldSite.HalfWidthMeters
                    : CitySiteTypePresets.HalfWidth(siteType),
                halfDepthMeters = worldSite.HalfDepthMeters > 0f
                    ? worldSite.HalfDepthMeters
                    : CitySiteTypePresets.HalfDepth(siteType),
                layoutSeed = worldSite.LayoutSeed,
                generateStreetGrid = templateSite == null || templateSite.generateStreetGrid,
                generateArterialRing = templateSite == null || templateSite.generateArterialRing,
                generateHighwayExits = templateSite == null || templateSite.generateHighwayExits,
                guaranteedHighwayExitCount = CitySiteTypePresets.GuaranteedExitCount(siteType),
                connectToRegionHighways = templateSite == null || templateSite.connectToRegionHighways,
                districtMix = useTemplateMix
                    ? CopyDistrictMix(templateSite, siteType)
                    : CitySiteTypePresets.BuildDefaultMix(siteType)
            };
        }

        private static List<CityDistrictWeight> CopyDistrictMix(CityHubSite templateSite, CitySiteType siteType)
        {
            if (templateSite?.districtMix == null || templateSite.districtMix.Count == 0)
                return CitySiteTypePresets.BuildDefaultMix(siteType);

            var copy = new List<CityDistrictWeight>(templateSite.districtMix.Count);
            for (var i = 0; i < templateSite.districtMix.Count; i++)
                copy.Add(templateSite.districtMix[i]);
            return copy;
        }
    }
}
