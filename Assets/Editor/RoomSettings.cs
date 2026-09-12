#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Per-room-type behavioural settings shared across all templates.
    ///     Controls which room types can be open-plan, which block front doors,
    ///     and global adjacency rules between room types.
    ///     Created via Assets → Create → Zombera → Building → Room Settings.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Room Settings",
        fileName = "RoomSettings",
        order = 98)]
    public sealed class RoomSettings : ScriptableObject
    {
        [Header("Scriptable Objects")]
        [Tooltip("Optional prop placement config. When assigned, the generator places props into each room after assembly based on room type and placement rules.")]
        public InteriorPropConfig PropConfig;

        [Header("Room Type Configs")]
        [Tooltip("Per-room-type behavioural flags.")]
        public RoomTypeConfig[] RoomTypes = Array.Empty<RoomTypeConfig>();

        [Header("Global Adjacency Rules")]
        [Tooltip("Spatial relationship rules between room types, shared across all templates.")]
        public RoomAdjacencyRule[] AdjacencyRules = Array.Empty<RoomAdjacencyRule>();

        /// <summary>
        ///     Returns the config for a given room type, or a default if not found.
        /// </summary>
        public RoomTypeConfig GetConfig(RoomType type)
        {
            foreach (var cfg in RoomTypes)
                if (cfg.Type == type)
                    return cfg;
            return RoomTypeConfig.DefaultFor(type);
        }

        /// <summary>
        ///     Returns true if any config for the given type has AllowOpenPlan = false.
        /// </summary>
        public bool CanBeOpenPlan(RoomType type)
        {
            return GetConfig(type).AllowOpenPlan;
        }

        /// <summary>
        ///     Returns true if this room type should block exterior door placement.
        /// </summary>
        public bool BlocksFrontDoor(RoomType type)
        {
            return GetConfig(type).BlockFrontDoor;
        }
    }

    /// <summary>
    ///     Behavioural flags for a single room type.
    /// </summary>
    [Serializable]
    public struct RoomTypeConfig
    {
        [Tooltip("The room type this config applies to.")]
        public RoomType Type;

        [Tooltip("When checked, this room type can be merged into open-plan areas.")]
        public bool AllowOpenPlan;

        [Tooltip("When checked, exterior doors will NOT be placed facing this room type.")]
        public bool BlockFrontDoor;

        [Tooltip("Minimum percentage of floor area this room type should occupy.")]
        [Range(0, 100)]
        public int FloorPercentageMin;

        [Tooltip("Maximum percentage of floor area this room type should occupy.")]
        [Range(0, 100)]
        public int FloorPercentageMax;

        public static RoomTypeConfig DefaultFor(RoomType type)
        {
            var cfg = new RoomTypeConfig
            {
                Type = type,
                AllowOpenPlan = type != RoomType.Bathroom && type != RoomType.Stairwell
                                && type != RoomType.CommercialKitchen && type != RoomType.ChangeRoom
                                && type != RoomType.Office && type != RoomType.StaffRoom,
                BlockFrontDoor = type == RoomType.Bedroom || type == RoomType.Stairwell
                                 || type == RoomType.Stockroom || type == RoomType.CommercialKitchen
                                 || type == RoomType.ChangeRoom || type == RoomType.Office
                                 || type == RoomType.StaffRoom,
                FloorPercentageMin = type == RoomType.Stairwell ? 0 : 5,
                FloorPercentageMax = type == RoomType.Stairwell ? 10 : 25
            };

            if (type == RoomType.SalesFloor)
            {
                cfg.FloorPercentageMin = 50;
                cfg.FloorPercentageMax = 80;
            }

            return cfg;
        }
    }
}
#endif
