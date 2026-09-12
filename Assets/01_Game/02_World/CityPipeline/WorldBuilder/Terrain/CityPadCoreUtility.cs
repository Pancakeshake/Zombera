using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Arterial-ring + flat-margin plateau cores. Escarpments use noisy slopes + talus;
    ///     outside that band, natural landforms/erosion own the field.
    /// </summary>
    public static partial class CityPadCoreUtility
    {
        public const float DefaultFlatMarginMeters = 30f;
        public const float DefaultPlateauSlopeMeters = 180f;

        public static float FlatMarginMeters(LandformProfile landforms) =>
            landforms != null
                ? Mathf.Max(0f, landforms.CityPadFlatMarginMeters)
                : DefaultFlatMarginMeters;

        public static float PlateauSlopeMeters(LandformProfile landforms) =>
            landforms != null
                ? landforms.CityPadPlateauSlopeMeters
                : DefaultPlateauSlopeMeters;

        /// <summary>
        ///     Builds pads whose plateau is arterial bounds expanded by flat margin (~30m).
        /// </summary>
        public static void BuildCityCorePads(
            WorldSitePlan sites,
            CityPrefabRoadNetworkBuilder builder,
            LandformField landforms,
            LandformProfile landformProfile,
            float pruneMargin,
            List<CityFlattenPad> pads)
        {
            BuildCityCorePads(
                sites,
                builder,
                landforms,
                landformProfile,
                pruneMargin,
                hydrology: null,
                hydrologySetbackMeters: 0f,
                pads: pads);
        }

        public static void BuildCityCorePads(
            WorldSitePlan sites,
            CityPrefabRoadNetworkBuilder builder,
            LandformField landforms,
            LandformProfile landformProfile,
            float pruneMargin,
            HydrologyPlan hydrology,
            float hydrologySetbackMeters,
            List<CityFlattenPad> pads)
        {
            pads.Clear();
            if (sites?.CitySites == null || sites.CitySites.Count == 0)
                return;

            var margin = FlatMarginMeters(landformProfile);
            var slopeReach = PlateauSlopeMeters(landformProfile);
            var template = builder != null ? builder.Layout : null;
            var regionSeed = builder != null ? builder.ResolveRegionSeed() : 12345;

            for (var i = 0; i < sites.CitySites.Count; i++)
            {
                var site = sites.CitySites[i];
                if (site == null)
                    continue;

                if (!TryResolveArterialCorePlateau(template, site, regionSeed, i, margin, out var plateau))
                    continue;
                if (OverlapsHydrologySetback(
                        landforms,
                        hydrology,
                        plateau,
                        hydrologySetbackMeters))
                    continue;

                var padY = landforms != null
                    ? LandformFieldSampling.SampleBilinear(landforms, site.CenterXZ.x, site.CenterXZ.y)
                    : site.PadHeightWorldY;

                var pad = new CityFlattenPad(plateau, padY, falloffMeters: slopeReach)
                {
                    HydrologyPruneMarginMeters = pruneMargin,
                    IsCoastal = site.IsCoastal,
                    SeawardNormalXZ = site.SeawardNormalXZ,
                    CoastExposure01 = site.CoastExposure01,
                    FalloffMetersSeaward = slopeReach
                };
                if (site.HasHighwayEntry)
                {
                    pad.HasHighwayEntry = true;
                    pad.HighwayEntryXZ = site.HighwayEntryXZ;
                    pad.HighwayEntryHeightWorldY = site.HighwayEntryHeightWorldY;
                }

                pads.Add(pad);
            }
        }

        private static bool OverlapsHydrologySetback(
            LandformField field,
            HydrologyPlan hydrology,
            Rect plateau,
            float setbackMeters)
        {
            if (field == null || hydrology == null || setbackMeters <= 0f)
                return false;
            if (hydrology.Width != field.Width || hydrology.Height != field.Height)
                return false;

            var expanded = Rect.MinMaxRect(
                plateau.xMin - setbackMeters,
                plateau.yMin - setbackMeters,
                plateau.xMax + setbackMeters,
                plateau.yMax + setbackMeters);
            var x0 = Mathf.Max(0, Mathf.FloorToInt((expanded.xMin - field.OriginXZ.x) / field.CellSize));
            var x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((expanded.xMax - field.OriginXZ.x) / field.CellSize));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((expanded.yMin - field.OriginXZ.y) / field.CellSize));
            var z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((expanded.yMax - field.OriginXZ.y) / field.CellSize));
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var water = hydrology.WaterClass[hydrology.Index(x, z)];
                    if (water == WorldWaterClass.River || water == WorldWaterClass.Lake)
                        return true;
                }
            }

            return false;
        }

        public static void BuildArterialCorePads(
            WorldSitePlan sites,
            CityPrefabRoadNetworkBuilder builder,
            LandformField landforms,
            LandformProfile landformProfile,
            float pruneMargin,
            List<CityFlattenPad> pads) =>
            BuildCityCorePads(sites, builder, landforms, landformProfile, pruneMargin, pads);

        public static bool TryResolveArterialCorePlateau(
            CityMathRoadLayout template,
            WorldCitySite site,
            int regionSeed,
            int siteIndex,
            float flatMarginMeters,
            out Rect plateau)
        {
            plateau = default;
            if (site == null)
                return false;

            if (template != null)
            {
                var hub = new CityHubSite
                {
                    centerXZ = site.CenterXZ,
                    halfWidthMeters = site.HalfWidthMeters,
                    halfDepthMeters = site.HalfDepthMeters,
                    layoutSeed = site.LayoutSeed,
                    generateArterialRing = true,
                    generateStreetGrid = true,
                    generateHighwayExits = true
                };
                var seed = CityRegionSiteLayoutUtility.ResolveSiteSeed(hub, regionSeed, siteIndex);
                if (CityRegionSiteLayoutUtility.TryResolveArterialPadBounds(
                        template, hub, seed, flatMarginMeters,
                        out _, out var outer) &&
                    outer.width > 0f && outer.height > 0f)
                {
                    plateau = outer;
                    return true;
                }
            }

            var halfW = Mathf.Max(40f, site.HalfWidthMeters * 0.55f);
            var halfD = Mathf.Max(40f, site.HalfDepthMeters * 0.55f);
            var m = Mathf.Max(0f, flatMarginMeters);
            plateau = Rect.MinMaxRect(
                site.CenterXZ.x - halfW - m,
                site.CenterXZ.y - halfD - m,
                site.CenterXZ.x + halfW + m,
                site.CenterXZ.y + halfD + m);
            return plateau.width > 0f && plateau.height > 0f;
        }

        public static bool TryResolveCorePlateau(
            CityMathRoadLayout template,
            WorldCitySite site,
            int regionSeed,
            int siteIndex,
            float flatMarginMeters,
            out Rect plateau) =>
            TryResolveArterialCorePlateau(template, site, regionSeed, siteIndex, flatMarginMeters, out plateau);

        public static bool TryResolveCorePlateau(
            WorldCitySite site,
            float flatMarginMeters,
            out Rect plateau) =>
            TryResolveArterialCorePlateau(null, site, 0, 0, flatMarginMeters, out plateau);

        public static void StampCores(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            LandformProfile landforms,
            HydrologyPlan hydrology,
            float seaLevelWorldY)
        {
            if (field?.WorldHeights == null || pads == null || pads.Count == 0 || landforms == null)
                return;

            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(landforms);
            var minPadY = CityPadReclaimPolicy.MinPadHeightWorldY(seaLevelWorldY, landforms);

            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                ResolveCoreHeight(field, pad, hydrology, maxReclaimDepth, minPadY);
                WritePlateau(field, pad, hydrology, maxReclaimDepth);
            }
        }

        /// <summary>
        ///     Flat arterial plateau + noisy escarpment + talus. Not a smooth mega-apron.
        /// </summary>
        public static void StampCoresWithBlend(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            LandformProfile landforms,
            RoadNetworkSettings roads,
            HydrologyPlan hydrology,
            float seaLevelWorldY)
        {
            _ = roads;
            StampCores(field, pads, landforms, hydrology, seaLevelWorldY);
            WriteNoisyPlateauSlopes(field, pads, landforms, hydrology);
            RelaxPadEdges(field, landforms, pads);
        }

        public static void RelaxPadEdges(
            LandformField field,
            LandformProfile landforms,
            IReadOnlyList<CityFlattenPad> pads)
        {
            if (field == null || landforms == null || pads == null || pads.Count == 0)
                return;
            if (!landforms.CityPadEdgeRelaxEnabled)
                return;

            ThermalErosionSolver.ApplyMaskedForPads(
                field,
                landforms,
                pads,
                landforms.CityPadApproachSlopeDegrees);
        }

        public static void MergeIntoExclusionMask(
            LandformField field,
            bool[] exclusionMask,
            IReadOnlyList<CityFlattenPad> pads)
        {
            if (field == null || exclusionMask == null || pads == null || pads.Count == 0)
                return;
            if (exclusionMask.Length != field.WorldHeights.Length)
                return;

            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                var plateau = pad.PlateauBoundsXZ;
                WorldToCellRect(field, plateau, out var x0, out var x1, out var z0, out var z1);
                for (var z = z0; z <= z1; z++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var center = field.CellCenterXZ(x, z);
                        if (center.x < plateau.xMin || center.x > plateau.xMax ||
                            center.y < plateau.yMin || center.y > plateau.yMax)
                            continue;
                        exclusionMask[field.Index(x, z)] = true;
                    }
                }
            }
        }

        private static void ResolveCoreHeight(
            LandformField field,
            CityFlattenPad pad,
            HydrologyPlan hydrology,
            float maxReclaimDepth,
            float minPadY)
        {
            var samples = new List<float>(128);
            var plateau = pad.PlateauBoundsXZ;
            WorldToCellRect(field, plateau, out var x0, out var x1, out var z0, out var z1);
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (center.x < plateau.xMin || center.x > plateau.xMax ||
                        center.y < plateau.yMin || center.y > plateau.yMax)
                        continue;
                    if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(hydrology, x, z, maxReclaimDepth))
                        continue;
                    samples.Add(field.WorldHeights[field.Index(x, z)]);
                }
            }

            if (samples.Count == 0)
            {
                pad.TargetHeightWorldY = Mathf.Max(pad.TargetHeightWorldY, minPadY);
                return;
            }

            samples.Sort();
            pad.TargetHeightWorldY = Mathf.Max(samples[samples.Count / 2], minPadY);
        }

        private static void WritePlateau(
            LandformField field,
            CityFlattenPad pad,
            HydrologyPlan hydrology,
            float maxReclaimDepth)
        {
            var plateau = pad.PlateauBoundsXZ;
            WorldToCellRect(field, plateau, out var x0, out var x1, out var z0, out var z1);
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (center.x < plateau.xMin || center.x > plateau.xMax ||
                        center.y < plateau.yMin || center.y > plateau.yMax)
                        continue;
                    if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(hydrology, x, z, maxReclaimDepth))
                        continue;
                    field.WorldHeights[field.Index(x, z)] = pad.TargetHeightWorldY;
                }
            }
        }

        internal static void WorldToCellRect(
            LandformField field,
            Rect bounds,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - field.OriginXZ.x) / field.CellSize));
            x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((bounds.xMax - field.OriginXZ.x) / field.CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - field.OriginXZ.y) / field.CellSize));
            z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((bounds.yMax - field.OriginXZ.y) / field.CellSize));
        }
    }
}
