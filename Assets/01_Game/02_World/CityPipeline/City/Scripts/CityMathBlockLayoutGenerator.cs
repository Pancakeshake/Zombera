using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Derives city block rectangles from the same math grid as <see cref="CityMathRoadLayoutGenerator"/>,
    ///     then assigns seed-stable districts (CityCore center, R/I neighborhoods, scattered commercial
    ///     storefront blocks, military outposts anywhere in the city, hospitals).
    /// </summary>
    public static class CityMathBlockLayoutGenerator
    {
        private const float MinBlockSpanMeters = 12f;

        public static IReadOnlyList<CityNamedArea> Generate(
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            int seed = 0,
            IReadOnlyList<CityDistrictWeight> districtMix = null)
        {
            if (layout == null || !layout.generateStreetGrid)
                return Array.Empty<CityNamedArea>();

            layout.Normalize();
            var resolved = layout.Resolve(seed);
            var effectiveSeed = resolved.Seed;
            var center = layout.centerXZ;
            var blocks = BuildBlockRects(layout, resolved, settings);
            if (blocks.Count == 0)
                return Array.Empty<CityNamedArea>();

            CityDistrictClusterPlanner.AssignClusters(blocks, center, effectiveSeed, districtMix);
            return blocks;
        }

        private static List<CityNamedArea> BuildBlockRects(
            CityMathRoadLayout layout,
            CityMathRoadLayoutResolved resolved,
            RoadNetworkSettings settings)
        {
            var districtInset = ResolveDistrictBlockInsetMeters(settings);
            var center = layout.centerXZ;
            var xMin = center.x - resolved.HalfWidthMeters;
            var xMax = center.x + resolved.HalfWidthMeters;
            var zMin = center.y - resolved.HalfDepthMeters;
            var zMax = center.y + resolved.HalfDepthMeters;

            var hasRingCorners = layout.generateArterialRing && layout.clipLocalStreetsToArterialRing;
            var ring = ResolveRingBounds(layout, resolved, center, hasRingCorners);

            var colLines = CityMathRoadLayoutGenerator.BuildAxisStreetLinesPublic(
                xMin, xMax, layout, resolved.Seed, 303);
            var rowLines = CityMathRoadLayoutGenerator.BuildAxisStreetLinesPublic(
                zMin, zMax, layout, resolved.Seed, 101);

            // Base xKnots: the uniform column positions (no offset adjustments).
            var baseXKnots = hasRingCorners
                ? BuildKnotChain(ring.X0, ring.X1, FilterOpenInterval(colLines, ring.X0, ring.X1))
                : BuildKnotChain(xMin, xMax, colLines);
            var zKnots = hasRingCorners
                ? BuildKnotChain(ring.Z0, ring.Z1, FilterOpenInterval(rowLines, ring.Z0, ring.Z1))
                : BuildKnotChain(zMin, zMax, rowLines);

            // No vertical merging — each z-band IS a block row bounded by horizontal roads.
            return BuildBlockAreas(baseXKnots, zKnots, districtInset, hasRingCorners, ring.CornerRadius);
        }

        private struct CityRingBounds
        {
            public float X0;
            public float X1;
            public float Z0;
            public float Z1;
            public float CornerRadius;
        }

        private static CityRingBounds ResolveRingBounds(
            CityMathRoadLayout layout,
            CityMathRoadLayoutResolved resolved,
            Vector2 center,
            bool hasRingCorners)
        {
            var ring = new CityRingBounds();
            if (!hasRingCorners)
                return ring;

            CityMathRoadLayoutGenerator.ResolveEffectiveSquareArterialRingBounds(
                layout, resolved, center,
                out ring.X0, out ring.X1, out ring.Z0, out ring.Z1);
            ring.CornerRadius = CityMathRoadLayoutGenerator.ResolveArterialCornerRadius(
                layout, resolved, ring.X0, ring.X1, ring.Z0, ring.Z1);
            return ring;
        }

        private static List<CityNamedArea> BuildBlockAreas(
            List<float> baseXKnots,
            List<float> zKnots,
            float districtInset,
            bool hasRingCorners,
            float arterialCornerRadius)
        {
            var zBandCount = zKnots.Count - 1;
            var nextId = 5000;
            var lastXi = baseXKnots.Count - 2;
            var rawBlocks = new List<CityNamedArea>();

            for (var zi = 0; zi < zBandCount; zi++)
            {
                for (var xi = 0; xi < baseXKnots.Count - 1; xi++)
                {
                    var rect = Rect.MinMaxRect(
                        baseXKnots[xi] + districtInset,
                        zKnots[zi] + districtInset,
                        baseXKnots[xi + 1] - districtInset,
                        zKnots[zi + 1] - districtInset);

                    if (rect.width < MinBlockSpanMeters || rect.height < MinBlockSpanMeters)
                        continue;

                    var roundedCorners = ResolveBlockCornerMask(
                        hasRingCorners, arterialCornerRadius, xi, zi, lastXi, zBandCount);

                    var outline = CityNamedAreaOutlineBuilder.BuildOutline(
                        rect, roundedCorners, arterialCornerRadius, districtInset);

                    rawBlocks.Add(CreateBlockArea(
                        nextId++, xi, zi, rect, roundedCorners, arterialCornerRadius, outline));
                }
            }

            return rawBlocks;
        }

        private static CityBlockCornerMask ResolveBlockCornerMask(
            bool hasRingCorners, float arterialCornerRadius, int xi, int zi, int lastXi, int zBandCount)
        {
            if (!hasRingCorners || arterialCornerRadius < 2f)
                return CityBlockCornerMask.None;

            var mask = CityBlockCornerMask.None;
            if (xi == 0 && zi == 0)
                mask |= CityBlockCornerMask.BottomLeft;
            if (xi == lastXi && zi == 0)
                mask |= CityBlockCornerMask.BottomRight;
            if (xi == lastXi && zi == zBandCount - 1)
                mask |= CityBlockCornerMask.TopRight;
            if (xi == 0 && zi == zBandCount - 1)
                mask |= CityBlockCornerMask.TopLeft;
            return mask;
        }

        private static CityNamedArea CreateBlockArea(
            int id, int xi, int zi, Rect rect, CityBlockCornerMask roundedCorners,
            float arterialCornerRadius, List<Vector2> outline)
        {
            return new CityNamedArea
            {
                id = id,
                gridX = xi,
                gridZ = zi,
                boundsXZ = rect,
                centerXZ = CityNamedAreaOutlineBuilder.ComputePolygonCentroid(outline),
                areaSquareMeters = CityNamedAreaOutlineBuilder.ComputePolygonArea(outline),
                roundedCorners = roundedCorners,
                arterialCornerRadiusMeters = arterialCornerRadius,
                outlineXZ = outline.ToArray(),
                districtType = CityDistrictType.Mixed,
                displayName = "Mixed",
                clusterName = string.Empty
            };
        }

        /// <summary>
        ///     Footpath corridor width beyond the sidewalk edge (curb extra + footpath width).
        ///     Returns zero when footpath meshes are disabled.
        /// </summary>
        public static float ResolveFootpathCorridorInsetMeters(RoadNetworkSettings settings)
        {
            if (settings == null || !settings.spawnFootpathMeshes)
                return 0f;
            return Mathf.Max(0f, settings.footpathCurbExtraMeters)
                 + Mathf.Max(0.5f, settings.footpathWidthMeters);
        }

        /// <summary>
        ///     World-space inset from each street centerline to the district block outline edge.
        ///     Half road width plus sidewalk/curb margin — excludes the footpath corridor band
        ///     (footpaths sit on the outer district outline; lots reserve corridor separately).
        /// </summary>
        public static float ResolveDistrictBlockInsetMeters(RoadNetworkSettings settings)
        {
            return ResolveRoadHalfWidth(settings)
                 + ResolveSidewalkOrCurbMarginMeters(settings);
        }

        private static float ResolveSidewalkOrCurbMarginMeters(RoadNetworkSettings settings)
        {
            if (settings != null && settings.spawnProceduralSidewalkMeshes)
            {
                return Mathf.Max(0.25f, settings.kerbWidthMeters)
                     + Mathf.Max(0.25f, settings.proceduralSidewalkWidthMeters);
            }

            if (settings != null && settings.spawnSidewalkMeshes)
                return settings.ResolveSidewalkWidthMeters();

            return 1f;
        }

        private static float ResolveRoadHalfWidth(RoadNetworkSettings settings)
        {
            var width = settings != null ? settings.ResolveWidthMeters(RoadClass.Local) : 6f;
            return Mathf.Max(1.5f, width * 0.5f);
        }

        private static List<float> BuildKnotChain(float edgeMin, float edgeMax, List<float> interior)
        {
            var knots = new List<float>(interior.Count + 2) { edgeMin };
            knots.AddRange(interior);
            knots.Add(edgeMax);
            return knots;
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

    }
}
