using System;
using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {

        private const string AreasContainerName = "CityNamedAreas";

        // ── Resolved district config from BuildConfig SO ──

        private bool ResolvedGenerateResidentialLots =>
            buildConfig?.generateResidentialLots ?? true;
        private bool ResolvedCreateEditorFloorVisuals =>
            buildConfig?.createEditorFloorVisuals ?? true;
        private float ResolvedFloorVisualHeight =>
            buildConfig?.floorVisualHeight ?? 0.06f;
        private LotSizeRange ResolvedResidentialLotSize =>
            buildConfig?.residentialLotSize ?? new LotSizeRange(16, 32);
        private LotSizeRange ResolvedCommercialLotSize =>
            buildConfig?.commercialLotSize ?? new LotSizeRange(28, 56);
        private LotSizeRange ResolvedIndustrialLotSize =>
            buildConfig?.industrialLotSize ?? new LotSizeRange(32, 64);
        private LotSizeRange ResolvedCityCoreLotSize =>
            buildConfig?.cityCoreLotSize ?? new LotSizeRange(48, 96);
        private float ResolvedGasStationChance =>
            buildConfig?.gasStationChance ?? 0.15f;

        // ── Public API ──

        public void GenerateNamedAreas()
        {
            // Region mode builds every site in one pass so all cities get their
            // blocks simultaneously; single-city mode keeps the legacy template flow.
            var areas = RegionModeActive
                ? BuildNamedAreasForAllSites()
                : BuildNamedAreasForSingleCity();

            if (areas == null || areas.Count == 0)
                return;

            ClearNamedAreas();
            SpawnNamedAreas(areas, ResolveGroundHeight);

            var summary = BuildDistrictSummary(areas);
            Debug.Log("[CityPrefabRoadNetworkBuilder] " + summary, this);
        }

        private IReadOnlyList<CityNamedArea> BuildNamedAreasForSingleCity()
        {
            var resolvedLayout = Layout;
            if (resolvedLayout == null || !resolvedLayout.generateStreetGrid)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Street grid disabled — no blocks to fill.", this);
                return null;
            }

            var workingLayout = resolvedLayout;
            workingLayout.Normalize();
            if (workingLayout.generateStreetGrid && workingLayout.generateArterialRing)
                workingLayout.clipLocalStreetsToArterialRing = true;

            workingLayout.centerXZ = ResolveLayoutCenterXZ();
            var seed = workingLayout.layoutSeed != 0 ? workingLayout.layoutSeed : 12345;
            return CityMathBlockLayoutGenerator.Generate(
                workingLayout, ResolveDistrictRoadNetworkSettings(), seed, districtMix: null);
        }

        private IReadOnlyList<CityNamedArea> BuildNamedAreasForAllSites()
        {
            var settings = ResolveDistrictRoadNetworkSettings();
            var combined = new List<CityNamedArea>();

            var region = ActiveRegionAsset;
            if (region == null)
                return combined;

            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                var siteLayout = ApplySiteToLayout(Layout, site);
                if (siteLayout == null || !siteLayout.generateStreetGrid)
                {
                    var siteLabel = site != null && !string.IsNullOrEmpty(site.displayName)
                        ? site.displayName
                        : "site #" + i;
                    Debug.LogWarning(
                        "[CityPrefabRoadNetworkBuilder] Street grid disabled for " + siteLabel + " — skipped.", this);
                    continue;
                }

                siteLayout.Normalize();
                if (siteLayout.generateStreetGrid && siteLayout.generateArterialRing)
                    siteLayout.clipLocalStreetsToArterialRing = true;

                siteLayout.centerXZ = site.centerXZ;
                var seed = ResolveSiteSeed(site, ResolveBuiltRegionSeed(), i);

                // Zone each site by its weighted district mix.
                IReadOnlyList<CityDistrictWeight> districtMix =
                    site.districtMix is { Count: > 0 } ? site.districtMix : null;

                var areas = CityMathBlockLayoutGenerator.Generate(siteLayout, settings, seed, districtMix);
                if (areas != null)
                    combined.AddRange(areas);
            }

            return combined;
        }

        public void GenerateDistrictLots()
        {
            if (!ResolvedGenerateResidentialLots)
            {
                Debug.Log("[CityPrefabRoadNetworkBuilder] District lots disabled — skipped.", this);
                return;
            }

            var container = transform.Find(AreasContainerName);
            if (container == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No named areas container — run Generate Named Areas first.", this);
                return;
            }

            var roadObjectsRoot = ResolveRoadObjectsRoot();
            var settings = ResolveDistrictRoadNetworkSettings();

            // Region mode subdivides every site in one pass, each with its own
            // region-derived seed; single-city mode keeps the template-seeded flow.
            var lotWatch = System.Diagnostics.Stopwatch.StartNew();
            var lotsCreated = 0;
            if (RegionModeActive)
            {
                var jobs = CollectSiteLotsJobs(ActiveRegionAsset, ResolveBuiltRegionSeed(), container);
                for (var i = 0; i < jobs.Count; i++)
                    lotsCreated += BuildLots(container, roadObjectsRoot, settings, jobs[i].Seed, jobs[i].Areas);
            }
            else
            {
                var resolvedLayout = Layout;
                var seed = resolvedLayout != null && resolvedLayout.layoutSeed != 0 ? resolvedLayout.layoutSeed : 12345;
                lotsCreated = BuildLots(container, roadObjectsRoot, settings, seed, areaFilter: null);
            }

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] District lots=" + lotsCreated +
                " in " + lotWatch.ElapsedMilliseconds + "ms.", this);
        }

        private readonly struct SiteLotsJob
        {
            public readonly int Seed;
            public readonly List<Transform> Areas;

            public SiteLotsJob(int seed, List<Transform> areas)
            {
                Seed = seed;
                Areas = areas;
            }
        }

        /// <summary>
        ///     Buckets named-area children by nearest site and resolves each site's
        ///     region-derived seed — mirrors <see cref="BuildNamedAreasForAllSites" />.
        /// </summary>
        private static List<SiteLotsJob> CollectSiteLotsJobs(
            CityRegionAsset region,
            int regionSeed,
            Transform container)
        {
            var buckets = BucketAreaChildrenBySite(region, container);
            var jobs = new List<SiteLotsJob>();
            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                if (site == null || buckets[i].Count == 0)
                    continue;

                jobs.Add(new SiteLotsJob(ResolveSiteSeed(site, regionSeed, i), buckets[i]));
            }

            return jobs;
        }

        private static List<Transform>[] BucketAreaChildrenBySite(CityRegionAsset region, Transform container)
        {
            var buckets = new List<Transform>[region.SiteCount];
            for (var i = 0; i < buckets.Length; i++)
                buckets[i] = new List<Transform>();

            for (var c = 0; c < container.childCount; c++)
            {
                var child = container.GetChild(c);
                var marker = child.GetComponent<CityNamedAreaMarker>();
                if (marker == null)
                    continue;

                var center = marker.GetHubShiftedBoundsXZ().center;
                buckets[FindNearestSiteIndex(region, center)].Add(child);
            }

            return buckets;
        }

        private static int FindNearestSiteIndex(CityRegionAsset region, Vector2 worldCenterXZ)
        {
            var bestIndex = 0;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                if (site == null)
                    continue;

                var dx = worldCenterXZ.x - site.centerXZ.x;
                var dy = worldCenterXZ.y - site.centerXZ.y;
                var sqr = dx * dx + dy * dy;
                if (sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                bestIndex = i;
            }

            return bestIndex;
        }

        private int BuildLots(
            Transform container,
            Transform roadObjectsRoot,
            RoadNetworkSettings settings,
            int seed,
            IReadOnlyList<Transform> areaFilter)
        {
            return CityPrefabResidentialLotBuilder.GenerateResidentialLots(
                container,
                new CityPrefabResidentialLotBuilder.Config
                {
                    FencePrefab = ResolveFencePrefab(),
                    GasStationChance = ResolvedGasStationChance,
                    ResidentialLotSize = ResolvedResidentialLotSize,
                    CommercialLotSize = ResolvedCommercialLotSize,
                    IndustrialLotSize = ResolvedIndustrialLotSize,
                    CityCoreLotSize = ResolvedCityCoreLotSize,
                    CreateEditorFloorVisuals = ResolvedCreateEditorFloorVisuals,
                    FloorVisualHeight = ResolvedFloorVisualHeight,
                    Seed = seed,
                    RoadObjectsRoot = roadObjectsRoot,
                    PlaceFences = false,
                    AreaFilter = areaFilter,
                    RoadSettings = settings
                });
        }

        public void GenerateDistrictFences()
        {
            if (!ResolvedGenerateResidentialLots)
            {
                Debug.Log("[CityPrefabRoadNetworkBuilder] Fences disabled — skipped.", this);
                return;
            }

            var container = transform.Find(AreasContainerName);
            if (container == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No named areas container — run Generate Named Areas first.", this);
                return;
            }

            var resolvedLayout = Layout;
            var seed = resolvedLayout != null && resolvedLayout.layoutSeed != 0 ? resolvedLayout.layoutSeed : 12345;
            var roadObjectsRoot = ResolveRoadObjectsRoot();
            var settings = ResolveDistrictRoadNetworkSettings();
            var allAreas = CityLotBoundsUtility.CollectFromAreasRoot(container);

            var fencesPlaced = CityPrefabResidentialLotBuilder.PlaceFencesOnly(
                container,
                new CityPrefabResidentialLotBuilder.Config
                {
                    FencePrefab = ResolveFencePrefab(),
                    CreateEditorFloorVisuals = ResolvedCreateEditorFloorVisuals,
                    FloorVisualHeight = ResolvedFloorVisualHeight,
                    Seed = seed,
                    RoadObjectsRoot = roadObjectsRoot,
                    PlaceFences = false,
                    AllAreas = allAreas,
                    RoadSettings = settings
                });

            Debug.Log("[CityPrefabRoadNetworkBuilder] Fences placed on " + fencesPlaced + " lots.", this);
        }

    }
}
