using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateSiteGenerationProjection
    {
        public const string WorldRegionSourceId = "world";

        public static RegionState CreateWorldRegion(WorldMapSession session)
        {
            return new RegionState
            {
                id = CreateWorldRegionId(session),
                sourceId = WorldRegionSourceId,
                displayName = "World",
                generationSeed = session.Seed,
                boundsXZ = session.WorldBoundsXZ
            };
        }

        public static WorldEntityId CreateWorldRegionId(WorldMapSession session) =>
            WorldStableIdFactory.CreateRegionId(
                session.Seed,
                WorldRegionSourceId,
                0,
                session.WorldBoundsXZ);

        public static List<SettlementState> CreateSettlements(
            WorldMapSession session,
            WorldSitePlan plan)
        {
            var settlements = new List<SettlementState>();
            var sites = plan?.CitySites;
            if (sites == null)
                return settlements;

            var regionId = CreateWorldRegionId(session);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site != null)
                    settlements.Add(CreateSettlement(session, regionId, site, i));
            }

            return settlements;
        }

        public static bool TryFindContainingSettlement(
            WorldMapSession session,
            WorldSitePlan plan,
            Rect boundsXZ,
            out SettlementState settlement)
        {
            settlement = null;
            var sites = plan?.CitySites;
            if (sites == null)
                return false;

            var regionId = CreateWorldRegionId(session);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || !ContainsBounds(site, boundsXZ))
                    continue;

                var candidate = CreateSettlement(session, regionId, site, i);
                if (settlement == null || candidate.id.CompareTo(settlement.id) < 0)
                    settlement = candidate;
            }

            return settlement != null;
        }

        public static bool TryFindSettlementAtPoint(
            WorldMapSession session,
            WorldSitePlan plan,
            Vector2 pointXZ,
            out SettlementState settlement)
        {
            settlement = null;
            var sites = plan?.CitySites;
            if (sites == null)
                return false;

            var regionId = CreateWorldRegionId(session);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || !SiteBounds(site).Contains(pointXZ))
                    continue;

                var candidate = CreateSettlement(session, regionId, site, i);
                if (settlement == null || candidate.id.CompareTo(settlement.id) < 0)
                    settlement = candidate;
            }

            return settlement != null;
        }

        private static SettlementState CreateSettlement(
            WorldMapSession session,
            WorldEntityId regionId,
            WorldCitySite site,
            int ordinal)
        {
            var sourceId = CreateSettlementSourceId(site, ordinal);
            var halfExtents = new Vector2(
                Mathf.Max(1f, site.HalfWidthMeters),
                Mathf.Max(1f, site.HalfDepthMeters));

            return new SettlementState
            {
                id = WorldStableIdFactory.CreateSettlementId(
                    session.Seed,
                    regionId,
                    sourceId,
                    ordinal,
                    site.CenterXZ,
                    halfExtents,
                    0f),
                sourceId = sourceId,
                regionId = regionId,
                displayName = site.DisplayName ?? string.Empty,
                centerXZ = site.CenterXZ,
                halfExtentsMeters = halfExtents,
                padHeightWorldY = site.PadHeightWorldY,
                buildabilityScore = site.BuildabilityScore,
                layoutSeed = site.LayoutSeed
            };
        }

        private static bool ContainsBounds(WorldCitySite site, Rect boundsXZ)
        {
            var siteBounds = SiteBounds(site);
            const float tolerance = 0.05f;
            return boundsXZ.xMin >= siteBounds.xMin - tolerance &&
                boundsXZ.xMax <= siteBounds.xMax + tolerance &&
                boundsXZ.yMin >= siteBounds.yMin - tolerance &&
                boundsXZ.yMax <= siteBounds.yMax + tolerance;
        }

        private static Rect SiteBounds(WorldCitySite site)
        {
            return Rect.MinMaxRect(
                site.CenterXZ.x - site.HalfWidthMeters,
                site.CenterXZ.y - site.HalfDepthMeters,
                site.CenterXZ.x + site.HalfWidthMeters,
                site.CenterXZ.y + site.HalfDepthMeters);
        }

        private static string CreateSettlementSourceId(WorldCitySite site, int ordinal)
        {
            if (site != null && site.StableId != 0UL)
                return "city:" + site.StableId.ToString("x16");

            return "city:" + ordinal.ToString();
        }
    }
}
