using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Run packing + slot math for <see cref="CityCommercialLotLayoutPlanner"/>.
    ///     Runs are straight lines of slot rects along a lot edge; pieces are
    ///     packed flush end-to-end so neighbouring shops share walls.
    /// </summary>
    public static partial class CityCommercialLotLayoutPlanner
    {
        private sealed class SlotPack
        {
            public Rect Rect;
            public BlockFace Face;
            public CityAssembledBuildingCatalogEntry Entry;
        }

        /// <summary>A straight band along a lot edge that shops pack into.</summary>
        private sealed class RunDef
        {
            public Rect Band;       // full-lot strip the run occupies on the cross axis
            public Vector2 Start;   // where the run begins (along-axis coordinate)
            public Vector2 Dir;     // unit direction the run extends in
            public float Length;    // usable run length
            public BlockFace Face;  // street face every slot in the run faces
        }

        private sealed class PieceInfo
        {
            public CityAssembledBuildingCatalogEntry Entry;
            public float Along; // world extent along the run axis (after door-yaw rotation)
        }

        private struct RunPair
        {
            public RunDef Primary;
            public RunDef Secondary;
        }

        // ── Packing ─────────────────────────────────────────────────

        /// <summary>
        ///     Filters candidates to pieces that fit the run band, computing each
        ///     piece's extents after its door-yaw rotation (a piece whose door sits
        ///     on a side wall occupies swapped extents in world space). When
        ///     <paramref name="requiredCross"/> is set, only pieces matching that
        ///     exact depth are kept (corner slots that must fill a fixed depth).
        ///     When <paramref name="ignoreCrossCap"/> is true (store-lot fill),
        ///     roof-overhang pieces that exceed ShopDepthMeters are still accepted
        ///     because placement scales them to the lot.
        /// </summary>
        private static List<PieceInfo> ResolvePieces(
            IReadOnlyList<CityAssembledBuildingCatalogEntry> candidates,
            float? requiredCross = null,
            bool ignoreCrossCap = false)
        {
            var pieces = new List<PieceInfo>(candidates?.Count ?? 0);
            if (candidates == null)
                return pieces;

            for (var i = 0; i < candidates.Count; i++)
            {
                var entry = candidates[i];
                if (entry?.prefab == null)
                    continue;

                var w = entry.ResolvePlacementWidth(false);
                var d = entry.ResolvePlacementDepth(false);
                if (CityBuildingPrefabFootprintUtility.TryMeasureFootprint(entry.prefab, out var measured) &&
                    measured.IsValid)
                {
                    w = measured.WidthMeters;
                    d = measured.DepthMeters;
                }

                if (w < 0.5f || d < 0.5f)
                    continue;

                var doorYaw = entry.doorYawResolved ? entry.doorYawOffsetDegrees : entry.yawOffsetDegrees;
                var door = CityBuildingRoadFacingUtility.GetDoorYawOffset(entry.prefab, doorYaw);
                var normalized = ((door % 360f) + 360f) % 360f;
                var swapped = Mathf.Abs(normalized - 90f) < 0.1f || Mathf.Abs(normalized - 270f) < 0.1f;

                var along = swapped ? d : w;
                var cross = swapped ? w : d;
                if (requiredCross.HasValue)
                {
                    if (Mathf.Abs(cross - requiredCross.Value) > 0.5f)
                        continue;
                }
                else if (!ignoreCrossCap && cross > ShopDepthMeters + 0.5f)
                {
                    continue;
                }

                if (along < MinShopMeters)
                    continue;

                pieces.Add(new PieceInfo { Entry = entry, Along = along });
            }

            return pieces;
        }

        /// <summary>
        ///     Greedy end-to-end packing: pieces are shuffled for variety, then the
        ///     first fitting piece is placed flush against the previous one. Pieces
        ///     that nearly complete the run are preferred so runs end with small,
        ///     parking-sized gaps instead of dead space.
        /// </summary>
        private static void PackRun(RunDef run, List<PieceInfo> pieces, System.Random rng, List<SlotPack> output)
        {
            if (pieces.Count == 0 || run.Length < MinShopMeters)
                return;

            // Shuffled working copy — used pieces are consumed so a run never
            // repeats the same prefab twice (varied shop fronts along the run).
            var pool = new List<PieceInfo>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
                pool.Add(pieces[i]);
            Shuffle(pool, rng);

            var used = 0f;
            while (run.Length - used >= MinShopMeters)
            {
                var remaining = run.Length - used;
                var chosen = -1;
                var chosenAlong = 0f;
                for (var i = 0; i < pool.Count; i++)
                {
                    var along = pool[i].Along;
                    if (along > remaining + 0.1f)
                        continue;

                    if (remaining - along < MinShopMeters)
                    {
                        chosen = i;
                        chosenAlong = along;
                        break;
                    }

                    if (chosen < 0)
                    {
                        chosen = i;
                        chosenAlong = along;
                    }
                }

                if (chosen < 0)
                    break;

                output.Add(new SlotPack
                {
                    Rect = MakeSlot(run, used, chosenAlong),
                    Face = run.Face,
                    Entry = pool[chosen].Entry
                });
                used += chosenAlong;
                pool.RemoveAt(chosen);
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static Rect MakeSlot(RunDef run, float startAlong, float width)
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

        private static RunDef HorizontalRun(BlockFace face, Rect lot, float startX, float bandYMin, float length, bool towardPositiveX)
        {
            return new RunDef
            {
                Band = Rect.MinMaxRect(lot.xMin, bandYMin, lot.xMax, bandYMin + SlotCrossSpan),
                Start = new Vector2(startX, bandYMin),
                Dir = towardPositiveX ? Vector2.right : Vector2.left,
                Length = length,
                Face = face
            };
        }

        private static RunDef VerticalRun(BlockFace face, Rect lot, float startY, float bandXMin, float length, bool towardPositiveZ)
        {
            return new RunDef
            {
                Band = Rect.MinMaxRect(bandXMin, lot.yMin, bandXMin + SlotCrossSpan, lot.yMax),
                Start = new Vector2(bandXMin, startY),
                Dir = towardPositiveZ ? Vector2.up : Vector2.down,
                Length = length,
                Face = face
            };
        }

        private static RunDef StripRun(Rect lot, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => HorizontalRun(face, lot, lot.xMin, lot.yMin + WalkwayDepthMeters, lot.width, true),
                BlockFace.North => HorizontalRun(face, lot, lot.xMin, lot.yMax - WalkwayDepthMeters - SlotCrossSpan, lot.width, true),
                BlockFace.West  => VerticalRun(face, lot, lot.yMin, lot.xMin + WalkwayDepthMeters, lot.height, true),
                _               => VerticalRun(face, lot, lot.yMin, lot.xMax - WalkwayDepthMeters - SlotCrossSpan, lot.height, true)
            };
        }

        /// <summary>The rear-edge run shared by court (U) back strips and gas station shops.</summary>
        private static RunDef CourtBackRun(Rect lot, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => HorizontalRun(face, lot, lot.xMin, lot.yMax - SlotCrossSpan, lot.width, true),
                BlockFace.North => HorizontalRun(face, lot, lot.xMin, lot.yMin, lot.width, true),
                BlockFace.West  => VerticalRun(face, lot, lot.yMin, lot.xMax - SlotCrossSpan, lot.height, true),
                _               => VerticalRun(face, lot, lot.yMin, lot.xMin, lot.height, true)
            };
        }

        /// <summary>
        ///     Corner L: two runs starting at the shared street corner. Door faces
        ///     point inward toward the lot centre (parking courtyard). The secondary
        ///     run is offset by the primary band thickness so the two first shops
        ///     meet back-edge to front-edge at 90° without overlapping.
        /// </summary>
        private static RunPair CornerRuns(Rect lot, BlockFace face, BlockFace cornerFace)
        {
            var inwardPrimary = OppositeFace(face);
            var inwardSecondary = OppositeFace(cornerFace);
            switch (face)
            {
                case BlockFace.South:
                    if (cornerFace == BlockFace.East)
                        return new RunPair
                        {
                            Primary = HorizontalRun(inwardPrimary, lot, lot.xMax - WalkwayDepthMeters, lot.yMin + WalkwayDepthMeters, lot.width - WalkwayDepthMeters, false),
                            Secondary = VerticalRun(inwardSecondary, lot, lot.yMin + CornerRunOffset, lot.xMax - WalkwayDepthMeters - SlotCrossSpan, lot.height - CornerRunOffset, true)
                        };
                    return new RunPair
                    {
                        Primary = HorizontalRun(inwardPrimary, lot, lot.xMin + WalkwayDepthMeters, lot.yMin + WalkwayDepthMeters, lot.width - WalkwayDepthMeters, true),
                        Secondary = VerticalRun(inwardSecondary, lot, lot.yMin + CornerRunOffset, lot.xMin + WalkwayDepthMeters, lot.height - CornerRunOffset, true)
                    };

                case BlockFace.North:
                    if (cornerFace == BlockFace.East)
                        return new RunPair
                        {
                            Primary = HorizontalRun(inwardPrimary, lot, lot.xMax - WalkwayDepthMeters, lot.yMax - WalkwayDepthMeters - SlotCrossSpan, lot.width - WalkwayDepthMeters, false),
                            Secondary = VerticalRun(inwardSecondary, lot, lot.yMax - CornerRunOffset, lot.xMax - WalkwayDepthMeters - SlotCrossSpan, lot.height - CornerRunOffset, false)
                        };
                    return new RunPair
                    {
                        Primary = HorizontalRun(inwardPrimary, lot, lot.xMin + WalkwayDepthMeters, lot.yMax - WalkwayDepthMeters - SlotCrossSpan, lot.width - WalkwayDepthMeters, true),
                        Secondary = VerticalRun(inwardSecondary, lot, lot.yMax - CornerRunOffset, lot.xMin + WalkwayDepthMeters, lot.height - CornerRunOffset, false)
                    };

                case BlockFace.West:
                    if (cornerFace == BlockFace.South)
                        return new RunPair
                        {
                            Primary = VerticalRun(inwardPrimary, lot, lot.yMin + WalkwayDepthMeters, lot.xMin + WalkwayDepthMeters, lot.height - WalkwayDepthMeters, true),
                            Secondary = HorizontalRun(inwardSecondary, lot, lot.xMin + CornerRunOffset, lot.yMin + WalkwayDepthMeters, lot.width - CornerRunOffset, true)
                        };
                    return new RunPair
                    {
                        Primary = VerticalRun(inwardPrimary, lot, lot.yMax - WalkwayDepthMeters, lot.xMin + WalkwayDepthMeters, lot.height - WalkwayDepthMeters, false),
                        Secondary = HorizontalRun(inwardSecondary, lot, lot.xMin + CornerRunOffset, lot.yMax - WalkwayDepthMeters - SlotCrossSpan, lot.width - CornerRunOffset, true)
                    };

                default:
                    if (cornerFace == BlockFace.South)
                        return new RunPair
                        {
                            Primary = VerticalRun(inwardPrimary, lot, lot.yMin + WalkwayDepthMeters, lot.xMax - WalkwayDepthMeters - SlotCrossSpan, lot.height - WalkwayDepthMeters, true),
                            Secondary = HorizontalRun(inwardSecondary, lot, lot.xMax - CornerRunOffset, lot.yMin + WalkwayDepthMeters, lot.width - CornerRunOffset, false)
                        };
                    return new RunPair
                    {
                        Primary = VerticalRun(inwardPrimary, lot, lot.yMax - WalkwayDepthMeters, lot.xMax - WalkwayDepthMeters - SlotCrossSpan, lot.height - WalkwayDepthMeters, false),
                        Secondary = HorizontalRun(inwardSecondary, lot, lot.xMax - CornerRunOffset, lot.yMax - WalkwayDepthMeters - SlotCrossSpan, lot.width - CornerRunOffset, false)
                    };
            }
        }

        private static BlockFace OppositeFace(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => BlockFace.North,
                BlockFace.North => BlockFace.South,
                BlockFace.West => BlockFace.East,
                _ => BlockFace.West
            };
        }

        /// <summary>Court U: back strip facing the street + two side legs facing the courtyard.</summary>
        private static void PackCourt(Rect lot, BlockFace face, List<PieceInfo> pieces, System.Random rng, List<SlotPack> output)
        {
            PackRun(CourtBackRun(lot, face), pieces, rng, output);

            // Legs stop at the back strip's building front so the last leg shop
            // touches the strip flush.
            var legLength = CrossLength(lot, face) - WalkwayDepthMeters - SlotCrossSpan + FrontInsetMeters;
            switch (face)
            {
                case BlockFace.South:
                    PackRun(VerticalRun(BlockFace.East, lot, lot.yMin + WalkwayDepthMeters, lot.xMin, legLength, true), pieces, rng, output);
                    PackRun(VerticalRun(BlockFace.West, lot, lot.yMin + WalkwayDepthMeters, lot.xMax - SlotCrossSpan, legLength, true), pieces, rng, output);
                    break;
                case BlockFace.North:
                    PackRun(VerticalRun(BlockFace.East, lot, lot.yMax - WalkwayDepthMeters, lot.xMin, legLength, false), pieces, rng, output);
                    PackRun(VerticalRun(BlockFace.West, lot, lot.yMax - WalkwayDepthMeters, lot.xMax - SlotCrossSpan, legLength, false), pieces, rng, output);
                    break;
                case BlockFace.West:
                    PackRun(HorizontalRun(BlockFace.North, lot, lot.xMin + WalkwayDepthMeters, lot.yMin, legLength, true), pieces, rng, output);
                    PackRun(HorizontalRun(BlockFace.South, lot, lot.xMin + WalkwayDepthMeters, lot.yMax - SlotCrossSpan, legLength, true), pieces, rng, output);
                    break;
                default:
                    PackRun(HorizontalRun(BlockFace.North, lot, lot.xMax - WalkwayDepthMeters, lot.yMin, legLength, false), pieces, rng, output);
                    PackRun(HorizontalRun(BlockFace.South, lot, lot.xMax - WalkwayDepthMeters, lot.yMax - SlotCrossSpan, legLength, false), pieces, rng, output);
                    break;
            }
        }

        // ── Supports (classification gates) ─────────────────────────

        private static bool SupportsStrip(Rect lot, BlockFace face)
        {
            return FrontageLength(lot, face) >= MinFrontageMeters &&
                   CrossLength(lot, face) >= MinCrossMeters;
        }

        private static bool SupportsCorner(Rect lot, BlockFace face, BlockFace cornerFace)
        {
            return FrontageLength(lot, face) >= MinFrontageMeters &&
                   CrossLength(lot, face) >= MinCrossMeters &&
                   FrontageLength(lot, cornerFace) >= CornerOtherFrontageMeters &&
                   CrossLength(lot, cornerFace) >= MinCrossMeters;
        }

        private static bool SupportsCourt(Rect lot, BlockFace face)
        {
            return FrontageLength(lot, face) >= CourtMinFrontageMeters &&
                   CrossLength(lot, face) >= CourtMinCrossMeters;
        }

        /// <summary>Enough frontage + depth for a rear shop and a pump forecourt.</summary>
        public static bool SupportsGasStation(Rect lot, BlockFace face)
        {
            return FrontageLength(lot, face) >= GasMinFrontageMeters &&
                   CrossLength(lot, face) >= GasMinCrossMeters;
        }

        private static float FrontageLength(Rect lot, BlockFace face)
        {
            return face == BlockFace.South || face == BlockFace.North ? lot.width : lot.height;
        }

        private static float CrossLength(Rect lot, BlockFace face)
        {
            return face == BlockFace.South || face == BlockFace.North ? lot.height : lot.width;
        }

        // ── Zone rects ──────────────────────────────────────────────

        private static Rect EdgeStripRect(Rect lot, BlockFace face, float depth)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMax, lot.yMin + depth),
                BlockFace.North => Rect.MinMaxRect(lot.xMin, lot.yMax - depth, lot.xMax, lot.yMax),
                BlockFace.West  => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMin + depth, lot.yMax),
                _               => Rect.MinMaxRect(lot.xMax - depth, lot.yMin, lot.xMax, lot.yMax)
            };
        }

        private static Rect InsetRectFromFace(Rect lot, BlockFace face, float inset)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(lot.xMin, lot.yMin + inset, lot.xMax, lot.yMax),
                BlockFace.North => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMax, lot.yMax - inset),
                BlockFace.West  => Rect.MinMaxRect(lot.xMin + inset, lot.yMin, lot.xMax, lot.yMax),
                _               => Rect.MinMaxRect(lot.xMin, lot.yMin, lot.xMax - inset, lot.yMax)
            };
        }

        /// <summary>Courtyard between the two legs and the back strip of a U court.</summary>
        private static Rect CourtCourtyardRect(Rect lot, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(lot.xMin + SlotCrossSpan, lot.yMin + WalkwayDepthMeters, lot.xMax - SlotCrossSpan, lot.yMax - SlotCrossSpan),
                BlockFace.North => Rect.MinMaxRect(lot.xMin + SlotCrossSpan, lot.yMin + SlotCrossSpan, lot.xMax - SlotCrossSpan, lot.yMax - WalkwayDepthMeters),
                BlockFace.West  => Rect.MinMaxRect(lot.xMin + WalkwayDepthMeters, lot.yMin + SlotCrossSpan, lot.xMax - SlotCrossSpan, lot.yMax - SlotCrossSpan),
                _               => Rect.MinMaxRect(lot.xMin + SlotCrossSpan, lot.yMin + SlotCrossSpan, lot.xMax - WalkwayDepthMeters, lot.yMax - SlotCrossSpan)
            };
        }

        /// <summary>Paved forecourt between the walkway and the rear shop of a gas station.</summary>
        private static Rect GasForecourtRect(Rect lot, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(lot.xMin, lot.yMin + WalkwayDepthMeters, lot.xMax, lot.yMax - SlotCrossSpan),
                BlockFace.North => Rect.MinMaxRect(lot.xMin, lot.yMin + SlotCrossSpan, lot.xMax, lot.yMax - WalkwayDepthMeters),
                BlockFace.West  => Rect.MinMaxRect(lot.xMin + WalkwayDepthMeters, lot.yMin, lot.xMax - SlotCrossSpan, lot.yMax),
                _               => Rect.MinMaxRect(lot.xMin + SlotCrossSpan, lot.yMin, lot.xMax - WalkwayDepthMeters, lot.yMax)
            };
        }
    }
}
