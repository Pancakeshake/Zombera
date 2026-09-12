using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Emits thin white-paint <see cref="LotSubZone"/> rects for commercial
    ///     carpark stall lines. Horizontal banks are double-loaded (face both
    ///     ways across an aisle) and shortened so side vertical banks + drive
    ///     lanes stay clear. Stalls stay open on the drive-in side.
    /// </summary>
    public static class CityCommercialParkingStripes
    {
        public const float StallWidthMeters = 3.5f;
        public const float StripeWidthMeters = 0.55f;
        public const float StallDepthMeters = 5.2f;
        public const float AisleWidthMeters = 6f;
        public const float EdgeMarginMeters = 0.4f;
        public const float MinParkingSpanMeters = 12f;

        /// <summary>
        ///     Gap between the ends of horizontal stall rows and the vertical
        ///     side banks so cars can turn / drive between them.
        /// </summary>
        private const float SideDriveGapMeters = 3.5f;

        /// <summary>
        ///     Appends stall dividers + a single head-end stop line (building /
        ///     kerb side). The aisle side stays open so cars can drive in.
        /// </summary>
        public static void AppendStripes(
            Rect parking, BlockFace carFacing, int stripeLayer, List<LotSubZone> zones)
        {
            if (zones == null || stripeLayer < 0)
                return;
            if (parking.width < MinParkingSpanMeters || parking.height < MinParkingSpanMeters)
                return;

            AppendFacingBank(parking, carFacing, stripeLayer, zones);
            AppendSideBanks(parking, carFacing, stripeLayer, zones);
        }

        /// <summary>
        ///     Double-loaded horizontal rows: each aisle has stalls facing the
        ///     storefront and an opposing row facing the other way. Rows are
        ///     shortened so they don't collide with the vertical side banks.
        /// </summary>
        private static void AppendFacingBank(
            Rect parking, BlockFace carFacing, int stripeLayer, List<LotSubZone> zones)
        {
            var faceNS = IsNorthSouth(carFacing);
            // Leave the side strips for vertical banks + a drive gap.
            var sideClear = StallDepthMeters + SideDriveGapMeters;
            var lane = InsetAlong(parking, carFacing, sideClear);
            var along = faceNS ? lane.width : lane.height;
            var cross = faceNS ? lane.height : lane.width;
            if (along < StallWidthMeters + EdgeMarginMeters * 2f || cross < StallDepthMeters)
                return;

            var stallCount = CountStalls(along);
            // Pair = nose-in toward store + nose-in opposite, sharing one aisle.
            var pairPitch = StallDepthMeters * 2f + AisleWidthMeters;
            var maxPairs = Mathf.Max(1, Mathf.FloorToInt((cross + AisleWidthMeters) / pairPitch));

            for (var pair = 0; pair < maxPairs; pair++)
            {
                var pairStart = pair * pairPitch;
                if (pairStart + StallDepthMeters > cross - EdgeMarginMeters)
                    break;

                // Row A — faces the primary store frontage.
                if (TryBuildStallRow(lane, carFacing, pairStart, StallDepthMeters, out var rowA))
                {
                    AppendDividers(rowA, carFacing, stallCount, stripeLayer, zones);
                    AppendHeadLine(rowA, carFacing, stripeLayer, zones);
                }

                // Row B — faces the opposite way (up/down double-load across aisle).
                var oppositeOffset = pairStart + StallDepthMeters + AisleWidthMeters;
                if (oppositeOffset + StallDepthMeters > cross - EdgeMarginMeters)
                    continue;

                var oppositeFace = Opposite(carFacing);
                if (TryBuildStallRow(lane, carFacing, oppositeOffset, StallDepthMeters, out var rowB))
                {
                    AppendDividers(rowB, oppositeFace, stallCount, stripeLayer, zones);
                    AppendHeadLine(rowB, oppositeFace, stripeLayer, zones);
                }
            }
        }

        /// <summary>
        ///     Vertical banks along the left/right edges, facing into the lot.
        /// </summary>
        private static void AppendSideBanks(
            Rect parking, BlockFace carFacing, int stripeLayer, List<LotSubZone> zones)
        {
            var left = carFacing is BlockFace.South or BlockFace.North
                ? BlockFace.West
                : BlockFace.South;
            var right = Opposite(left);

            AppendSideBank(parking, left, carFacing, stripeLayer, zones);
            AppendSideBank(parking, right, carFacing, stripeLayer, zones);
        }

        private static void AppendSideBank(
            Rect parking, BlockFace sideFacing, BlockFace primaryFacing,
            int stripeLayer, List<LotSubZone> zones)
        {
            var alongSide = IsNorthSouth(primaryFacing) ? parking.height : parking.width;
            var crossSide = IsNorthSouth(primaryFacing) ? parking.width : parking.height;
            if (alongSide < MinParkingSpanMeters || crossSide < StallDepthMeters * 2f + AisleWidthMeters)
                return;

            // Clear the first horizontal pair so side banks start deeper in the lot.
            var insetFromPrimary = StallDepthMeters * 2f + AisleWidthMeters;
            if (!TryBuildSideBankRect(parking, sideFacing, primaryFacing, insetFromPrimary,
                    StallDepthMeters, out var bank))
                return;

            var along = IsNorthSouth(sideFacing) ? bank.width : bank.height;
            if (along < StallWidthMeters + EdgeMarginMeters * 2f)
                return;

            var stallCount = CountStalls(along);
            AppendDividers(bank, sideFacing, stallCount, stripeLayer, zones);
            AppendHeadLine(bank, sideFacing, stripeLayer, zones);
        }

        /// <summary>
        ///     Shrinks the parking rect on the sides perpendicular to
        ///     <paramref name="primaryFacing"/> so horizontal rows leave room
        ///     for vertical banks and drive-through gaps.
        /// </summary>
        private static Rect InsetAlong(Rect parking, BlockFace primaryFacing, float clear)
        {
            if (IsNorthSouth(primaryFacing))
            {
                if (parking.width <= clear * 2f + StallWidthMeters)
                    return parking;
                return Rect.MinMaxRect(
                    parking.xMin + clear, parking.yMin,
                    parking.xMax - clear, parking.yMax);
            }

            if (parking.height <= clear * 2f + StallWidthMeters)
                return parking;
            return Rect.MinMaxRect(
                parking.xMin, parking.yMin + clear,
                parking.xMax, parking.yMax - clear);
        }

        private static bool TryBuildStallRow(
            Rect parking, BlockFace carFacing, float insetFromStore, float depth, out Rect row)
        {
            row = default;
            if (depth < 1f)
                return false;

            row = carFacing switch
            {
                BlockFace.South => Rect.MinMaxRect(
                    parking.xMin, parking.yMin + insetFromStore,
                    parking.xMax, parking.yMin + insetFromStore + depth),
                BlockFace.North => Rect.MinMaxRect(
                    parking.xMin, parking.yMax - insetFromStore - depth,
                    parking.xMax, parking.yMax - insetFromStore),
                BlockFace.West => Rect.MinMaxRect(
                    parking.xMin + insetFromStore, parking.yMin,
                    parking.xMin + insetFromStore + depth, parking.yMax),
                _ => Rect.MinMaxRect(
                    parking.xMax - insetFromStore - depth, parking.yMin,
                    parking.xMax - insetFromStore, parking.yMax)
            };
            return row.width >= 1f && row.height >= 1f;
        }

        private static bool TryBuildSideBankRect(
            Rect parking, BlockFace sideFacing, BlockFace primaryFacing,
            float clearPrimary, float depth, out Rect bank)
        {
            bank = default;
            var core = primaryFacing switch
            {
                BlockFace.South => Rect.MinMaxRect(
                    parking.xMin, parking.yMin + clearPrimary, parking.xMax, parking.yMax),
                BlockFace.North => Rect.MinMaxRect(
                    parking.xMin, parking.yMin, parking.xMax, parking.yMax - clearPrimary),
                BlockFace.West => Rect.MinMaxRect(
                    parking.xMin + clearPrimary, parking.yMin, parking.xMax, parking.yMax),
                _ => Rect.MinMaxRect(
                    parking.xMin, parking.yMin, parking.xMax - clearPrimary, parking.yMax)
            };

            if (core.width < StallDepthMeters || core.height < MinParkingSpanMeters * 0.5f)
                return false;

            return TryBuildStallRow(core, sideFacing, 0f, depth, out bank);
        }

        private static void AppendDividers(
            Rect row, BlockFace carFacing, int stallCount, int stripeLayer, List<LotSubZone> zones)
        {
            var faceNS = IsNorthSouth(carFacing);
            var along = faceNS ? row.width : row.height;
            var usable = along - EdgeMarginMeters * 2f;
            var step = usable / stallCount;
            var half = StripeWidthMeters * 0.5f;

            for (var i = 0; i <= stallCount; i++)
            {
                var t = EdgeMarginMeters + i * step;
                if (faceNS)
                {
                    var x = row.xMin + t;
                    AddStripe(Rect.MinMaxRect(x - half, row.yMin, x + half, row.yMax),
                        stripeLayer, zones);
                }
                else
                {
                    var z = row.yMin + t;
                    AddStripe(Rect.MinMaxRect(row.xMin, z - half, row.xMax, z + half),
                        stripeLayer, zones);
                }
            }
        }

        /// <summary>
        ///     Single stop line at the nose of the stall. Opposite edge stays open.
        /// </summary>
        private static void AppendHeadLine(
            Rect row, BlockFace carFacing, int stripeLayer, List<LotSubZone> zones)
        {
            var half = StripeWidthMeters * 0.5f;
            var inset = EdgeMarginMeters;

            switch (carFacing)
            {
                case BlockFace.South:
                    AddStripe(Rect.MinMaxRect(row.xMin + inset, row.yMin - half, row.xMax - inset, row.yMin + half),
                        stripeLayer, zones);
                    break;
                case BlockFace.North:
                    AddStripe(Rect.MinMaxRect(row.xMin + inset, row.yMax - half, row.xMax - inset, row.yMax + half),
                        stripeLayer, zones);
                    break;
                case BlockFace.West:
                    AddStripe(Rect.MinMaxRect(row.xMin - half, row.yMin + inset, row.xMin + half, row.yMax - inset),
                        stripeLayer, zones);
                    break;
                default:
                    AddStripe(Rect.MinMaxRect(row.xMax - half, row.yMin + inset, row.xMax + half, row.yMax - inset),
                        stripeLayer, zones);
                    break;
            }
        }

        private static int CountStalls(float alongMeters)
        {
            var usable = alongMeters - EdgeMarginMeters * 2f;
            return Mathf.Max(1, Mathf.FloorToInt(usable / StallWidthMeters));
        }

        private static bool IsNorthSouth(BlockFace face) =>
            face is BlockFace.North or BlockFace.South;

        private static BlockFace Opposite(BlockFace face) => face switch
        {
            BlockFace.South => BlockFace.North,
            BlockFace.North => BlockFace.South,
            BlockFace.West => BlockFace.East,
            _ => BlockFace.West
        };

        private static void AddStripe(Rect bounds, int stripeLayer, List<LotSubZone> zones)
        {
            if (bounds.width < 0.05f || bounds.height < 0.05f)
                return;

            zones.Add(new LotSubZone
            {
                TextureLayerIndex = stripeLayer,
                Bounds = bounds,
                HeightOffset = 0.02f,
                DisplayName = "ParkingStripe",
                Sharp = true
            });
        }
    }
}
