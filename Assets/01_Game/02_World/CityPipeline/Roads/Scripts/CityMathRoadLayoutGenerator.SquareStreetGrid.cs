using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class CityMathRoadLayoutGenerator
    {
        /// <summary>
        ///     Junction manifest keyed by quantized XZ position. Always null in the uniform
        ///     grid path (varyInnerStreetTopology removed); downstream consumers handle null.
        /// </summary>
        internal static Dictionary<Vector2Int, CityStreetJunctionEntry> LastJunctionManifest { get; }

        private const float CornerDeadEndPullbackMeters = 6f;

        private static void AddSquareStreetGrid(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            var width = ResolveUnifiedCityRoadWidthMeters(settings);
            var xMin = center.x - resolved.HalfWidthMeters;
            var xMax = center.x + resolved.HalfWidthMeters;
            var zMin = center.y - resolved.HalfDepthMeters;
            var zMax = center.y + resolved.HalfDepthMeters;
            var ring = BuildSquareStreetGridRingContext(layout, resolved, center);

            var rowLines = BuildAxisStreetLines(zMin, zMax, layout, resolved.Seed, 101);
            var colLines = BuildAxisStreetLines(xMin, xMax, layout, resolved.Seed, 303);

            EmitSquareStreetGridSegments(
                network,
                new SquareStreetGridSegmentRequest(
                    width, rowLines, horizontal: true, new AxisSpanFallback(xMin, xMax), ring, idBase: 2000));

            EmitSquareStreetGridSegments(
                network,
                new SquareStreetGridSegmentRequest(
                    width, colLines, horizontal: false, new AxisSpanFallback(zMin, zMax), ring, idBase: 3000));
        }

        private static SquareStreetGridRingContext BuildSquareStreetGridRingContext(
            CityMathRoadLayout layout,
            CityMathRoadLayoutResolved resolved,
            Vector2 center)
        {
            var clipToRing = layout.generateArterialRing && layout.clipLocalStreetsToArterialRing;
            if (!clipToRing)
                return new SquareStreetGridRingContext(false, 0f, 0f, 0f, 0f, 0f, false);

            ResolveEffectiveSquareArterialRingBounds(
                layout,
                resolved,
                center,
                out var ringX0,
                out var ringX1,
                out var ringZ0,
                out var ringZ1);

            var cornerRadius = ResolveArterialCornerRadius(layout, resolved, ringX0, ringX1, ringZ0, ringZ1);
            var clampCornerZones = cornerRadius >= 2f &&
                                   ringX1 - ringX0 - cornerRadius * 2f >= 4f &&
                                   ringZ1 - ringZ0 - cornerRadius * 2f >= 4f;
            return new SquareStreetGridRingContext(
                clipToRing, ringX0, ringX1, ringZ0, ringZ1, cornerRadius, clampCornerZones);
        }

        private static void EmitSquareStreetGridSegments(
            RoadNetworkRuntime network,
            SquareStreetGridSegmentRequest request)
        {
            for (var index = 0; index < request.FixedAxisLines.Count; index++)
            {
                var fixedValue = request.FixedAxisLines[index];
                if (IsExcludedSquareStreetGridLine(fixedValue, request.Horizontal, request.Ring))
                    continue;

                ResolveSquareStreetGridSpan(
                    request.Horizontal,
                    request.Fallback.Min,
                    request.Fallback.Max,
                    fixedValue,
                    request.Ring,
                    out var spanMin,
                    out var spanMax);

                if (spanMax - spanMin < 1f)
                    continue;

                if (request.Horizontal)
                {
                    AddAxisAlignedSegment(
                        network,
                        request.IdBase + index,
                        RoadClass.Local,
                        request.Width,
                        new Vector2(spanMin, fixedValue),
                        new Vector2(spanMax, fixedValue));
                }
                else
                {
                    AddAxisAlignedSegment(
                        network,
                        request.IdBase + index,
                        RoadClass.Local,
                        request.Width,
                        new Vector2(fixedValue, spanMin),
                        new Vector2(fixedValue, spanMax));
                }
            }
        }

        private static bool IsExcludedSquareStreetGridLine(
            float fixedAxisValue,
            bool horizontal,
            SquareStreetGridRingContext ring)
        {
            if (!ring.ClipToRing)
                return false;

            return horizontal
                ? fixedAxisValue <= ring.RingZ0 + 0.01f || fixedAxisValue >= ring.RingZ1 - 0.01f
                : fixedAxisValue <= ring.RingX0 + 0.01f || fixedAxisValue >= ring.RingX1 - 0.01f;
        }

        private static void ResolveSquareStreetGridSpan(
            bool horizontal,
            float fallbackMin,
            float fallbackMax,
            float fixedAxisValue,
            SquareStreetGridRingContext ring,
            out float spanMin,
            out float spanMax)
        {
            if (ring.ClipToRing)
            {
                if (horizontal)
                {
                    spanMin = ring.RingX0;
                    spanMax = ring.RingX1;
                }
                else
                {
                    spanMin = ring.RingZ0;
                    spanMax = ring.RingZ1;
                }
            }
            else
            {
                spanMin = fallbackMin;
                spanMax = fallbackMax;
            }

            if (!ring.ClampCornerZones)
                return;

            var edgeDistance = horizontal
                ? Mathf.Min(fixedAxisValue - ring.RingZ0, ring.RingZ1 - fixedAxisValue)
                : Mathf.Min(fixedAxisValue - ring.RingX0, ring.RingX1 - fixedAxisValue);
            var inset = ResolveCornerZoneEdgeInset(edgeDistance, ring.CornerRadius);
            spanMin += inset;
            spanMax -= inset;
        }

        /// <summary>
        ///     Streets crossing the ring near a rounded corner stop short of the arc (dead-end stub)
        ///     instead of forming junctions on the curve, which EasyRoads rejects as too sharp.
        /// </summary>
        private static float ResolveCornerZoneEdgeInset(float edgeDistance, float cornerRadius)
        {
            if (edgeDistance >= cornerRadius + CornerJunctionClearanceMeters)
                return 0f;

            var arcInset = 0f;
            if (edgeDistance < cornerRadius)
            {
                var d = cornerRadius - Mathf.Max(0f, edgeDistance);
                arcInset = cornerRadius - Mathf.Sqrt(Mathf.Max(0f, cornerRadius * cornerRadius - d * d));
            }

            return arcInset + CornerDeadEndPullbackMeters;
        }

    }
}