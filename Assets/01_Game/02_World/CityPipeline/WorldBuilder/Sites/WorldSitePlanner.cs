using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Picks city and landmark sites from buildability / clearance / hydrology constraints.</summary>
    public sealed partial class WorldSitePlanner
    {
        private const float MaxCitySlopeDegrees = 12f;
        private const float ProvisionalMaxCitySlopeDegrees = 32f;
        private const float MaxLandmarkSlopeDegrees = 20f;
        private const float MinCityBuildability = 0.45f;
        private const float ProvisionalMinCityBuildability = 0.28f;
        private const float MinLandmarkBuildability = 0.3f;
        private const float MaxNoBuildMask = 0.5f;

        private readonly List<WorldSiteDiagnostic> _diagnostics = new();

        private OrogenPlan _orogen;
        private LandformProfile _landforms;
        private float _passHalfWidth = 120f;

        public IReadOnlyList<WorldSiteDiagnostic> Diagnostics => _diagnostics;

        public WorldSitePlan Plan(in WorldSitePlanArgs args)
        {
            _diagnostics.Clear();
            _orogen = args.Orogen;
            _landforms = args.Profile?.Landforms;
            _passHalfWidth = args.Profile?.Landforms != null
                ? Mathf.Max(40f, args.Profile.Landforms.PassCorridorHalfWidthMeters)
                : 120f;
            if (args.Profile?.MapSizeSettings == null || args.TerrainQuery == null)
            {
                _diagnostics.Add(new WorldSiteDiagnostic("missing_inputs", "Profile or terrain query missing.", Vector2.zero, true));
                return new WorldSitePlan();
            }

            var settings = args.Profile.MapSizeSettings;
            var localRng = args.Rng?.CreateStream(unchecked((int)0xC17E0001)) ??
                           new DeterministicRng(args.Session.Seed ^ unchecked((int)0xC17E0001));
            var quota = settings.ResolveQuota(args.Session.Tier, args.Session.Seed);
            var orderedTypes = WorldSettlementHierarchy.ExpandOrderedTypes(
                quota,
                localRng.CreateStream(unchecked((int)0xC17E00A1)));
            var cityTarget = orderedTypes.Count;
            var roadPad = settings.RoadExitClearanceMeters;
            var falloffGuard = args.Profile.Landforms != null
                ? Mathf.Max(
                    WorldSiteSpacing.DefaultFalloffGuardMeters,
                    args.Profile.Landforms.CityPadFalloffMinMeters)
                : WorldSiteSpacing.DefaultFalloffGuardMeters;
            var bounds = args.WorldBoundsOverride ?? args.Session.WorldBoundsXZ;
            var deepWater = ResolveDeepWaterDepth(args.Profile);
            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(args.Profile.Landforms);
            var seaLevel = args.Profile.Hydrology != null ? args.Profile.Hydrology.SeaLevelWorldY : 0f;
            var minPadY = CityPadReclaimPolicy.MinPadHeightWorldY(seaLevel, args.Profile.Landforms);
            var cities = new List<WorldCitySite>(cityTarget);

            // Provisional (pre-biome/hydro): relax slope/footprint/continuity so Large quotas
            // can land on rugged landforms; orogen peaks still blocked.
            var minBuild = args.ProvisionalPlacement ? ProvisionalMinCityBuildability : MinCityBuildability;
            var maxSlope = args.ProvisionalPlacement ? ProvisionalMaxCitySlopeDegrees : MaxCitySlopeDegrees;
            PlaceSitesByHierarchy(new PlaceSitesByHierarchyArgs
            {
                Sites = cities,
                OrderedTypes = orderedTypes,
                Bounds = bounds,
                RoadPadMeters = roadPad,
                FalloffGuardMeters = falloffGuard,
                TerrainQuery = args.TerrainQuery,
                LocalRng = localRng,
                Seed = args.Session.Seed,
                MinBuildability = minBuild,
                MaxSlopeDegrees = maxSlope,
                DeepWaterDepth = deepWater,
                MaxReclaimDepth = maxReclaimDepth,
                MinPadY = minPadY,
                RequireFootprint = true,
                Existing = null,
                TileCatalog = args.TileCatalog,
                EnforceFootprintBuildability = !args.ProvisionalPlacement,
                EnforceContinuityGate = !args.ProvisionalPlacement
            });

            var landmarkTarget = Mathf.Max(1, cityTarget / 3);
            var landmarks = new List<WorldCitySite>(landmarkTarget);
            var landmarkFootprint = settings.LargestCityFootprintRadiusMeters * 0.5f;
            PlaceSites(new PlaceSitesArgs
            {
                Sites = landmarks,
                Target = landmarkTarget,
                Bounds = bounds,
                Clearance = landmarkFootprint + roadPad,
                FootprintRadius = landmarkFootprint,
                TerrainQuery = args.TerrainQuery,
                LocalRng = localRng.CreateStream(unchecked((int)0xC17E0002)),
                Seed = args.Session.Seed ^ 0x4C4D4B01,
                NamePrefix = "Landmark",
                MinBuildability = MinLandmarkBuildability,
                MaxSlopeDegrees = MaxLandmarkSlopeDegrees,
                DeepWaterDepth = deepWater,
                MaxReclaimDepth = maxReclaimDepth,
                MinPadY = minPadY,
                RequireFootprint = false,
                Existing = cities,
                TileCatalog = args.TileCatalog,
                SiteType = CitySiteType.Town,
                DisplayIndex = 0,
                EnforceFootprintBuildability = true,
                EnforceContinuityGate = true,
                RoadPadMeters = roadPad,
                FalloffGuardMeters = falloffGuard
            });

            if (cities.Count < cityTarget)
            {
                _diagnostics.Add(new WorldSiteDiagnostic(
                    "under_target",
                    $"Selected {cities.Count}/{cityTarget} city sites " +
                    $"(quota M{quota.Metropolis}/C{quota.City}/S{quota.Settlements}).",
                    bounds.center,
                    true));
            }

            return new WorldSitePlan(cities, landmarks);
        }

        /// <summary>Re-rolls city positions when MST highway edges fail pathfinding.</summary>
        public void ImproveHighwayConnectivity(in ImproveHighwayConnectivityArgs args)
        {
            if (!TryCreateHighwayImproveContext(args, out var ctx))
                return;

            var rerollCounts = new int[args.Cities.Count];
            var edgeCache = new Dictionary<long, bool>(64);
            var guard = 0;
            while (guard++ < 64)
            {
                var failed = CollectFailedHighwayEdges(
                    args.Cities,
                    ctx.RoadSettings,
                    ctx.CostField,
                    args.TerrainQuery,
                    _landforms,
                    edgeCache,
                    args.Session.Seed);
                if (failed.Count == 0)
                    return;

                if (!TryImproveAnyFailedEdge(args, ctx, failed, rerollCounts, edgeCache))
                    return;
            }
        }

        private bool TryCreateHighwayImproveContext(
            in ImproveHighwayConnectivityArgs args,
            out HighwayImproveContext ctx)
        {
            ctx = default;
            var roadSettings = args.Profile?.RoadNetworkSettings;
            if (roadSettings == null ||
                !roadSettings.planInterCityHighwaysBeforePads ||
                !roadSettings.connectCitiesWithHighways ||
                args.Cities == null ||
                args.Cities.Count < 2 ||
                args.TerrainQuery == null)
                return false;

            var mapSettings = args.Profile.MapSizeSettings;
            if (mapSettings == null)
                return false;

            _orogen = args.Orogen;
            _landforms = args.Profile.Landforms;
            _passHalfWidth = args.Profile.Landforms != null
                ? Mathf.Max(40f, args.Profile.Landforms.PassCorridorHalfWidthMeters)
                : 120f;

            var links = InterCityHighwayPlanner.BuildLinks(args.Cities, roadSettings, args.Session.Seed);
            var costField = InterCityHighwayPlanner.BuildCostFieldForLinks(
                args.Session,
                args.Profile,
                args.TerrainQuery,
                roadSettings,
                args.Orogen,
                args.Cities,
                links);
            if (costField == null || !costField.IsValid)
                return false;

            var seaLevel = args.Profile.Hydrology != null ? args.Profile.Hydrology.SeaLevelWorldY : 0f;
            ctx = new HighwayImproveContext
            {
                RoadSettings = roadSettings,
                CostField = costField,
                RoadPad = mapSettings.RoadExitClearanceMeters,
                FalloffGuard = args.Profile.Landforms != null
                    ? Mathf.Max(
                        WorldSiteSpacing.DefaultFalloffGuardMeters,
                        args.Profile.Landforms.CityPadFalloffMinMeters)
                    : WorldSiteSpacing.DefaultFalloffGuardMeters,
                DeepWater = ResolveDeepWaterDepth(args.Profile),
                MaxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(args.Profile.Landforms),
                MinPadY = CityPadReclaimPolicy.MinPadHeightWorldY(seaLevel, args.Profile.Landforms),
                MaxAttemptsPerCity = Mathf.Max(0, roadSettings.maxConnectivityRerollAttemptsPerCity),
                LocalRng = args.Rng?.CreateStream(unchecked((int)0xC17E00C1)) ??
                           new DeterministicRng(args.Session.Seed ^ unchecked((int)0xC17E00C1))
            };
            return true;
        }

        private bool TryImproveAnyFailedEdge(
            in ImproveHighwayConnectivityArgs args,
            HighwayImproveContext ctx,
            List<(int, int)> failed,
            int[] rerollCounts,
            Dictionary<long, bool> edgeCache)
        {
            for (var f = 0; f < failed.Count; f++)
            {
                if (TryImproveFailedEdge(args, ctx, failed[f], rerollCounts, edgeCache))
                    return true;
            }

            return false;
        }

        private bool TryImproveFailedEdge(
            in ImproveHighwayConnectivityArgs args,
            HighwayImproveContext ctx,
            (int, int) edge,
            int[] rerollCounts,
            Dictionary<long, bool> edgeCache)
        {
            var indexToReroll = ChooseRerollIndex(args.Cities, edge.Item1, edge.Item2, args.Bounds.center);
            if (rerollCounts[indexToReroll] >= ctx.MaxAttemptsPerCity)
            {
                _diagnostics.Add(new WorldSiteDiagnostic(
                    "highway_edge_fail",
                    $"Highway edge {edge.Item1}->{edge.Item2} failed after rerolls.",
                    args.Cities[indexToReroll].CenterXZ,
                    isError: true));
                return false;
            }

            var city = args.Cities[indexToReroll];
            var footprintRadius = Mathf.Max(city.HalfWidthMeters, city.HalfDepthMeters);
            var apronGuard = WorldSiteSpacing.ApronGuardMeters(city.SiteType, ctx.FalloffGuard);
            var clearance = footprintRadius + apronGuard + ctx.RoadPad;
            if (!TryRerollCitySite(
                    new TryRerollCitySiteArgs
                    {
                        Cities = args.Cities,
                        IndexToReroll = indexToReroll,
                        Bounds = args.Bounds,
                        Clearance = clearance,
                        FootprintRadius = footprintRadius,
                        RoadPadMeters = ctx.RoadPad,
                        FalloffGuardMeters = ctx.FalloffGuard,
                        TerrainQuery = args.TerrainQuery,
                        LocalRng = ctx.LocalRng.CreateStream(
                            unchecked((int)(0xC17E1000 + indexToReroll * 17 + rerollCounts[indexToReroll]))),
                        Seed = args.Session.Seed,
                        DeepWaterDepth = ctx.DeepWater,
                        MaxReclaimDepth = ctx.MaxReclaimDepth,
                        TileCatalog = args.TileCatalog
                    },
                    out var newCenter))
                return false;

            ApplyRerolledCitySite(args, ctx, indexToReroll, newCenter, rerollCounts);
            InvalidateIncidentEdgeCache(edgeCache, indexToReroll, args.Cities.Count);
            return true;
        }

        private void ApplyRerolledCitySite(
            in ImproveHighwayConnectivityArgs args,
            HighwayImproveContext ctx,
            int indexToReroll,
            Vector2 newCenter,
            int[] rerollCounts)
        {
            rerollCounts[indexToReroll]++;
            args.Cities[indexToReroll].CenterXZ = newCenter;
            if (args.TerrainQuery.TrySample(newCenter, out var sample))
            {
                args.Cities[indexToReroll].PadHeightWorldY = Mathf.Max(sample.HeightWorldY, ctx.MinPadY);
                args.Cities[indexToReroll].BuildabilityScore = sample.Buildability;
            }

            var coastal = ConsumeLastAcceptCoastal(out var seaward);
            args.Cities[indexToReroll].IsCoastal = coastal;
            args.Cities[indexToReroll].SeawardNormalXZ = seaward;
            args.Cities[indexToReroll].CoastExposure01 = coastal ? 1f : 0f;
            args.Cities[indexToReroll].ClearHighwayEntry();

            var links = InterCityHighwayPlanner.BuildLinks(args.Cities, ctx.RoadSettings, args.Session.Seed);
            ctx.CostField = InterCityHighwayPlanner.BuildCostFieldForLinks(
                args.Session,
                args.Profile,
                args.TerrainQuery,
                ctx.RoadSettings,
                args.Orogen,
                args.Cities,
                links);

            _diagnostics.Add(new WorldSiteDiagnostic(
                "highway_reroll_ok",
                $"Re-rolled city {indexToReroll} for highway connectivity.",
                newCenter));
        }

        private static List<(int, int)> CollectFailedHighwayEdges(
            IReadOnlyList<WorldCitySite> cities,
            RoadNetworkSettings settings,
            TerrainRoadCostField costField,
            IWorldTerrainQuery terrainQuery,
            LandformProfile landforms,
            Dictionary<long, bool> edgeCache,
            int seed)
        {
            var failed = new List<(int, int)>();
            var links = InterCityHighwayPlanner.BuildLinks(cities, settings, seed);
            for (var i = 0; i < links.Count; i++)
            {
                var indexA = links[i].IndexA;
                var indexB = links[i].IndexB;
                var key = EdgeCacheKey(indexA, indexB);
                if (edgeCache != null && edgeCache.TryGetValue(key, out var cachedOk))
                {
                    if (!cachedOk)
                        failed.Add((indexA, indexB));
                    continue;
                }

                var ok = InterCityHighwayPlanner.TryValidateEdge(
                    cities[indexA], cities[indexB], settings, costField, terrainQuery, landforms);
                if (edgeCache != null)
                    edgeCache[key] = ok;
                if (ok)
                    continue;
                failed.Add((indexA, indexB));
            }

            return failed;
        }

        private static long EdgeCacheKey(int indexA, int indexB)
        {
            var lo = indexA < indexB ? indexA : indexB;
            var hi = indexA < indexB ? indexB : indexA;
            return ((long)lo << 32) | (uint)hi;
        }

        private static void InvalidateIncidentEdgeCache(Dictionary<long, bool> edgeCache, int siteIndex, int cityCount)
        {
            if (edgeCache == null || cityCount <= 0)
                return;
            for (var other = 0; other < cityCount; other++)
            {
                if (other == siteIndex)
                    continue;
                edgeCache.Remove(EdgeCacheKey(siteIndex, other));
            }
        }

        private static int ChooseRerollIndex(
            IReadOnlyList<WorldCitySite> cities,
            int indexA,
            int indexB,
            Vector2 mapCenter)
        {
            var distA = (cities[indexA].CenterXZ - mapCenter).sqrMagnitude;
            var distB = (cities[indexB].CenterXZ - mapCenter).sqrMagnitude;
            return distA >= distB ? indexA : indexB;
        }

        private bool TryRerollCitySite(in TryRerollCitySiteArgs args, out Vector2 newCenter)
        {
            newCenter = default;
            var city = args.Cities[args.IndexToReroll];
            var siteType = city != null ? city.SiteType : CitySiteType.Town;
            const int maxAttempts = 48;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var x = Mathf.Lerp(
                    args.Bounds.xMin + args.Clearance,
                    args.Bounds.xMax - args.Clearance,
                    args.LocalRng.NextFloat01());
                var z = Mathf.Lerp(
                    args.Bounds.yMin + args.Clearance,
                    args.Bounds.yMax - args.Clearance,
                    args.LocalRng.NextFloat01());
                var center = new Vector2(x, z);
                if (!TryAcceptSite(
                        new TryAcceptSiteArgs
                        {
                            Center = center,
                            TerrainQuery = args.TerrainQuery,
                            MinBuildability = MinCityBuildability,
                            MaxSlopeDegrees = MaxCitySlopeDegrees,
                            DeepWaterDepth = args.DeepWaterDepth,
                            MaxReclaimDepth = args.MaxReclaimDepth,
                            RequireFootprint = true,
                            TileCatalog = args.TileCatalog,
                            FootprintRadius = args.FootprintRadius,
                            EnforceFootprintBuildability = true,
                            EnforceContinuityGate = true
                        },
                        out _,
                        out _))
                    continue;

                if (!FootprintInsideBounds(center, args.FootprintRadius, args.Bounds))
                    continue;

                if (TooCloseExcluding(
                        args.Cities,
                        center,
                        siteType,
                        args.FootprintRadius,
                        args.RoadPadMeters,
                        args.FalloffGuardMeters,
                        args.IndexToReroll))
                    continue;

                newCenter = center;
                return true;
            }

            _ = args.Seed;
            return false;
        }

        private static bool TooCloseExcluding(
            IReadOnlyList<WorldCitySite> sites,
            Vector2 center,
            CitySiteType candidateType,
            float candidateFootprintRadius,
            float roadPadMeters,
            float falloffGuardMeters,
            int excludedIndex)
        {
            if (sites == null)
                return false;

            for (var i = 0; i < sites.Count; i++)
            {
                if (i == excludedIndex || sites[i] == null)
                    continue;
                var minSeparation = WorldSiteSpacing.MinCenterSeparationMeters(
                    sites[i],
                    candidateType,
                    candidateFootprintRadius,
                    roadPadMeters,
                    falloffGuardMeters);
                if (Vector2.Distance(sites[i].CenterXZ, center) < minSeparation)
                    return true;
            }

            return false;
        }
    }
}
