#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed partial class InterCityHighwayPlannerTests
    {
        [Test]
        public void Topology_BuildMinimumSpanningTree_IsDeterministic()
        {
            var positions = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1000f, 0f),
                new Vector2(500f, 900f),
                new Vector2(2000f, 500f)
            };
            var connected = new List<int> { 0, 1, 2, 3 };

            var first = InterCityHighwayTopology.BuildMinimumSpanningTree(positions, connected, 100f);
            var second = InterCityHighwayTopology.BuildMinimumSpanningTree(positions, connected, 100f);

            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].IndexA, second[i].IndexA);
                Assert.AreEqual(first[i].IndexB, second[i].IndexB);
            }
        }

        [Test]
        public void Topology_MinLinkDistance_SkipsTooClosePairs()
        {
            var positions = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(100f, 0f),
                new Vector2(2000f, 0f)
            };
            var connected = new List<int> { 0, 1, 2 };
            var links = InterCityHighwayTopology.BuildMinimumSpanningTree(positions, connected, 600f);

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual(0, links[0].IndexA);
            Assert.AreEqual(2, links[0].IndexB);
        }

        [Test]
        public void Topology_ExtraLinks_DoNotChangeMstPrefix()
        {
            var positions = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1000f, 0f),
                new Vector2(500f, 900f),
                new Vector2(2000f, 500f),
                new Vector2(1500f, 1500f)
            };
            var connected = new List<int> { 0, 1, 2, 3, 4 };
            var pureMst = InterCityHighwayTopology.BuildMinimumSpanningTree(positions, connected, 100f);
            var withExtras = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions,
                connected,
                minLinkDistanceMeters: 100f,
                extraLoopChance: 1f,
                seed: 42,
                out var mstEdgeCount);

            Assert.AreEqual(pureMst.Count, mstEdgeCount);
            Assert.Greater(withExtras.Count, mstEdgeCount);
            for (var i = 0; i < mstEdgeCount; i++)
            {
                Assert.AreEqual(pureMst[i].IndexA, withExtras[i].IndexA);
                Assert.AreEqual(pureMst[i].IndexB, withExtras[i].IndexB);
            }
        }

        [Test]
        public void Topology_FailedExtras_DoNotImplyFailedMst()
        {
            var positions = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1200f, 0f),
                new Vector2(600f, 1000f),
                new Vector2(1800f, 800f)
            };
            var connected = new List<int> { 0, 1, 2, 3 };
            var links = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions,
                connected,
                minLinkDistanceMeters: 100f,
                extraLoopChance: 1f,
                seed: 7,
                out var mstEdgeCount);

            var extraCount = links.Count - mstEdgeCount;
            Assert.Greater(mstEdgeCount, 0);
            Assert.Greater(extraCount, 0);

            // Accounting contract: extras can fail without marking MST failures.
            var failedMst = 0;
            var failedExtra = extraCount;
            Assert.AreEqual(0, failedMst);
            Assert.AreEqual(extraCount, failedExtra);
            Assert.AreEqual(mstEdgeCount, links.Count - failedExtra);
        }

        [Test]
        public void Topology_ExtraLinks_SameSeed_AreDeterministic()
        {
            var positions = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1200f, 0f),
                new Vector2(600f, 1100f),
                new Vector2(2000f, 700f),
                new Vector2(1500f, 1500f)
            };
            var connected = new List<int> { 0, 1, 2, 3, 4 };
            const int seed = 42;

            var first = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions, connected, 100f, extraLoopChance: 1f, seed, out var mstA);
            var second = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions, connected, 100f, extraLoopChance: 1f, seed, out var mstB);

            Assert.AreEqual(mstA, mstB);
            Assert.AreEqual(first.Count, second.Count);
            Assert.Greater(first.Count, mstA);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].IndexA, second[i].IndexA);
                Assert.AreEqual(first[i].IndexB, second[i].IndexB);
            }
        }

        [Test]
        public void BuildLinks_UsesProvidedSeed_MatchingPlanSeedUsage()
        {
            var settings = ScriptableObject.CreateInstance<RoadNetworkSettings>();
            settings.connectCitiesWithHighways = true;
            settings.highwayExtraLoopChance = 1f;
            settings.highwayMinLinkDistanceMeters = 100f;

            var cities = BuildCities(
                new Vector2(0f, 0f),
                new Vector2(1200f, 0f),
                new Vector2(600f, 1100f),
                new Vector2(2000f, 700f));

            // Plan routes with session.Seed; BuildLinks must accept the same seed.
            const int planSeed = 11;
            var viaBuildLinks = InterCityHighwayPlanner.BuildLinks(cities, settings, seed: planSeed);
            var viaBuildLinksAgain = InterCityHighwayPlanner.BuildLinks(cities, settings, seed: planSeed);
            var viaTopology = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                new[]
                {
                    cities[0].CenterXZ,
                    cities[1].CenterXZ,
                    cities[2].CenterXZ,
                    cities[3].CenterXZ
                },
                new List<int> { 0, 1, 2, 3 },
                Mathf.Max(50f, settings.highwayMinLinkDistanceMeters),
                settings.highwayExtraLoopChance,
                planSeed,
                out _);

            Assert.AreEqual(viaBuildLinks.Count, viaBuildLinksAgain.Count);
            Assert.AreEqual(viaBuildLinks.Count, viaTopology.Count);
            Assert.Greater(viaBuildLinks.Count, 0);
            for (var i = 0; i < viaBuildLinks.Count; i++)
            {
                Assert.AreEqual(viaBuildLinks[i].IndexA, viaBuildLinksAgain[i].IndexA);
                Assert.AreEqual(viaBuildLinks[i].IndexB, viaBuildLinksAgain[i].IndexB);
                Assert.AreEqual(viaBuildLinks[i].IndexA, viaTopology[i].IndexA);
                Assert.AreEqual(viaBuildLinks[i].IndexB, viaTopology[i].IndexB);
            }

            var otherSeed = InterCityHighwayPlanner.BuildLinks(cities, settings, seed: 99);
            Assert.AreEqual(viaBuildLinks.Count, otherSeed.Count);
        }

        [Test]
        public void FootprintUtility_ResolveEdgePoint_LiesOnBoundary()
        {
            var center = new Vector2(100f, 200f);
            var toward = new Vector2(500f, 200f);
            var edge = InterCitySiteFootprintUtility.ResolveEdgePoint(center, 280f, 240f, toward, out _);

            Assert.AreEqual(center.x + 280f, edge.x, 0.01f);
            Assert.AreEqual(center.y, edge.y, 0.01f);
            Assert.Greater(Vector2.Distance(center, edge), 200f);
        }

        [Test]
        public void Planner_SetsPreserveWorldPath_OnHighways()
        {
            var settings = ScriptableObject.CreateInstance<RoadNetworkSettings>();
            settings.connectCitiesWithHighways = true;
            settings.fallbackToStraightPathWhenPathfindingFails = false;
            settings.enableTerrainValidatedInfrastructureFallback = false;
            settings.highwayWidth = 12f;

            var profile = ScriptableObject.CreateInstance<WorldGenerationProfile>();
            var profileSo = new SerializedObject(profile);
            profileSo.FindProperty("roadNetworkSettings").objectReferenceValue = settings;
            profileSo.ApplyModifiedPropertiesWithoutUndo();

            var session = WorldMapSession.Create(
                WorldMapSizeTier.Small,
                seed: 42,
                profileVersion: 1,
                originXZ: Vector2.zero,
                tilesPerSide: 4,
                tileSizeMeters: 1000f);

            var cities = BuildCities(
                new Vector2(500f, 500f),
                new Vector2(2500f, 2500f));

            var planner = new InterCityHighwayPlanner();
            var result = planner.Plan(session, cities, profile, terrainQuery: null, assignSiteEntries: false);

            // Fail-loud: no terrain + no straight fallback ⇒ zero highways, MST edges failed.
            Assert.AreEqual(0, result.Network.Roads.Count);
            Assert.Greater(result.FailedMstEdgeCount, 0);
            Assert.AreEqual(result.FailedEdgeCount, result.FailedMstEdgeCount + result.FailedExtraEdgeCount);
        }

        [Test]
        public void Planner_EdgeAnchors_AreNotCityCenters()
        {
            var siteA = new WorldCitySite
            {
                CenterXZ = new Vector2(0f, 0f),
                HalfWidthMeters = 200f,
                HalfDepthMeters = 200f
            };
            var siteB = new WorldCitySite
            {
                CenterXZ = new Vector2(2000f, 0f),
                HalfWidthMeters = 200f,
                HalfDepthMeters = 200f
            };

            var anchorA = InterCitySiteFootprintUtility.ResolveEdgePoint(
                siteA.CenterXZ,
                siteA.HalfWidthMeters,
                siteA.HalfDepthMeters,
                siteB.CenterXZ,
                out _);
            var anchorB = InterCitySiteFootprintUtility.ResolveEdgePoint(
                siteB.CenterXZ,
                siteB.HalfWidthMeters,
                siteB.HalfDepthMeters,
                siteA.CenterXZ,
                out _);

            Assert.Greater(Vector2.Distance(siteA.CenterXZ, anchorA), 150f);
            Assert.Greater(Vector2.Distance(siteB.CenterXZ, anchorB), 150f);
        }

        [Test]
        public void DistanceToRectEdge_InsideReturnsMargin()
        {
            var rect = Rect.MinMaxRect(0f, 0f, 100f, 80f);
            var inside = new Vector2(50f, 40f);
            Assert.AreEqual(40f, InterCitySiteFootprintUtility.DistanceToRectEdge(inside, rect), 0.01f);
        }

        [Test]
        public void ResolveSiteAnchor_UsesPlateauNotFullHalfWidth()
        {
            var site = new WorldCitySite
            {
                CenterXZ = new Vector2(0f, 0f),
                HalfWidthMeters = 200f,
                HalfDepthMeters = 200f
            };
            var toward = new Vector2(1000f, 0f);
            var anchor = InterCityHighwayPlanner.ResolveSiteAnchor(site, toward, null, null, 0);
            // Fallback plateau half = max(40, 200*0.55)+flatMargin(30) => 140
            Assert.AreEqual(140f, Mathf.Abs(anchor.x), 0.5f);
            Assert.AreEqual(0f, anchor.y, 0.5f);
            Assert.Less(Mathf.Abs(anchor.x), site.HalfWidthMeters);
        }

        private static List<WorldCitySite> BuildCities(params Vector2[] centers)
        {
            var cities = new List<WorldCitySite>(centers.Length);
            for (var i = 0; i < centers.Length; i++)
            {
                cities.Add(new WorldCitySite
                {
                    CenterXZ = centers[i],
                    HalfWidthMeters = 200f,
                    HalfDepthMeters = 200f
                });
            }

            return cities;
        }
    }
}
#endif
