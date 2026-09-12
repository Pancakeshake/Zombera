using System;
using UnityEngine;

namespace Zombera.World.Roads
{
    public readonly struct CityMathRoadLayoutResolved
    {
        public readonly int Seed;
        public readonly float HalfWidthMeters;
        public readonly float HalfDepthMeters;

        public CityMathRoadLayoutResolved(int seed, float halfWidthMeters, float halfDepthMeters)
        {
            Seed = seed;
            HalfWidthMeters = halfWidthMeters;
            HalfDepthMeters = halfDepthMeters;
        }
    }

    [Serializable]
    public sealed class CityMathRoadLayout
    {
        // ── Toggles ──────────────────────────────────────────
        [Header("Feature Toggles")]
        [Tooltip("Sample independent X/Z half-extents from the ranges below (rectangles, not only squares).")]
        public bool randomizeCityExtents = true;

        [Tooltip("Vary spacing between street lines so blocks are mixed rectangles/squares.")]
        public bool randomizeBlockSpacing = true;

        public bool generateStreetGrid = true;

        public bool generateArterialRing = true;

        [Tooltip("Keeps local streets inside the arterial ring so they terminate on the ring (3-way T junctions).")]
        public bool clipLocalStreetsToArterialRing = true;

        public bool generateHighwayExits;

        // ── Footprint ────────────────────────────────────────
        [Header("Footprint")]
        public Vector2 centerXZ;

        [Tooltip("City block grid footprint shape.")]
        public CityFootprintShape footprintShape = CityFootprintShape.Square;

        [Tooltip("Used when Randomize City Extents is off. Square: uniform half-size. Circle: radius.")]
        [Min(50f)] public float cityRadiusMeters = 280f;

        [Tooltip("Used when Randomize Block Spacing is off.")]
        [Min(10f)] public float streetSpacingMeters = 80f;

        [Min(50f)] public float cityHalfWidthMinMeters = 200f;
        [Min(50f)] public float cityHalfWidthMaxMeters = 300f;
        [Min(50f)] public float cityHalfDepthMinMeters = 180f;
        [Min(50f)] public float cityHalfDepthMaxMeters = 280f;

        [Min(10f)] public float blockSpacingMinMeters = 55f;
        [Min(10f)] public float blockSpacingMaxMeters = 95f;
        [Range(0f, 0.45f)] public float blockSpacingJitter = 0.22f;

        // ── Arterial Ring ────────────────────────────────────
        [Header("Arterial Ring")]
        [Tooltip("Rounded arterial corner radius between T intersections. 0 = auto from street spacing.")]
        [Min(0f)] public float arterialCornerRadiusMeters;

        [Range(0.4f, 0.95f)] public float arterialRingRadiusNormalized = 0.72f;

        [Min(0f)] public float arterialRingWobbleMeters = 0f;

        // ── Highway Exits ────────────────────────────────────
        [Header("Highway Exits")]
        [Min(1)] public int highwayExitCount = 4;

        [Min(0)]
        [Tooltip("When > 0, always emits this many highway exits spread across the ring edges. 0 = legacy probabilistic exits.")]
        public int guaranteedHighwayExitCount;

        [Range(0f, 6.2831855f)] public float highwayExitAngleOffsetRadians;

        [Range(0.1f, 0.9f)] public float highwayExitStartNormalized = 0.55f;

        [Range(0.5f, 1.2f)] public float highwayExitEndNormalized = 1f;

        /// <summary>
        ///     Set by CityPrefabRoadNetworkBuilder before generation. When valid
        ///     (Max > Min), highway exits extend to the MapMagic terrain boundary
        ///     instead of stopping at the city footprint edge.
        /// </summary>
        [NonSerialized] public float worldTerrainXMin;
        [NonSerialized] public float worldTerrainXMax;
        [NonSerialized] public float worldTerrainZMin;
        [NonSerialized] public float worldTerrainZMax;

        // ── Seed ─────────────────────────────────────────────
        [Header("Generation")]
        public int layoutSeed;

