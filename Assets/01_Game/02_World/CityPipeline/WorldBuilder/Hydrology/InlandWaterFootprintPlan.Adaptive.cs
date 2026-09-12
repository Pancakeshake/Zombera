using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Adaptive control-point density. Keyed points (sources, mouths, junctions, lake
    /// connections, major bends, width extrema, elevation transitions) always survive; everything
    /// else is removed while lateral bank error, width interpolation error and height error stay
    /// inside the profile tolerances, and inserted back wherever spacing demands it.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        private const int MaxSpacingRefinements = 4096;

        /// <summary>
        /// Source index handed to inserted controls. Negative so it can never collide with a real
        /// <see cref="RiverPolyline"/> index, keeping the carver's width/clearance lookup unambiguous.
        /// </summary>
        private int _nextInsertedSourceIndex = -1;

        public void ApplyAdaptiveControlPoints(InlandWaterFootprintOptions options)
        {
            var resolved = options.Normalized();
            for (var i = 0; i < _features.Count; i++)
            {
                SimplifyFeature(_features[i], resolved);
                EnforceSpacing(_features[i], resolved);
            }
        }

        /// <summary>
        /// Inserts a control point into a feature. Used by the repair pass when the fitted Crest
        /// ribbon drifts past the carved edge tolerance.
        /// </summary>
        public bool InsertPoint(
            ulong stableId,
            int index,
            Vector2 centerXZ,
            float halfWidthMeters,
            float surfaceWorldY,
            PointRole role = PointRole.Inserted)
        {
            if (!_byId.TryGetValue(stableId, out var feature) || feature.Points.Count == 0)
                return false;

            var insertAt = Mathf.Clamp(index, 0, feature.Points.Count);
            var template = feature.Points[Mathf.Clamp(insertAt, 0, feature.Points.Count - 1)];
            var sourceIndex = _nextInsertedSourceIndex--;
            feature.Points.Insert(insertAt, new Point
            {
                CenterXZ = centerXZ,
                SurfaceWorldY = surfaceWorldY,
                TargetWetHalfWidthMeters = Mathf.Max(0.5f, halfWidthMeters),
                BankShoulderMeters = template.BankShoulderMeters,
                RequestedBedClearanceMeters = template.RequestedBedClearanceMeters,
                Role = role,
                SourceIndex = sourceIndex
            });
            return true;
        }

        private static void SimplifyFeature(Feature feature, in InlandWaterFootprintOptions options)
        {
            var points = feature.Points;
            if (points.Count <= 2)
                return;

            var pinned = CollectPinnedIndices(points);
            if (pinned.Count >= points.Count)
                return;

            var keep = new bool[points.Count];
            for (var i = 0; i < pinned.Count; i++)
                keep[pinned[i]] = true;

            for (var i = 0; i < pinned.Count - 1; i++)
                SimplifySpan(points, pinned[i], pinned[i + 1], options, keep);

            var kept = new List<Point>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                if (keep[i])
                    kept.Add(points[i]);
            }

            if (kept.Count < 2)
                return;

            points.Clear();
            points.AddRange(kept);
        }

        private static List<int> CollectPinnedIndices(List<Point> points)
        {
            var pinned = new List<int>(points.Count) { 0 };
            for (var i = 1; i < points.Count - 1; i++)
            {
                if (points[i].IsPinned)
                    pinned.Add(i);
            }

            pinned.Add(points.Count - 1);
            return pinned;
        }

        /// <summary>
        /// Douglas-Peucker over the combined lateral/width/height error. Runs on an explicit stack
        /// so long river polylines cannot overflow the call stack.
        /// </summary>
        private static void SimplifySpan(
            List<Point> points,
            int from,
            int to,
            in InlandWaterFootprintOptions options,
            bool[] keep)
        {
            if (to - from < 2)
                return;

            var stack = new Stack<(int from, int to)>();
            stack.Push((from, to));
            while (stack.Count > 0)
            {
                var (start, end) = stack.Pop();
                if (end - start < 2)
                    continue;

                var worstIndex = -1;
                var worstError = 1f;
                for (var i = start + 1; i < end; i++)
                {
                    var error = ResolveSpanError(points, start, end, i, options);
                    if (error <= worstError)
                        continue;
                    worstError = error;
                    worstIndex = i;
                }

                if (worstIndex < 0)
                    continue;

                keep[worstIndex] = true;
                stack.Push((start, worstIndex));
                stack.Push((worstIndex, end));
            }
        }

        /// <summary>Normalized error at a point; 1.0 means exactly at tolerance.</summary>
        private static float ResolveSpanError(
            List<Point> points,
            int from,
            int to,
            int index,
            in InlandWaterFootprintOptions options)
        {
            var a = points[from];
            var b = points[to];
            var p = points[index];
            var span = b.CenterXZ - a.CenterXZ;
            var lengthSq = span.sqrMagnitude;
            var t = lengthSq > 1e-6f
                ? Mathf.Clamp01(Vector2.Dot(p.CenterXZ - a.CenterXZ, span) / lengthSq)
                : 0f;

            var lateral = Vector2.Distance(p.CenterXZ, Vector2.Lerp(a.CenterXZ, b.CenterXZ, t));
            var width = Mathf.Abs(p.TargetWetHalfWidthMeters -
                                  Mathf.Lerp(a.TargetWetHalfWidthMeters, b.TargetWetHalfWidthMeters, t));
            var height = Mathf.Abs(p.SurfaceWorldY - Mathf.Lerp(a.SurfaceWorldY, b.SurfaceWorldY, t));

            return Mathf.Max(
                lateral / options.LateralToleranceMeters,
                Mathf.Max(
                    width / options.WidthToleranceMeters,
                    height / options.HeightToleranceMeters));
        }

        private static void EnforceSpacing(Feature feature, in InlandWaterFootprintOptions options)
        {
            var points = feature.Points;
            if (points.Count < 2)
                return;

            RefineLongSegments(points, options.MaxPointSpacingMeters);
            MergeShortSegments(points, options.MinPointSpacingMeters);
        }

        private static void RefineLongSegments(List<Point> points, float maxSpacingMeters)
        {
            if (maxSpacingMeters <= 0f)
                return;

            var guard = 0;
            while (guard++ < MaxSpacingRefinements)
            {
                var inserted = false;
                for (var i = 0; i < points.Count - 1; i++)
                {
                    if (Vector2.Distance(points[i].CenterXZ, points[i + 1].CenterXZ) <= maxSpacingMeters)
                        continue;
                    points.Insert(i + 1, InterpolatePoint(points[i], points[i + 1], 0.5f));
                    inserted = true;
                    i++;
                }

                if (!inserted)
                    return;
            }
        }

        private static void MergeShortSegments(List<Point> points, float minSpacingMeters)
        {
            if (minSpacingMeters <= 0f)
                return;

            for (var i = points.Count - 2; i >= 1 && points.Count > 2; i--)
            {
                if (points[i].IsPinned)
                    continue;
                if (Vector2.Distance(points[i - 1].CenterXZ, points[i].CenterXZ) >= minSpacingMeters)
                    continue;
                points.RemoveAt(i);
            }
        }

        private static Point InterpolatePoint(Point from, Point to, float t) => new()
        {
            CenterXZ = Vector2.Lerp(from.CenterXZ, to.CenterXZ, t),
            SurfaceWorldY = Mathf.Lerp(from.SurfaceWorldY, to.SurfaceWorldY, t),
            TargetWetHalfWidthMeters = Mathf.Lerp(
                from.TargetWetHalfWidthMeters, to.TargetWetHalfWidthMeters, t),
            BankShoulderMeters = Mathf.Max(from.BankShoulderMeters, to.BankShoulderMeters),
            RequestedBedClearanceMeters = Mathf.Max(
                from.RequestedBedClearanceMeters, to.RequestedBedClearanceMeters),
            IsJunction = false,
            Role = PointRole.Inserted
        };
    }
}
