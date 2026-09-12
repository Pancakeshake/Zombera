using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    public readonly struct CityNamedAreaBuildingPlacement
    {
        public readonly CityAssembledBuildingCatalogEntry Entry;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion Rotation;
        public readonly bool UseProxy;

        public CityNamedAreaBuildingPlacement(
            CityAssembledBuildingCatalogEntry entry,
            Vector3 worldPosition,
            Quaternion rotation,
            bool useProxy)
        {
            Entry = entry;
            WorldPosition = worldPosition;
            Rotation = rotation;
            UseProxy = useProxy;
        }
    }

    public readonly struct CityNamedAreaBuildingLayoutArea
    {
        public readonly Rect BlockBounds;
        public readonly IReadOnlyList<Vector2> Outline;
        public readonly CityBlockCornerMask RoundedCorners;
        public readonly float GroundY;
        public readonly CityDistrictType DistrictType;

        public CityNamedAreaBuildingLayoutArea(
            Rect blockBounds,
            IReadOnlyList<Vector2> outline,
            CityBlockCornerMask roundedCorners,
            float groundY,
            CityDistrictType districtType)
        {
            BlockBounds = blockBounds;
            Outline = outline;
            RoundedCorners = roundedCorners;
            GroundY = groundY;
            DistrictType = districtType;
        }
    }

    public readonly struct CityNamedAreaBuildingLayoutCatalog
    {
        public readonly IReadOnlyList<CityAssembledBuildingCatalogEntry> Entries;
        public readonly CityNamedAreaBuildingLayoutSettings Settings;
        public readonly bool PreferProxies;
        public readonly System.Random Rng;

        public CityNamedAreaBuildingLayoutCatalog(
            IReadOnlyList<CityAssembledBuildingCatalogEntry> entries,
            CityNamedAreaBuildingLayoutSettings settings,
            bool preferProxies,
            System.Random rng)
        {
            Entries = entries;
            Settings = settings;
            PreferProxies = preferProxies;
            Rng = rng;
        }
    }

    public readonly struct CityNamedAreaBuildingLayoutRequest
    {
        public readonly CityNamedAreaBuildingLayoutArea Area;
        public readonly CityNamedAreaBuildingLayoutCatalog Catalog;

        public CityNamedAreaBuildingLayoutRequest(
            CityNamedAreaBuildingLayoutArea area,
            CityNamedAreaBuildingLayoutCatalog catalog)
        {
            Area = area;
            Catalog = catalog;
        }
    }

    /// <summary>
    ///     Packs buildings into city blocks with street-facing perimeter rows and optional interior fill.
    /// </summary>
    public static partial class CityNamedAreaBuildingLayout
    {
        private const float MaxBlockFootprintFraction = 0.35f;
        private const float MinInteriorSpanMeters = 24f;

        private readonly struct FaceRowCursorState
        {
            public readonly BlockFace Face;
            public readonly float Cursor;
            public readonly float EdgeCoord;
            public readonly float Yaw;
            public readonly float WalkMax;
            public readonly float MaxInward;
            public readonly float Spacing;

            public FaceRowCursorState(
                BlockFace face,
                float cursor,
                float edgeCoord,
                float yaw,
                float walkMax,
                float maxInward,
                float spacing)
            {
                Face = face;
                Cursor = cursor;
                EdgeCoord = edgeCoord;
                Yaw = yaw;
                WalkMax = walkMax;
                MaxInward = maxInward;
                Spacing = spacing;
            }
        }

        private sealed class LayoutBuildContext
        {
            public List<CityNamedAreaBuildingPlacement> Results;
            public List<Rect> Occupied;
            public IReadOnlyList<Vector2> Outline;
            public float GroundY;
            public IReadOnlyList<CityAssembledBuildingCatalogEntry> Candidates;
            public CityNamedAreaBuildingLayoutSettings Settings;
            public bool PreferProxies;
            public float Grid;
            public float GridOriginX;
            public float GridOriginZ;
            public System.Random Rng;
            public List<CityAssembledBuildingCatalogEntry> FitsScratch;
            public List<float> FitWidthsScratch;
            public List<float> FitDepthsScratch;
            public List<int> FitIndicesScratch;
        }

        public static List<CityNamedAreaBuildingPlacement> BuildPlacements(CityNamedAreaBuildingLayoutRequest request)
        {
            var results = new List<CityNamedAreaBuildingPlacement>();
            var area = request.Area;
            var catalog = request.Catalog;
            if (area.Outline == null || area.Outline.Count < 3 ||
                catalog.Entries == null || catalog.Entries.Count == 0 || catalog.Rng == null)
                return results;

            var settings = catalog.Settings;
            settings.Clamp();
            var candidates = FilterCatalog(catalog.Entries, area.DistrictType);
            candidates = FilterCandidatesForBlock(candidates, area.BlockBounds, catalog.PreferProxies);
            if (candidates.Count == 0)
                return results;

            if (area.BlockBounds.width <= 0.01f || area.BlockBounds.height <= 0.01f)
                return results;

            var grid = settings.gridCellMeters;
            var outlineBounds = CityNamedAreaPolygonUtility.ComputeBounds(area.Outline);
            var gridOriginX = SnapDown(outlineBounds.xMin, grid);
            var gridOriginZ = SnapDown(outlineBounds.yMin, grid);
            var setback = settings.streetSetbackMeters;
            if (area.RoundedCorners != CityBlockCornerMask.None)
                setback += grid;

            var buildable = Rect.MinMaxRect(
                outlineBounds.xMin + setback,
                outlineBounds.yMin + setback,
                outlineBounds.xMax - setback,
                outlineBounds.yMax - setback);

            if (buildable.width < grid * 2f || buildable.height < grid * 2f)
                return results;

            var occupied = new List<Rect>(64);
            var ctx = new LayoutBuildContext
            {
                FitsScratch = new List<CityAssembledBuildingCatalogEntry>(8),
                FitWidthsScratch = new List<float>(8),
                FitDepthsScratch = new List<float>(8),
                FitIndicesScratch = new List<int>(8),
                Results = results,
                Occupied = occupied,
                Outline = area.Outline,
                GroundY = area.GroundY,
                Candidates = candidates,
                Settings = settings,
                PreferProxies = catalog.PreferProxies,
                Grid = grid,
                GridOriginX = gridOriginX,
                GridOriginZ = gridOriginZ,
                Rng = catalog.Rng
            };

            var perimeterDepth = 0f;
            if (settings.placePerimeterRows && settings.rowsPerRoadSide > 0)
                perimeterDepth = PlacePerimeterRows(ctx, buildable);

            if (settings.fillInterior &&
                buildable.width >= MinInteriorSpanMeters &&
                buildable.height >= MinInteriorSpanMeters)
            {
                var inner = Rect.MinMaxRect(
                    buildable.xMin + perimeterDepth,
                    buildable.yMin + perimeterDepth,
                    buildable.xMax - perimeterDepth,
                    buildable.yMax - perimeterDepth);

                if (inner.width >= MinInteriorSpanMeters && inner.height >= MinInteriorSpanMeters)
                    PlaceInteriorGrid(ctx, inner);
            }

            return results;
        }

        private static List<CityAssembledBuildingCatalogEntry> FilterCandidatesForBlock(
            IReadOnlyList<CityAssembledBuildingCatalogEntry> candidates,
            Rect blockBounds,
            bool preferProxies)
        {
            var filtered = new List<CityAssembledBuildingCatalogEntry>(candidates.Count);
            var maxWidth = Mathf.Max(
                CityBuildingPrefabFootprintUtility.ModularGridCellMeters,
                blockBounds.width * MaxBlockFootprintFraction);
            var maxDepth = Mathf.Max(
                CityBuildingPrefabFootprintUtility.ModularGridCellMeters,
                blockBounds.height * MaxBlockFootprintFraction);

            for (var i = 0; i < candidates.Count; i++)
            {
                var entry = candidates[i];
                if (entry == null)
                    continue;

                var width = entry.ResolvePlacementWidth(preferProxies);
                var depth = entry.ResolvePlacementDepth(preferProxies);
                CityBuildingPrefabFootprintUtility.ClampToBlock(ref width, ref depth, blockBounds, MaxBlockFootprintFraction);
                if (width <= maxWidth + 0.01f && depth <= maxDepth + 0.01f)
                    filtered.Add(entry);
            }

            return filtered;
        }

        private static float PlacePerimeterRows(LayoutBuildContext ctx, Rect buildable)
        {
            var inwardOffset = 0f;
            var faces = new[]
            {
                BlockFace.South,
                BlockFace.North,
                BlockFace.West,
                BlockFace.East
            };

            for (var row = 0; row < ctx.Settings.rowsPerRoadSide; row++)
            {
                var rowDepth = 0f;
                for (var f = 0; f < faces.Length; f++)
                {
                    rowDepth = Mathf.Max(
                        rowDepth,
                        PlaceFaceRow(ctx, buildable, faces[f], inwardOffset));
                }

                inwardOffset += rowDepth + ctx.Settings.rowSpacingMeters;
            }

            return inwardOffset;
        }

        private static float PlaceFaceRow(
            LayoutBuildContext ctx,
            Rect buildable,
            BlockFace face,
            float inwardOffset)
        {
            ResolveFaceAxes(face, buildable, inwardOffset, out var walkMin, out var walkMax, out var edgeCoord, out var yaw);
            var walkOrigin = face is BlockFace.South or BlockFace.North ? ctx.GridOriginX : ctx.GridOriginZ;
            var cursor = SnapUp(walkMin, walkOrigin, ctx.Grid);
            var maxDepth = 0f;
            var spacing = ctx.Settings.minBuildingSpacingMeters;
            var maxInward = ResolveMaxInwardDepth(face, buildable, edgeCoord);

            while (cursor + ctx.Grid <= walkMax + 0.01f)
            {
                var rowState = new FaceRowCursorState(face, cursor, edgeCoord, yaw, walkMax, maxInward, spacing);
                if (TryPlaceFaceRowAtCursor(ctx, rowState, out var nextCursor, out var placedDepth))
                {
                    maxDepth = Mathf.Max(maxDepth, placedDepth);
                    cursor = nextCursor;
                }
                else
                {
                    cursor += ctx.Grid;
                }
            }

            return maxDepth;
        }

        private static bool TryPlaceFaceRowAtCursor(
            LayoutBuildContext ctx,
            FaceRowCursorState row,
            out float nextCursor,
            out float placedDepth)
        {
            nextCursor = row.Cursor + ctx.Grid;
            placedDepth = 0f;

            var remaining = row.WalkMax - row.Cursor;
            var probeWidth = Mathf.Min(remaining, ctx.Grid * 4f);
            var allowedDepth = ComputeAllowedDepth(
                ctx.Outline,
                row.Face,
                row.Cursor,
                row.EdgeCoord,
                probeWidth,
                row.MaxInward,
                ctx.Grid);
            if (allowedDepth < ctx.Grid)
                return false;

            var entry = PickEntryForSpan(
                ctx,
                remaining,
                allowedDepth,
                out var localWidth,
                out var localDepth);
            if (entry == null)
                return false;

            GetWorldExtents(localWidth, localDepth, row.Yaw, out var extentX, out var extentZ);
            if (!TryGetWorldRect(row.Face, row.Cursor, row.EdgeCoord, extentX, extentZ, out var xMin, out var zMin))
                return false;

            var center = new Vector2(xMin + extentX * 0.5f, zMin + extentZ * 0.5f);
            var doorYaw = entry.doorYawResolved ? entry.doorYawOffsetDegrees : CityBuildingRoadFacingUtility.GetDoorYawOffset(entry.prefab, entry.yawOffsetDegrees);
            var rotation = Quaternion.Euler(0f, row.Yaw + doorYaw, 0f);
            if (!IsValidPlacement(ctx, center, extentX, extentZ, row.Spacing))
                return false;

            ctx.Occupied.Add(BuildOccupancyRect(center, extentX, extentZ, row.Spacing));
            placedDepth = row.Face is BlockFace.South or BlockFace.North ? extentZ : extentX;
            ctx.Results.Add(CreatePlacement(entry, center, ctx.GroundY, rotation, ctx.PreferProxies));

            var walkExtent = row.Face is BlockFace.South or BlockFace.North ? extentX : extentZ;
            nextCursor = row.Cursor + walkExtent + row.Spacing;
            return true;
        }

        private static float ResolveMaxInwardDepth(BlockFace face, Rect buildable, float edgeCoord)
        {
            return face switch
            {
                BlockFace.South => buildable.yMax - edgeCoord,
                BlockFace.North => edgeCoord - buildable.yMin,
                BlockFace.West => buildable.xMax - edgeCoord,
                _ => edgeCoord - buildable.xMin
            };
        }

        private static float ComputeAllowedDepth(
            IReadOnlyList<Vector2> outline,
            BlockFace face,
            float walkCursor,
            float edgeCoord,
            float probeWidth,
            float maxInward,
            float grid)
        {
            probeWidth = SnapUp(probeWidth, grid);
            switch (face)
            {
                case BlockFace.South:
                    return CityNamedAreaPolygonUtility.ComputeMaxDepthForAxisRect(
                        outline, walkCursor, edgeCoord, probeWidth, maxInward, grid);
                case BlockFace.North:
                    return CityNamedAreaPolygonUtility.ComputeMaxDepthForAxisRect(
                        outline, walkCursor, edgeCoord - maxInward, probeWidth, maxInward, grid);
                case BlockFace.West:
                    return CityNamedAreaPolygonUtility.ComputeMaxDepthForAxisRect(
                        outline, edgeCoord, walkCursor, maxInward, probeWidth, grid);
                default:
                    return CityNamedAreaPolygonUtility.ComputeMaxDepthForAxisRect(
                        outline, edgeCoord - maxInward, walkCursor, maxInward, probeWidth, grid);
            }
        }

    }
}