using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Block-level commercial layout: store lots trace a U / L / strip along
    ///     the block edges while the interior becomes parking in front of the
    ///     stores. Works in the same rect space as the lot subdivision step.
    ///     The U (court) opens toward the city center; the L hugs one corner;
    ///     the strip runs along the downtown-facing edge.
    /// </summary>
    public static partial class CityCommercialBlockLayoutPlanner
    {
        private const float MinFrontage = 14f;
        private const float MinCross = 16f;
        private const float UMinFrontage = 40f;
        private const float UMinCross = 30f;
        private const float CornerOtherFrontage = 27f;
        private const float MinParking = 4f;
        private static readonly float[] WidthPool = { 12f, 15f, 18f, 24f };

        private static float Walkway => CityCommercialLotLayoutPlanner.WalkwayDepthMeters;

        /// <summary>
        ///     Store front inset at block scale — the placement fit check requires
        ///     ≥ 0.3 m on every slot edge, so buildings sit this far back from the
        ///     band's street / parking edge (0.35 > 0.3 with float guard).
        /// </summary>
        private const float FrontInset = 0.35f;

        /// <summary>Store band thickness: front inset + 12 m piece + 0.35 m tail (12.7).</summary>
        private const float StoreBandSpan = FrontInset + 12f + 0.35f;

        /// <summary>
        ///     U front corner slot depth — longer than the leg band it meets at a
        ///     right angle so the corner shop extends past the branch and keeps
        ///     room for its door. 0.35 inset + 15 m piece + 0.1 tolerance slack.
        /// </summary>
        private const float CornerLotDepth = 15.3f;

        public sealed class StoreLotSpec
        {
            public Rect Rect;
            public BlockFace Face;
            public CommercialLotKind Kind = CommercialLotKind.StoreLot;
        }

        public sealed class Plan
        {
            public CommercialLayoutType Style;
            /// <summary>
            ///     Direction parked cars face toward the primary store frontage
            ///     (equals the block's street-edge face for strip / L / U).
            /// </summary>
            public BlockFace ParkingCarFacing;
            public readonly List<StoreLotSpec> StoreLots = new();
            public readonly List<Rect> Walkways = new();
            public readonly List<Rect> Parking = new();
        }

        private sealed class Run
        {
            public Rect Band;       // full cross band across the block
            public Vector2 Start;   // where the run begins (along-axis)
            public Vector2 Dir;     // unit direction the run extends in
            public float Length;    // usable run length
            public BlockFace Face;  // parking-facing side of the store lots
        }

        /// <summary>
        ///     Builds the block layout, or null when the block is too small for a
        ///     store layout (caller falls back to the legacy per-lot path).
        /// </summary>
        public static Plan TryBuild(Rect block, Vector2 cityCenter, int seed)
        {
            var face = ResolvePrimaryFace(block, cityCenter);
            var frontage = FrontageLength(block, face);
            var cross = CrossLength(block, face);
            if (frontage < MinFrontage || cross < MinCross)
                return null;

            var rng = new System.Random(seed);

            if (frontage >= UMinFrontage && cross >= UMinCross)
                return BuildCourt(block, face, rng);

            if (cross >= CornerOtherFrontage)
                return BuildCorner(block, face, rng.Next(2) == 0, rng);

            return BuildStrip(block, face, rng);
        }

        // ── Styles ─────────────────────────────────────────────────

        private static Plan BuildStrip(Rect block, BlockFace face, System.Random rng)
        {
            var plan = new Plan { Style = CommercialLayoutType.Strip, ParkingCarFacing = face };
            plan.Walkways.Add(EdgeStripRect(block, face, Walkway));
            PackRun(plan, StripRun(block, face), rng, paintLeftover: true, centerLeftover: true);

            var parking = InsetRectFromFace(block, face, Walkway + StoreBandSpan);
            if (Mathf.Min(parking.width, parking.height) >= MinParking)
                plan.Parking.Add(parking);
            return plan;
        }

        private static Plan BuildCorner(Rect block, BlockFace face, bool leftSide, System.Random rng)
        {
            var plan = new Plan { Style = CommercialLayoutType.Corner, ParkingCarFacing = face };
            var corner = CornerRuns(block, face, leftSide);
            plan.Walkways.Add(EdgeStripRect(block, face, Walkway));
            plan.Walkways.Add(EdgeStripRect(block, corner.SecondFace, Walkway));
            PackRun(plan, corner.Primary, rng, paintLeftover: true);
            PackRun(plan, corner.Secondary, rng, paintLeftover: true);

            var parking = InsetRectFromFace(block, face, Walkway + StoreBandSpan);
            parking = InsetRectFromFace(parking, corner.SecondFace, Walkway + StoreBandSpan);
            if (Mathf.Min(parking.width, parking.height) >= MinParking)
                plan.Parking.Add(parking);
            return plan;
        }

        private static Plan BuildCourt(Rect block, BlockFace face, System.Random rng)
        {
            var plan = new Plan { Style = CommercialLayoutType.Court, ParkingCarFacing = face };
            plan.Walkways.Add(EdgeStripRect(block, face, Walkway));

            // U stores: long front row faces the courtyard; side legs face the
            // courtyard; corner shops match the adjacent leg (not U centre).
            var frontFace = OppositeFace(face);
            AddCourtFrontCornerLots(plan, block, face);
            var legStart = FrontRowBack(block, face);
            var legLength = CrossLength(block, face) - Walkway - CornerLotDepth;
            switch (face)
            {
                case BlockFace.South:
                    PackRun(plan, HorizontalRun(frontFace, block, block.xMin + StoreBandSpan, block.yMin + Walkway, block.width - StoreBandSpan * 2f, true), rng, paintLeftover: true, centerLeftover: true);
                    PackRun(plan, VerticalRun(BlockFace.East, block, legStart, block.xMin, legLength, true), rng, paintLeftover: true);
                    PackRun(plan, VerticalRun(BlockFace.West, block, legStart, block.xMax - StoreBandSpan, legLength, true), rng, paintLeftover: true);
                    break;
                case BlockFace.North:
                    PackRun(plan, HorizontalRun(frontFace, block, block.xMin + StoreBandSpan, block.yMax - Walkway - StoreBandSpan, block.width - StoreBandSpan * 2f, true), rng, paintLeftover: true, centerLeftover: true);
                    PackRun(plan, VerticalRun(BlockFace.East, block, legStart, block.xMin, legLength, false), rng, paintLeftover: true);
                    PackRun(plan, VerticalRun(BlockFace.West, block, legStart, block.xMax - StoreBandSpan, legLength, false), rng, paintLeftover: true);
                    break;
                case BlockFace.West:
                    PackRun(plan, VerticalRun(frontFace, block, block.yMin + StoreBandSpan, block.xMin + Walkway, block.height - StoreBandSpan * 2f, true), rng, paintLeftover: true, centerLeftover: true);
                    PackRun(plan, HorizontalRun(BlockFace.North, block, legStart, block.yMin, legLength, true), rng, paintLeftover: true);
                    PackRun(plan, HorizontalRun(BlockFace.South, block, legStart, block.yMax - StoreBandSpan, legLength, true), rng, paintLeftover: true);
                    break;
                default:
                    PackRun(plan, VerticalRun(frontFace, block, block.yMin + StoreBandSpan, block.xMax - Walkway - StoreBandSpan, block.height - StoreBandSpan * 2f, true), rng, paintLeftover: true, centerLeftover: true);
                    PackRun(plan, HorizontalRun(BlockFace.North, block, legStart, block.yMin, legLength, false), rng, paintLeftover: true);
                    PackRun(plan, HorizontalRun(BlockFace.South, block, legStart, block.yMax - StoreBandSpan, legLength, false), rng, paintLeftover: true);
                    break;
            }

            var courtyard = CourtCourtyardRect(block, face);
            if (Mathf.Min(courtyard.width, courtyard.height) >= MinParking)
                plan.Parking.Add(courtyard);
            return plan;
        }

        /// <summary>
        ///     The two corner stores at the street ends of the long front row.
        ///     Face the same direction as the adjacent long U leg (not the U
        ///     centre) so doors continue the side-branch orientation; deeper than
        ///     the leg band so the corner shop extends past the branch.
        /// </summary>
        private static void AddCourtFrontCornerLots(Plan plan, Rect block, BlockFace face)
        {
            switch (face)
            {
                case BlockFace.South:
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMin, block.yMin + Walkway, block.xMin + StoreBandSpan, block.yMin + Walkway + CornerLotDepth), Face = BlockFace.East, Kind = CommercialLotKind.StoreLotCorner });
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMax - StoreBandSpan, block.yMin + Walkway, block.xMax, block.yMin + Walkway + CornerLotDepth), Face = BlockFace.West, Kind = CommercialLotKind.StoreLotCorner });
                    break;
                case BlockFace.North:
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMin, block.yMax - Walkway - CornerLotDepth, block.xMin + StoreBandSpan, block.yMax - Walkway), Face = BlockFace.East, Kind = CommercialLotKind.StoreLotCorner });
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMax - StoreBandSpan, block.yMax - Walkway - CornerLotDepth, block.xMax, block.yMax - Walkway), Face = BlockFace.West, Kind = CommercialLotKind.StoreLotCorner });
                    break;
                case BlockFace.West:
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMin + Walkway, block.yMin, block.xMin + Walkway + CornerLotDepth, block.yMin + StoreBandSpan), Face = BlockFace.North, Kind = CommercialLotKind.StoreLotCorner });
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMin + Walkway, block.yMax - StoreBandSpan, block.xMin + Walkway + CornerLotDepth, block.yMax), Face = BlockFace.South, Kind = CommercialLotKind.StoreLotCorner });
                    break;
                default:
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMax - Walkway - CornerLotDepth, block.yMin, block.xMax - Walkway, block.yMin + StoreBandSpan), Face = BlockFace.North, Kind = CommercialLotKind.StoreLotCorner });
                    plan.StoreLots.Add(new StoreLotSpec { Rect = Rect.MinMaxRect(block.xMax - Walkway - CornerLotDepth, block.yMax - StoreBandSpan, block.xMax - Walkway, block.yMax), Face = BlockFace.South, Kind = CommercialLotKind.StoreLotCorner });
                    break;
            }
        }

        /// <summary>Along-axis coordinate of the corner lots' courtyard edge (where the legs start).</summary>
        private static float FrontRowBack(Rect block, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => block.yMin + Walkway + CornerLotDepth,
                BlockFace.North => block.yMax - Walkway - CornerLotDepth,
                BlockFace.West  => block.xMin + Walkway + CornerLotDepth,
                _               => block.xMax - Walkway - CornerLotDepth
            };
        }

        private static BlockFace OppositeFace(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => BlockFace.North,
                BlockFace.North => BlockFace.South,
                BlockFace.West  => BlockFace.East,
                _               => BlockFace.West
            };
        }

        // ── Store lot packing ──────────────────────────────────────

        /// <summary>
        ///     Packs contiguous store lots along a run from the 12/15/18/24 m pool.
        ///     Tries several shuffled orders and keeps the best fill. When
        ///     <paramref name="centerLeftover"/> is set, the packed row is shifted
        ///     so any unfillable remainder splits evenly at both ends; with
        ///     <paramref name="paintLeftover"/> the remainder is painted as
        ///     parking so store rows read seamless.
        /// </summary>
        private static void PackRun(
            Plan plan, Run run, System.Random rng, bool paintLeftover, bool centerLeftover = false)
        {
            const int attempts = 4;
            var bestSlots = new List<Rect>();
            var bestLeftover = float.MaxValue;

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var pool = new List<float>(WidthPool.Length);
                for (var i = 0; i < WidthPool.Length; i++)
                    pool.Add(WidthPool[i]);
                for (var i = pool.Count - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }

                var slots = new List<Rect>(8);
                var used = 0f;
                while (run.Length - used >= 12f)
                {
                    var remaining = run.Length - used;
                    var chosen = -1;
                    var chosenWidth = 0f;
                    for (var i = 0; i < pool.Count; i++)
                    {
                        var width = pool[i];
                        if (width > remaining + 0.1f)
                            continue;

                        if (remaining - width < 12f)
                        {
                            chosen = i; // nearly completes the run — prefer it
                            chosenWidth = width;
                            break;
                        }

                        if (chosen < 0)
                        {
                            chosen = i;
                            chosenWidth = width;
                        }
                    }

                    if (chosen < 0)
                        break;

                    slots.Add(MakeSlot(run, used, chosenWidth));
                    used += chosenWidth;
                }

                var leftover = run.Length - used;
                if (leftover >= bestLeftover)
                    continue;

                bestLeftover = leftover;
                bestSlots = slots;
            }

            var offset = centerLeftover ? bestLeftover * 0.5f : 0f;
            for (var i = 0; i < bestSlots.Count; i++)
            {
                var rect = bestSlots[i];
                plan.StoreLots.Add(new StoreLotSpec { Rect = ShiftSlot(run, rect, offset), Face = run.Face });
            }

            AddLeftoverParking(plan, run, run.Length - bestLeftover, bestLeftover, centerLeftover, paintLeftover);
        }

        private static Rect ShiftSlot(Run run, Rect slot, float offset)
        {
            if (run.Dir.x > 0f) return new Rect(slot.x + offset, slot.y, slot.width, slot.height);
            if (run.Dir.x < 0f) return new Rect(slot.x - offset, slot.y, slot.width, slot.height);
            if (run.Dir.y > 0f) return new Rect(slot.x, slot.y + offset, slot.width, slot.height);
            return new Rect(slot.x, slot.y - offset, slot.width, slot.height);
        }

        private static void AddLeftoverParking(
            Plan plan, Run run, float usedAlong, float leftover, bool centered, bool paintLeftover)
        {
            if (!paintLeftover || leftover < 1f)
                return;

            if (!centered)
            {
                plan.Parking.Add(LeftoverRect(run, usedAlong, leftover));
                return;
            }

            var half = leftover * 0.5f;
            plan.Parking.Add(LeftoverRect(run, 0f, half));
            plan.Parking.Add(LeftoverRect(run, run.Length - half, half));
        }

        /// <summary>Band rectangle along the run from startAlong for length metres.</summary>
        private static Rect LeftoverRect(Run run, float startAlong, float length)
        {
            if (run.Dir.x != 0f)
            {
                var x0 = run.Start.x + run.Dir.x * startAlong;
                var x1 = x0 + run.Dir.x * length;
                if (x0 > x1) (x0, x1) = (x1, x0);
                return Rect.MinMaxRect(x0, run.Band.yMin, x1, run.Band.yMax);
            }

            var y0 = run.Start.y + run.Dir.y * startAlong;
            var y1 = y0 + run.Dir.y * length;
            if (y0 > y1) (y0, y1) = (y1, y0);
            return Rect.MinMaxRect(run.Band.xMin, y0, run.Band.xMax, y1);
        }

        private static Rect MakeSlot(Run run, float startAlong, float width)
        {
            if (run.Dir.x != 0f)
            {
                var x0 = run.Start.x + run.Dir.x * startAlong;
                var x1 = x0 + run.Dir.x * width;
                if (x0 > x1) (x0, x1) = (x1, x0);
                return Rect.MinMaxRect(x0, run.Band.yMin, x1, run.Band.yMax);
            }

            var y0 = run.Start.y + run.Dir.y * startAlong;
            var y1 = y0 + run.Dir.y * width;
            if (y0 > y1) (y0, y1) = (y1, y0);
            return Rect.MinMaxRect(run.Band.xMin, y0, run.Band.xMax, y1);
        }

        // ── Run construction ────────────────────────────────────────

        private static Run HorizontalRun(BlockFace face, Rect block, float startX, float bandYMin, float length, bool towardPositiveX)
        {
            return new Run
            {
                Band = Rect.MinMaxRect(block.xMin, bandYMin, block.xMax, bandYMin + StoreBandSpan),
                Start = new Vector2(startX, bandYMin),
                Dir = towardPositiveX ? Vector2.right : Vector2.left,
                Length = length,
                Face = face
            };
        }

        private static Run VerticalRun(BlockFace face, Rect block, float startY, float bandXMin, float length, bool towardPositiveZ)
        {
            return new Run
            {
                Band = Rect.MinMaxRect(bandXMin, block.yMin, bandXMin + StoreBandSpan, block.yMax),
                Start = new Vector2(bandXMin, startY),
                Dir = towardPositiveZ ? Vector2.up : Vector2.down,
                Length = length,
                Face = face
            };
        }

        private static Run StripRun(Rect block, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => HorizontalRun(face, block, block.xMin, block.yMin + Walkway, block.width, true),
                BlockFace.North => HorizontalRun(face, block, block.xMin, block.yMax - Walkway - StoreBandSpan, block.width, true),
                BlockFace.West  => VerticalRun(face, block, block.yMin, block.xMin + Walkway, block.height, true),
                _               => VerticalRun(face, block, block.yMin, block.xMax - Walkway - StoreBandSpan, block.height, true)
            };
        }

        private struct CornerPair
        {
            public Run Primary;
            public Run Secondary;
            public BlockFace SecondFace;
        }

        /// <summary>
        ///     Two runs meeting at one end of the primary street edge. Door faces
        ///     point inward toward the parking courtyard (lot centre), while
        ///     <see cref="CornerPair.SecondFace"/> stays on the street edge for
        ///     walkway/parking insets.
        /// </summary>
        private static CornerPair CornerRuns(Rect block, BlockFace face, bool leftSide)
        {
            var inwardPrimary = OppositeFace(face);
            switch (face)
            {
                case BlockFace.South:
                    if (leftSide)
                        return new CornerPair
                        {
                            Primary = HorizontalRun(inwardPrimary, block, block.xMin + Walkway, block.yMin + Walkway, block.width - Walkway, true),
                            Secondary = VerticalRun(OppositeFace(BlockFace.West), block, block.yMin + CornerOffset, block.xMin + Walkway, block.height - CornerOffset, true),
                            SecondFace = BlockFace.West
                        };
                    return new CornerPair
                    {
                        Primary = HorizontalRun(inwardPrimary, block, block.xMax - Walkway, block.yMin + Walkway, block.width - Walkway, false),
                        Secondary = VerticalRun(OppositeFace(BlockFace.East), block, block.yMin + CornerOffset, block.xMax - Walkway - StoreBandSpan, block.height - CornerOffset, true),
                        SecondFace = BlockFace.East
                    };

                case BlockFace.North:
                    if (leftSide)
                        return new CornerPair
                        {
                            Primary = HorizontalRun(inwardPrimary, block, block.xMin + Walkway, block.yMax - Walkway - StoreBandSpan, block.width - Walkway, true),
                            Secondary = VerticalRun(OppositeFace(BlockFace.West), block, block.yMax - CornerOffset, block.xMin + Walkway, block.height - CornerOffset, false),
                            SecondFace = BlockFace.West
                        };
                    return new CornerPair
                    {
                        Primary = HorizontalRun(inwardPrimary, block, block.xMax - Walkway, block.yMax - Walkway - StoreBandSpan, block.width - Walkway, false),
                        Secondary = VerticalRun(OppositeFace(BlockFace.East), block, block.yMax - CornerOffset, block.xMax - Walkway - StoreBandSpan, block.height - CornerOffset, false),
                        SecondFace = BlockFace.East
                    };

                case BlockFace.West:
                    if (leftSide)
                        return new CornerPair
                        {
                            Primary = VerticalRun(inwardPrimary, block, block.yMin + Walkway, block.xMin + Walkway, block.height - Walkway, true),
                            Secondary = HorizontalRun(OppositeFace(BlockFace.South), block, block.xMin + CornerOffset, block.yMin + Walkway, block.width - CornerOffset, true),
                            SecondFace = BlockFace.South
                        };
                    return new CornerPair
                    {
                        Primary = VerticalRun(inwardPrimary, block, block.yMax - Walkway, block.xMin + Walkway, block.height - Walkway, false),
                        Secondary = HorizontalRun(OppositeFace(BlockFace.North), block, block.xMin + CornerOffset, block.yMax - Walkway - StoreBandSpan, block.width - CornerOffset, true),
                        SecondFace = BlockFace.North
                    };

                default:
                    if (leftSide)
                        return new CornerPair
                        {
                            Primary = VerticalRun(inwardPrimary, block, block.yMin + Walkway, block.xMax - Walkway - StoreBandSpan, block.height - Walkway, true),
                            Secondary = HorizontalRun(OppositeFace(BlockFace.South), block, block.xMax - CornerOffset, block.yMin + Walkway, block.width - CornerOffset, false),
                            SecondFace = BlockFace.South
                        };
                    return new CornerPair
                    {
                        Primary = VerticalRun(inwardPrimary, block, block.yMax - Walkway, block.xMax - Walkway - StoreBandSpan, block.height - Walkway, false),
                        Secondary = HorizontalRun(OppositeFace(BlockFace.North), block, block.xMax - CornerOffset, block.yMax - Walkway - StoreBandSpan, block.width - CornerOffset, false),
                        SecondFace = BlockFace.North
                    };
            }
        }

        /// <summary>
        ///     Block-layout corner offset: the secondary run starts exactly at the
        ///     primary band's building back wall (walkway + inset + 12 m piece).
        /// </summary>
        private static float CornerOffset => Walkway + FrontInset + CityCommercialLotLayoutPlanner.ShopDepthMeters;
    }
}
