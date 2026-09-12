#if UNITY_EDITOR
using System;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    /// <summary>
    ///     ScriptableObject defining one residential house archetype — how many rooms of each type,
    ///     adjacency rules, sizing constraints, and footprint limits.
    ///     Created via Assets → Create → Zombera → Building → Residential House Template.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Residential House Template",
        fileName = "Residential_NewTemplate",
        order = 100)]
    public sealed class ResidentialHouseTemplate : ScriptableObject
    {
        [Header("Scriptable Objects")]
        [Tooltip("Room behavioural settings (open-plan eligibility, door blocking, adjacency).")]
        public RoomSettings RoomSettings;

        [Header("Identity")]
        [Tooltip("Human-readable name shown in the generator UI (e.g. 'Studio', '1 Bedroom').")]
        public string DisplayName = "New Template";

        [Tooltip("The lot/district type this template is designed for.")]
        public CityDistrictType LotType = CityDistrictType.Residential;

        [Tooltip("When true AND RoomSettings permits, LivingRoom+Kitchen+Dining merge into one open-plan area.")]
        public bool PreferOpenPlan = false;

        [Tooltip("Preferred floor count (1 = bungalow/ranch, 2 = two-story). Generator supports up to 20.")]
        [Range(1, 20)]
        public int PreferredFloorCount = 1;

        [Header("Footprint Bounds")]
        [Tooltip("Minimum footprint width in cells (3m each).")]
        [Min(3)]
        public int MinFootprintWidth = 4;

        [Tooltip("Maximum footprint width in cells. 0 = unbounded.")]
        [Min(0)]
        public int MaxFootprintWidth = 8;

        [Tooltip("Minimum footprint depth in cells (3m each). 0 = reuse MinFootprintWidth.")]
        [Min(3)]
        public int MinFootprintDepth = 4;

        [Tooltip("Maximum footprint depth in cells. 0 = reuse MaxFootprintWidth.")]
        [Min(0)]
        public int MaxFootprintDepth = 8;

        [Header("Required Rooms")]
        [Tooltip("Rooms that MUST be placed for this archetype to be valid.")]
        public RoomTemplateEntry[] RequiredRooms = System.Array.Empty<RoomTemplateEntry>();

        [Header("Optional Rooms")]
        [Tooltip("Rooms that MAY be placed if grid space allows, after required rooms are placed.")]
        public RoomTemplateEntry[] OptionalRooms = System.Array.Empty<RoomTemplateEntry>();

        [Header("Roof")]
        [Tooltip("Allowed roof types for this template. The generator picks one randomly. Empty = use BuildingKitConfig default (gable kit-of-parts).")]
        public RoofTypeConfig[] AllowedRoofTypes = System.Array.Empty<RoofTypeConfig>();

        [Header("Exterior")]
        [Tooltip("Minimum number of exterior doors (0 = auto-derive from archetype).")]
        [Range(0, 4)]
        public int MinExteriorDoors = 1;

        [Tooltip("Maximum number of exterior doors.")]
        [Range(0, 8)]
        public int MaxExteriorDoors = 2;

        [Tooltip("When false, exterior windows stay off the side walls so the building can snap flush against neighbours (commercial shop types). Default true.")]
        public bool AllowSideWindows = true;

        [Tooltip("When true (commercial snap types), the shop-glass storefront continues around one corner onto the adjacent side wall, forming an L-shaped storefront. The ground door is pinned to the corner-adjacent storefront segment.")]
        public bool CornerStorefront = false;

        /// <summary>
        ///     Total minimum room count (sum of all RequiredRooms MinCount).
        /// </summary>
        public int TotalMinRooms
        {
            get
            {
                var sum = 0;
                foreach (var entry in RequiredRooms)
                    sum += entry.MinCount;
                return sum;
            }
        }

        /// <summary>
        ///     Minimum total cells needed based on required room sizing constraints.
        ///     Used as a quick reject: if the grid is smaller than this, the template won't fit.
        /// </summary>
        public int EstimatedMinCells
        {
            get
            {
                var sum = 0;
                foreach (var entry in RequiredRooms)
                    sum += entry.MinCount;
                return sum;
            }
        }
    }
}
#endif
