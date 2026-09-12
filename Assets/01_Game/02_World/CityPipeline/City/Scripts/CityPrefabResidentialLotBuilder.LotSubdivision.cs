#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    internal static partial class CityPrefabResidentialLotBuilder
    {
        /// <summary>
        ///     Buildings are authored on a 3 m cell grid (e.g. 3x4, 5x5 cells), so lot
        ///     sides are rounded UP to whole 3 m multiples — every lot then fits
        ///     whole-cell variants with even margins.
        /// </summary>
        private const float LotSizeGridMeters = 3f;

        private static float SnapUpToLotSizeGrid(float meters)
        {
            var snapped = Mathf.Ceil(meters / LotSizeGridMeters) * LotSizeGridMeters;
            return Mathf.Max(LotSizeGridMeters, snapped);
        }

        // ── Block subdivision ────────────────────────────────────────────

        private static List<LotPlacement> SubdivideBlockIntoLots(SubdivideArgs a)
        {
            var frontMin = a.StreetFrontX ? a.Block.xMin : a.Block.yMin;
            var frontMax = a.StreetFrontX ? a.Block.xMax : a.Block.yMax;
            var depthMin = a.StreetFrontX ? a.Block.yMin : a.Block.xMin;
            var depthMax = a.StreetFrontX ? a.Block.yMax : a.Block.xMax;

            var effectiveMinWidth = Mathf.Max(20f, a.LotWidthMin);
            var depthStrips = BuildLotDepthStrips(depthMin, depthMax, a.LotDepthMin, a.LotDepthMax, a.Rng);

            var frontDivisions = BuildLotFrontDivisions(frontMin, frontMax, effectiveMinWidth, a.LotWidthMax, a.Rng);

            var allCells = BuildAllSubdivisionCells(a.StreetFrontX, depthStrips, frontDivisions);

            if (!a.IsCurved || a.Outline == null || a.Outline.Count < 4)
                return WrapStraightBlockLots(allCells);

            return SubdivideCurvedBlock(allCells, a);
        }

        private static List<Rect> BuildAllSubdivisionCells(
            bool streetFrontX, List<float> depthStrips, List<float> frontDivisions)
        {
            var cells = new List<Rect>();
            for (var s = 0; s < depthStrips.Count - 1; s++)
            {
                var stripNear = depthStrips[s];
                var stripFar = depthStrips[s + 1];
                for (var d = 0; d < frontDivisions.Count - 1; d++)
                {
                    cells.Add(streetFrontX
                        ? Rect.MinMaxRect(frontDivisions[d], stripNear, frontDivisions[d + 1], stripFar)
                        : Rect.MinMaxRect(stripNear, frontDivisions[d], stripFar, frontDivisions[d + 1]));
                }
            }
            return cells;
        }

        private static List<LotPlacement> WrapStraightBlockLots(List<Rect> cells)
        {
            var results = new List<LotPlacement>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
                results.Add(new LotPlacement { Bounds = cells[i] });
            return results;
        }

        private static List<LotPlacement> SubdivideCurvedBlock(List<Rect> allCells, SubdivideArgs a)
        {
            var effectiveMinWidth = Mathf.Max(20f, a.LotWidthMin);
            var step = Mathf.Max(0.75f, effectiveMinWidth * 0.25f);

            var cornerFillBounds = CityNamedAreaPolygonUtility.GetCornerFillBounds(a.Block, a.Corners,
                a.FilletRadius, a.LotDepthMax, null);
            var squareLots = CollectSquareLotsInOutline(allCells, a.Outline, cornerFillBounds,
                effectiveMinWidth, step, a.LotWidthMin);

            AppendCornerGapLot(a.Block, a.Outline, a.Corners, a.FilletRadius,
                a.LotDepthMax, a.LotWidthMin, squareLots);

            return squareLots;
        }

        private static List<LotPlacement> CollectSquareLotsInOutline(
            List<Rect> allCells, IReadOnlyList<Vector2> outline, Rect cornerFillBounds,
            float effectiveMinWidth, float step, float lotWidthMin)
        {
            var minDepth = Mathf.Max(10f, lotWidthMin * 0.4f);
            var squareLots = new List<LotPlacement>();

            for (var i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];

                if (cornerFillBounds.width > 0.5f && cornerFillBounds.Overlaps(cell))
                    continue;

                var hw = cell.width * 0.5f;
                var hd = cell.height * 0.5f;
                if (CityNamedAreaPolygonUtility.ContainsAxisAlignedRectSampled(
                        outline, cell.center, hw, hd, step))
                {
                    squareLots.Add(new LotPlacement { Bounds = cell });
                }
                else
                {
                    var shrunk = cell;
                    if (CityNamedAreaPolygonUtility.TryFitAxisAlignedLotInPolygon(
                            outline, ref shrunk, step, minDepth, effectiveMinWidth))
                        squareLots.Add(new LotPlacement { Bounds = shrunk });
                }
            }

            return squareLots;
        }

        private static void AppendCornerGapLot(
            Rect block, IReadOnlyList<Vector2> outline, CityBlockCornerMask corners,
            float filletRadius, float lotDepthMax, float lotWidthMin, List<LotPlacement> squareLots)
        {
            var squareRects = new List<Rect>();
            for (var s = 0; s < squareLots.Count; s++) squareRects.Add(squareLots[s].Bounds);
            var fillBounds = CityNamedAreaPolygonUtility.GetCornerFillBounds(block, corners,
                filletRadius, lotDepthMax, squareRects);
            var cornerPoly = new List<Vector2>();
            var minCornerArea = lotWidthMin * lotWidthMin * 0.5f;
            if (CityNamedAreaPolygonUtility.TryBuildCornerGapPolygon(
                    outline, fillBounds, cornerPoly, minCornerArea))
            {
                var cornerBounds = CityNamedAreaPolygonUtility.ComputeBounds(cornerPoly);
                squareLots.Add(new LotPlacement
                {
                    Bounds = cornerBounds,
                    IsCornerLot = true,
                    ClippedOutline = cornerPoly
                });
            }
        }

        /// <summary>
        ///     Builds depth division points from <paramref name="depthMin"/> (road edge)
        ///     toward <paramref name="depthMax"/> (block back). All rows share ONE
        ///     uniform depth (≤ 2 rows per block) so every lot gets the same
        ///     front/back yard proportions — random per-row depths + landlocked-row
        ///     absorption were producing massive lots next to tiny ones in deep blocks.
        ///     The last strip always extends to <paramref name="depthMax"/>.
        /// </summary>
        private static List<float> BuildLotDepthStrips(
            float depthMin, float depthMax,
            float lotDepthMin, float lotDepthMax,
            System.Random rng)
        {
            var divisions = new List<float> { depthMin };
            var totalDepth = depthMax - depthMin;
            var effectiveMinDepth = Mathf.Max(10f, lotDepthMin * 0.4f);

            // One target depth for the whole block; split into at most two rows
            // (front + rear street sides) so interior landlocked rows never form.
            var targetDepth = effectiveMinDepth +
                (float)rng.NextDouble() * Mathf.Max(1f, lotDepthMax - effectiveMinDepth);
            var maxRows = Mathf.Max(1, Mathf.FloorToInt(totalDepth / effectiveMinDepth));
            var rowCount = Mathf.Clamp(Mathf.RoundToInt(totalDepth / targetDepth), 1, Mathf.Min(maxRows, 2));
            var rowDepth = Mathf.Min(totalDepth, SnapUpToLotSizeGrid(totalDepth / rowCount));

            var cursor = depthMin;
            while (cursor < depthMax - effectiveMinDepth)
            {
                var remaining = depthMax - cursor;
                var stripDepth = Mathf.Min(remaining, rowDepth);

                if (stripDepth < effectiveMinDepth)
                    break;

                cursor += stripDepth;
                divisions.Add(cursor);
            }

            // Absorb any remainder below the effective minimum into the previous strip
            // instead of adding a narrow tail. The old `> 1f` guard let remainders of
            // exactly 1m (or less) slip through and become 1m-deep sliver strips on the
            // block edge — lots too small for any building, later marked
            // "No building candidate fitted this lot".
            var remainder = depthMax - cursor;
            if (remainder <= 0f)
                return divisions;
            if (divisions.Count > 1 && remainder < effectiveMinDepth)
                divisions[divisions.Count - 1] = depthMax;
            else
                divisions.Add(depthMax);

            return divisions;
        }

        private static List<float> BuildLotFrontDivisions(
            float frontMin, float frontMax, float effectiveMinWidth, float lotWidthMax, System.Random rng)
        {
            var frontDivisions = new List<float> { frontMin };
            var cursor = frontMin;
            while (cursor < frontMax - effectiveMinWidth)
            {
                var remaining = frontMax - cursor;
                var rawWidth = effectiveMinWidth + (float)rng.NextDouble() * (lotWidthMax - effectiveMinWidth);
                var lotWidth = Mathf.Min(remaining, SnapUpToLotSizeGrid(rawWidth));

                if (lotWidth < effectiveMinWidth)
                    break;

                cursor += lotWidth;
                frontDivisions.Add(cursor);
            }

            // Absorb any remainder below the effective minimum into the previous column
            // instead of adding a narrow tail. The old `> 1f` guard let remainders of
            // exactly 1m (or less) slip through and become 1m-wide sliver columns on the
            // block edge (e.g. a 28m front -> 27m + 1m columns).
            var remainder = frontMax - cursor;
            if (remainder <= 0f)
                return frontDivisions;
            if (frontDivisions.Count > 1 && remainder < effectiveMinWidth)
                frontDivisions[frontDivisions.Count - 1] = frontMax;
            else
                frontDivisions.Add(frontMax);

            return frontDivisions;
        }
    }
}
#endif