        public void Normalize()
        {
            cityRadiusMeters = Mathf.Max(50f, cityRadiusMeters);
            streetSpacingMeters = Mathf.Max(10f, streetSpacingMeters);
            cityHalfWidthMinMeters = Mathf.Max(50f, cityHalfWidthMinMeters);
            cityHalfWidthMaxMeters = Mathf.Max(cityHalfWidthMinMeters, cityHalfWidthMaxMeters);
            cityHalfDepthMinMeters = Mathf.Max(50f, cityHalfDepthMinMeters);
            cityHalfDepthMaxMeters = Mathf.Max(cityHalfDepthMinMeters, cityHalfDepthMaxMeters);
            blockSpacingMinMeters = Mathf.Max(10f, blockSpacingMinMeters);
            blockSpacingMaxMeters = Mathf.Max(blockSpacingMinMeters, blockSpacingMaxMeters);
            blockSpacingJitter = Mathf.Clamp(blockSpacingJitter, 0f, 0.45f);
            highwayExitCount = Mathf.Max(1, highwayExitCount);
            guaranteedHighwayExitCount = Mathf.Max(0, guaranteedHighwayExitCount);
            // 0 means the serialized layout predates this field — use the default
            // ring size instead of clamping to the 0.4 minimum (a tiny ring starves
            // the grid of ring T-knots and breaks highway junction anchoring).
            if (arterialRingRadiusNormalized <= 0f)
                arterialRingRadiusNormalized = 0.72f;
            else
                arterialRingRadiusNormalized = Mathf.Clamp(arterialRingRadiusNormalized, 0.4f, 0.95f);
            highwayExitStartNormalized = Mathf.Clamp(highwayExitStartNormalized, 0.1f, 0.9f);
            highwayExitEndNormalized = Mathf.Clamp(highwayExitEndNormalized, highwayExitStartNormalized, 1.2f);
        }

        public CityMathRoadLayoutResolved Resolve(int seed = 0)
        {
            Normalize();

            var effectiveSeed = CityMathRoadLayoutGenerator.ResolveEffectiveLayoutSeed(seed, layoutSeed);
            var rng = new System.Random(effectiveSeed);

            float halfX;
            float halfZ;
            if (randomizeCityExtents)
            {
                halfX = SampleRange(cityHalfWidthMinMeters, cityHalfWidthMaxMeters, rng);
                halfZ = SampleRange(cityHalfDepthMinMeters, cityHalfDepthMaxMeters, rng);
            }
            else
            {
                halfX = cityRadiusMeters;
                halfZ = cityRadiusMeters;
            }

            return new CityMathRoadLayoutResolved(effectiveSeed, halfX, halfZ);
        }

        public Rect ComputeBoundsRect(int seed = 0)
        {
            var resolved = Resolve(seed);
            return ComputeBoundsRect(resolved);
        }

        public Rect ComputeBoundsRect(CityMathRoadLayoutResolved resolved)
        {
            var padX = resolved.HalfWidthMeters * 1.05f;
            var padZ = resolved.HalfDepthMeters * 1.05f;
            return Rect.MinMaxRect(
                centerXZ.x - padX,
                centerXZ.y - padZ,
                centerXZ.x + padX,
                centerXZ.y + padZ);
        }

        public void GetFootprintSquare(out float xMin, out float xMax, out float zMin, out float zMax, int seed = 0) =>
            GetFootprintSquare(centerXZ, out xMin, out xMax, out zMin, out zMax, seed);

        public void GetFootprintSquare(
            Vector2 center,
            out float xMin,
            out float xMax,
            out float zMin,
            out float zMax,
            int seed = 0)
        {
            var resolved = Resolve(seed);
            xMin = center.x - resolved.HalfWidthMeters;
            xMax = center.x + resolved.HalfWidthMeters;
            zMin = center.y - resolved.HalfDepthMeters;
            zMax = center.y + resolved.HalfDepthMeters;
        }

