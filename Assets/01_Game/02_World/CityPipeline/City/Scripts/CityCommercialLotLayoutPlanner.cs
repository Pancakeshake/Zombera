using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>Layout classification for commercial district lots.</summary>
    public enum CommercialLayoutType
    {
        /// <summary>Legacy single-building path.</summary>
        Single,
        /// <summary>Connected shop run packed along one street frontage.</summary>
        Strip,
        /// <summary>Two connected runs meeting at a street corner (L shape).</summary>
        Corner,
        /// <summary>Back strip + two side legs opening onto the street (U court).</summary>
        Court,
        /// <summary>Rear shop with a paved pump forecourt.</summary>
        GasStation
    }

    /// <summary>One building slot inside a commercial lot plan.</summary>
    public sealed class CommercialShopSlot
    {
        public Rect SlotRect;
        public BlockFace StreetFace;
        public CityAssembledBuildingCatalogEntry Entry;
    }

    /// <summary>Building slots + terrain zones derived from one commercial lot rect.</summary>
    public sealed class CommercialLotPlan
    {
        public CommercialLayoutType Type = CommercialLayoutType.Single;
        public readonly List<CommercialShopSlot> Slots = new();
        public readonly List<LotSubZone> Zones = new();
    }

    /// <summary>
    ///     Derives snap-together commercial layouts (strip / corner L / U court /
    ///     gas station) from a lot rect + assigned lot kind. Deterministic — the
    ///     placement step and the lot terrain step compute identical geometry
    ///     from (lotRect, blockBounds, streetFace, kind).
    /// </summary>
    public static partial class CityCommercialLotLayoutPlanner
    {
        public const float WalkwayDepthMeters = 2f;

        /// <summary>
        ///     Placement geometry contract: <c>TryComputeLotPlacement</c> pulls the
        ///     building 1 m in from the slot's street edge (its inset floor) and
        ///     keeps a 0.3 m tail at the back — slot cross spans include both so
        ///     perpendicular runs stay flush.
        /// </summary>
        internal const float FrontInsetMeters = 1f;
        internal const float ShopDepthMeters = 12f;
        internal const float SlotCrossSpan = FrontInsetMeters + ShopDepthMeters + 0.3f;
        internal const float CornerRunOffset = WalkwayDepthMeters + FrontInsetMeters + ShopDepthMeters;
        internal const float MinShopMeters = 6f;
        internal const float MinParkingMeters = 4f;
        internal const float MinFrontageMeters = 14f;
        internal const float MinCrossMeters = 16f;
        internal const float CornerOtherFrontageMeters = 27f;
        internal const float CourtMinFrontageMeters = 32f;
        internal const float CourtMinCrossMeters = 28f;
        internal const float GasMinFrontageMeters = 20f;
        internal const float GasMinCrossMeters = 20f;

        private sealed class Geometry
        {
            public CommercialLayoutType Type;
            public BlockFace CornerFace;
            public readonly List<Rect> WalkwayRects = new(2);
            public readonly List<Rect> ParkingRects = new(2);
        }

        /// <summary>
        ///     Classifies the lot without candidates — shared by subdivision-time
        ///     kind assignment and the Auto fallback path.
        /// </summary>
        public static CommercialLayoutType ClassifyType(Rect lotRect, Rect blockBounds, BlockFace streetFace)
        {
            return PlanGeometry(lotRect, blockBounds, streetFace, CommercialLotKind.Auto).Type;
        }

        /// <summary>
        ///     Builds the slot plan for one commercial lot and assigns a catalog
        ///     entry to each slot. The explicit kind (marker-assigned during the
        ///     District Lots step) is honoured when its geometry fits; otherwise
        ///     the plan falls back to geometric classification.
        /// </summary>
        public static CommercialLotPlan BuildPlan(
            Rect lotRect, Rect blockBounds, BlockFace streetFace, CommercialLotKind kind,
            IReadOnlyList<CityAssembledBuildingCatalogEntry> candidates, System.Random rng)
        {
            var plan = new CommercialLotPlan();
            if (kind is CommercialLotKind.StoreLot or CommercialLotKind.StoreLotCorner)
                return BuildStoreLotPlan(lotRect, streetFace, candidates, rng,
                    preferCorner: kind == CommercialLotKind.StoreLotCorner);

            var geometry = PlanGeometry(lotRect, blockBounds, streetFace, kind);
            plan.Type = geometry.Type;
            if (geometry.Type == CommercialLayoutType.Single)
                return plan;

            // Ignore ShopDepthMeters cap — placement scales pieces to each slot,
            // so roof-overhang footprints (~13.1 m on a 12 m kit) still pack.
            var pieces = ResolvePieces(candidates, ignoreCrossCap: true);
            var packs = new List<SlotPack>(8);
            switch (geometry.Type)
            {
                case CommercialLayoutType.Corner:
                {
                    var corner = CornerRuns(lotRect, streetFace, geometry.CornerFace);
                    PackRun(corner.Primary, pieces, rng, packs);
                    PackRun(corner.Secondary, pieces, rng, packs);
                    break;
                }
                case CommercialLayoutType.Court:
                    PackCourt(lotRect, streetFace, pieces, rng, packs);
                    break;
                case CommercialLayoutType.GasStation:
                    PackRun(CourtBackRun(lotRect, streetFace), pieces, rng, packs);
                    break;
                default:
                    PackRun(StripRun(lotRect, streetFace), pieces, rng, packs);
                    break;
            }

            for (var i = 0; i < packs.Count; i++)
            {
                plan.Slots.Add(new CommercialShopSlot
                {
                    SlotRect = packs[i].Rect,
                    StreetFace = packs[i].Face,
                    Entry = packs[i].Entry
                });
            }

            return plan;
        }

        /// <summary>
        ///     Emits the walkway + parking sub-zones for a commercial lot, using
        ///     texture layer indices resolved by the caller. Empty for Single lots.
        ///     When <paramref name="stripeTextureLayer"/> ≥ 0, stall stripes are
        ///     painted over each parking rect.
        /// </summary>
        public static List<LotSubZone> BuildTerrainZones(
            Rect lotRect, Rect blockBounds, BlockFace streetFace, CommercialLotKind kind,
            int walkwayTextureLayer, int parkingTextureLayer, int stripeTextureLayer = -1)
        {
            var geometry = PlanGeometry(lotRect, blockBounds, streetFace, kind);
            var zones = new List<LotSubZone>(
                geometry.WalkwayRects.Count + geometry.ParkingRects.Count * 8);

            if (walkwayTextureLayer >= 0)
                AddZones(geometry.WalkwayRects, walkwayTextureLayer, 0.015f, "CommercialWalkway", zones);

            if (parkingTextureLayer >= 0)
            {
                AddZones(geometry.ParkingRects, parkingTextureLayer, 0f, "CommercialParking", zones);
                for (var i = 0; i < geometry.ParkingRects.Count; i++)
                {
                    // Lot-level strip/L/U: cars face the street-edge store band.
                    CityCommercialParkingStripes.AppendStripes(
                        geometry.ParkingRects[i], streetFace, stripeTextureLayer, zones);
                }
            }

            return zones;
        }

        // ── Geometry ────────────────────────────────────────────────

        /// <summary>
        ///     Block-layout store lot: one building fills the lot, placed flush at
        ///     its parking edge. Picks the closest along-axis piece from the
        ///     12/15/18/24 m pool — placement then scales XZ to the lot so
        ///     neighbours stay wall-to-wall even when catalog dims overhang.
        /// </summary>
        private static CommercialLotPlan BuildStoreLotPlan(
            Rect lotRect, BlockFace streetFace,
            IReadOnlyList<CityAssembledBuildingCatalogEntry> candidates, System.Random rng,
            bool preferCorner)
        {
            var plan = new CommercialLotPlan { Type = CommercialLayoutType.Strip };

            // Corner slots are 15 m deep — prefer pieces that fill them exactly
            // so less non-uniform scale is needed; fall back to any shop piece.
            var pieces = preferCorner
                ? ResolvePieces(candidates, requiredCross: 15f)
                : ResolvePieces(candidates, ignoreCrossCap: true);
            if (pieces.Count == 0)
                pieces = ResolvePieces(candidates, ignoreCrossCap: true);
            if (pieces.Count == 0)
                return plan;

            // Along axis follows the street — width for N/S faces, height for E/W.
            var lotAlong = streetFace is BlockFace.South or BlockFace.North
                ? lotRect.width
                : lotRect.height;

            var bestDelta = float.MaxValue;
            for (var i = 0; i < pieces.Count; i++)
            {
                var delta = Mathf.Abs(pieces[i].Along - lotAlong);
                if (delta < bestDelta)
                    bestDelta = delta;
            }

            var fitting = new List<CityAssembledBuildingCatalogEntry>(4);
            for (var i = 0; i < pieces.Count; i++)
            {
                if (Mathf.Abs(pieces[i].Along - lotAlong) <= bestDelta + 0.01f)
                    fitting.Add(pieces[i].Entry);
            }

            if (fitting.Count == 0)
                return plan;

            // Corner slots prefer the two-glass-side corner-shop pieces when one
            // fits — falls back to any store piece otherwise.
            if (preferCorner)
            {
                var cornerOnly = new List<CityAssembledBuildingCatalogEntry>(4);
                for (var i = 0; i < fitting.Count; i++)
                {
                    if (fitting[i].prefab.name.Contains("Corner_Shop", System.StringComparison.OrdinalIgnoreCase))
                        cornerOnly.Add(fitting[i]);
                }

                if (cornerOnly.Count > 0)
                    fitting = cornerOnly;
            }

            var entry = fitting[rng.Next(fitting.Count)];
            plan.Slots.Add(new CommercialShopSlot
            {
                // Exact lot rect — placement scales the building to fill it.
                SlotRect = lotRect,
                StreetFace = streetFace,
                Entry = entry
            });
            return plan;
        }

        private static Geometry PlanGeometry(
            Rect lot, Rect blockBounds, BlockFace streetFace, CommercialLotKind kind)
        {
            var geometry = new Geometry();
            if (lot.width < MinFrontageMeters || lot.height < MinFrontageMeters)
                return geometry;

            var openFaces = new List<BlockFace>(4);
            CityBuildingRoadFacingUtility.TryGetLotOpenFaces(
                lot, blockBounds, blockBounds.width >= blockBounds.height, openFaces);
            var cornerFace = FindAdjacentOpenFace(streetFace, openFaces);

            if (kind == CommercialLotKind.GasStation)
            {
                if (SupportsGasStation(lot, streetFace))
                {
                    BuildGasStationGeometry(lot, streetFace, geometry);
                    return geometry;
                }

                kind = CommercialLotKind.Auto; // undersized — fall back to natural shape
            }

            switch (kind)
            {
                case CommercialLotKind.CornerL:
                    if (cornerFace.HasValue && SupportsCorner(lot, streetFace, cornerFace.Value))
                    {
                        BuildCornerGeometry(lot, streetFace, cornerFace.Value, geometry);
                        return geometry;
                    }
                    break;
                case CommercialLotKind.CourtU:
                    if (SupportsCourt(lot, streetFace))
                    {
                        BuildCourtGeometry(lot, streetFace, geometry);
                        return geometry;
                    }
                    break;
                case CommercialLotKind.Strip:
                    if (SupportsStrip(lot, streetFace))
                    {
                        BuildStripGeometry(lot, streetFace, geometry);
                        return geometry;
                    }
                    break;
            }

            if (cornerFace.HasValue && SupportsCorner(lot, streetFace, cornerFace.Value))
            {
                BuildCornerGeometry(lot, streetFace, cornerFace.Value, geometry);
                return geometry;
            }

            if (openFaces.Count == 1 && SupportsCourt(lot, streetFace))
            {
                BuildCourtGeometry(lot, streetFace, geometry);
                return geometry;
            }

            if (SupportsStrip(lot, streetFace))
                BuildStripGeometry(lot, streetFace, geometry);

            return geometry;
        }

        private static void BuildStripGeometry(Rect lot, BlockFace face, Geometry geometry)
        {
            geometry.Type = CommercialLayoutType.Strip;
            geometry.WalkwayRects.Add(EdgeStripRect(lot, face, WalkwayDepthMeters));

            var parking = InsetRectFromFace(lot, face, WalkwayDepthMeters + SlotCrossSpan);
            if (Mathf.Min(parking.width, parking.height) >= MinParkingMeters)
                geometry.ParkingRects.Add(parking);
        }

        private static void BuildCornerGeometry(Rect lot, BlockFace face, BlockFace cornerFace, Geometry geometry)
        {
            geometry.Type = CommercialLayoutType.Corner;
            geometry.CornerFace = cornerFace;
            geometry.WalkwayRects.Add(EdgeStripRect(lot, face, WalkwayDepthMeters));
            geometry.WalkwayRects.Add(EdgeStripRect(lot, cornerFace, WalkwayDepthMeters));

            // Remainder after both run bands = the courtyard parking rect.
            var remainder = InsetRectFromFace(lot, face, WalkwayDepthMeters + SlotCrossSpan);
            remainder = InsetRectFromFace(remainder, cornerFace, WalkwayDepthMeters + SlotCrossSpan);
            if (Mathf.Min(remainder.width, remainder.height) >= MinParkingMeters)
                geometry.ParkingRects.Add(remainder);
        }

        private static void BuildCourtGeometry(Rect lot, BlockFace face, Geometry geometry)
        {
            geometry.Type = CommercialLayoutType.Court;
            geometry.WalkwayRects.Add(EdgeStripRect(lot, face, WalkwayDepthMeters));

            var courtyard = CourtCourtyardRect(lot, face);
            if (Mathf.Min(courtyard.width, courtyard.height) >= MinParkingMeters)
                geometry.ParkingRects.Add(courtyard);
        }

        private static void BuildGasStationGeometry(Rect lot, BlockFace face, Geometry geometry)
        {
            geometry.Type = CommercialLayoutType.GasStation;
            geometry.WalkwayRects.Add(EdgeStripRect(lot, face, WalkwayDepthMeters));
            geometry.ParkingRects.Add(GasForecourtRect(lot, face));
        }

        // ── Face helpers ────────────────────────────────────────────

        private static BlockFace? FindAdjacentOpenFace(BlockFace face, List<BlockFace> openFaces)
        {
            for (var i = 0; i < openFaces.Count; i++)
                if (AreFacesAdjacent(face, openFaces[i]))
                    return openFaces[i];
            return null;
        }

        private static bool AreFacesAdjacent(BlockFace a, BlockFace b)
        {
            if (a == b)
                return false;
            var aHorizontal = a == BlockFace.South || a == BlockFace.North;
            var bHorizontal = b == BlockFace.South || b == BlockFace.North;
            return aHorizontal != bHorizontal;
        }

        private static void AddZones(
            List<Rect> rects, int layer, float heightOffset, string name, List<LotSubZone> zones)
        {
            for (var i = 0; i < rects.Count; i++)
                zones.Add(new LotSubZone
                {
                    TextureLayerIndex = layer,
                    Bounds = rects[i],
                    HeightOffset = heightOffset,
                    DisplayName = name,
                    Sharp = true
                });
        }
    }
}
