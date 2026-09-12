using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class CityMathRoadLayoutGenerator
    {
        private readonly struct SquareStreetGridRingContext
        {
            public SquareStreetGridRingContext(
                bool clipToRing,
                float ringX0,
                float ringX1,
                float ringZ0,
                float ringZ1,
                float cornerRadius,
                bool clampCornerZones)
            {
                ClipToRing = clipToRing;
                RingX0 = ringX0;
                RingX1 = ringX1;
                RingZ0 = ringZ0;
                RingZ1 = ringZ1;
                CornerRadius = cornerRadius;
                ClampCornerZones = clampCornerZones;
            }

            public bool ClipToRing { get; }
            public float RingX0 { get; }
            public float RingX1 { get; }
            public float RingZ0 { get; }
            public float RingZ1 { get; }
            public float CornerRadius { get; }
            public bool ClampCornerZones { get; }
        }

        private readonly struct AxisSpanFallback
        {
            public AxisSpanFallback(float min, float max)
            {
                Min = min;
                Max = max;
            }

            public float Min { get; }
            public float Max { get; }
        }

        private readonly struct SquareStreetGridSegmentRequest
        {
            public SquareStreetGridSegmentRequest(
                float width,
                IReadOnlyList<float> fixedAxisLines,
                bool horizontal,
                AxisSpanFallback fallback,
                SquareStreetGridRingContext ring,
                int idBase)
            {
                Width = width;
                FixedAxisLines = fixedAxisLines;
                Horizontal = horizontal;
                Fallback = fallback;
                Ring = ring;
                IdBase = idBase;
            }

            public float Width { get; }
            public IReadOnlyList<float> FixedAxisLines { get; }
            public bool Horizontal { get; }
            public AxisSpanFallback Fallback { get; }
            public SquareStreetGridRingContext Ring { get; }
            public int IdBase { get; }
        }

        private const float CornerJunctionClearanceMeters = 18f;

    /// <summary>
    ///     Ground-truth junction type and orientation recorded at topology emit time.
    ///     Used by junction wiring to avoid guessing T-stem direction from marker heuristics.
    /// </summary>
    public enum CityJunctionKind { T, X }

    public struct CityStreetJunctionEntry
    {
        public Vector2 PositionXZ;
        public CityJunctionKind Kind;
        /// <summary>
        ///     For T-junctions: normalized direction the branch road approaches FROM
        ///     (points toward the junction). For X-junctions: Vector2.zero.
        /// </summary>
        public Vector2 BranchApproachDirection;
    }

    }
}