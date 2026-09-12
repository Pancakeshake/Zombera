#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    internal static partial class CityPrefabResidentialLotBuilder
    {

        // ── Landlocked lot merge ──────────────────────────────────────────

        /// <summary>
        ///     Folds interior grid cells (no block-boundary contact) into the
        ///     nearest road-touching lot in the same column.  Prevents landlocked
        ///     cells from being created in the first place.
        /// </summary>
        private static void FoldInteriorCellsIntoColumns(List<LotPlacement> lots, Rect block)
        {
            const float snap = 0.05f;
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = lots.Count - 1; i >= 0; i--)
                {
                    if (lots[i].IsCornerLot) continue;
                    if (TouchesBlockBoundary(lots[i].Bounds, block, snap)) continue;

                    // Find same-column road-touching lot.
                    var targetIndex = FindSameColumnBoundaryLot(lots, i, block, snap);
                    if (targetIndex < 0) continue;

                    var target = lots[targetIndex];
                    target.Bounds = RectUnion(target.Bounds, lots[i].Bounds);
                    lots[targetIndex] = target;
                    lots.RemoveAt(i);
                    changed = true;
                }
            }
        }

        /// <summary>
        ///     Splits lots whose width:depth or depth:width exceeds <paramref name="maxRatio"/>
        ///     along the long axis into roughly equal chunks.  Both halves must touch
        ///     a block boundary.  Repeats until stable.
        /// </summary>
        private static void SplitElongatedLots(List<LotPlacement> lots, Rect block,
            float minWidth, float minDepth, float maxRatio)
        {
            const float snap = 0.05f;
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = lots.Count - 1; i >= 0; i--)
                {
                    if (TrySplitElongatedLot(lots, i, block, minWidth, minDepth, maxRatio, snap))
                        changed = true;
                }
            }
        }

        private static bool TrySplitElongatedLot(
            List<LotPlacement> lots, int i, Rect block,
            float minWidth, float minDepth, float maxRatio, float snap)
        {
            if (lots[i].IsCornerLot || lots[i].IsCurvedLot) return false;
            var b = lots[i].Bounds;
            var ratio = Mathf.Max(b.width, b.height) / Mathf.Max(0.5f, Mathf.Min(b.width, b.height));
            if (ratio <= maxRatio) return false;

            if (!TrySplitRectByLongAxis(b, minWidth, minDepth, out var halfA, out var halfB))
                return false;

            if (!TouchesBlockBoundary(halfA, block, snap) ||
                !TouchesBlockBoundary(halfB, block, snap))
                return false;

            lots[i] = new LotPlacement { Bounds = halfA };
            lots.Insert(i + 1, new LotPlacement { Bounds = halfB });
            return true;
        }

        private static bool TrySplitRectByLongAxis(
            Rect b, float minWidth, float minDepth, out Rect halfA, out Rect halfB)
        {
            halfA = halfB = default;
            var splitVertically = b.width > b.height;
            // The SPLIT axis needs room for two above-minimum halves. The
            // perpendicular axis is unchanged by the split, so it only needs to
            // exist — requiring it above its minimum here blocked the splits that
            // row-wise sliver merging depends on: a below-min-width column then
            // stayed one tall lot whose AABB-union merges swallowed neighbours
            // (overlapping lots, e.g. blocks whose whole width < 2x min width).
            if (splitVertically && b.width >= minWidth * 2f && b.height > 0.01f)
            {
                var mid = b.xMin + b.width * 0.5f;
                halfA = Rect.MinMaxRect(b.xMin, b.yMin, mid, b.yMax);
                halfB = Rect.MinMaxRect(mid, b.yMin, b.xMax, b.yMax);
                return true;
            }
            if (!splitVertically && b.height >= minDepth * 2f && b.width > 0.01f)
            {
                var mid = b.yMin + b.height * 0.5f;
                halfA = Rect.MinMaxRect(b.xMin, b.yMin, b.xMax, mid);
                halfB = Rect.MinMaxRect(b.xMin, mid, b.xMax, b.yMax);
                return true;
            }
            return false;
        }

        private static int FindSameColumnBoundaryLot(List<LotPlacement> lots, int sourceIndex, Rect block, float snap)
        {
            var source = lots[sourceIndex].Bounds;
            for (var i = 0; i < lots.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (lots[i].IsCornerLot) continue;
                if (!TouchesBlockBoundary(lots[i].Bounds, block, snap)) continue;
                if (SameColumn(source, lots[i].Bounds, snap)) return i;
            }

            return -1;
        }

        /// <summary>
        ///     Absorbs landlocked lots without a max-size cap — road access
        ///     always wins.  SplitOversizedLots handles any resulting mega-lots.
        /// </summary>
        private static void AbsorbLandlockedLots(List<LotPlacement> lots, Rect block)
        {
            const float snap = 0.05f;
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = lots.Count - 1; i >= 0; i--)
                {
                    if (lots[i].IsCornerLot) continue;
                    if (TouchesBlockBoundary(lots[i].Bounds, block, snap)) continue;

                    // Find ANY adjacent neighbour without max cap.
                    var targetIndex = FindBestMergeTarget(lots, i, block, snap, float.MaxValue, float.MaxValue);
                    if (targetIndex < 0) continue;

                    var target = lots[targetIndex];
                    target.Bounds = RectUnion(target.Bounds, lots[i].Bounds);
                    lots[targetIndex] = target;
                    lots.RemoveAt(i);
                    changed = true;
                }
            }
        }

        /// <summary>
        ///     Final gate: forces every remaining landlocked lot into any adjacent
        ///     neighbour (ignoring all caps).  Logs an error if any persist.
        /// </summary>
        private static void EnsureAllLotsRoadAccessible(List<LotPlacement> lots, Rect block)
        {
            AbsorbLandlockedLots(lots, block);

            const float snap = 0.05f;
            for (var i = lots.Count - 1; i >= 0; i--)
            {
                if (lots[i].IsCornerLot) continue;
                if (TouchesBlockBoundary(lots[i].Bounds, block, snap)) continue;

                // Force-absorb into first adjacent neighbour.
                for (var j = 0; j < lots.Count; j++)
                {
                    if (j == i || lots[j].IsCornerLot) continue;
                    if (SharedEdgeLength(lots[i].Bounds, lots[j].Bounds, snap) < 0.5f) continue;
                    if (UnionOverlapsOtherLots(lots, RectUnion(lots[j].Bounds, lots[i].Bounds), i, j))
                        continue;
                    var target = lots[j];
                    target.Bounds = RectUnion(target.Bounds, lots[i].Bounds);
                    lots[j] = target;
                    lots.RemoveAt(i);
                    Debug.LogError("[CityPrefabResidentialLotBuilder] Force-absorbed landlocked lot " +
                                   i + " into neighbour " + j + ".  Regenerate areas + lots.");
                    break;
                }
            }
        }

        private static void MergeSliverLotsIntoNeighbors(List<LotPlacement> lots, Rect block,
            float minWidth, float minDepth, float maxWidth, float maxDepth)
        {
            const float snap = 0.05f;
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = lots.Count - 1; i >= 0; i--)
                {
                    if (lots[i].IsCornerLot) continue;
                    var b = lots[i].Bounds;
                    if (b.width >= minWidth && b.height >= minDepth) continue;

                    var targetIndex = FindBestMergeTarget(lots, i, block, snap, maxWidth, maxDepth);
                    // Last resort: absorb uncapped — a below-minimum lot must not
                    // survive. SplitOversizedLots runs afterwards and re-splits
                    // any oversized result back toward the district maximum.
                    if (targetIndex < 0)
                        targetIndex = FindBestMergeTarget(lots, i, block, snap, float.MaxValue, float.MaxValue);
                    if (targetIndex < 0) continue;

                    var target = lots[targetIndex];
                    target.Bounds = RectUnion(target.Bounds, b);
                    lots[targetIndex] = target;
                    lots.RemoveAt(i);
                    changed = true;
                }
            }
        }

        /// <summary>
        ///     Splits any lot exceeding max width or depth into two smaller rects
        ///     along the long axis.  Both halves must stay above minima and touch
        ///     at least one block boundary.  Repeats until stable.
        /// </summary>
        private static void SplitOversizedLots(List<LotPlacement> lots, Rect block,
            float minWidth, float minDepth, float maxWidth, float maxDepth, System.Random rng)
        {
            const float snap = 0.05f;
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = lots.Count - 1; i >= 0; i--)
                {
                    if (TrySplitOversizedLot(new OversizedSplitArgs
                        {
                            Lots = lots, Index = i, Block = block,
                            MinWidth = minWidth, MinDepth = minDepth,
                            MaxWidth = maxWidth, MaxDepth = maxDepth,
                            Rng = rng, Snap = snap
                        })) changed = true;
                }
            }
        }

        private static bool TrySplitOversizedLot(OversizedSplitArgs a)
        {
            if (a.Lots[a.Index].IsCornerLot || a.Lots[a.Index].IsCurvedLot) return false;
            var b = a.Lots[a.Index].Bounds;
            if (b.width <= a.MaxWidth && b.height <= a.MaxDepth) return false;

            if (!TrySplitRectByLongAxisRandom(b, a.MinWidth, a.MinDepth, a.Rng, out var halfA, out var halfB))
                return false;

            if (!TouchesBlockBoundary(halfA, a.Block, a.Snap) ||
                !TouchesBlockBoundary(halfB, a.Block, a.Snap))
                return false;

            a.Lots[a.Index] = new LotPlacement { Bounds = halfA };
            a.Lots.Insert(a.Index + 1, new LotPlacement { Bounds = halfB });
            return true;
        }

        private static bool TrySplitRectByLongAxisRandom(
            Rect b, float minWidth, float minDepth, System.Random rng, out Rect halfA, out Rect halfB)
        {
            halfA = halfB = default;
            var splitVertically = b.width > b.height;
            // Split axis needs room for two above-minimum halves; the unchanged
            // perpendicular axis only needs to exist (see TrySplitRectByLongAxis).
            if (splitVertically && b.width >= minWidth * 2f && b.height > 0.01f)
            {
                var splitX = b.xMin + minWidth +
                    (float)rng.NextDouble() * Mathf.Max(0.5f, b.width - minWidth * 2f);
                halfA = Rect.MinMaxRect(b.xMin, b.yMin, splitX, b.yMax);
                halfB = Rect.MinMaxRect(splitX, b.yMin, b.xMax, b.yMax);
                return true;
            }
            if (!splitVertically && b.height >= minDepth * 2f && b.width > 0.01f)
            {
                var splitZ = b.yMin + minDepth +
                    (float)rng.NextDouble() * Mathf.Max(0.5f, b.height - minDepth * 2f);
                halfA = Rect.MinMaxRect(b.xMin, b.yMin, b.xMax, splitZ);
                halfB = Rect.MinMaxRect(b.xMin, splitZ, b.xMax, b.yMax);
                return true;
            }
            return false;
        }

        // ── Rect utilities ────────────────────────────────────────────────

        private static bool TouchesBlockBoundary(Rect lot, Rect block, float snap)
        {
            return Mathf.Abs(lot.xMin - block.xMin) < snap ||
                   Mathf.Abs(lot.xMax - block.xMax) < snap ||
                   Mathf.Abs(lot.yMin - block.yMin) < snap ||
                   Mathf.Abs(lot.yMax - block.yMax) < snap;
        }

        /// <summary>
        ///     True when <paramref name="union" /> overlaps any lot other than the source
        ///     and candidate target. AABB unions of L-shaped cell groups can swallow
        ///     lots that remain in the list — such merges must never be chosen or the
        ///     subdivision produces overlapping lots (buildings then sit on fences).
        /// </summary>
        private static bool UnionOverlapsOtherLots(List<LotPlacement> lots, Rect union, int sourceIndex, int targetIndex)
        {
            for (var k = 0; k < lots.Count; k++)
            {
                if (k == sourceIndex || k == targetIndex) continue;
                var other = lots[k].Bounds;
                var ix = Mathf.Min(union.xMax, other.xMax) - Mathf.Max(union.xMin, other.xMin);
                var iz = Mathf.Min(union.yMax, other.yMax) - Mathf.Max(union.yMin, other.yMin);
                if (ix > 0.05f && iz > 0.05f)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Finds the best neighbour to absorb a lot.
        ///     Score: boundary touch (+1000) + same-column (+500) + shared edge − area penalty.
        ///     Rejects merges that would exceed max dimensions or overlap a third lot.
        /// </summary>
        private static int FindBestMergeTarget(List<LotPlacement> lots, int sourceIndex, Rect block, float snap,
            float maxWidth, float maxDepth)
        {
            var source = lots[sourceIndex].Bounds;
            var bestIndex = -1;
            var bestScore = float.MinValue;

            for (var i = 0; i < lots.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (lots[i].IsCornerLot) continue;

                if (UnionOverlapsOtherLots(lots, RectUnion(source, lots[i].Bounds), sourceIndex, i))
                    continue;

                var score = ScoreMergeTarget(source, lots[i].Bounds, block, snap, maxWidth, maxDepth);
                if (float.IsNegativeInfinity(score)) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float ScoreMergeTarget(
            Rect source, Rect neighbor, Rect block, float snap, float maxWidth, float maxDepth)
        {
            var sharedEdgeLen = SharedEdgeLength(source, neighbor, snap);
            if (sharedEdgeLen < 0.5f) return float.NegativeInfinity;

            var union = RectUnion(neighbor, source);
            if (union.width > maxWidth + 0.5f || union.height > maxDepth + 0.5f)
                return float.NegativeInfinity;

            var score = sharedEdgeLen;
            if (TouchesBlockBoundary(neighbor, block, snap)) score += 1000f;
            if (SameColumn(source, neighbor, snap)) score += 500f;
            score -= (neighbor.width * neighbor.height) * 0.001f;
            return score;
        }

        private static bool SameColumn(Rect a, Rect b, float snap)
        {
            return Mathf.Abs(a.xMin - b.xMin) < snap && Mathf.Abs(a.xMax - b.xMax) < snap;
        }

        private static float SharedEdgeLength(Rect a, Rect b, float snap)
        {
            var overlapX = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
            var overlapZ = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

            // Vertical adjacency: rects share X range and Z edges touch.
            if (overlapX > 0.5f && (Mathf.Abs(a.yMax - b.yMin) < snap || Mathf.Abs(b.yMax - a.yMin) < snap))
                return overlapX;

            // Horizontal adjacency: rects share Z range and X edges touch.
            if (overlapZ > 0.5f && (Mathf.Abs(a.xMax - b.xMin) < snap || Mathf.Abs(b.xMax - a.xMin) < snap))
                return overlapZ;

            return 0f;
        }

        private static Rect RectUnion(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

    }
}
#endif
