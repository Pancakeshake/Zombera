using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    public static partial class CityNamedAreaBuildingLayout
    {
        private static void PlaceInteriorGrid(LayoutBuildContext ctx, Rect inner)
        {
            var spacing = ctx.Settings.minBuildingSpacingMeters;
            var z = SnapUp(inner.yMin, ctx.GridOriginZ, ctx.Grid);

            while (z < inner.yMax - ctx.Grid)
            {
                var rowDepth = ctx.Grid;
                var x = SnapUp(inner.xMin, ctx.GridOriginX, ctx.Grid);
                while (x < inner.xMax - ctx.Grid)
                {
                    if (TryPlaceInteriorAt(ctx, inner, ref x, z, spacing, ref rowDepth))
                        continue;

                    x += ctx.Grid;
                }

                z += rowDepth + spacing;
            }
        }

        private static bool TryPlaceInteriorAt(
            LayoutBuildContext ctx,
            Rect inner,
            ref float x,
            float z,
            float spacing,
            ref float rowDepth)
        {
            var remainingX = inner.xMax - x;
            var remainingZ = inner.yMax - z;
            var allowedDepth = CityNamedAreaPolygonUtility.ComputeMaxDepthForAxisRect(
                ctx.Outline,
                x,
                z,
                Mathf.Min(remainingX, ctx.Grid * 6f),
                remainingZ,
                ctx.Grid);
            if (allowedDepth < ctx.Grid)
                return false;

            var entry = PickEntryForArea(ctx, remainingX, allowedDepth, out var localWidth, out var localDepth);
            if (entry == null)
                return false;

            var yaw = ResolveInteriorYaw(inner, x, z, localWidth, localDepth);
            GetWorldExtents(localWidth, localDepth, yaw, out var extentX, out var extentZ);
            if (x + extentX > inner.xMax + 0.01f || z + extentZ > inner.yMax + 0.01f)
                return false;

            var center = new Vector2(x + extentX * 0.5f, z + extentZ * 0.5f);
            if (!IsValidPlacement(ctx, center, extentX, extentZ, spacing))
                return false;

            ctx.Occupied.Add(BuildOccupancyRect(center, extentX, extentZ, spacing));
            ctx.Results.Add(CreatePlacement(
                entry,
                center,
                ctx.GroundY,
                Quaternion.Euler(0f, yaw + (entry.doorYawResolved ? entry.doorYawOffsetDegrees : CityBuildingRoadFacingUtility.GetDoorYawOffset(entry.prefab, entry.yawOffsetDegrees)), 0f),
                ctx.PreferProxies));

            rowDepth = Mathf.Max(rowDepth, extentZ);
            x += extentX + spacing;
            return true;
        }

        private static float ResolveInteriorYaw(Rect inner, float x, float z, float localWidth, float localDepth)
        {
            var tentativeCX = x + localWidth * 0.5f;
            var tentativeCZ = z + localDepth * 0.5f;
            var distSouth = tentativeCZ - inner.yMin;
            var distNorth = inner.yMax - tentativeCZ;
            var distWest = tentativeCX - inner.xMin;
            var distEast = inner.xMax - tentativeCX;
            var minEdge = Mathf.Min(distSouth, distNorth, distWest, distEast);

            // Match GetRoadNormal: South=180, North=0, West=270, East=90.
            if (Mathf.Abs(minEdge - distSouth) < 0.01f) return 180f;
            if (Mathf.Abs(minEdge - distNorth) < 0.01f) return 0f;
            if (Mathf.Abs(minEdge - distWest) < 0.01f) return 270f;
            return 90f;
        }

        private static void ResolveFaceAxes(
            BlockFace face,
            Rect buildable,
            float inwardOffset,
            out float walkMin,
            out float walkMax,
            out float edgeCoord,
            out float yaw)
        {
            switch (face)
            {
                case BlockFace.South:
                    walkMin = buildable.xMin;
                    walkMax = buildable.xMax;
                    edgeCoord = buildable.yMin + inwardOffset;
                    yaw = 180f;
                    break;
                case BlockFace.North:
                    walkMin = buildable.xMin;
                    walkMax = buildable.xMax;
                    edgeCoord = buildable.yMax - inwardOffset;
                    yaw = 0f;
                    break;
                case BlockFace.West:
                    walkMin = buildable.yMin;
                    walkMax = buildable.yMax;
                    edgeCoord = buildable.xMin + inwardOffset;
                     yaw = 270f;
                     break;
                 default:
                     walkMin = buildable.yMin;
                     walkMax = buildable.yMax;
                     edgeCoord = buildable.xMax - inwardOffset;
                     yaw = 90f;
                    break;
            }
        }

        private static bool TryGetWorldRect(
            BlockFace face,
            float walkCursor,
            float edgeCoord,
            float extentX,
            float extentZ,
            out float xMin,
            out float zMin)
        {
            switch (face)
            {
                case BlockFace.South:
                    xMin = walkCursor;
                    zMin = edgeCoord;
                    return true;
                case BlockFace.North:
                    xMin = walkCursor;
                    zMin = edgeCoord - extentZ;
                    return true;
                case BlockFace.West:
                    xMin = edgeCoord;
                    zMin = walkCursor;
                    return true;
                default:
                    xMin = edgeCoord - extentX;
                    zMin = walkCursor;
                    return true;
            }
        }

        private static bool IsValidPlacement(
            LayoutBuildContext ctx,
            Vector2 center,
            float extentX,
            float extentZ,
            float spacing)
        {
            var margin = ctx.Settings.polygonSafetyMarginMeters;
            var halfX = Mathf.Max(0.1f, extentX * 0.5f - margin);
            var halfZ = Mathf.Max(0.1f, extentZ * 0.5f - margin);
            if (!CityNamedAreaPolygonUtility.ContainsAxisAlignedRectSampled(
                    ctx.Outline,
                    center,
                    halfX,
                    halfZ,
                    ctx.Grid))
                return false;

            return !OverlapsAny(ctx.Occupied, BuildOccupancyRect(center, extentX, extentZ, spacing));
        }

        private static CityNamedAreaBuildingPlacement CreatePlacement(
            CityAssembledBuildingCatalogEntry entry,
            Vector2 center,
            float groundY,
            Quaternion rotation,
            bool preferProxies)
        {
            var useProxy = preferProxies && entry.proxyPrefab != null;
            return new CityNamedAreaBuildingPlacement(
                entry,
                new Vector3(center.x, groundY, center.y),
                rotation,
                useProxy);
        }

        private static Rect BuildOccupancyRect(Vector2 center, float extentX, float extentZ, float spacing)
        {
            var pad = spacing * 0.5f;
            return Rect.MinMaxRect(
                center.x - extentX * 0.5f - pad,
                center.y - extentZ * 0.5f - pad,
                center.x + extentX * 0.5f + pad,
                center.y + extentZ * 0.5f + pad);
        }

        private static CityAssembledBuildingCatalogEntry PickEntryForSpan(
            LayoutBuildContext ctx,
            float maxSpan,
            float maxDepth,
            out float localWidth,
            out float localDepth)
        {
            localWidth = 0f;
            localDepth = 0f;
            var fits = ctx.FitsScratch;
            var fitWidths = ctx.FitWidthsScratch;
            var fitDepths = ctx.FitDepthsScratch;
            fits.Clear();
            fitWidths.Clear();
            fitDepths.Clear();

            for (var i = 0; i < ctx.Candidates.Count; i++)
            {
                var entry = ctx.Candidates[i];
                var width = SnapUp(PaddedSize(entry.ResolvePlacementWidth(ctx.PreferProxies), ctx.Settings), ctx.Grid);
                var depth = SnapUp(PaddedSize(entry.ResolvePlacementDepth(ctx.PreferProxies), ctx.Settings), ctx.Grid);
                if (width <= maxSpan + 0.01f && depth <= maxDepth + 0.01f)
                {
                    fits.Add(entry);
                    fitWidths.Add(width);
                    fitDepths.Add(depth);
                }
            }

            if (fits.Count == 0)
                return null;

            var indices = ctx.FitIndicesScratch;
            indices.Clear();
            for (var i = 0; i < fits.Count; i++)
                indices.Add(i);

            indices.Sort((a, b) => fitWidths[b].CompareTo(fitWidths[a]));
            var chosen = indices[ctx.Rng.Next(Mathf.Min(3, indices.Count))];
            localWidth = fitWidths[chosen];
            localDepth = fitDepths[chosen];
            return fits[chosen];
        }

        private static CityAssembledBuildingCatalogEntry PickEntryForArea(
            LayoutBuildContext ctx,
            float maxWidth,
            float maxDepth,
            out float localWidth,
            out float localDepth)
        {
            return PickEntryForSpan(ctx, maxWidth, maxDepth, out localWidth, out localDepth);
        }

        private static float PaddedSize(float meters, CityNamedAreaBuildingLayoutSettings settings)
        {
            return Mathf.Max(settings.gridCellMeters, meters + settings.footprintPaddingMeters);
        }

        private static void GetWorldExtents(float localWidth, float localDepth, float yawDegrees, out float extentX, out float extentZ)
        {
            var normalized = Mathf.Repeat(yawDegrees, 360f);
            if (Mathf.Approximately(normalized, 90f) || Mathf.Approximately(normalized, 270f))
            {
                extentX = localDepth;
                extentZ = localWidth;
                return;
            }

            extentX = localWidth;
            extentZ = localDepth;
        }

        /// <summary>
        ///     Strict category filter: a district only receives buildings from its own
        ///     top-level category folder (…/Buildings_Modular_Complete/&lt;District&gt;/…).
        ///     Only the catch-all Mixed district may draw from every category.
        /// </summary>
        private static List<CityAssembledBuildingCatalogEntry> FilterCatalog(
            IReadOnlyList<CityAssembledBuildingCatalogEntry> catalog,
            CityDistrictType districtType)
        {
            var matches = new List<CityAssembledBuildingCatalogEntry>();
            for (var i = 0; i < catalog.Count; i++)
            {
                var entry = catalog[i];
                if (entry?.prefab == null)
                    continue;

                if (districtType == CityDistrictType.Mixed || entry.districtType == districtType)
                    matches.Add(entry);
            }

            return matches;
        }

        private static bool OverlapsAny(List<Rect> occupied, Rect candidate)
        {
            for (var i = 0; i < occupied.Count; i++)
            {
                if (occupied[i].Overlaps(candidate))
                    return true;
            }

            return false;
        }

        private static float SnapUp(float value, float grid)
        {
            return Mathf.Ceil(value / grid) * grid;
        }

        private static float SnapUp(float value, float origin, float grid)
        {
            return origin + Mathf.Ceil((value - origin) / grid) * grid;
        }

        private static float SnapDown(float value, float grid)
        {
            return Mathf.Floor(value / grid) * grid;
        }

    }
}