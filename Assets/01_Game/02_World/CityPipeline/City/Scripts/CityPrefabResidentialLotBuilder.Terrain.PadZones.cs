#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Lot terrain sub-zone subdivision and mesh creation.
    ///     Replaces the single Lot_XX fill quad with multiple sub-zone
    ///     GameObjects (driveway, front yard, backyard, etc.).
    /// </summary>
    internal static partial class CityPrefabResidentialLotBuilder
    {
        private static void AddDriveway(in SubZoneContext ctx, in BuildingPadRect pad, LotBuildingAnchor? building, List<LotSubZone> zones, ref int heightOrder)
        {
            if (!ctx.Layout.generateDriveway || ctx.DriveWidth <= 0.5f || ctx.DriveDepth <= 0.5f)
                return;

            // Door-aware driveway: run from the street edge to the placed building's
            // door, offset beside the door footpath (not centred under it).
            // Corner lots keep the secondary-street geometric driveway.
            if (!ctx.IsCorner && building.HasValue && building.Value.HasDoor)
            {
                AddDoorAnchoredDriveway(in ctx, in pad, building.Value.DoorXZ, zones, ref heightOrder);
                return;
            }

            if (ctx.IsCorner)
            {
                // Driveway on the secondary street edge. Mapped with the secondary
                // face's own orientation (see AddSecondaryFrontYard).
                EdgeMappingForFace(ctx.SecondFace, ctx.LotRect, out var dvNear, out var dvFar, out var dvLeft, out var dvRight);
                var dvSign = dvFar > dvNear ? 1f : -1f;
                var dvStart = dvNear;
                var dvEnd = dvNear + dvSign * ctx.DriveDepth;
                var dvWidth = Mathf.Min(ctx.DriveWidth, Mathf.Abs(dvRight - dvLeft) - ctx.SideWidth * 2f - 0.5f);
                var dvOffset = dvLeft + ctx.Layout.drivewaySideOffsetMeters;
                if (dvOffset < dvLeft + ctx.SideWidth) dvOffset = dvLeft + ctx.SideWidth;
                if (dvOffset + dvWidth > dvRight - ctx.SideWidth) dvOffset = dvRight - ctx.SideWidth - dvWidth;
                if (dvWidth > 0.5f)
                {
                    zones.Add(MakeSubZone(ctx.Layout.drivewayTextureIndex,
                        BuildRectForFace(IsHorizontalFace(ctx.SecondFace),
                            dvStart, dvEnd, dvOffset, dvOffset + dvWidth), ref heightOrder, "Driveway"));
                }
                return;
            }

            var dvFarEdge = ctx.Near + ctx.SignDeep * ctx.DriveDepth;
            zones.Add(MakeSubZone(ctx.Layout.drivewayTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet,
                    ctx.Near, dvFarEdge, ctx.DriveLeft, ctx.DriveLeft + ctx.DriveWidth), ref heightOrder, "Driveway"));
        }

        private static void AddSideYards(in SubZoneContext ctx, in BuildingPadRect pad, List<LotSubZone> zones, ref int heightOrder)
        {
            if (!ctx.SideYardsActive)
                return;

            if (ctx.IsCorner)
            {
                AddCornerSideYards(in ctx, zones, ref heightOrder);
                return;
            }

            if (pad.Absolute)
            {
                AddAbsoluteSideYards(in ctx, in pad, zones, ref heightOrder);
                return;
            }

            var yardStart = ctx.Near + ctx.SignDeep * ctx.FrontDepth;
            if (Mathf.Abs(ctx.Far - yardStart) <= 1f)
                return;

            zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet,
                    yardStart, ctx.Far, ctx.Left, ctx.Left + ctx.SideWidth), ref heightOrder, "SideYard_L"));
            zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet,
                    yardStart, ctx.Far, ctx.Right - ctx.SideWidth, ctx.Right), ref heightOrder, "SideYard_R"));
        }

        private static void AddCornerSideYards(in SubZoneContext ctx, List<LotSubZone> zones, ref int heightOrder)
        {
            // Corner lots only place side yards on edges that are NOT street-facing.
            var sharedSouth = !ctx.TouchesSouth;
            var sharedNorth = !ctx.TouchesNorth;
            var sharedWest = !ctx.TouchesWest;
            var sharedEast = !ctx.TouchesEast;

            if (sharedWest)
            {
                zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                    Rect.MinMaxRect(ctx.LotRect.xMin, ctx.LotRect.yMin + ctx.FrontDepth,
                        ctx.LotRect.xMin + ctx.SideWidth, ctx.LotRect.yMax), ref heightOrder, "SideYard_W"));
            }
            if (sharedEast)
            {
                zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                    Rect.MinMaxRect(ctx.LotRect.xMax - ctx.SideWidth, ctx.LotRect.yMin + ctx.FrontDepth,
                        ctx.LotRect.xMax, ctx.LotRect.yMax), ref heightOrder, "SideYard_E"));
            }
            if (sharedSouth)
            {
                zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                    Rect.MinMaxRect(ctx.LotRect.xMin + (sharedWest ? ctx.SideWidth : 0f),
                        ctx.LotRect.yMin, ctx.LotRect.xMax - (sharedEast ? ctx.SideWidth : 0f),
                        ctx.LotRect.yMin + ctx.SideWidth), ref heightOrder, "SideYard_S"));
            }
            if (sharedNorth)
            {
                zones.Add(MakeSubZone(ctx.Layout.sideYardTextureIndex,
                    Rect.MinMaxRect(ctx.LotRect.xMin + (sharedWest ? ctx.SideWidth : 0f),
                        ctx.LotRect.yMax - ctx.SideWidth, ctx.LotRect.xMax - (sharedEast ? ctx.SideWidth : 0f),
                        ctx.LotRect.yMax), ref heightOrder, "SideYard_N"));
            }
        }

        private readonly struct BuildingPadRect
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Near;
            public readonly float Far;

            /// <summary>True when Left/Right/Near/Far are absolute XZ rect bounds (building-anchored or corner pads).</summary>
            public readonly bool Absolute;

            public BuildingPadRect(float left, float right, float near, float far, bool absolute = false)
            {
                Left = left;
                Right = right;
                Near = near;
                Far = far;
                Absolute = absolute;
            }
        }

        private static BuildingPadRect ComputeBuildingPad(in SubZoneContext ctx, LotBuildingAnchor? building)
        {
            if (building.HasValue && building.Value.HasFootprint)
            {
                // Door-aware: the pad is the placed building's exact measured
                // footprint, clamped inside the lot. No apron — the slab must
                // not bleed past the building.
                var f = building.Value.FootprintXZ;
                return new BuildingPadRect(
                    Mathf.Max(ctx.LotRect.xMin, f.xMin),
                    Mathf.Min(ctx.LotRect.xMax, f.xMax),
                    Mathf.Max(ctx.LotRect.yMin, f.yMin),
                    Mathf.Min(ctx.LotRect.yMax, f.yMax),
                    absolute: true);
            }

            if (ctx.IsCorner)
            {
                // Corner lot: building pad fills the interior after front + side yards.
                var insetLeft = ctx.TouchesWest ? ctx.FrontDepth : ctx.SideWidth;
                var insetRight = ctx.TouchesEast ? ctx.FrontDepth : ctx.SideWidth;
                var insetBottom = ctx.TouchesSouth ? ctx.FrontDepth : ctx.SideWidth;
                var insetTop = ctx.TouchesNorth ? ctx.FrontDepth : ctx.SideWidth;
                return new BuildingPadRect(
                    ctx.LotRect.xMin + insetLeft, ctx.LotRect.xMax - insetRight,
                    ctx.LotRect.yMin + insetBottom, ctx.LotRect.yMax - insetTop,
                    absolute: true);
            }

            var left = ctx.SideYardsActive ? ctx.Left + ctx.SideWidth : ctx.Left;
            var right = ctx.SideYardsActive ? ctx.Right - ctx.SideWidth : ctx.Right;
            var streetDepth = ctx.FrontYardActive ? ctx.FrontDepth : 0f;
            var near = ctx.Near + ctx.SignDeep * (streetDepth + (ctx.Layout.generateDriveway ? ctx.DriveDepth : 0f));
            // Backyard reserve — generous so lots keep a real rear yard, with a
            // 2 m floor so back-to-back pads never share a border.
            var remaining = Mathf.Abs(ctx.Far - near);
            var backyardDepth = remaining >= 3f
                ? Mathf.Clamp(Mathf.Max(remaining * 0.3f, 2f), 0f, 8f)
                : 0f;
            var far = ctx.Far - ctx.SignDeep * (backyardDepth >= 2f ? backyardDepth : 0f);
            return new BuildingPadRect(left, right, near, far);
        }

        private static void AddBuildingPad(
            in SubZoneContext ctx, in BuildingPadRect pad, LotBuildingAnchor? building,
            List<LotSubZone> zones, ref int heightOrder)
        {
            // Geometric / corner-fill pads cover most of the lot and read as
            // oversized slabs beside smaller houses. Only stamp when we have the
            // placed building's measured footprint.
            if (!building.HasValue || !building.Value.HasFootprint)
                return;

            var padW = Mathf.Abs(pad.Right - pad.Left);
            var padH = Mathf.Abs(pad.Far - pad.Near);
            // Building-anchored pads use exact measured footprints — small houses
            // (3 m wide) must still get a slab, so the floor is 2 m, not 4 m.
            if (!ctx.Layout.generateBuildingPad || padW <= 2f || padH <= 2f)
                return;

            var padRect = Rect.MinMaxRect(
                pad.Left, Mathf.Min(pad.Near, pad.Far),
                pad.Right, Mathf.Max(pad.Near, pad.Far));
            zones.Add(MakeSubZone(ctx.Layout.buildingPadTextureIndex, padRect, ref heightOrder, "BuildingPad"));
        }

        private static void AddBackyard(in SubZoneContext ctx, in BuildingPadRect pad, List<LotSubZone> zones, ref int heightOrder)
        {
            // Building-anchored pads: rear yard fills from the pad's rear edge to
            // the lot rear across the full width so it matches the front-yard grass.
            if (pad.Absolute && !ctx.IsCorner && ctx.Layout.generateBuildingPad)
            {
                var rearEdge = ResolveAbsolutePadRearEdge(in ctx, in pad);
                var backyardMeters = Mathf.Abs(ctx.Far - rearEdge);
                if (backyardMeters >= 1f)
                {
                    zones.Add(MakeSubZone(ctx.Layout.backyardTextureIndex,
                        BuildRectForFace(ctx.IsHorizontalStreet, rearEdge, ctx.Far, ctx.Left, ctx.Right),
                        ref heightOrder, "Backyard"));
                }
                return;
            }

            // Corner lots: the "rear" is a secondary front yard, not a backyard —
            // but only when a building pad fills the interior. Without a pad the
            // interior would stay unpainted, so fall through to the full-rear path.
            if (ctx.IsCorner && ctx.Layout.generateBuildingPad)
                return;

            if (ctx.Layout.generateBuildingPad)
            {
                // Skip when it would be a sliver — absorbs into the building pad.
                var backyardM = Mathf.Abs(ctx.Far - pad.Far);
                if (backyardM < 2f)
                    return;

                zones.Add(MakeSubZone(ctx.Layout.backyardTextureIndex,
                    BuildRectForFace(ctx.IsHorizontalStreet,
                        pad.Far, ctx.Far, ctx.Left, ctx.Right), ref heightOrder, "Backyard"));
                return;
            }

            // No building pad — the entire rear area is backyard.
            var byNear = ctx.Near + ctx.SignDeep * ctx.FrontDepth;
            if (Mathf.Abs(ctx.Far - byNear) > 0.5f)
            {
                zones.Add(MakeSubZone(ctx.Layout.backyardTextureIndex,
                    BuildRectForFace(ctx.IsHorizontalStreet,
                        byNear, ctx.Far, ctx.Left, ctx.Right), ref heightOrder, "Backyard"));
            }
        }

        private static void AddFootpath(in SubZoneContext ctx, in BuildingPadRect pad,
            LotBuildingAnchor? building, List<LotSubZone> zones, ref int heightOrder)
        {
            if (!IsDoorFootpathActive(in ctx, in pad))
                return;

            var pathCenter = ResolveFootpathCenter(in ctx, in pad, building);
            var pathHalf = ctx.Layout.footpathWidthMeters * 0.5f;
            pathCenter = Mathf.Clamp(pathCenter, ctx.Left + pathHalf, ctx.Right - pathHalf);

            // Resolve the pad's near edge along the street's DEEP axis. Non-corner
            // pads store Near/Far along the deep axis with Near on the STREET side
            // — use it directly, otherwise the path runs through the slab. Corner
            // and building-anchored pads are absolute X/Z rects, and which edge
            // faces the street depends on the face (North lots meet the street at
            // zMax, East lots at xMax), so pick the edge nearest ctx.Near.
            var padDeepNear = ctx.IsCorner || pad.Absolute
                ? ResolveCornerPadStreetEdge(in ctx, in pad)
                : pad.Near;

            zones.Add(MakeSubZone(ctx.Layout.footpathTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet,
                    ctx.Near, padDeepNear, pathCenter - pathHalf, pathCenter + pathHalf), ref heightOrder, "Footpath"));
        }

        /// <summary>
        ///     The corner-lot pad edge nearest the street along the deep axis.
        ///     Corner pads are absolute X/Z rects, so the street-side edge depends
        ///     on the face — a South lot's pad near edge is pad.Near (zMin side),
        ///     but a North lot's is pad.Far (zMax side).
        /// </summary>
        private static float ResolveCornerPadStreetEdge(in SubZoneContext ctx, in BuildingPadRect pad)
        {
            if (ctx.IsHorizontalStreet)
            {
                var toNear = Mathf.Abs(pad.Near - ctx.Near);
                var toFar = Mathf.Abs(pad.Far - ctx.Near);
                return toNear <= toFar ? pad.Near : pad.Far;
            }

            var toLeft = Mathf.Abs(pad.Left - ctx.Near);
            var toRight = Mathf.Abs(pad.Right - ctx.Near);
            return toLeft <= toRight ? pad.Left : pad.Right;
        }

    }
}
#endif
