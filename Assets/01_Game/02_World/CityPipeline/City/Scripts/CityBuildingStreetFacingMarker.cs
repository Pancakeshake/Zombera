namespace Zombera.World.City
{
    /// <summary>
    ///     Stamped on placed building instances during residential placement.
    ///     Records which <see cref="BlockFace"/> is the open/street-facing side
    ///     so door-path generation can route toward that specific edge.
    /// </summary>
    public sealed class CityBuildingStreetFacingMarker : UnityEngine.MonoBehaviour
    {
        public BlockFace streetFace;

        /// <summary>World-space anchor of the street-facing door (StairSocket), recorded at placement.</summary>
        public UnityEngine.Vector3 doorAnchorWorld;
        public bool hasDoorAnchor;

        /// <summary>Axis-aligned building footprint rect in world XZ, recorded at placement.</summary>
        public UnityEngine.Rect footprintXZ;
    }
}
