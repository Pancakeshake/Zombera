#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Grass-aware tree regions: restricts the tree scatter to each lot's
    ///     grass yard sub-zones so trees stay off painted driveways, footpaths
    ///     and building slabs.
    /// </summary>
    internal static partial class CityDistrictLotDecorationScatter
    {
        // MicroSplat world grass layers (GrassGreen, GrassYellow).
        private const int GrassLayerGreen = 0;
        private const int GrassLayerYellow = 1;

        private const float MinGrassRegionSpanMeters = 1.5f;

        /// <summary>
        ///     Builds tree-scatter regions from the lot's grass yard sub-zones.
        ///     Returns null when no terrain layout is configured (caller falls
        ///     back to the legacy yard regions). An empty list means the lot has
        ///     no grass surface and trees should be skipped entirely.
        /// </summary>
        public static List<YardScatterRegion> BuildGrassTreeRegions(
            ScatterRequest request, Rect lotRect, BlockFace streetFace, Transform placedBuildings)
        {
            if (request.TerrainLayout == null
                || request.Catalog.Trees == null
                || request.Catalog.Trees.Count == 0)
                return null;

            var layout = request.TerrainLayout.GetLayout(request.Marker.DistrictType);
            var anchor = FindBuildingAnchorForLot(placedBuildings, lotRect);
            var subZones = CityPrefabResidentialLotBuilder.SubdivideLotIntoSections(
                lotRect, streetFace, request.Marker.GetHubShiftedBoundsXZ(), layout, anchor);
            if (subZones.Count == 0)
                return null;

            var results = new List<YardScatterRegion>(4);
            for (var i = 0; i < subZones.Count; i++)
            {
                var zone = subZones[i];
                if (!IsGrassYardZone(zone))
                    continue;

                var inset = InsetRect(zone.Bounds, request.Settings.lotEdgeMarginMeters);
                if (inset.width < MinGrassRegionSpanMeters || inset.height < MinGrassRegionSpanMeters)
                    continue;

                var kind = zone.DisplayName.IndexOf("Front", StringComparison.Ordinal) >= 0
                    ? YardRegionKind.FrontYard
                    : YardRegionKind.SideOrBack;
                results.Add(new YardScatterRegion(inset, kind));
            }

            return results;
        }

        private static bool IsGrassYardZone(in LotSubZone zone)
        {
            if (zone.DisplayName.IndexOf("Yard", StringComparison.Ordinal) < 0)
                return false;

            return zone.TextureLayerIndex == GrassLayerGreen
                   || zone.TextureLayerIndex == GrassLayerYellow;
        }

        private static Rect InsetRect(Rect rect, float marginMeters)
        {
            if (marginMeters <= 0f)
                return rect;

            return Rect.MinMaxRect(
                rect.xMin + marginMeters,
                rect.yMin + marginMeters,
                rect.xMax - marginMeters,
                rect.yMax - marginMeters);
        }

        private static CityPrefabResidentialLotBuilder.LotBuildingAnchor? FindBuildingAnchorForLot(
            Transform placedBuildingsRoot, Rect lotRect)
        {
            if (placedBuildingsRoot == null)
                return null;

            var bestOverlap = 0f;
            CityBuildingStreetFacingMarker bestMarker = null;
            Rect bestFootprint = default;

            for (var i = 0; i < placedBuildingsRoot.childCount; i++)
            {
                var marker = placedBuildingsRoot.GetChild(i).GetComponent<CityBuildingStreetFacingMarker>();
                if (marker == null
                    || marker.footprintXZ.width < 0.5f || marker.footprintXZ.height < 0.5f)
                    continue;

                var footprint = marker.footprintXZ;
                var overlap = OverlapAreaXZ(lotRect, footprint);
                if (overlap <= bestOverlap)
                    continue;

                bestOverlap = overlap;
                bestMarker = marker;
                bestFootprint = footprint;
            }

            if (bestMarker == null)
                return null;

            var minOverlap = Mathf.Min(4f, bestFootprint.width * bestFootprint.height * 0.2f);
            if (bestOverlap < minOverlap)
                return null;

            return new CityPrefabResidentialLotBuilder.LotBuildingAnchor(
                bestFootprint,
                new Vector2(bestMarker.doorAnchorWorld.x, bestMarker.doorAnchorWorld.z),
                bestMarker.hasDoorAnchor);
        }

        private static float OverlapAreaXZ(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            var w = xMax - xMin;
            var h = yMax - yMin;
            if (w <= 0f || h <= 0f)
                return 0f;
            return w * h;
        }
    }
}
#endif
