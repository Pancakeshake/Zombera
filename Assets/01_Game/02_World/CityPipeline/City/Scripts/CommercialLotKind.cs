namespace Zombera.World.City
{
    /// <summary>
    ///     District lot styling variants. Residential is the default; commercial
    ///     districts spread across the variants below for visual variance.
    ///     Stored on <see cref="CityLotFacingMarker"/> during the District Lots
    ///     step so placement, terrain painting, and future steps all agree on
    ///     the same kind without re-classifying.
    /// </summary>
    public enum CommercialLotKind
    {
        /// <summary>Legacy behaviour — classify geometrically at plan time.</summary>
        Auto = 0,

        /// <summary>Connected shop run packed flush along one street frontage.</summary>
        Strip = 1,

        /// <summary>Two connected runs meeting at a street corner (L shape).</summary>
        CornerL = 2,

        /// <summary>Back strip + two side legs opening onto the street (U court).</summary>
        CourtU = 3,

        /// <summary>Shop at the rear with a paved pump forecourt toward the street.</summary>
        GasStation = 4,

        /// <summary>
        ///     Pre-shaped store lot from a block-level commercial layout (U / L /
        ///     strip) — one building fills the lot flush with its neighbours.
        /// </summary>
        StoreLot = 5,

        /// <summary>Store lot at a street corner of a block layout — prefers corner-shop pieces.</summary>
        StoreLotCorner = 6
    }
}
