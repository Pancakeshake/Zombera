using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private void PrepareActiveRegionSites(CityRegionAsset region, int scatterSeedOverride, int regionSeed)
        {
            if (_hasPipelineWorldBounds)
            {
                region.ConfigureScatterForWorldBounds(_pipelineWorldBoundsXZ);
            }

            var hasTerrain = _hasPipelineWorldBounds &&
                             _pipelineWorldBoundsXZ.width > 0f &&
                             _pipelineWorldBoundsXZ.height > 0f;
            var terrainBounds = hasTerrain ? _pipelineWorldBoundsXZ : default(Rect?);
            var sitesValid = !hasTerrain || region.AllSitesInsideScatterZone(terrainBounds);

            if (!sitesValid && !region.autoScatterOnBuild)
            {
                region.ConstrainSitesToScatterZone(scatterSeedOverride, terrainBounds);
                sitesValid = !hasTerrain || region.AllSitesInsideScatterZone(terrainBounds);
                if (!sitesValid)
                    region.autoScatterOnBuild = true;
            }

            if (region.autoScatterOnBuild)
            {
                var scattered = region.RandomizeSites(scatterSeedOverride);
                region.ResolveScatterArea(out _, out var zoneHalfExtent);
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Region auto-scatter: placed " + scattered + "/" +
                    region.EffectiveScatterSiteCount + " sites in a " + (zoneHalfExtent * 2f / 1000f).ToString("F0") +
                    "km zone (" + region.mapTilesPerSide + " tiles), seed=" + regionSeed + ".", this);
            }
            else if (hasTerrain)
            {
                region.ConstrainSitesToScatterZone(scatterSeedOverride, terrainBounds);
            }

            var relocated = region.EnsureSpacedSites(scatterSeedOverride);
            if (relocated > 0)
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] Region sites overlapped — auto-spaced " + relocated +
                    " site(s) with " + region.minSiteClearanceMeters + "m clearance.", this);
            }

            if (hasTerrain && !region.AllSitesInsideScatterZone(terrainBounds))
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] Sites still outside terrain after preparation — forcing rescatter.",
                    this);
                region.autoScatterOnBuild = true;
                var scattered = region.RandomizeSites(scatterSeedOverride);
                region.ConstrainSitesToScatterZone(scatterSeedOverride, terrainBounds);
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Forced rescatter placed " + scattered + "/" +
                    region.EffectiveScatterSiteCount + " sites inside terrain bounds.",
                    this);
            }

            if (hasTerrain && !region.AllSitesInsideScatterZone(terrainBounds))
            {
                Debug.LogError(
                    "[CityPrefabRoadNetworkBuilder] One or more city sites still fall outside the " +
                    "terrain scatter zone after forced rescatter. Check map tier and CityRegion scatter settings.",
                    this);
            }
        }

        /// <summary>
        ///     Builds EVERY site in the region in one pass: each city keeps its own
        ///     randomized layout (seed derived from the region seed + site index) with
        ///     guaranteed highway exits, and inter-city highways (MST) link the cities
        ///     through real grid-knot junctions on both rings.
        /// </summary>
        private bool TryBuildRegionalNetworkContext(
            CityMathRoadLayout template,
            RoadNetworkSettings settings,
            out CityRoadNetworkBuildContext buildContext)
        {
            buildContext = default;
            var region = ActiveRegionAsset;
            if (region == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Region mode active but no City Region asset.", this);
                return false;
            }

            var regionSeed = ResolveRegionSeed();
            var scatterSeedOverride = region.randomizeRegionSeedPerBuild || fixedRegionSeedOverride != 0
                ? regionSeed
                : 0;

            PrepareActiveRegionSites(region, scatterSeedOverride, regionSeed);

            var network = new RoadNetworkRuntime(regionSeed);
            var sites = new List<CityHubSite>(region.SiteCount);
            var boundsUnion = default(Rect);
            var innerUnion = default(Rect);
            var outerUnion = default(Rect);
            var hasInnerFlattenRect = false;
            var totalSegments = 0;

            for (var i = 0; i < region.SiteCount; i++)
            {
                var site = region.GetSite(i);
                if (site == null)
                    continue;

                var siteLayout = ApplySiteToLayout(template, site);
                // Region mode: no standalone exit stubs — the inter-city MST highways
                // ARE the exits, anchored at ring T-junctions facing each neighbour.
                siteLayout.generateHighwayExits = false;
                siteLayout.guaranteedHighwayExitCount = 0;
                var siteSeed = ResolveSiteSeed(site, regionSeed, i);

                var siteNetwork = CityMathRoadLayoutGenerator.Generate(siteLayout, settings, siteSeed);
                if (siteNetwork.Roads.Count == 0)
                {
                    Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Region site '" + site.displayName + "' produced zero roads — skipped.", this);
                    continue;
                }

                RenumberRoads(siteNetwork, i * 100000);
                AppendRoads(network, siteNetwork);
                sites.Add(site);

                var resolved = siteLayout.Resolve(siteSeed);
                var siteBounds = ExpandBoundsForArterialRing(siteLayout, resolved, siteLayout.ComputeBoundsRect(resolved));
                boundsUnion = UnionRect(boundsUnion, siteBounds);

                siteLayout.GetTerrainFlattenBounds(site.centerXZ, siteSeed, out var inner, out var outer);
                innerUnion = UnionRect(innerUnion, inner);
                outerUnion = UnionRect(outerUnion, outer);
                hasInnerFlattenRect = true;
                totalSegments += siteLayout.EstimateSplitSegmentCount();

                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Region site '" + site.displayName + "' (" + site.siteType +
                    ") at " + site.centerXZ + ", seed=" + siteSeed + ", roads=" + siteNetwork.Roads.Count, this);
            }

            if (network.Roads.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Region layout produced zero roads.", this);
                return false;
            }

            var highwayCount = 0;
            if (region.generateRegionHighways)
            {
                // Prefer world-planned highways from artifacts so mesh matches corridor carve/tunnels.
                if (TryImportArtifactHighways(network, out highwayCount))
                {
                    Debug.Log(
                        "[CityPrefabRoadNetworkBuilder] Imported " + highwayCount +
                        " world-planned inter-city highway(s) from artifacts.",
                        this);
                }
                else if (HasPipelineRoadAuthority())
                {
                    // Artifact Roads present (even empty) owns highways — never bowed MST fallback.
                    Debug.LogWarning(
                        "[CityPrefabRoadNetworkBuilder] World highway planner produced no " +
                        "terrain-valid inter-city highways; skipping city fallback geometry.",
                        this);
                }
                else
                {
                    highwayCount = CityMathRoadLayoutGenerator.AddInterCityHighways(
                        network,
                        sites,
                        settings,
                        regionSeed,
                        region.highwayMinLinkDistanceMeters);
                    Debug.Log(
                        "[CityPrefabRoadNetworkBuilder] Region inter-city highways added: " + highwayCount,
                        this);
                }
            }

            if (!ValidateRegionJunctionBudget(totalSegments))
                return false;

            buildContext = new CityRoadNetworkBuildContext
            {
                Network = network,
                Bounds = ExpandRect(UnionRect(boundsUnion, RoadsUnionBounds(network)), 20f, 20f),
                InnerFlattenRect = innerUnion,
                OuterFlattenRect = outerUnion,
                HasInnerFlattenRect = hasInnerFlattenRect
            };

            // Only record the seed once the network is known to be publishable —
            // otherwise a blocked build leaves later steps pointing at roads that
            // were never published.
            if (!ValidateBuiltNetwork(ref buildContext))
                return false;

            lastBuiltRegionSeed = regionSeed;
            return true;
        }

        private bool ValidateRegionJunctionBudget(int totalSegments)
        {
            if (!CreateJunctionConnectors)
                return true;

            if (totalSegments <= ProceduralRoadSystem.CityPrefabMaxSplitSegmentsHardBlock)
                return true;

            Debug.LogError(
                "[CityPrefabRoadNetworkBuilder] Region build blocked: estimated " + totalSegments +
                " split segments across all sites exceeds the hard limit (" +
                ProceduralRoadSystem.CityPrefabMaxSplitSegmentsHardBlock +
                "). Increase block spacing or remove sites.", this);
            return false;
        }

        private static void RenumberRoads(RoadNetworkRuntime network, int idOffset)
        {
            for (var i = 0; i < network.Roads.Count; i++)
                network.Roads[i].id += idOffset;
        }

        private static void AppendRoads(RoadNetworkRuntime target, RoadNetworkRuntime source)
        {
            for (var i = 0; i < source.Roads.Count; i++)
                target.AddRoad(source.Roads[i]);
        }

        private static Rect UnionRect(Rect a, Rect b)
        {
            if (a.width <= 0f && a.height <= 0f)
                return b;
            if (b.width <= 0f && b.height <= 0f)
                return a;

            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        private static Rect RoadsUnionBounds(RoadNetworkRuntime network)
        {
            var union = default(Rect);
            for (var i = 0; i < network.Roads.Count; i++)
                union = UnionRect(union, network.Roads[i].BoundsXZ);
            return union;
        }

        private static Rect ExpandRect(Rect rect, float padX, float padZ) =>
            Rect.MinMaxRect(rect.xMin - padX, rect.yMin - padZ, rect.xMax + padX, rect.yMax + padZ);

        private Vector2 ResolveFlattenCenterXZ(CityRoadNetworkBuildContext buildContext)
        {
            if (RegionModeActive && buildContext.InnerFlattenRect.width > 0f && buildContext.InnerFlattenRect.height > 0f)
                return buildContext.InnerFlattenRect.center;
            return ResolveLayoutCenterXZ();
        }

        /// <summary>
        ///     Copies world-planned highways from pipeline artifacts into the city network.
        ///     Returns false when no planned highways are available.
        /// </summary>
        private bool TryImportArtifactHighways(RoadNetworkRuntime network, out int imported)
        {
            imported = 0;
            var source = _infrastructureArtifacts?.Roads?.Roads;
            if (source == null || source.Count == 0)
                return false;

            for (var i = 0; i < source.Count; i++)
            {
                var road = source[i];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                network.AddRoad(CloneHighway(road));
                imported++;
            }

            return imported > 0;
        }

        private static RoadPolyline CloneHighway(RoadPolyline source)
        {
            var clone = new RoadPolyline
            {
                id = source.id,
                roadClass = RoadClass.Highway,
                widthMeters = source.widthMeters,
                preserveWorldPath = true,
                curvedMarkers = source.curvedMarkers,
                pointsXZ = new List<Vector2>(source.pointsXZ.Count)
            };
            for (var i = 0; i < source.pointsXZ.Count; i++)
                clone.pointsXZ.Add(source.pointsXZ[i]);
            return clone;
        }

        /// <summary>Force rebuild of Combined_* meshes after world highway geometry changes.</summary>
        public void InvalidateRoadCache(string reason = null)
        {
            _lastGeneratedRoadNetwork = null;
            _cachedRoadPolylines.Clear();
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Road cache invalidated: " + reason,
                    this);
            }
        }
    }
}