        public Rect GetFootprintRect(Vector2 center, int seed = 0)
        {
            GetFootprintSquare(center, out var xMin, out var xMax, out var zMin, out var zMax, seed);
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        public Rect GetFootprintRect(int seed = 0) => GetFootprintRect(centerXZ, seed);

        public void GetArterialSquare(out float xMin, out float xMax, out float zMin, out float zMax, int seed = 0) =>
            GetArterialSquare(centerXZ, out xMin, out xMax, out zMin, out zMax, seed);

        public void GetArterialSquare(
            Vector2 center,
            out float xMin,
            out float xMax,
            out float zMin,
            out float zMax,
            int seed = 0)
        {
            var resolved = Resolve(seed);
            CityMathRoadLayoutGenerator.ResolveEffectiveSquareArterialRingBounds(
                this, resolved, center, out xMin, out xMax, out zMin, out zMax);
        }

        public Rect GetArterialRect(Vector2 center, int seed = 0)
        {
            GetArterialSquare(center, out var xMin, out var xMax, out var zMin, out var zMax, seed);
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        public Rect GetArterialRect(int seed = 0) => GetArterialRect(centerXZ, seed);

        /// <summary>
        ///     Inner (yellow arterial) and outer (blue footprint) flatten bounds.
        ///     Flatten is full strength inside inner and smoothly fades to zero at outer.
        /// </summary>
        public void GetTerrainFlattenBounds(Vector2 center, int seed, out Rect innerRect, out Rect outerRect)
        {
            Normalize();
            outerRect = GetFootprintRect(center, seed);

            if (!generateArterialRing)
            {
                innerRect = outerRect;
                return;
            }

            innerRect = GetArterialRect(center, seed);
        }

        public void GetTerrainFlattenBounds(int seed, out Rect innerRect, out Rect outerRect) =>
            GetTerrainFlattenBounds(centerXZ, seed, out innerRect, out outerRect);

        /// <summary>
        ///     Inner flatten bounds — yellow arterial gizmo when a ring is enabled.
        /// </summary>
        public Rect GetTerrainFlattenRect(Vector2 center, int seed = 0)
        {
            GetTerrainFlattenBounds(center, seed, out var innerRect, out _);
            return innerRect;
        }

        public Rect GetTerrainFlattenRect(int seed = 0) => GetTerrainFlattenRect(centerXZ, seed);

        public void RollNewSeed()
        {
            layoutSeed = unchecked((int)DateTime.UtcNow.Ticks);
            if (layoutSeed == 0) layoutSeed = 1;
        }

        /// <summary>
        ///     Rough upper bound for intersection-split street segments (square grid).
        /// </summary>
        public int EstimateSplitSegmentCount()
        {
            Normalize();

            var halfX = randomizeCityExtents ? cityHalfWidthMaxMeters : cityRadiusMeters;
            var halfZ = randomizeCityExtents ? cityHalfDepthMaxMeters : cityRadiusMeters;

            // Expected (average) spacing, not the worst-case minimum — the minimum
            // over-estimates big cities and trips the junction budget hard block.
            var spacing = randomizeBlockSpacing
                ? Mathf.Max(10f, (blockSpacingMinMeters + blockSpacingMaxMeters) * 0.5f)
                : streetSpacingMeters;

            var segmentEstimate = 0;
            if (generateStreetGrid)
            {
                var lineCountX = Mathf.FloorToInt((2f * halfX) / spacing) + 1;
                var lineCountZ = Mathf.FloorToInt((2f * halfZ) / spacing) + 1;
                segmentEstimate += lineCountX * (lineCountZ + 1) + lineCountZ * (lineCountX + 1);
            }

            if (generateArterialRing)
                segmentEstimate += 4;

            if (generateHighwayExits)
                segmentEstimate += Mathf.Clamp(highwayExitCount, 1, 4);

            return segmentEstimate;
        }

        private static float SampleRange(float min, float max, System.Random rng)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
