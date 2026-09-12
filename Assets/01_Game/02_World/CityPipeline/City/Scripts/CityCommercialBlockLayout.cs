using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Block-level commercial layout data attached to a DistrictLots container.
    ///     Store lots live as regular lot children; this component carries the
    ///     shared walkway + parking paint zones (world rects) for the terrain step.
    /// </summary>
    public sealed class CityCommercialBlockLayout : MonoBehaviour
    {
        public CommercialLayoutType Style;

        /// <summary>
        ///     Direction parked cars face toward the primary store frontage
        ///     (orients stall stripes in the shared parking).
        /// </summary>
        public BlockFace ParkingCarFacing;

        // Non-readonly so Unity serializes the zone rects across domain reloads.
        public List<Rect> WalkwayZones = new();
        public List<Rect> ParkingZones = new();
    }
}
