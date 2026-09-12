using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Sidewalk candidate collection for <see cref="CityPowerLinePlacer"/>:
    ///     walks every district lot, resolves its street-facing edge, and emits
    ///     grid-aligned pole positions along the sidewalk line — excluding the
    ///     terrain painted for each building's driveway and front-door path, so
    ///     poles land on the footpath on either side of those crossings.
    /// </summary>
    public static partial class CityPowerLinePlacer
    {
        private const float EdgeSnapMeters = 0.05f;
        private const float DrivewayGapMeters = 0.4f;
        private const float MinSegmentMeters = 0.5f;

        private readonly struct LotDoorAnchor
        {
            public readonly Rect Footprint;
            public readonly Vector2 Door;
            public readonly bool HasDoor;

            public LotDoorAnchor(Rect footprint, Vector2 door, bool hasDoor)
            {
                Footprint = footprint;
                Door = door;
                HasDoor = hasDoor;
            }
        }

        private static void CollectSidewalkCandidates(
            Transform areasRoot,
            DistrictLotTerrainLayout lotTerrainLayout,
            float sidewalkOffset,
            float crossingClearance,
            float spacing,
            List<PoleCandidate> results)
        {
            for (var a = 0; a < areasRoot.childCount; a++)
                CollectAreaCandidates(areasRoot.GetChild(a), lotTerrainLayout,
                    sidewalkOffset, crossingClearance, spacing, results);
        }

        private static void CollectAreaCandidates(
            Transform area,
            DistrictLotTerrainLayout lotTerrainLayout,
            float sidewalkOffset,
            float crossingClearance,
            float spacing,
            List<PoleCandidate> results)
        {
            var marker = area.GetComponent<CityNamedAreaMarker>();
            var lotsRoot = area.Find("DistrictLots");
            if (lotsRoot == null) lotsRoot = area.Find("ResidentialLots");
            if (marker == null || lotsRoot == null)
                return;

            var blockBounds = marker.GetHubShiftedBoundsXZ();
            var layout = lotTerrainLayout != null
                ? lotTerrainLayout.GetLayout(marker.DistrictType)
                : null;

            var anchors = CollectBuildingAnchors(
                area.Find(CityDistrictLotPlacement.PlacedBuildingsContainerName));

            for (var l = 0; l < lotsRoot.childCount; l++)
                CollectLotCandidates(lotsRoot.GetChild(l), blockBounds, layout, anchors,
                    sidewalkOffset, crossingClearance, spacing, results);
        }

        private static List<LotDoorAnchor> CollectBuildingAnchors(Transform placedBuildings)
        {
            var anchors = new List<LotDoorAnchor>();
            if (placedBuildings == null)
                return anchors;

            for (var i = 0; i < placedBuildings.childCount; i++)
            {
                var marker = placedBuildings.GetChild(i).GetComponent<CityBuildingStreetFacingMarker>();
                if (marker == null ||
                    marker.footprintXZ.width < 0.5f || marker.footprintXZ.height < 0.5f)
                    continue;

                anchors.Add(new LotDoorAnchor(
                    marker.footprintXZ,
                    new Vector2(marker.doorAnchorWorld.x, marker.doorAnchorWorld.z),
                    marker.hasDoorAnchor));
            }

            return anchors;
        }

        private static LotDoorAnchor? FindAnchorForLot(Rect lotRect, List<LotDoorAnchor> anchors)
        {
            if (anchors == null) return null;
            for (var i = 0; i < anchors.Count; i++)
                if (lotRect.Contains(anchors[i].Footprint.center))
                    return anchors[i];
            return null;
        }

        private static void CollectLotCandidates(
            Transform lot,
            Rect blockBounds,
            LotTerrainZoneLayout layout,
            List<LotDoorAnchor> anchors,
            float sidewalkOffset,
            float crossingClearance,
            float spacing,
            List<PoleCandidate> results)
        {
            var lotRect = CityDistrictLotPlacement.ExtractLotRect(lot);
            if (lotRect.width < 4f || lotRect.height < 4f)
                return;

            var facingMarker = lot.GetComponent<CityLotFacingMarker>();
            var streetFace = facingMarker != null
                ? facingMarker.streetFace
                : CityBuildingRoadFacingUtility.ResolveLotStreetFace(lotRect, blockBounds);

            ResolveStreetFrame(streetFace, lotRect,
                out var isHorizontal, out var near, out var far, out var left, out var right);

            // Sidewalk line: the street edge pushed outward (toward the road) by
            // the configured offset — inside the strip between lot edge and road.
            var deepCoord = near - (far > near ? 1f : -1f) * sidewalkOffset;
            var facing = Quaternion.LookRotation(OutwardForFace(streetFace), Vector3.up);

            var anchor = FindAnchorForLot(lotRect, anchors);
            ResolveCrossingBand(lotRect, blockBounds, isHorizontal, left, right, layout, anchor,
                out var bandMin, out var bandMax);

            if (bandMax >= bandMin)
            {
                AddSegmentCandidates(isHorizontal, deepCoord, left, bandMin - crossingClearance,
                    spacing, facing, results);
                AddSegmentCandidates(isHorizontal, deepCoord, bandMax + crossingClearance, right,
                    spacing, facing, results);
            }
            else
            {
                AddSegmentCandidates(isHorizontal, deepCoord, left, right, spacing, facing, results);
            }
        }

        private static Vector3 OutwardForFace(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Vector3.back,
                BlockFace.North => Vector3.forward,
                BlockFace.West  => Vector3.left,
                _               => Vector3.right
            };
        }

        /// <summary>
        ///     Mirrors the lot terrain subdivision frame: near/far run along the
        ///     street's DEEP axis (street → rear), left/right along the ACROSS
        ///     axis (the street edge itself).
        /// </summary>
        private static void ResolveStreetFrame(
            BlockFace streetFace, Rect lotRect,
            out bool isHorizontal, out float near, out float far, out float left, out float right)
        {
            switch (streetFace)
            {
                case BlockFace.South:
                    near = lotRect.yMin; far = lotRect.yMax;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontal = true;
                    break;
                case BlockFace.North:
                    near = lotRect.yMax; far = lotRect.yMin;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontal = true;
                    break;
                case BlockFace.West:
                    near = lotRect.xMin; far = lotRect.xMax;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontal = false;
                    break;
                default:
                    near = lotRect.xMax; far = lotRect.xMin;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontal = false;
                    break;
            }
        }

        // ── Driveway / door-path crossing band ──

        /// <summary>
        ///     The along-street span occupied by the lot's painted driveway and
        ///     front-door footpath, mirroring the terrain subdivision math. An
        ///     empty result (<c>bandMin &gt; bandMax</c>) means no crossing exists.
        /// </summary>
        private static void ResolveCrossingBand(
            Rect lotRect,
            Rect blockBounds,
            bool isHorizontal,
            float left,
            float right,
            LotTerrainZoneLayout layout,
            LotDoorAnchor? anchor,
            out float bandMin,
            out float bandMax)
        {
            bandMin = float.PositiveInfinity;
            bandMax = float.NegativeInfinity;
            if (layout == null)
            {
                bandMin = 0f;
                bandMax = -1f;
                return;
            }

            var spanAcross = Mathf.Abs(right - left);
            var sideWidth = ResolveSideYardWidth(spanAcross, layout.sideYardFractionOfLotWidth);
            var isCorner = CountTouchingBlockEdges(lotRect, blockBounds) >= 2;

            var hasDoor = anchor.HasValue && anchor.Value.HasDoor;
            var centerAcross = hasDoor
                ? (isHorizontal ? anchor.Value.Door.x : anchor.Value.Door.y)
                : (left + right) * 0.5f;

            // Footpath gating mirrors the terrain painter: the pad must be wide
            // enough to actually host a footpath beside the slab.
            var padAcross = hasDoor
                ? (isHorizontal ? anchor.Value.Footprint.width : anchor.Value.Footprint.height)
                : (isCorner ? spanAcross * 0.5f : spanAcross - sideWidth * 2f);
            var footActive = layout.generateDoorFootpath && layout.generateBuildingPad
                && layout.footpathWidthMeters > 0.4f
                && padAcross > layout.footpathWidthMeters + 0.5f;

            if (footActive)
            {
                var pathHalf = layout.footpathWidthMeters * 0.5f;
                GrowBand(centerAcross - pathHalf, centerAcross + pathHalf, ref bandMin, ref bandMax);
            }

            // Corner-lot driveways run on the secondary street, so the primary
            // sidewalk only needs the door-path exclusion.
            if (layout.generateDriveway && !isCorner)
            {
                var driveWidth = Mathf.Min(layout.drivewayWidthMeters, spanAcross);
                var hasDriveBand = false;
                var driveLeft = 0f;

                if (hasDoor && driveWidth > 0.5f)
                {
                    // Door-anchored: sits beside the footpath with a thin gap,
                    // exactly like the painted driveway.
                    var pathHalfGap = footActive
                        ? layout.footpathWidthMeters * 0.5f + DrivewayGapMeters
                        : 0f;
                    driveLeft = centerAcross + pathHalfGap;
                    if (driveLeft + driveWidth > right)
                        driveLeft = centerAcross - pathHalfGap - driveWidth;
                    driveLeft = Mathf.Clamp(driveLeft, left, right - driveWidth);
                    hasDriveBand = true;
                }
                else if (!hasDoor &&
                         ComputeGeometricDriveway(spanAcross, left, right, sideWidth, layout,
                             out driveLeft, out driveWidth))
                {
                    hasDriveBand = true;
                }

                if (hasDriveBand)
                    GrowBand(driveLeft, driveLeft + driveWidth, ref bandMin, ref bandMax);
            }

            if (bandMin > bandMax)
            {
                bandMin = 0f;
                bandMax = -1f;
            }
        }

        /// <summary>
        ///     Geometric driveway placement — the same math the lot terrain
        ///     subdivision uses when no placed-building anchor exists.
        /// </summary>
        private static bool ComputeGeometricDriveway(
            float spanAcross, float left, float right, float sideWidth,
            LotTerrainZoneLayout layout, out float bandMin, out float bandMax)
        {
            bandMin = 0f;
            bandMax = -1f;
            if (!layout.generateDriveway) return false;

            var driveWidth = Mathf.Min(layout.drivewayWidthMeters, spanAcross - sideWidth * 2f - 0.5f);
            if (driveWidth <= 0.5f) return false;

            var driveLeft = left + layout.drivewaySideOffsetMeters;
            if (driveLeft < left + sideWidth) driveLeft = left + sideWidth;
            if (driveLeft + driveWidth > right - sideWidth) driveLeft = right - sideWidth - driveWidth;
            if (driveLeft < left)
            {
                driveLeft = left;
                driveWidth = Mathf.Min(driveWidth, spanAcross * 0.4f);
            }

            bandMin = driveLeft;
            bandMax = driveLeft + driveWidth;
            return true;
        }

        /// <summary>
        ///     Side-yard band width — mirrors the terrain subdivision's helper so
        ///     the crossing bands line up with the painted driveway.
        /// </summary>
        private static float ResolveSideYardWidth(float spanAcross, float fraction)
        {
            var width = spanAcross * fraction;
            if (spanAcross >= 8f)
                width = Mathf.Max(width, 1.5f);
            return Mathf.Min(width, spanAcross * 0.18f);
        }

        private static int CountTouchingBlockEdges(Rect lotRect, Rect blockBounds)
        {
            var count = 0;
            if (Mathf.Abs(lotRect.yMin - blockBounds.yMin) < EdgeSnapMeters) count++;
            if (Mathf.Abs(lotRect.yMax - blockBounds.yMax) < EdgeSnapMeters) count++;
            if (Mathf.Abs(lotRect.xMin - blockBounds.xMin) < EdgeSnapMeters) count++;
            if (Mathf.Abs(lotRect.xMax - blockBounds.xMax) < EdgeSnapMeters) count++;
            return count;
        }

        private static void GrowBand(float min, float max, ref float bandMin, ref float bandMax)
        {
            bandMin = Mathf.Min(bandMin, min);
            bandMax = Mathf.Max(bandMax, max);
        }

        /// <summary>
        ///     Emits grid-aligned pole positions along one sidewalk segment: slots
        ///     at multiples of the spacing along the street axis, so neighbouring
        ///     lots merge into a single uniform pole run.
        /// </summary>
        private static void AddSegmentCandidates(
            bool isHorizontal,
            float deepCoord,
            float segMin,
            float segMax,
            float spacing,
            Quaternion facing,
            List<PoleCandidate> results)
        {
            if (segMax - segMin < MinSegmentMeters)
                return;

            var first = Mathf.Ceil(segMin / spacing) * spacing;
            for (var t = first; t <= segMax + 0.01f; t += spacing)
            {
                var pos = isHorizontal
                    ? new Vector2(t, deepCoord)
                    : new Vector2(deepCoord, t);
                results.Add(new PoleCandidate(pos, facing));
            }
        }
    }
}
