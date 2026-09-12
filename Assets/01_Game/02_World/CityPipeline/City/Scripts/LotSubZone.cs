using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Surface material type for a sub-zone within a district lot.
    ///     Each value maps to a terrain layer via <see cref="CityLotTerrainPainter"/>.
    /// </summary>
    public enum LotSurfaceType
    {
        /// <summary>Natural grass or dirt.</summary>
        Grass,
        /// <summary>Smooth concrete slab — industrial lots, building pads.</summary>
        Concrete,
        /// <summary>Dark asphalt — driveways, carparks.</summary>
        Asphalt,
        /// <summary>Loose gravel/aggregate — rural driveways, service areas.</summary>
        Gravel,
        /// <summary>Brick or stone pavers — footpaths, CityCore lots.</summary>
        Paver,
        /// <summary>Mulch or flower-bed surface — decorative front yards.</summary>
        Garden
    }

    /// <summary>
    ///     A sub-divided zone within a single district lot.
    ///     Each sub-zone carries its own surface type, bounds, and optional
    ///     polygon outline (for curved/corner lots).
    /// </summary>
    [System.Serializable]
    public struct LotSubZone
    {
        /// <summary>
        ///     Direct index into the District Lots TextureArrayConfig.
        ///     Set from LotTerrainZoneLayout.textureLayerIndex.
        ///     -1 means use legacy LotSurfaceType lookup.
        /// </summary>
        public int TextureLayerIndex;

        /// <summary>Axis-aligned XZ bounding rectangle (world-space metres).</summary>
        public Rect Bounds;

        /// <summary>
        ///     World-space Y offset above the lot ground plane, in metres.
        ///     Used to prevent z-fighting between adjacent sub-zones (e.g. driveway
        ///     sits 0.005 m above front yard so the asphalt is visible at seams).
        /// </summary>
        public float HeightOffset;

        /// <summary>
        ///     Human-readable identifier used for the child GameObject name
        ///     (e.g. "FrontYard", "Driveway", "Backyard").
        /// </summary>
        public string DisplayName;

        /// <summary>
        ///     When true, the Lot Terrain painter stamps this zone with a binary
        ///     edge (no soft falloff) and full layer replace — used for driveways,
        ///     building slabs, and door footpaths.
        /// </summary>
        public bool Sharp;

        /// <summary>
        ///     Clipped polygon outline for curved or corner lots, in world-space XZ.
        ///     When null, <see cref="Bounds"/> is used as a simple axis-aligned quad.
        /// </summary>
        public List<Vector2> ClippedOutline;
    }
}
