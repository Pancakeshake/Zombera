using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Pre-sampled world Y heights along a road polyline for consistent corridor flattening.
    /// </summary>
    public sealed partial class PolylineHeightProfile
    {
        private const float GridCellMeters = 32f;

        private readonly IReadOnlyList<Vector2> _points;
        private readonly float[] _heights;
        private readonly Rect _bounds;
        private readonly Dictionary<long, List<int>> _segmentGrid;

        private PolylineHeightProfile(IReadOnlyList<Vector2> points, float[] heights)
        {
            _points = points;
            _heights = heights;
            _bounds = ComputeBounds(points);
            _segmentGrid = BuildSegmentGrid(points, _bounds);
        }

        public bool IsValid => _points != null && _points.Count >= 2 && _heights != null && _heights.Length == _points.Count;

        public Rect BoundsXZ => _bounds;

        public IReadOnlyList<Vector2> PointsXZ => _points;

        public static PolylineHeightProfile Build(
            IReadOnlyList<Vector2> pointsXZ,
            Func<Vector2, float> sampleHeight,
            float resampleSpacingMeters = 12f,
            int smoothingIterations = 2,
            float maxSlopeDegrees = 0f,
            bool pinEndpoints = false,
            Func<Vector2, bool> softPinAt = null,
            float maxDeviationFromSamplesMeters = 0f)
        {
            if (pointsXZ == null || pointsXZ.Count < 2 || sampleHeight == null)
                return null;

            var resampled = RoadMeshBuilder.ResamplePolyline(
                pointsXZ,
                Mathf.Clamp(resampleSpacingMeters, 4f, 24f));
            if (resampled == null || resampled.Count < 2)
                return null;

            var heights = new float[resampled.Count];
            var softPinned = softPinAt != null ? new bool[resampled.Count] : null;
            for (var i = 0; i < resampled.Count; i++)
            {
                heights[i] = sampleHeight(resampled[i]);
                if (softPinned != null)
                    softPinned[i] = softPinAt(resampled[i]);
            }

            // Soft-pin replaces hard endpoint pin (avoids propagating pad Y along the whole path).
            if (softPinned != null)
                pinEndpoints = false;

            var original = softPinned != null ? (float[])heights.Clone() : null;

            SmoothHeightsInPlace(heights, smoothingIterations);
            ClampToSampleDeviation(heights, sampleHeight, resampled, maxDeviationFromSamplesMeters);
            if (maxSlopeDegrees > 0f)
            {
                ClampSlopesInPlace(
                    heights,
                    resampled,
                    Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad),
                    pinEndpoints);
            }

            if (pinEndpoints && maxSlopeDegrees > 0f)
            {
                SmoothHeightsInPlace(heights, 1);
                heights[0] = sampleHeight(resampled[0]);
                heights[heights.Length - 1] = sampleHeight(resampled[resampled.Count - 1]);
                ClampSlopesInPlace(
                    heights,
                    resampled,
                    Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad),
                    pinEndpoints: true);
            }

            if (softPinned != null && original != null && maxSlopeDegrees > 0f)
                ApplySoftPins(heights, original, resampled, softPinned, maxSlopeDegrees);

            return new PolylineHeightProfile(resampled, heights);
        }

        /// <summary>
        ///     Straight grade between polyline endpoint samples (ignores intermediate terrain).
        ///     Used for mountain-tunnel cover detection so valley mouths bridge under ridges
        ///     instead of climbing a slope-clamped terrain-follow bed.
        /// </summary>
        public static PolylineHeightProfile BuildEndpointLerpBed(
            IReadOnlyList<Vector2> pointsXZ,
            Func<Vector2, float> sampleHeight,
            float resampleSpacingMeters = 12f)
        {
            if (pointsXZ == null || pointsXZ.Count < 2 || sampleHeight == null)
                return null;

            var resampled = RoadMeshBuilder.ResamplePolyline(
                pointsXZ,
                Mathf.Clamp(resampleSpacingMeters, 4f, 24f));
            if (resampled == null || resampled.Count < 2)
                return null;

            var heights = new float[resampled.Count];
            var prefix = new float[resampled.Count];
            for (var i = 1; i < resampled.Count; i++)
                prefix[i] = prefix[i - 1] + Vector2.Distance(resampled[i - 1], resampled[i]);

            var total = Mathf.Max(0.01f, prefix[prefix.Length - 1]);
            var y0 = sampleHeight(resampled[0]);
            var y1 = sampleHeight(resampled[resampled.Count - 1]);
            for (var i = 0; i < resampled.Count; i++)
                heights[i] = Mathf.Lerp(y0, y1, prefix[i] / total);

            return new PolylineHeightProfile(resampled, heights);
        }

        private static void ClampToSampleDeviation(
            float[] heights,
            Func<Vector2, float> sampleHeight,
            IReadOnlyList<Vector2> points,
            float maxDeviationMeters)
        {
            if (maxDeviationMeters <= 0f)
                return;

            for (var i = 0; i < heights.Length; i++)
            {
                var sampledHeight = sampleHeight(points[i]);
                heights[i] = Mathf.Clamp(
                    heights[i],
                    sampledHeight - maxDeviationMeters,
                    sampledHeight + maxDeviationMeters);
            }
        }

        private static void ApplySoftPins(
            float[] heights,
            float[] original,
            IReadOnlyList<Vector2> points,
            bool[] softPinned,
            float maxSlopeDegrees)
        {
            var any = false;
            for (var i = 0; i < softPinned.Length; i++)
            {
                if (!softPinned[i])
                    continue;
                heights[i] = original[i];
                any = true;
            }

            if (!any)
                return;

            var ratio = Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad);
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 1; i < heights.Length; i++)
                {
                    if (softPinned[i])
                        continue;
                    ClampStep(heights, points, i, i - 1, ratio);
                }

                for (var i = heights.Length - 2; i >= 0; i--)
                {
                    if (softPinned[i])
                        continue;
                    ClampStep(heights, points, i, i + 1, ratio);
                }
            }

            for (var i = 0; i < softPinned.Length; i++)
            {
                if (softPinned[i])
                    heights[i] = original[i];
            }
        }

        public PolylineHeightProfile WithSmoothedHeights(int iterations = 2)
        {
            if (!IsValid)
                return this;

            var copy = new float[_heights.Length];
            for (var i = 0; i < _heights.Length; i++)
                copy[i] = _heights[i];

            SmoothHeightsInPlace(copy, iterations);
            return new PolylineHeightProfile(_points, copy);
        }

        public float DistanceToPath(Vector2 worldPoint, out Vector2 closestPoint) =>
            QueryNearest(worldPoint, out closestPoint, out _);

        public float SampleHeightAlongPath(Vector2 worldPoint)
        {
            if (!IsValid)
                return 0f;

            QueryNearest(worldPoint, out _, out var height);
            return height;
        }

        /// <summary>Single nearest-segment query returning distance and interpolated path height.</summary>
        public float QueryNearest(Vector2 worldPoint, out Vector2 closestPoint, out float heightAlongPath) =>
            QueryNearest(worldPoint, float.PositiveInfinity, out closestPoint, out heightAlongPath, out _);

        /// <summary>
        ///     Nearest-segment query with an optional search radius. When
        ///     <paramref name="maxUsefulDistanceMeters"/> is finite, only nearby grid rings are
        ///     searched (no full-polyline fallback), which keeps road-bed apply cheap off-path.
        /// </summary>
        public float QueryNearest(
            Vector2 worldPoint,
            float maxUsefulDistanceMeters,
            out Vector2 closestPoint,
            out float heightAlongPath,
            out float localGradeAbs)
        {
            closestPoint = default;
            heightAlongPath = 0f;
            localGradeAbs = 0f;
            if (!IsValid)
                return float.MaxValue;

            var dist = DistancePointToPolyline(
                worldPoint,
                _points,
                _heights,
                _segmentGrid,
                _bounds,
                maxUsefulDistanceMeters,
                out closestPoint,
                out var segmentIndex,
                out var segmentT,
                out localGradeAbs);
            if (dist >= float.MaxValue)
                return dist;

            segmentIndex = Mathf.Clamp(segmentIndex, 0, _heights.Length - 2);
            heightAlongPath = Mathf.Lerp(_heights[segmentIndex], _heights[segmentIndex + 1], segmentT);
            return dist;
        }

        private static void SmoothHeightsInPlace(float[] heights, int iterations)
        {
            if (heights == null || heights.Length < 3 || iterations <= 0)
                return;

            var scratch = new float[heights.Length];
            for (var pass = 0; pass < iterations; pass++)
            {
                scratch[0] = heights[0];
                scratch[heights.Length - 1] = heights[heights.Length - 1];
                for (var i = 1; i < heights.Length - 1; i++)
                    scratch[i] = (heights[i - 1] + heights[i] + heights[i + 1]) / 3f;

                for (var i = 0; i < heights.Length; i++)
                    heights[i] = scratch[i];
            }
        }

        private static void ClampSlopesInPlace(
            float[] heights,
            IReadOnlyList<Vector2> points,
            float maxSlopeRatio,
            bool pinEndpoints = false)
        {
            if (heights == null || points == null || heights.Length < 2 || points.Count != heights.Length ||
                maxSlopeRatio <= 0f)
                return;

            var end0 = heights[0];
            var endN = heights[heights.Length - 1];

            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 1; i < heights.Length; i++)
                    ClampStep(heights, points, i, i - 1, maxSlopeRatio);

                for (var i = heights.Length - 2; i >= 0; i--)
                    ClampStep(heights, points, i, i + 1, maxSlopeRatio);
            }

            if (!pinEndpoints)
                return;

            heights[0] = end0;
            heights[heights.Length - 1] = endN;
            for (var i = 1; i < heights.Length; i++)
                ClampStep(heights, points, i, i - 1, maxSlopeRatio);
            for (var i = heights.Length - 2; i >= 1; i--)
                ClampStep(heights, points, i, i + 1, maxSlopeRatio);
            heights[0] = end0;
            heights[heights.Length - 1] = endN;
        }

        private static void ClampStep(
            float[] heights,
            IReadOnlyList<Vector2> points,
            int fromIndex,
            int toIndex,
            float maxSlopeRatio)
        {
            var distance = Vector2.Distance(points[fromIndex], points[toIndex]);
            if (distance <= 0.01f)
                return;

            var maxDelta = maxSlopeRatio * distance;
            var delta = heights[fromIndex] - heights[toIndex];
            if (Mathf.Abs(delta) <= maxDelta)
                return;

            heights[fromIndex] = heights[toIndex] + Mathf.Sign(delta) * maxDelta;
        }
    }
}
