#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Room types used by the building archetype system. Each archetype specifies which
    ///     room types are required or optional, plus adjacency rules between them.
    /// </summary>
    public enum RoomType
    {
        LivingRoom,
        Kitchen,
        Bedroom,
        Bathroom,
        Hallway,
        Dining,
        Storage,
        Garage,
        Entry,
        Utility,

        /// <summary>Main retail area at the front of a shop (open plan, allows the street door).</summary>
        SalesFloor,

        /// <summary>Back-of-house storage for a shop.</summary>
        Stockroom,

        /// <summary>Food-prep kitchen for a commercial kitchen / bakery.</summary>
        CommercialKitchen,

        /// <summary>Customer fitting rooms (clothing stores only).</summary>
        ChangeRoom,

        /// <summary>Back office for a shop.</summary>
        Office,

        /// <summary>Staff break room.</summary>
        StaffRoom,

        /// <summary>Auto-generated room around stair transition cells. Gets walls on all sides except stair entrance/exit.</summary>
        Stairwell
    }

    /// <summary>
    ///     How strongly two room types should (or should not) be placed next to each other.
    /// </summary>
    public enum AdjacencyType
    {
        /// <summary>Layout must place these room types adjacent. If impossible, the template is invalid for this grid.</summary>
        Required,

        /// <summary>Layout should try to place these adjacent, but it's not a hard failure if it can't.</summary>
        Preferred,

        /// <summary>Layout should avoid placing these adjacent (e.g. bathroom next to kitchen).</summary>
        Avoid
    }

    /// <summary>
    ///     Pairwise rule defining how two room types should relate spatially.
    /// </summary>
    [Serializable]
    public struct RoomAdjacencyRule
    {
        [Tooltip("First room type in the pair.")]
        public RoomType RoomA;

        [Tooltip("Second room type in the pair.")]
        public RoomType RoomB;

        [Tooltip("Required = must touch, Preferred = should touch, Avoid = should not touch.")]
        public AdjacencyType Adjacency;
    }

    /// <summary>
    ///     Describes one room slot in a house template — what type and how many.
    ///     Sizing and behaviour come from the shared <see cref="RoomSettings"/> SO.
    /// </summary>
    [Serializable]
    public struct RoomTemplateEntry
    {
        [Tooltip("What kind of room this is.")]
        public RoomType RoomType;

        [Tooltip("Minimum instances of this room type.")]
        [Min(0)]
        public int MinCount;

        [Tooltip("Maximum instances of this room type.")]
        [Min(0)]
        public int MaxCount;

        /// <summary>
        ///     Resolves the target count for this entry using the given random source.
        ///     Clamped so MaxCount >= MinCount.
        /// </summary>
        public readonly int ResolveCount(System.Random random)
        {
            var min = Mathf.Max(0, MinCount);
            var max = Mathf.Max(min, MaxCount);
            if (min >= max)
                return min;

            return min + random.Next(0, max - min + 1);
        }
    }
}
#endif
