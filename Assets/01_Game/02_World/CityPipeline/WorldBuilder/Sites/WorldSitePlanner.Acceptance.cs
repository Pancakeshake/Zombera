using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Site acceptance gates, placement attempts, and footprint probes.</summary>
    public sealed partial class WorldSitePlanner
    {
        private void PlaceSitesByHierarchy(in PlaceSitesByHierarchyArgs args)
        {
            if (args.OrderedTypes == null || args.OrderedTypes.Count == 0)
                return;

            var typeCounts = new int[5];
            for (var i = 0; i < args.OrderedTypes.Count; i++)
            {
                var siteType = args.OrderedTypes[i];
                var footprintRadius = WorldSettlementHierarchy.FootprintRadiusMeters(siteType);
                var apronGuard = WorldSiteSpacing.ApronGuardMeters(siteType, args.FalloffGuardMeters);
                var typeClearance = footprintRadius + apronGuard + Mathf.Max(0f, args.RoadPadMeters);
                var typeIndex = (int)siteType;
                if (typeIndex < 0 || typeIndex >= typeCounts.Length)
                    typeIndex = 0;
                typeCounts[typeIndex]++;

                var namePrefix = WorldSettlementHierarchy.DisplayNamePrefix(siteType);
                var before = args.Sites.Count;
                PlaceSites(new PlaceSitesArgs
                {
                    Sites = args.Sites,
                    Target = before + 1,
                    Bounds = args.Bounds,
                    Clearance = typeClearance,
                    FootprintRadius = footprintRadius,
                    TerrainQuery = args.TerrainQuery,
                    LocalRng = args.LocalRng.CreateStream(unchecked((int)(0xC17E2000 + i))),
                    Seed = args.Seed ^ (i * 9973),
                    NamePrefix = namePrefix,
                    MinBuildability = args.MinBuildability,
                    MaxSlopeDegrees = args.MaxSlopeDegrees,
                    DeepWaterDepth = args.DeepWaterDepth,
                    MaxReclaimDepth = args.MaxReclaimDepth,
                    MinPadY = args.MinPadY,
                    RequireFootprint = args.RequireFootprint,
                    Existing = args.Existing,
                    TileCatalog = args.TileCatalog,
                    SiteType = siteType,
                    DisplayIndex = typeCounts[typeIndex],
                    EnforceFootprintBuildability = args.EnforceFootprintBuildability,
                    EnforceContinuityGate = args.EnforceContinuityGate,
                    RoadPadMeters = args.RoadPadMeters,
                    FalloffGuardMeters = args.FalloffGuardMeters
                });

                if (args.Sites.Count == before)
                {
                    _diagnostics.Add(new WorldSiteDiagnostic(
                        "hierarchy_place_fail",
                        $"Failed to place {namePrefix} slot {typeCounts[typeIndex]}.",
                        args.Bounds.center,
                        true));
                }
            }
        }

        private void PlaceSites(in PlaceSitesArgs args)
        {
            var attempts = 0;
            var remaining = Mathf.Max(1, args.Target - args.Sites.Count);
            var attemptMul = args.EnforceContinuityGate ? 120 : 220;
            var maxAttempts = Mathf.Max(96, remaining * attemptMul);
            while (args.Sites.Count < args.Target && attempts < maxAttempts)
            {
                attempts++;
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
                            MinBuildability = args.MinBuildability,
                            MaxSlopeDegrees = args.MaxSlopeDegrees,
                            DeepWaterDepth = args.DeepWaterDepth,
                            MaxReclaimDepth = args.MaxReclaimDepth,
                            RequireFootprint = args.RequireFootprint,
                            TileCatalog = args.TileCatalog,
                            FootprintRadius = args.FootprintRadius,
                            EnforceFootprintBuildability = args.EnforceFootprintBuildability,
                            EnforceContinuityGate = args.EnforceContinuityGate
                        },
                        out var sample,
                        out var rejectCode))
                {
                    _diagnostics.Add(new WorldSiteDiagnostic(rejectCode, rejectCode, center));
                    continue;
                }

                if (TooClose(
                        args.Sites,
                        center,
                        args.SiteType,
                        args.FootprintRadius,
                        args.RoadPadMeters,
                        args.FalloffGuardMeters) ||
                    (args.Existing != null &&
                     TooClose(
                         args.Existing,
                         center,
                         args.SiteType,
                         args.FootprintRadius,
                         args.RoadPadMeters,
                         args.FalloffGuardMeters)))
                {
                    _diagnostics.Add(new WorldSiteDiagnostic("reject_spacing", "Too close to another site.", center));
                    continue;
                }

                if (!FootprintInsideBounds(center, args.FootprintRadius, args.Bounds))
                {
                    _diagnostics.Add(new WorldSiteDiagnostic("reject_bounds", "Footprint outside map bounds.", center));
                    continue;
                }

                AddAcceptedSite(args, center, sample);
            }
        }

        private void AddAcceptedSite(in PlaceSitesArgs args, Vector2 center, WorldTerrainSample sample)
        {
            var hasher = new StableHash64((ulong)args.Seed);
            hasher.Append(args.Sites.Count);
            hasher.Append(center.x);
            hasher.Append(center.y);
            hasher.Append(args.NamePrefix);
            hasher.Append((int)args.SiteType);

            var indexLabel = args.DisplayIndex > 0 ? args.DisplayIndex : args.Sites.Count + 1;
            var coastal = ConsumeLastAcceptCoastal(out var seaward);
            args.Sites.Add(new WorldCitySite
            {
                StableId = hasher.Finalize(),
                DisplayName = $"{args.NamePrefix}_{indexLabel}",
                SiteType = args.SiteType,
                CenterXZ = center,
                HalfWidthMeters = CitySiteTypePresets.HalfWidth(args.SiteType),
                HalfDepthMeters = CitySiteTypePresets.HalfDepth(args.SiteType),
                PadHeightWorldY = Mathf.Max(sample.HeightWorldY, args.MinPadY),
                BuildabilityScore = sample.Buildability,
                LayoutSeed = args.LocalRng.NextInt(),
                IsCoastal = coastal,
                SeawardNormalXZ = seaward,
                CoastExposure01 = coastal ? 1f : 0f
            });
        }

        private bool TryAcceptSite(
            in TryAcceptSiteArgs args,
            out WorldTerrainSample sample,
            out string rejectCode)
        {
            sample = default;
            rejectCode = "sample_fail";
            if (!args.TerrainQuery.TrySample(args.Center, out sample))
                return false;

            if (!PassesTerrainGates(
                    sample,
                    args.MinBuildability,
                    args.MaxSlopeDegrees,
                    args.DeepWaterDepth,
                    args.MaxReclaimDepth,
                    out rejectCode))
                return false;

            if (args.RequireFootprint &&
                _orogen != null &&
                !PassesOrogenCityGate(args.Center, out rejectCode))
                return false;

            if (args.TileCatalog != null &&
                !FootprintOverlapsLiveTerrain(args.Center, args.FootprintRadius))
            {
                rejectCode = "reject_no_terrain";
                return false;
            }

            if (!args.RequireFootprint)
                return true;

            if (args.EnforceFootprintBuildability &&
                !FootprintIsBuildable(
                    new FootprintIsBuildableArgs
                    {
                        Center = args.Center,
                        TerrainQuery = args.TerrainQuery,
                        MinBuildability = args.MinBuildability * 0.85f,
                        MaxSlopeDegrees = args.MaxSlopeDegrees + 2f,
                        DeepWaterDepth = args.DeepWaterDepth,
                        MaxReclaimDepth = args.MaxReclaimDepth,
                        TileCatalog = args.TileCatalog,
                        FootprintRadius = args.FootprintRadius
                    },
                    out rejectCode))
                return false;

            return PassesContinuityAndRememberCoastal(args, sample.HeightWorldY, out rejectCode);
        }

        private bool PassesContinuityAndRememberCoastal(
            in TryAcceptSiteArgs args,
            float sampleHeightWorldY,
            out string rejectCode)
        {
            rejectCode = null;
            var half = args.FootprintRadius;
            var isCoastal = CityCoastalPadUtility.TryDeriveCoastal(
                args.Center,
                half,
                half,
                args.TerrainQuery,
                _landforms,
                out var seaward,
                out _);

            if (args.EnforceContinuityGate &&
                !CityPadContinuity.PassesQueryContinuityGate(
                    new CityPadContinuity.QueryContinuityGateArgs(
                        args.TerrainQuery,
                        args.Center,
                        args.FootprintRadius,
                        _landforms,
                        sampleHeightWorldY,
                        isCoastal,
                        seaward),
                    out rejectCode))
                return false;

            s_lastAcceptCoastal = isCoastal;
            s_lastAcceptSeaward = seaward;
            return true;
        }

        [System.ThreadStatic] private static bool s_lastAcceptCoastal;
        [System.ThreadStatic] private static Vector2 s_lastAcceptSeaward;

        internal static bool ConsumeLastAcceptCoastal(out Vector2 seawardNormal)
        {
            seawardNormal = s_lastAcceptSeaward;
            var coastal = s_lastAcceptCoastal;
            s_lastAcceptCoastal = false;
            s_lastAcceptSeaward = default;
            return coastal;
        }

        private bool PassesOrogenCityGate(Vector2 center, out string rejectCode)
        {
            rejectCode = null;
            var core = _orogen.SampleOrogenCoreMask(center.x, center.y);
            if (core < 0.45f)
                return true;

            var pass = _orogen.SamplePassAttract(center.x, center.y, _passHalfWidth * 1.5f);
            if (pass >= 0.55f)
                return true;

            rejectCode = "reject_orogen";
            return false;
        }

        private static bool FootprintOverlapsLiveTerrain(Vector2 center, float footprintRadius)
        {
            var rect = new Rect(
                center.x - footprintRadius,
                center.y - footprintRadius,
                footprintRadius * 2f,
                footprintRadius * 2f);
            return WorldTileInfoUtility.TryResolveTerrainsOverlapping(rect, out var terrains) &&
                   terrains != null &&
                   terrains.Count > 0;
        }

        private static bool PassesTerrainGates(
            WorldTerrainSample sample,
            float minBuildability,
            float maxSlopeDegrees,
            float deepWaterDepth,
            float maxReclaimDepth,
            out string rejectCode)
        {
            if (sample.HeightWorldY < 0f)
            {
                rejectCode = "reject_below_zero";
                return false;
            }

            var reclaimable = CityPadReclaimPolicy.IsReclaimable(
                sample.Water.Class,
                sample.Water.DepthMeters,
                maxReclaimDepth);

            if (!reclaimable && sample.NoBuildMask >= MaxNoBuildMask)
            {
                rejectCode = "reject_nobuild";
                return false;
            }

            if (!PassesWaterGate(sample, deepWaterDepth, reclaimable, out rejectCode))
                return false;

            if (sample.SlopeDegrees > maxSlopeDegrees)
            {
                rejectCode = "reject_steep";
                return false;
            }

            if (!reclaimable && sample.Buildability < minBuildability)
            {
                rejectCode = "reject_buildability";
                return false;
            }

            rejectCode = null;
            return true;
        }

        private static bool PassesWaterGate(
            WorldTerrainSample sample,
            float deepWaterDepth,
            bool reclaimable,
            out string rejectCode)
        {
            if (!reclaimable)
                return WorldWaterPlacementGate.PassesWaterSample(sample.Water, deepWaterDepth, out rejectCode);

            rejectCode = null;
            return true;
        }

        private static bool FootprintIsBuildable(in FootprintIsBuildableArgs args, out string rejectCode)
        {
            rejectCode = null;
            const float probe = 90f;
            var offsets = new[]
            {
                new Vector2(probe, 0f),
                new Vector2(-probe, 0f),
                new Vector2(0f, probe),
                new Vector2(0f, -probe)
            };

            for (var i = 0; i < offsets.Length; i++)
            {
                var probeCenter = args.Center + offsets[i];
                if (!args.TerrainQuery.TrySample(probeCenter, out var sample))
                {
                    rejectCode = "reject_footprint_sample";
                    return false;
                }

                if (args.TileCatalog != null &&
                    !FootprintOverlapsLiveTerrain(probeCenter, args.FootprintRadius * 0.35f))
                {
                    rejectCode = "reject_footprint_terrain";
                    return false;
                }

                if (PassesTerrainGates(
                        sample,
                        args.MinBuildability,
                        args.MaxSlopeDegrees,
                        args.DeepWaterDepth,
                        args.MaxReclaimDepth,
                        out rejectCode))
                    continue;

                rejectCode = "reject_footprint";
                return false;
            }

            return true;
        }

        private static float ResolveDeepWaterDepth(WorldGenerationProfile profile) =>
            WorldWaterPlacementGate.ResolveDeepWaterDepth(profile);

        private static bool TooClose(
            IReadOnlyList<WorldCitySite> sites,
            Vector2 center,
            CitySiteType candidateType,
            float candidateFootprintRadius,
            float roadPadMeters,
            float falloffGuardMeters)
        {
            if (sites == null)
                return false;

            for (var i = 0; i < sites.Count; i++)
            {
                var other = sites[i];
                if (other == null)
                    continue;
                var minSeparation = WorldSiteSpacing.MinCenterSeparationMeters(
                    other,
                    candidateType,
                    candidateFootprintRadius,
                    roadPadMeters,
                    falloffGuardMeters);
                if (Vector2.Distance(other.CenterXZ, center) < minSeparation)
                    return true;
            }

            return false;
        }

        private static bool FootprintInsideBounds(Vector2 center, float footprintRadius, Rect bounds) =>
            center.x - footprintRadius >= bounds.xMin &&
            center.x + footprintRadius <= bounds.xMax &&
            center.y - footprintRadius >= bounds.yMin &&
            center.y + footprintRadius <= bounds.yMax;
    }
}
