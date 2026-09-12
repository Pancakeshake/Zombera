#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Door-aware terrain zone helpers: when a building has been placed on a lot,
    ///     the painted slab matches the building footprint and the driveway / footpath
    ///     anchor to its real street-facing door.
    /// </summary>
    internal static partial class CityPrefabResidentialLotBuilder
    {
        /// <summary>
        ///     Placed-building info consumed by <see cref="SubdivideLotIntoSections" />.
        ///     Absent for lots without a fitted building (geometric fallback).
        /// </summary>
        public readonly struct LotBuildingAnchor
        {
            public readonly Rect FootprintXZ;
            public readonly Vector2 DoorXZ;
            public readonly bool HasFootprint;
            public readonly bool HasDoor;

            public LotBuildingAnchor(Rect footprintXZ, Vector2 doorXZ, bool hasDoor)
            {
                FootprintXZ = footprintXZ;
                DoorXZ = doorXZ;
                HasDoor = hasDoor;
                HasFootprint = footprintXZ.width > 0.5f && footprintXZ.height > 0.5f;
            }
        }

        private static void AddDoorAnchoredDriveway(in SubZoneContext ctx, in BuildingPadRect pad, Vector2 doorXZ,
            List<LotSubZone> zones, ref int heightOrder)
        {
            var across = ctx.IsHorizontalStreet ? doorXZ.x : doorXZ.y;
            var deep = ctx.IsHorizontalStreet ? doorXZ.y : doorXZ.x;

            var driveWidth = Mathf.Min(ctx.DriveWidth, Mathf.Abs(ctx.Right - ctx.Left));
            var driveEnd = Mathf.Clamp(deep, Mathf.Min(ctx.Near, ctx.Far), Mathf.Max(ctx.Near, ctx.Far));

            // Sit the driveway beside the door footpath instead of centred under
            // it — same street→door direction, laterally offset by the path
            // half-width (plus a thin grass gap). Prefer the right side from the
            // street, fall back to the left, then clamp inside the lot.
            const float gapMeters = 0.4f;
            var pathHalf = IsDoorFootpathActive(in ctx, in pad)
                ? ctx.Layout.footpathWidthMeters * 0.5f + gapMeters
                : 0f;
            var driveLeft = across + pathHalf;
            if (driveLeft + driveWidth > ctx.Right)
                driveLeft = across - pathHalf - driveWidth;
            driveLeft = Mathf.Clamp(driveLeft, ctx.Left, ctx.Right - driveWidth);

            zones.Add(MakeSubZone(ctx.Layout.drivewayTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet, ctx.Near, driveEnd, driveLeft, driveLeft + driveWidth),
                ref heightOrder, "Driveway"));
        }

        /// <summary>
        ///     Mirrors the footpath's own gating so the driveway offset only
        ///     applies when a door footpath is actually generated.
        /// </summary>
        private static bool IsDoorFootpathActive(in SubZoneContext ctx, in BuildingPadRect pad)
        {
            if (!ctx.Layout.generateDoorFootpath || !ctx.Layout.generateBuildingPad ||
                ctx.Layout.footpathWidthMeters <= 0.4f)
                return false;

            var padAcross = pad.Absolute
                ? (ctx.IsHorizontalStreet ? Mathf.Abs(pad.Right - pad.Left) : Mathf.Abs(pad.Far - pad.Near))
                : Mathf.Abs(pad.Right - pad.Left);

            return padAcross > ctx.Layout.footpathWidthMeters + 0.5f;
        }

        /// <summary>
        ///     Side yards for building-anchored pads: the grass bands between the lot
        ///     edges and the pad sides, spanning the depth behind the front yard.
        ///     Each band is capped so small buildings on large lots don't produce
        ///     oversized lawns (leftover shows the district base paint).
        /// </summary>
        private static void AddAbsoluteSideYards(in SubZoneContext ctx, in BuildingPadRect pad,
            List<LotSubZone> zones, ref int heightOrder)
        {
            // Fill the full flank from lot edge to pad — capping left district-green
            // gaps beside houses that didn't match the front-yard grass.
            var yardStart = ctx.Near + ctx.SignDeep * ctx.FrontDepth;
            if (Mathf.Abs(ctx.Far - yardStart) <= 1f)
                return;

            var start = Mathf.Min(yardStart, ctx.Far);
            var end = Mathf.Max(yardStart, ctx.Far);

            if (ctx.IsHorizontalStreet)
            {
                var leftWidth = pad.Left - ctx.LotRect.xMin;
                if (leftWidth >= 1f)
                    zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                        Rect.MinMaxRect(ctx.LotRect.xMin, start, ctx.LotRect.xMin + leftWidth, end), ref heightOrder, "SideYard_L"));

                var rightWidth = ctx.LotRect.xMax - pad.Right;
                if (rightWidth >= 1f)
                    zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                        Rect.MinMaxRect(ctx.LotRect.xMax - rightWidth, start, ctx.LotRect.xMax, end), ref heightOrder, "SideYard_R"));
            }
            else
            {
                var nearWidth = pad.Near - ctx.LotRect.yMin;
                if (nearWidth >= 1f)
                    zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                        Rect.MinMaxRect(start, ctx.LotRect.yMin, end, ctx.LotRect.yMin + nearWidth), ref heightOrder, "SideYard_L"));

                var farWidth = ctx.LotRect.yMax - pad.Far;
                if (farWidth >= 1f)
                    zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                        Rect.MinMaxRect(start, ctx.LotRect.yMax - farWidth, end, ctx.LotRect.yMax), ref heightOrder, "SideYard_R"));
            }
        }

        /// <summary>
        ///     The deep-axis coordinate of an absolute pad's rear edge (the edge
        ///     farthest from the street).
        /// </summary>
        private static float ResolveAbsolutePadRearEdge(in SubZoneContext ctx, in BuildingPadRect pad)
        {
            if (ctx.IsHorizontalStreet)
                return ctx.SignDeep > 0f ? Mathf.Max(pad.Near, pad.Far) : Mathf.Min(pad.Near, pad.Far);

            return ctx.SignDeep > 0f ? Mathf.Max(pad.Left, pad.Right) : Mathf.Min(pad.Left, pad.Right);
        }

        private static float ResolveFootpathCenter(in SubZoneContext ctx, in BuildingPadRect pad, LotBuildingAnchor? building)
        {
            if (building.HasValue && building.Value.HasDoor)
                return ctx.IsHorizontalStreet ? building.Value.DoorXZ.x : building.Value.DoorXZ.y;

            if (pad.Absolute)
                return ctx.IsHorizontalStreet
                    ? (pad.Left + pad.Right) * 0.5f
                    : (pad.Near + pad.Far) * 0.5f;

            return (pad.Left + pad.Right) * 0.5f;
        }
    }
}
#endif
