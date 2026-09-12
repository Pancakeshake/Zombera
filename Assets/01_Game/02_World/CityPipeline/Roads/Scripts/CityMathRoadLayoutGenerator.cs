using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public enum CityFootprintShape
    {
        Square = 0
    }

    /// <summary>
    ///     Seed-stable mathematical city road layout (grid, ring arterials, highway exits).
    ///     Used by the City Prefab Creation Hub and shareable with world-map city centres.
    /// </summary>
    public static partial class CityMathRoadLayoutGenerator
    {
        internal static int ResolveEffectiveLayoutSeed(int seed, int layoutSeed)
        {
            if (seed != 0)
                return seed;

            if (layoutSeed != 0)
                return layoutSeed;

            return 12345;
        }

        public static RoadNetworkRuntime Generate(CityMathRoadLayout layout, RoadNetworkSettings settings, int seed = 0)
        {
            settings ??= ScriptableObject.CreateInstance<RoadNetworkSettings>();
            layout.Normalize();

            var resolvedSeed = ResolveEffectiveLayoutSeed(seed, layout.layoutSeed);
            var resolved = layout.Resolve(resolvedSeed);
            var network = new RoadNetworkRuntime(resolvedSeed);
            var center = layout.centerXZ;

            if (layout.generateStreetGrid)
                AddStreetGrid(network, layout, settings, center, resolved);

            if (layout.generateArterialRing)
                AddArterialRing(network, layout, settings, center, resolved);

            if (layout.generateHighwayExits)
                AddHighwayExits(network, layout, settings, center, resolved);

            return network;
        }

        private static void AddStreetGrid(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            AddSquareStreetGrid(network, layout, settings, center, resolved);
        }

        private static void AddArterialRing(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            AddSquareArterialRing(network, layout, settings, center, resolved);
        }

        private static void AddHighwayExits(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            AddSquareHighwayExits(network, layout, settings, center, resolved);
        }

        // -----------------------------------------------------------------------
        // Shared data types used by multiple subsystems
        // -----------------------------------------------------------------------

        internal readonly struct SquareRingBounds
        {
            public SquareRingBounds(float x0, float x1, float z0, float z1)
            {
                X0 = x0;
                X1 = x1;
                Z0 = z0;
                Z1 = z1;
            }

            public float X0 { get; }
            public float X1 { get; }
            public float Z0 { get; }
            public float Z1 { get; }
        }

        internal readonly struct SquareRingJunctionExtents
        {
            public SquareRingJunctionExtents(float colFirst, float colLast, float rowFirst, float rowLast)
            {
                ColFirst = colFirst;
                ColLast = colLast;
                RowFirst = rowFirst;
                RowLast = rowLast;
            }

            public float ColFirst { get; }
            public float ColLast { get; }
            public float RowFirst { get; }
            public float RowLast { get; }
        }

        // -----------------------------------------------------------------------
        // Axis street line generation (shared by street grid, ring bounds, exits)
        // -----------------------------------------------------------------------

        internal static List<float> BuildAxisStreetLinesPublic(
            float axisMin,
            float axisMax,
            CityMathRoadLayout layout,
            int baseSeed,
            int rngSalt)
        {
            return BuildAxisStreetLines(axisMin, axisMax, layout, baseSeed, rngSalt);
        }

        internal static List<float> BuildAxisStreetLines(
            float axisMin,
            float axisMax,
            CityMathRoadLayout layout,
            int baseSeed,
            int rngSalt)
        {
            var span = axisMax - axisMin;
            if (span <= 1f) return new List<float> { axisMin, axisMax };

            if (!layout.randomizeBlockSpacing)
            {
                var spacing = layout.streetSpacingMeters;
                var uniform = new List<float>();
                for (var value = axisMin; value <= axisMax + 0.01f; value += spacing)
                    uniform.Add(value);

                if (uniform.Count == 0 || uniform[uniform.Count - 1] < axisMax - 0.01f)
                    uniform.Add(axisMax);

                return uniform;
            }

            var axisRng = CreateDerivedRng(baseSeed, rngSalt);
            var minGap = layout.blockSpacingMinMeters;
            var maxGap = layout.blockSpacingMaxMeters;
            var lines = new List<float> { axisMin };
            var cursor = axisMin;

            while (cursor < axisMax - minGap * 0.45f)
            {
                var gap = SampleBlockSpacing(minGap, maxGap, layout.blockSpacingJitter, axisRng);
                cursor += gap;
                if (cursor >= axisMax - 0.5f)
                    break;

                lines.Add(cursor);
            }

            if (lines[lines.Count - 1] < axisMax - 0.5f)
                lines.Add(axisMax);

            return lines;
        }

        // -----------------------------------------------------------------------
        // Ring bounds resolution (shared by street grid, arterial ring, exits)
        // -----------------------------------------------------------------------

        internal static void ResolveSquareArterialRingBounds(
            Vector2 center,
            CityMathRoadLayoutResolved resolved,
            float ringNorm,
            out float x0,
            out float x1,
            out float z0,
            out float z1)
        {
            ringNorm = Mathf.Clamp01(ringNorm);
            var insetX = resolved.HalfWidthMeters * ringNorm;
            var insetZ = resolved.HalfDepthMeters * ringNorm;
            x0 = center.x - insetX;
            x1 = center.x + insetX;
            z0 = center.y - insetZ;
            z1 = center.y + insetZ;
        }

        /// <summary>
        ///     Ring bounds with a minimum edge-plot guarantee: when a randomized street line lands
        ///     closer to a ring edge than cornerRadius + clearance, that edge is pushed outward
        ///     (past the configured city area) so edge plots never become slivers and streets never
        ///     cross the rounded corner arcs.
        /// </summary>
        internal static void ResolveEffectiveSquareArterialRingBounds(
            CityMathRoadLayout layout,
            CityMathRoadLayoutResolved resolved,
            Vector2 center,
            out float x0,
            out float x1,
            out float z0,
            out float z1)
        {
            ResolveSquareArterialRingBounds(
                center, resolved, layout.arterialRingRadiusNormalized, out x0, out x1, out z0, out z1);

            if (!layout.generateStreetGrid || !layout.clipLocalStreetsToArterialRing)
                return;

            var cornerRadius = ResolveArterialCornerRadius(layout, resolved, x0, x1, z0, z1);
            var minEdgeGap = cornerRadius + CornerJunctionClearanceMeters;

            var xMin = center.x - resolved.HalfWidthMeters;
            var xMax = center.x + resolved.HalfWidthMeters;
            var zMin = center.y - resolved.HalfDepthMeters;
            var zMax = center.y + resolved.HalfDepthMeters;
            var rowLines = BuildAxisStreetLines(zMin, zMax, layout, resolved.Seed, 101);
            var colLines = BuildAxisStreetLines(xMin, xMax, layout, resolved.Seed, 303);

            z0 = ExpandRingEdgeOutwardMin(z0, rowLines, minEdgeGap);
            z1 = ExpandRingEdgeOutwardMax(z1, rowLines, minEdgeGap);
            x0 = ExpandRingEdgeOutwardMin(x0, colLines, minEdgeGap);
            x1 = ExpandRingEdgeOutwardMax(x1, colLines, minEdgeGap);
        }

        internal static float ResolveArterialCornerRadius(
            CityMathRoadLayout layout,
            CityMathRoadLayoutResolved resolved,
            float x0,
            float x1,
            float z0,
            float z1)
        {
            var ringSpan = Mathf.Min(x1 - x0, z1 - z0);
            var maxRadius = Mathf.Max(2f, ringSpan * 0.45f);
            var configured = layout.arterialCornerRadiusMeters;
            if (configured <= 0f)
                configured = Mathf.Clamp(layout.streetSpacingMeters * 0.45f, 12f, 28f);

            return Mathf.Clamp(configured, 2f, maxRadius);
        }

        /// <summary>
        ///     Pushed edges land slack past the exact clearance so streets sit strictly inside the
        ///     ring-chain knot band (FilterOpenInterval excludes values exactly on its bounds).
        /// </summary>
        private const float RingEdgeExpansionSlackMeters = 2f;

        private static float ExpandRingEdgeOutwardMin(float edge, List<float> lines, float minGap)
        {
            for (var guard = 0; guard < 12; guard++)
            {
                var nearestViolation = float.MaxValue;
                var hasViolation = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line > edge + 0.01f && line - edge < minGap && line < nearestViolation)
                    {
                        nearestViolation = line;
                        hasViolation = true;
                    }
                }

                if (!hasViolation)
                    return edge;

                edge = nearestViolation - minGap - RingEdgeExpansionSlackMeters;
            }

            return edge;
        }

        private static float ExpandRingEdgeOutwardMax(float edge, List<float> lines, float minGap)
        {
            for (var guard = 0; guard < 12; guard++)
            {
                var nearestViolation = float.MinValue;
                var hasViolation = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line < edge - 0.01f && edge - line < minGap && line > nearestViolation)
                    {
                        nearestViolation = line;
                        hasViolation = true;
                    }
                }

                if (!hasViolation)
                    return edge;

                edge = nearestViolation + minGap + RingEdgeExpansionSlackMeters;
            }

            return edge;
        }

        // -----------------------------------------------------------------------
        // Shared helpers (used by multiple partial files)
        // -----------------------------------------------------------------------

        private static float ResolveUnifiedCityRoadWidthMeters(RoadNetworkSettings settings)
        {
            return settings != null ? settings.ResolveWidthMeters(RoadClass.Local) : 6f;
        }

        private static List<float> FilterOpenInterval(List<float> values, float min, float max)
        {
            var result = new List<float>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value > min + 0.01f && value < max - 0.01f)
                    result.Add(value);
            }

            return result;
        }

        private static void AppendDistinctPointXZ(List<Vector2> points, Vector2 point)
        {
            if (points.Count > 0 && Vector2.Distance(points[points.Count - 1], point) < 0.5f)
                return;

            points.Add(point);
        }

        private static void AppendStraightLeadMarkers(
            List<Vector2> points,
            Vector2 from,
            Vector2 to,
            bool appendFromSecond = false)
        {
            var distance = Vector2.Distance(from, to);
            if (!appendFromSecond)
                AppendDistinctPointXZ(points, from);

            if (distance >= 8f)
                AppendDistinctPointXZ(points, Vector2.Lerp(from, to, 0.5f));

            if (appendFromSecond)
                AppendDistinctPointXZ(points, to);
        }

        // -----------------------------------------------------------------------
        // RNG helpers
        // -----------------------------------------------------------------------

        private static int MixSeed(int seed, int salt)
        {
            unchecked
            {
                return seed * 73856093 ^ salt * 19349663;
            }
        }

        private static System.Random CreateDerivedRng(int seed, int salt)
        {
            return new System.Random(MixSeed(seed, salt));
        }

        private static float SampleBlockSpacing(float minGap, float maxGap, float jitter, System.Random rng)
        {
            var gap = minGap + (float)rng.NextDouble() * (maxGap - minGap);
            if (jitter > 0.001f)
            {
                var scale = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                gap *= scale;
            }

            return Mathf.Clamp(gap, minGap, maxGap);
        }

        private static void AddAxisAlignedSegment(
            RoadNetworkRuntime network,
            int id,
            RoadClass roadClass,
            float width,
            Vector2 start,
            Vector2 end)
        {
            var road = new RoadPolyline
            {
                id = id,
                roadClass = roadClass,
                widthMeters = width
            };
            road.pointsXZ.Add(start);
            road.pointsXZ.Add(end);
            network.AddRoad(road);
        }

        // -----------------------------------------------------------------------
        // Test / debug helpers
        // -----------------------------------------------------------------------

        /// <summary>
        ///     Minimal layout for debugging EasyRoads T connectors: three arms meeting at center.
        /// </summary>
        public static RoadNetworkRuntime GenerateTIntersectionTest(
            Vector2 center,
            float armLengthMeters = 45f,
            float widthMeters = 6f)
        {
            armLengthMeters = Mathf.Max(10f, armLengthMeters);
            widthMeters = Mathf.Max(3f, widthMeters);

            var network = new RoadNetworkRuntime(9001);
            var width = widthMeters;

            AddAxisAlignedSegment(network, 9001, RoadClass.Local, width,
                new Vector2(center.x, center.y - armLengthMeters), center);
            AddAxisAlignedSegment(network, 9002, RoadClass.Local, width,
                center, new Vector2(center.x, center.y + armLengthMeters));
            AddAxisAlignedSegment(network, 9003, RoadClass.Local, width,
                center, new Vector2(center.x + armLengthMeters, center.y));

            return network;
        }

        public static Rect ComputeTIntersectionTestBounds(Vector2 center, float armLengthMeters = 45f)
        {
            armLengthMeters = Mathf.Max(10f, armLengthMeters);
            var pad = 4f;
            return Rect.MinMaxRect(
                center.x - armLengthMeters - pad,
                center.y - armLengthMeters - pad,
                center.x + armLengthMeters + pad,
                center.y + armLengthMeters + pad);
        }
    }
}
