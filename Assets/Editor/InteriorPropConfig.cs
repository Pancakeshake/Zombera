#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     ScriptableObject that defines which interior props (furniture, clutter, etc.) are placed
    ///     in each room type during building generation. Each room type gets its own weighted prop list
    ///     with placement constraints (wall distance, door clearance, visual-lot gating).
    ///
    ///     Create via Assets → Create → Zombera → Building → Interior Prop Config.
    ///     Save the asset under <c>Assets/02_Shared/ScriptableObjects/Buildings/Props/</c>.
    ///
    ///     Assigned on a <see cref="ResidentialHouseTemplate"/> so each archetype can have its own
    ///     prop dressing. The generator reads this during <c>BuildHierarchyAndSavePrefab</c> and
    ///     places props editor-time into the generated prefab.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Interior Prop Config",
        fileName = "InteriorPropConfig",
        order = 101)]
    public sealed class InteriorPropConfig : ScriptableObject
    {
        [Header("Scriptable Objects")]
        [Tooltip("Reference to RoomSettings — constrains available room types. Entries are auto-synced from this asset's RoomTypes.")]
        public RoomSettings roomSettings;

        [Header("Room Entries")]
        [Tooltip("Per-room-type prop lists. Rooms not listed here use the fallback.")]
        public RoomPropEntry[] roomEntries = Array.Empty<RoomPropEntry>();

        [Header("Door Clearance")]
        [Tooltip("Radius (m) of the no-prop zone around each door position. Props cannot be placed inside this circle.")]
        [Min(0f)]
        public float doorClearanceRadius = 1.5f;

        [Header("Fallback")]
        [Tooltip("Props used when a room type has no explicit entry. Leave empty to skip unlisted rooms.")]
        public PropPlacement[] fallbackProps = Array.Empty<PropPlacement>();

        [Tooltip("How many placement attempts to make per room when using the fallback list.")]
        [Min(0)]
        public int fallbackSpawnAttempts = 4;

        /// <summary>
        ///     Returns the room entry for the given type, or null.
        /// </summary>
        public RoomPropEntry? GetEntryForRoom(RoomType roomType)
        {
            foreach (var entry in roomEntries)
                if (entry.roomType == roomType)
                    return entry;
            return null;
        }
    }

    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>
    ///     How a prop is positioned relative to room boundaries.
    /// </summary>
    public enum WallAlignment
    {
        /// <summary>Pushed against a room wall (prefers exterior, falls back to interior). This is the default.</summary>
        AgainstWall = 1,

        /// <summary>Roughly centered in the room.</summary>
        Centered = 2,
    }

    // ── Structs ─────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Maps a single room type to a weighted list of props, guaranteed arrangements,
    ///     and placement parameters.
    /// </summary>
    [Serializable]
    public struct RoomPropEntry
    {
        [Tooltip("Which room type this entry applies to.")]
        public RoomType roomType;

        [Tooltip("How many weighted prop placement attempts to make in each room of this type (set to 0 for hand-crafted prefabs only).")]
        [Min(0)]
        public int spawnAttempts;

        [Header("Props")]
        [Tooltip("Props for this room type. Guaranteed props (guaranteedCount > 0) are always placed. Weighted props fill remaining space.")]
        public PropPlacement[] props;
    }

    /// <summary>
    ///     A single prop placement definition — prefab, weight, per-room cap,
    ///     rotation options, and placement constraint rules.
    /// </summary>
    [Serializable]
    public struct PropPlacement
    {
        [Tooltip("Prop prefab to instantiate.")]
        public GameObject prefab;

        [Tooltip("Relative spawn weight. Higher = more likely to be picked. Ignored for guaranteed placements.")]
        [Min(0f)]
        public float weight;

        [Tooltip("Maximum number of this prop allowed per room (includes guaranteed + weighted placements).")]
        [Min(0)]
        public int maxPerRoom;

        [Tooltip("Number of this prop guaranteed per room. Placed in Phase 2 before weighted fill. 0 = optional only.")]
        [Min(0)]
        public int guaranteedCount;

        [Tooltip("Randomise Y rotation. When yawSnapDegrees > 0, picks from multiples of that angle.")]
        public bool randomiseYaw;

        [Tooltip("Yaw snap increment in degrees. 90 = cardinal only. 0 = free rotation. Ignored when randomiseYaw is false.")]
        [Min(0f)]
        public float yawSnapDegrees;

        [Tooltip("Extra Y offset above the floor surface (m).")]
        public float yOffset;

        [Header("Placement Strategy")]
        [Tooltip("How this prop is positioned relative to room walls. For arrangement anchors, AgainstWall is the default when set to None.")]
        public WallAlignment wallAlignment;

        [Header("Placement Rules")]
        [Tooltip("Minimum distance from external/perimeter walls (metres). 0 = no restriction.")]
        [Min(0f)]
        public float minWallDistance;

        [Tooltip("Minimum distance from door positions (metres). 0 = no restriction.")]
        [Min(0f)]
        public float minDoorDistance;

        [Header("Overlap Avoidance")]
        [Tooltip("Approximate XZ radius (m) for overlap detection. Props won't be placed closer than (r1 + r2 + 0.3m). 0 = no overlap check.")]
        [Min(0f)]
        public float footprintRadius;

        [Header("Scale")]
        [Tooltip("Uniform scale multiplier applied after instantiation. 1 = prefab native scale. Use this to correct overscaled furniture packs.")]
        [Min(0.001f)]
        public float scaleOverride;
    }

}
#endif
