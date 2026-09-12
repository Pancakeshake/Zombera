#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.Core;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldSitePlanCityRegionAdapterTests
    {
        [Test]
        public void CreateSessionRegion_SessionSitesMatchPlanCenters()
        {
            var template = CreateTemplateAsset();
            var plan = new WorldSitePlan(
                new List<WorldCitySite>
                {
                    new()
                    {
                        DisplayName = "North Hub",
                        CenterXZ = new Vector2(1200f, 3400f),
                        HalfWidthMeters = 320f,
                        HalfDepthMeters = 280f,
                        LayoutSeed = 11
                    },
                    new()
                    {
                        DisplayName = "South Hub",
                        CenterXZ = new Vector2(4800f, 900f),
                        HalfWidthMeters = 260f,
                        HalfDepthMeters = 220f,
                        LayoutSeed = 22
                    }
                });
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Medium,
                seed: 9090,
                profileVersion: 3,
                originXZ: new Vector2(0f, 0f),
                tilesPerSide: 8,
                tileSizeMeters: 1000f);

            var sessionRegion = WorldSitePlanCityRegionAdapter.CreateSessionRegion(
                template,
                plan,
                session,
                null,
                session.WorldBoundsXZ,
                out var roadsSeed);
            try
            {
                Assert.AreEqual(2, sessionRegion.sites.Count);
                Assert.AreEqual(plan.CitySites[0].CenterXZ, sessionRegion.sites[0].centerXZ);
                Assert.AreEqual(plan.CitySites[1].CenterXZ, sessionRegion.sites[1].centerXZ);
                Assert.AreEqual("North Hub", sessionRegion.sites[0].displayName);
                Assert.AreEqual("South Hub", sessionRegion.sites[1].displayName);
                Assert.AreEqual(8, sessionRegion.mapTilesPerSide);
                sessionRegion.ResolveScatterArea(out _, out var halfExtent);
                // No landform profile → scatter uses full session bounds (half of 8000m).
                Assert.AreEqual(4000f, halfExtent, 1f);
                Assert.AreNotEqual(0, roadsSeed);
                Assert.IsFalse(sessionRegion.autoScatterOnBuild);
                Assert.IsFalse(sessionRegion.randomizeRegionSeedPerBuild);
            }
            finally
            {
                WorldSitePlanCityRegionAdapter.DestroySessionRegion(sessionRegion);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void CreateSessionRegion_DoesNotMutateTemplateAsset()
        {
            var template = CreateTemplateAsset();
            var originalSiteCount = template.sites.Count;
            var originalCenter = template.sites[0].centerXZ;
            var originalDisplayName = template.sites[0].displayName;
            var originalRegionSeed = template.regionSeed;
            var originalScatterFlag = template.autoScatterOnBuild;

            var plan = new WorldSitePlan(new List<WorldCitySite>
            {
                new()
                {
                    DisplayName = "Plan Only City",
                    CenterXZ = new Vector2(2222f, 3333f)
                }
            });
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 5150,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 4,
                tileSizeMeters: 1000f);

            var sessionRegion = WorldSitePlanCityRegionAdapter.CreateSessionRegion(
                template,
                plan,
                session,
                null,
                session.WorldBoundsXZ,
                out _);
            try
            {
                Assert.AreEqual(originalSiteCount, template.sites.Count);
                Assert.AreEqual(originalCenter, template.sites[0].centerXZ);
                Assert.AreEqual(originalDisplayName, template.sites[0].displayName);
                Assert.AreEqual(originalRegionSeed, template.regionSeed);
                Assert.AreEqual(originalScatterFlag, template.autoScatterOnBuild);
                Assert.AreNotEqual(template, sessionRegion);
            }
            finally
            {
                WorldSitePlanCityRegionAdapter.DestroySessionRegion(sessionRegion);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void ConfigureScatterForSession_SmallTierUsesFullWorldBounds()
        {
            var region = ScriptableObject.CreateInstance<CityRegionAsset>();
            region.scatterHalfExtentMeters = 5000f;
            region.mapTilesPerSide = 10;
            region.scatterSiteCount = 18;

            var mapSize = ScriptableObject.CreateInstance<WorldMapSizeSettings>();
            var landforms = ScriptableObject.CreateInstance<LandformProfile>();
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 42,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 4,
                tileSizeMeters: 1000f);

            try
            {
                region.ConfigureScatterForSession(session, mapSize, landforms);
                Assert.AreEqual(4, region.mapTilesPerSide);
                Assert.AreEqual(3, region.scatterSiteCount);
                region.ResolveScatterArea(out var center, out var halfExtent);
                Assert.AreEqual(new Vector2(2000f, 2000f), center);
                Assert.AreEqual(2000f, halfExtent, 0.01f);

                var fromUtility = WorldSiteBoundsUtility.ResolvePlayableSiteBounds(session, null, null);
                Assert.AreEqual(session.WorldBoundsXZ, fromUtility);
            }
            finally
            {
                Object.DestroyImmediate(region);
                Object.DestroyImmediate(mapSize);
                Object.DestroyImmediate(landforms);
            }
        }

        [Test]
        public void RandomizeSites_RespectsFootprintInsideSmallGrid()
        {
            var region = ScriptableObject.CreateInstance<CityRegionAsset>();
            region.scatterSiteCount = 1;
            region.sites = new List<CityHubSite>
            {
                new()
                {
                    displayName = "Large City",
                    halfWidthMeters = 760f,
                    halfDepthMeters = 660f
                }
            };

            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 77,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 4,
                tileSizeMeters: 1000f);
            region.ConfigureScatterForSession(session, null);

            var placed = region.RandomizeSites(4242);
            try
            {
                Assert.AreEqual(1, placed);
                Assert.IsTrue(region.AllSitesInsideScatterZone(session.WorldBoundsXZ));
                Assert.IsTrue(region.AllSiteFootprintsInside(session.WorldBoundsXZ));
            }
            finally
            {
                Object.DestroyImmediate(region);
            }
        }

        [Test]
        public void CreateSessionRegion_PropagatesPlanSiteTypeAndFootprint()
        {
            var template = CreateTemplateAsset();
            var plan = new WorldSitePlan(new List<WorldCitySite>
            {
                new()
                {
                    DisplayName = "Metropolis_1",
                    SiteType = CitySiteType.Metropolis,
                    CenterXZ = new Vector2(2500f, 2500f),
                    HalfWidthMeters = CitySiteTypePresets.HalfWidth(CitySiteType.Metropolis),
                    HalfDepthMeters = CitySiteTypePresets.HalfDepth(CitySiteType.Metropolis),
                    LayoutSeed = 99
                },
                new()
                {
                    DisplayName = "Village_1",
                    SiteType = CitySiteType.Village,
                    CenterXZ = new Vector2(1000f, 1000f),
                    HalfWidthMeters = CitySiteTypePresets.HalfWidth(CitySiteType.Village),
                    HalfDepthMeters = CitySiteTypePresets.HalfDepth(CitySiteType.Village),
                    LayoutSeed = 100
                }
            });
            var session = WorldMapSession.Create(
                WorldMapSizeTier.Large,
                seed: 4242,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 10,
                tileSizeMeters: 1000f);

            var sessionRegion = WorldSitePlanCityRegionAdapter.CreateSessionRegion(
                template,
                plan,
                session,
                null,
                session.WorldBoundsXZ,
                out _);
            try
            {
                Assert.AreEqual(CitySiteType.Metropolis, sessionRegion.sites[0].siteType);
                Assert.AreEqual(CitySiteType.Village, sessionRegion.sites[1].siteType);
                Assert.AreEqual(
                    CitySiteTypePresets.HalfWidth(CitySiteType.Metropolis),
                    sessionRegion.sites[0].halfWidthMeters);
                Assert.AreEqual(
                    CitySiteTypePresets.GuaranteedExitCount(CitySiteType.Metropolis),
                    sessionRegion.sites[0].guaranteedHighwayExitCount);
                Assert.AreEqual(
                    CitySiteTypePresets.GuaranteedExitCount(CitySiteType.Village),
                    sessionRegion.sites[1].guaranteedHighwayExitCount);
            }
            finally
            {
                WorldSitePlanCityRegionAdapter.DestroySessionRegion(sessionRegion);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void ResolveQuota_LargeTierGuaranteesMetropolisCitySettlementCounts()
        {
            var mapSize = ScriptableObject.CreateInstance<WorldMapSizeSettings>();
            try
            {
                var quota = mapSize.ResolveQuota(WorldMapSizeTier.Large, seed: 12345);
                Assert.AreEqual(2, quota.Metropolis);
                Assert.AreEqual(5, quota.City);
                Assert.GreaterOrEqual(quota.Settlements, 13);
                Assert.LessOrEqual(quota.Settlements, 18);
                Assert.AreEqual(quota.Metropolis + quota.City + quota.Settlements, quota.Total);

                var types = WorldSettlementHierarchy.ExpandOrderedTypes(
                    quota,
                    new DeterministicRng(99));
                Assert.AreEqual(quota.Total, types.Count);
                Assert.AreEqual(CitySiteType.Metropolis, types[0]);
                Assert.AreEqual(CitySiteType.Metropolis, types[1]);
                Assert.AreEqual(CitySiteType.City, types[2]);
                Assert.AreEqual(CitySiteType.City, types[6]);
                Assert.AreNotEqual(CitySiteType.City, types[7]);
            }
            finally
            {
                Object.DestroyImmediate(mapSize);
            }
        }

        private static CityRegionAsset CreateTemplateAsset()
        {
            var template = ScriptableObject.CreateInstance<CityRegionAsset>();
            template.displayName = "Template Region";
            template.regionSeed = 101;
            template.autoScatterOnBuild = true;
            template.sites = new List<CityHubSite>
            {
                new()
                {
                    displayName = "Template City",
                    siteType = CitySiteType.Town,
                    centerXZ = new Vector2(9999f, 8888f),
                    halfWidthMeters = 300f,
                    halfDepthMeters = 250f,
                    layoutSeed = 7,
                    guaranteedHighwayExitCount = 3
                }
            };
            return template;
        }
    }
}
#endif
