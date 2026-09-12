using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Lightweight marker placed on residential lot GameObjects during subdivision.
    ///     Stores the <see cref="BlockFace"/> of the street-facing (open) edge so
    ///     downstream systems don't need to re-derive it from mesh bounds.
    /// </summary>
    public sealed class CityLotFacingMarker : MonoBehaviour
    {
        public BlockFace streetFace;

        /// <summary>Commercial styling variant (residential lots ignore this).</summary>
        public CommercialLotKind commercialKind = CommercialLotKind.Auto;

        public bool isCornerLot;
        public bool isCurvedLot;
    }
}
