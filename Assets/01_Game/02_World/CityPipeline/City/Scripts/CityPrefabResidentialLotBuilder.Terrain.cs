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
        private const float SubZoneHeightStep = 0.002f;

        private const float BlockEdgeSnap = 0.05f;

        /// <summary>
        ///     Subdivides a single lot into terrain sub-zones based on its
        ///     street-facing edge(s), block bounds, and the per-district layout.
        ///     Corner lots (touching two block edges) get front yards on both streets
        ///     and a driveway on the secondary street.
        /// </summary>
        public static List<LotSubZone> SubdivideLotIntoSections(
            Rect lotRect, BlockFace streetFace, Rect blockRect, LotTerrainZoneLayout layout,
            LotBuildingAnchor? building = null)
        {
            var subZones = new List<LotSubZone>();
            if (lotRect.width < 4f || lotRect.height < 4f || layout == null)
                return subZones;

            var ctx = BuildSubZoneContext(lotRect, streetFace, blockRect, layout);
            var heightOrder = 0;

            // Door-aware mode: the painted slab is the placed building footprint.
            // Driveway / footpath / yard layout still use geometric pad math when
            // no building is present — but BuildingPad itself is only stamped for
            // a real footprint (geometric pads were oversized vs houses).
            var pad = ComputeBuildingPad(in ctx, building);

            AddPrimaryFrontYard(in ctx, subZones, ref heightOrder);
            AddSecondaryFrontYard(in ctx, subZones, ref heightOrder);
            AddDriveway(in ctx, in pad, building, subZones, ref heightOrder);
            AddSideYards(in ctx, in pad, subZones, ref heightOrder);
            AddBuildingPad(in ctx, in pad, building, subZones, ref heightOrder);
            AddBackyard(in ctx, in pad, subZones, ref heightOrder);
            AddFootpath(in ctx, in pad, building, subZones, ref heightOrder);

            return subZones;
        }

        // ── Sub-zone context + measurement helpers ──────────────────

        private struct SubZoneContext
        {
            public Rect LotRect;
            public BlockFace SecondFace;
            public bool IsCorner;
            public bool TouchesSouth;
            public bool TouchesNorth;
            public bool TouchesWest;
            public bool TouchesEast;
            public bool IsHorizontalStreet;
            public float Near;
            public float Far;
            public float Left;
            public float Right;
            public float SignDeep;
            public float FrontDepth;
            public float SideWidth;
            public float DriveWidth;
            public float DriveDepth;
            public float DriveLeft;
            public LotTerrainZoneLayout Layout;
            public bool FrontYardActive;
            public bool SideYardsActive;
        }

        private static SubZoneContext BuildSubZoneContext(
            Rect lotRect, BlockFace streetFace, Rect blockRect, LotTerrainZoneLayout layout)
        {
            ComputeEdgeTouches(lotRect, blockRect, out var south, out var north, out var west, out var east);
            var isCorner = IsCornerSubZone(south, north, west, east);
            var secondFace = isCorner ? ResolveSecondFace(streetFace, south, north, west, east) : streetFace;

            ResolveStreetFrame(streetFace, lotRect, out var isHorizontal, out var near, out var far, out var left, out var right);

            var signDeep = far > near ? 1f : -1f;
            var absSpanAcross = Mathf.Abs(right - left);
            var absSpanDeep = Mathf.Abs(far - near);
            // Cap the front yard so deep lots don't grow oversized lawns
            // (side/back yards have matching caps elsewhere).
            var frontDepth = Mathf.Min(absSpanDeep * layout.frontYardFractionOfLotDepth, 6f);
            var sideWidth = ResolveSideYardWidth(absSpanAcross, layout.sideYardFractionOfLotWidth);

            var drive = ComputeDriveway(absSpanAcross, absSpanDeep, left, right, sideWidth, layout);

            return new SubZoneContext
            {
                LotRect = lotRect,
                SecondFace = secondFace,
                IsCorner = isCorner,
                TouchesSouth = south,
                TouchesNorth = north,
                TouchesWest = west,
                TouchesEast = east,
                IsHorizontalStreet = isHorizontal,
                Near = near,
                Far = far,
                Left = left,
                Right = right,
                SignDeep = signDeep,
                FrontDepth = frontDepth,
                SideWidth = sideWidth,
                DriveWidth = drive.Width,
                DriveDepth = drive.Depth,
                DriveLeft = drive.Left,
                Layout = layout,
                FrontYardActive = frontDepth >= 1.5f,
                SideYardsActive = sideWidth >= 1.0f
            };
        }

        /// <summary>
        ///     Side-yard band width: the layout fraction of the lot width, but never
        ///     below 1.5 m on lots ≥ 8 m wide and never above 18% of the width.
        ///     Guarantees a visible grass strip between neighbouring slabs.
        /// </summary>
        private static float ResolveSideYardWidth(float spanAcross, float fraction)
        {
            var width = spanAcross * fraction;
            if (spanAcross >= 8f)
                width = Mathf.Max(width, 1.5f);
            return Mathf.Min(width, spanAcross * 0.18f);
        }

        private static void ComputeEdgeTouches(
            Rect lotRect, Rect blockRect,
            out bool south, out bool north, out bool west, out bool east)
        {
            south = Mathf.Abs(lotRect.yMin - blockRect.yMin) < BlockEdgeSnap;
            north = Mathf.Abs(lotRect.yMax - blockRect.yMax) < BlockEdgeSnap;
            west = Mathf.Abs(lotRect.xMin - blockRect.xMin) < BlockEdgeSnap;
            east = Mathf.Abs(lotRect.xMax - blockRect.xMax) < BlockEdgeSnap;
        }

        private static bool IsCornerSubZone(bool south, bool north, bool west, bool east) =>
            (south ? 1 : 0) + (north ? 1 : 0) + (west ? 1 : 0) + (east ? 1 : 0) >= 2;

        private static BlockFace ResolveSecondFace(
            BlockFace streetFace, bool south, bool north, bool west, bool east)
        {
            switch (streetFace)
            {
                case BlockFace.South:
                    return PickSecondFace(west, BlockFace.West, east, BlockFace.East, north, BlockFace.North, streetFace);
                case BlockFace.North:
                    return PickSecondFace(east, BlockFace.East, west, BlockFace.West, south, BlockFace.South, streetFace);
                case BlockFace.West:
                    return PickSecondFace(north, BlockFace.North, south, BlockFace.South, east, BlockFace.East, streetFace);
                default:
                    return PickSecondFace(south, BlockFace.South, north, BlockFace.North, west, BlockFace.West, streetFace);
            }
        }

        private static BlockFace PickSecondFace(
            bool firstTouch, BlockFace firstFace,
            bool secondTouch, BlockFace secondFace,
            bool thirdTouch, BlockFace thirdFace,
            BlockFace fallback)
        {
            if (firstTouch) return firstFace;
            if (secondTouch) return secondFace;
            if (thirdTouch) return thirdFace;
            return fallback;
        }

        private static void ResolveStreetFrame(
            BlockFace streetFace, Rect lotRect,
            out bool isHorizontalStreet, out float near, out float far, out float left, out float right)
        {
            // near/far run along the lot's DEEP axis (street → rear) and left/right
            // along the ACROSS axis. left is ALWAYS the lower lateral bound — every
            // inset helper (side yards, driveway, building pad) does left + width /
            // right - width, so reversing them pushed zones past the lot edge.
            switch (streetFace)
            {
                case BlockFace.South:
                    near = lotRect.yMin; far = lotRect.yMax;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontalStreet = true;
                    break;
                case BlockFace.North:
                    near = lotRect.yMax; far = lotRect.yMin;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontalStreet = true;
                    break;
                case BlockFace.West:
                    near = lotRect.xMin; far = lotRect.xMax;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontalStreet = false;
                    break;
                default:
                    near = lotRect.xMax; far = lotRect.xMin;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontalStreet = false;
                    break;
            }
        }

        private readonly struct DrivewayMeasure
        {
            public readonly float Width;
            public readonly float Depth;
            public readonly float Left;

            public DrivewayMeasure(float width, float depth, float left)
            {
                Width = width;
                Depth = depth;
                Left = left;
            }
        }

        /// <summary>
        ///     True when a street on the given face runs horizontally (deep axis = Z).
        /// </summary>
        private static bool IsHorizontalFace(BlockFace face) =>
            face == BlockFace.South || face == BlockFace.North;

        private static DrivewayMeasure ComputeDriveway(
            float absSpanAcross, float absSpanDeep, float left, float right, float sideWidth,
            LotTerrainZoneLayout layout)
        {
            var driveWidth = layout.generateDriveway
                ? Mathf.Min(layout.drivewayWidthMeters, absSpanAcross - sideWidth * 2f - 0.5f)
                : 0f;
            // Cap driveway depth so it doesn't eat shallow lots or push the pad
            // against the rear neighbour's pad.
            var driveDepth = Mathf.Min(absSpanDeep * layout.drivewayDepthFractionOfLotDepth, 6f);
            var driveLeft = left + layout.drivewaySideOffsetMeters;

            if (driveWidth <= 0.5f)
                return new DrivewayMeasure(driveWidth, driveDepth, driveLeft);
            if (driveLeft < left + sideWidth) driveLeft = left + sideWidth;
            if (driveLeft + driveWidth > right - sideWidth) driveLeft = right - sideWidth - driveWidth;
            if (driveLeft < left) { driveLeft = left; driveWidth = Mathf.Min(driveWidth, absSpanAcross * 0.4f); }

            return new DrivewayMeasure(driveWidth, driveDepth, driveLeft);
        }

        // ── Zone builders ───────────────────────────────────────────

        private static void AddPrimaryFrontYard(in SubZoneContext ctx, List<LotSubZone> zones, ref int heightOrder)
        {
            if (!ctx.FrontYardActive)
                return;

            var fyFar = ctx.Near + ctx.SignDeep * ctx.FrontDepth;
            zones.Add(MakeSubZone(ctx.Layout.frontYardTextureIndex,
                BuildRectForFace(ctx.IsHorizontalStreet,
                    ctx.Near, fyFar, ctx.Left, ctx.Right), ref heightOrder, "FrontYard"));
        }

        private static void AddSecondaryFrontYard(in SubZoneContext ctx, List<LotSubZone> zones, ref int heightOrder)
        {
            if (!ctx.IsCorner)
                return;

            // The secondary front yard occupies the strip along the second street.
            // It overlaps with the primary front yard at the corner — the overlap is
            // fine (z-height ordering resolves which one is visible).
            // The rect must be mapped with the SECONDARY face's own orientation — a
            // lot spanning the whole block depth touches two opposite edges, so the
            // secondary face can run the same way as the primary (not perpendicular).
            EdgeMappingForFace(ctx.SecondFace, ctx.LotRect, out var secNear, out var secFar, out var secLeft, out var secRight);
            var secSign = secFar > secNear ? 1f : -1f;
            var secFarEdge = secNear + secSign * ctx.FrontDepth;

            zones.Add(MakeSubZone(ctx.Layout.frontYardTextureIndex,
                BuildRectForFace(IsHorizontalFace(ctx.SecondFace),
                    secNear, secFarEdge, secLeft, secRight), ref heightOrder, "FrontYard_Second"));
        }

        // ── Helpers ────────────────────────────────────────────────

        private static LotSubZone MakeSubZone(
            int textureLayerIndex, Rect bounds, ref int heightOrder, string displayName)
        {
            return new LotSubZone
            {
                TextureLayerIndex = textureLayerIndex,
                Bounds = bounds,
                HeightOffset = heightOrder++ * SubZoneHeightStep,
                DisplayName = displayName,
                Sharp = displayName is "Driveway" or "BuildingPad" or "Footpath",
                ClippedOutline = null
            };
        }

        /// <summary>
        ///     Resolves near/far/left/right edge coordinates for a given BlockFace
        ///     relative to a lot rect. Used by corner-lot secondary street logic.
        /// </summary>
        private static void EdgeMappingForFace(
            BlockFace face, Rect lotRect,
            out float near, out float far, out float left, out float right)
        {
            // Same convention as ResolveStreetFrame: near/far along the deep axis,
            // left always the lower lateral bound.
            switch (face)
            {
                case BlockFace.South:
                    near = lotRect.yMin; far = lotRect.yMax;
                    left = lotRect.xMin; right = lotRect.xMax;
                    break;
                case BlockFace.North:
                    near = lotRect.yMax; far = lotRect.yMin;
                    left = lotRect.xMin; right = lotRect.xMax;
                    break;
                case BlockFace.West:
                    near = lotRect.xMin; far = lotRect.xMax;
                    left = lotRect.yMin; right = lotRect.yMax;
                    break;
                default:
                    near = lotRect.xMax; far = lotRect.xMin;
                    left = lotRect.yMin; right = lotRect.yMax;
                    break;
            }
        }

        /// <summary>
        ///     Builds a world-space XZ rect from either (near, far, left, right)
        ///     where the axes were chosen by <see cref="isHorizontalStreet"/>.
        ///     When the street runs horizontally (South/North), near/far are Z values
        ///     and left/right are X values. When vertical (West/East), they swap.
        /// </summary>
        private static Rect BuildRectForFace(
            bool isHorizontalStreet, float near, float far, float left, float right)
        {
            float xMin, xMax, yMin, yMax;
            if (isHorizontalStreet)
            {
                xMin = Mathf.Min(left, right);
                xMax = Mathf.Max(left, right);
                yMin = Mathf.Min(near, far);
                yMax = Mathf.Max(near, far);
            }
            else
            {
                xMin = Mathf.Min(near, far);
                xMax = Mathf.Max(near, far);
                yMin = Mathf.Min(left, right);
                yMax = Mathf.Max(left, right);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
#endif
