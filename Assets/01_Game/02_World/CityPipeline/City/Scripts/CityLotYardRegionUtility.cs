using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public enum YardRegionKind
    {
        FrontYard = 0,
        SideOrBack = 1,
        FullLot = 2
    }

    public readonly struct YardScatterRegion
    {
        public readonly Rect Bounds;
        public readonly YardRegionKind Kind;

        public YardScatterRegion(Rect bounds, YardRegionKind kind)
        {
            Bounds = bounds;
            Kind = kind;
        }
    }

    /// <summary>
    ///     Derives axis-aligned scatter zones inside a lot, excluding building footprint.
    /// </summary>
    public static class CityLotYardRegionUtility
    {
        private const float MinRegionSpanMeters = 1.5f;

        public static void BuildScatterRegions(
            Rect lotRect,
            Rect? buildingBounds,
            BlockFace streetFace,
            float frontYardDepthMeters,
            float buildingClearanceMeters,
            float lotEdgeMarginMeters,
            List<YardScatterRegion> results)
        {
            results.Clear();

            var lot = InsetRect(lotRect, lotEdgeMarginMeters);
            if (lot.width < MinRegionSpanMeters || lot.height < MinRegionSpanMeters)
                return;

            var hasBuilding = buildingBounds.HasValue && buildingBounds.Value.width > 0.01f;
            if (!hasBuilding)
            {
                results.Add(new YardScatterRegion(lot, YardRegionKind.FullLot));
                return;
            }

            var building = InflateRect(buildingBounds.Value, buildingClearanceMeters);
            var front = BuildFrontStrip(lot, streetFace, frontYardDepthMeters);
            if (IsValidRegion(front))
                results.Add(new YardScatterRegion(front, YardRegionKind.FrontYard));

            AppendSideAndBackRegions(lot, building, front, streetFace, results);
        }

        private static void AppendSideAndBackRegions(
            Rect lot,
            Rect building,
            Rect frontStrip,
            BlockFace streetFace,
            List<YardScatterRegion> results)
        {
            switch (streetFace)
            {
                case BlockFace.South:
                    TryAddRegion(results, Rect.MinMaxRect(lot.xMin, building.yMax, lot.xMax, lot.yMax), YardRegionKind.SideOrBack);
                    TryAddSideBand(results, lot, building, frontStrip, vertical: true);
                    break;
                case BlockFace.North:
                    TryAddRegion(results, Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMax, building.yMin), YardRegionKind.SideOrBack);
                    TryAddSideBand(results, lot, building, frontStrip, vertical: true);
                    break;
                case BlockFace.West:
                    TryAddRegion(results, Rect.MinMaxRect(building.xMax, lot.yMin, lot.xMax, lot.yMax), YardRegionKind.SideOrBack);
                    TryAddSideBand(results, lot, building, frontStrip, vertical: false);
                    break;
                default:
                    TryAddRegion(results, Rect.MinMaxRect(lot.xMin, lot.yMin, building.xMin, lot.yMax), YardRegionKind.SideOrBack);
                    TryAddSideBand(results, lot, building, frontStrip, vertical: false);
                    break;
            }
        }

        private static void TryAddSideBand(
            List<YardScatterRegion> results,
            Rect lot,
            Rect building,
            Rect frontStrip,
            bool vertical)
        {
            if (vertical)
            {
                var zMin = Mathf.Max(lot.yMin, frontStrip.yMax);
                var zMax = Mathf.Min(lot.yMax, building.yMax);
                TryAddRegion(results, Rect.MinMaxRect(lot.xMin, zMin, building.xMin, zMax), YardRegionKind.SideOrBack);
                TryAddRegion(results, Rect.MinMaxRect(building.xMax, zMin, lot.xMax, zMax), YardRegionKind.SideOrBack);
            }
            else
            {
                var xMin = Mathf.Max(lot.xMin, frontStrip.xMax);
                var xMax = Mathf.Min(lot.xMax, building.xMax);
                TryAddRegion(results, Rect.MinMaxRect(xMin, lot.yMin, xMax, building.yMin), YardRegionKind.SideOrBack);
                TryAddRegion(results, Rect.MinMaxRect(xMin, building.yMax, xMax, lot.yMax), YardRegionKind.SideOrBack);
            }
        }

        private static Rect BuildFrontStrip(Rect lot, BlockFace streetFace, float depthMeters)
        {
            depthMeters = Mathf.Clamp(depthMeters, 0.5f, Mathf.Min(lot.width, lot.height) * 0.45f);
            return streetFace switch
            {
                BlockFace.South => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMax, lot.yMin + depthMeters),
                BlockFace.North => Rect.MinMaxRect(lot.xMin, lot.yMax - depthMeters, lot.xMax, lot.yMax),
                BlockFace.West => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMin + depthMeters, lot.yMax),
                _ => Rect.MinMaxRect(lot.xMax - depthMeters, lot.yMin, lot.xMax, lot.yMax)
            };
        }

        private static void TryAddRegion(List<YardScatterRegion> results, Rect rect, YardRegionKind kind)
        {
            if (!IsValidRegion(rect))
                return;

            results.Add(new YardScatterRegion(rect, kind));
        }

        private static bool IsValidRegion(Rect rect) =>
            rect.width >= MinRegionSpanMeters && rect.height >= MinRegionSpanMeters;

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

        private static Rect InflateRect(Rect rect, float marginMeters)
        {
            if (marginMeters <= 0f)
                return rect;

            return Rect.MinMaxRect(
                rect.xMin - marginMeters,
                rect.yMin - marginMeters,
                rect.xMax + marginMeters,
                rect.yMax + marginMeters);
        }
    }
}
