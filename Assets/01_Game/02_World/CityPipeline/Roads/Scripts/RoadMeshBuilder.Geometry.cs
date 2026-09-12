using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class RoadMeshBuilder
    {
        /// <summary>
        ///     Offsets a polyline by a given distance using proper miter-joint corner handling.
        ///     At each vertex the two adjacent segment normals are averaged; for sharp turns
        ///     the miter length is capped to avoid spike-like stretched geometry.
        /// </summary>
        internal static List<Vector2> OffsetPolyline(IReadOnlyList<Vector2> polyline, float offsetMeters)
        {
            var output = new List<Vector2>(polyline?.Count ?? 0);
            if (polyline == null || polyline.Count == 0) return output;
            if (Mathf.Abs(offsetMeters) < 0.001f)
            {
                output.AddRange(polyline);
                return output;
            }

            const float miterLimit = 2.5f; // cap miter length relative to offset to avoid spikes

            for (var i = 0; i < polyline.Count; i++)
            {
                var current = polyline[i];

                // Direction of the segment BEFORE this vertex (or same for endpoints)
                Vector2 segDirBefore;
                if (i > 0)
                {
                    segDirBefore = (current - polyline[i - 1]).normalized;
                }
                else
                {
                    segDirBefore = (polyline[Mathf.Min(i + 1, polyline.Count - 1)] - current).normalized;
                }

                // Direction of the segment AFTER this vertex (or same for endpoints)
                Vector2 segDirAfter;
                if (i < polyline.Count - 1)
                {
                    segDirAfter = (polyline[i + 1] - current).normalized;
                }
                else
                {
                    segDirAfter = (current - polyline[Mathf.Max(i - 1, 0)]).normalized;
                }

                // Normals (perpendicular, to the right) for each segment
                var normalBefore = new Vector2(-segDirBefore.y, segDirBefore.x);
                var normalAfter = new Vector2(-segDirAfter.y, segDirAfter.x);

                // Offsets of the two segment lines at this vertex
                var offsetBefore = current + normalBefore * offsetMeters;
                var offsetAfter = current + normalAfter * offsetMeters;

                // If the two segment directions are nearly parallel, just use the average normal
                var dot = Vector2.Dot(segDirBefore, segDirAfter);
                if (dot > 0.999f || i == 0 || i == polyline.Count - 1)
                {
                    var avgNormal = (normalBefore + normalAfter) * 0.5f;
                    if (avgNormal.sqrMagnitude < 0.0001f)
                        avgNormal = normalBefore;
                    avgNormal.Normalize();
                    output.Add(current + avgNormal * offsetMeters);
                    continue;
                }

                // Compute the miter intersection of the two offset segment lines.
                // Line 1: offsetBefore + t * segDirBefore
                // Line 2: offsetAfter  + s * segDirAfter
                if (!LineIntersection2D(offsetBefore, segDirBefore, offsetAfter, segDirAfter, out var miterPoint))
                {
                    // Parallel lines — fall back to average normal
                    var avgNormal = (normalBefore + normalAfter).normalized;
                    output.Add(current + avgNormal * offsetMeters);
                    continue;
                }

                // Clamp miter length to avoid spikes on sharp corners
                var miterVec = miterPoint - current;
                var miterLen = miterVec.magnitude;
                var maxMiter = Mathf.Abs(offsetMeters) * miterLimit;
                if (miterLen > maxMiter)
                {
                    // Bevel: use the two offset points instead of the miter intersection
                    output.Add(offsetBefore);
                    output.Add(offsetAfter);
                }
                else
                {
                    output.Add(miterPoint);
                }
            }

            // Deduplicate consecutive points that are extremely close
            for (var i = output.Count - 1; i > 0; i--)
            {
                if ((output[i] - output[i - 1]).sqrMagnitude < 0.0001f)
                    output.RemoveAt(i);
            }

            return output;
        }

        /// <summary>
        ///     Computes the intersection of two 2D lines given as (origin, direction).
        ///     Line 1: o1 + t * d1,  Line 2: o2 + s * d2.
        /// </summary>
        private static bool LineIntersection2D(
            Vector2 o1, Vector2 d1,
            Vector2 o2, Vector2 d2,
            out Vector2 intersection)
        {
            intersection = Vector2.zero;

            var cross = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(cross) < 0.000001f)
                return false;

            var delta = o2 - o1;
            var t = (delta.x * d2.y - delta.y * d2.x) / cross;

            intersection = o1 + d1 * t;
            return true;
        }

        internal static List<Vector2> ResamplePolyline(IReadOnlyList<Vector2> points, float stepMeters)
        {
            var outPts = new List<Vector2>(256);
            if (points == null || points.Count < 2) return outPts;

            outPts.Add(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var segLen = Vector2.Distance(a, b);
                if (segLen < 0.001f) continue;

                var steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.25f, stepMeters)));
                for (var s = 1; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    outPts.Add(Vector2.Lerp(a, b, t));
                }
            }

            for (var i = outPts.Count - 1; i > 0; i--)
                if ((outPts[i] - outPts[i - 1]).sqrMagnitude < 0.0001f)
                    outPts.RemoveAt(i);

            return outPts;
        }

        public static List<List<Vector2>> ExtractOverlappingSubPolylines(IReadOnlyList<Vector2> polyline, Rect rect)
        {
            var result = new List<List<Vector2>>();
            if (polyline == null || polyline.Count < 2) return result;

            List<Vector2> current = null;
            for (var i = 1; i < polyline.Count; i++)
            {
                var a = polyline[i - 1];
                var b = polyline[i];
                var segBounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x),
                    Mathf.Max(a.y, b.y));

                if (!segBounds.Overlaps(rect, true))
                {
                    if (current != null && current.Count >= 2) result.Add(current);
                    current = null;
                    continue;
                }

                current ??= new List<Vector2>();
                if (current.Count == 0) current.Add(a);
                current.Add(b);
            }

            if (current != null && current.Count >= 2) result.Add(current);

            const float seamPadMeters = 10f;
            for (var i = 0; i < result.Count; i++) ExtendEnds(result[i], seamPadMeters);

            return result;
        }

        private static void ExtendEnds(List<Vector2> pts, float pad)
        {
            if (pts == null || pts.Count < 2 || pad <= 0f) return;

            var a0 = pts[0];
            var a1 = pts[1];
            var dirA = a0 - a1;
            if (dirA.sqrMagnitude > 0.0001f) pts[0] = a0 + dirA.normalized * pad;

            var b0 = pts[^1];
            var b1 = pts[^2];
            var dirB = b0 - b1;
            if (dirB.sqrMagnitude > 0.0001f) pts[^1] = b0 + dirB.normalized * pad;
        }

        internal static List<Vector2> ResamplePolylineUniformCount(IReadOnlyList<Vector2> points, int targetCount)
        {
            var outPts = new List<Vector2>(targetCount);
            if (points == null || points.Count < 2 || targetCount < 2)
                return outPts;

            var cumulative = new float[points.Count];
            for (var i = 1; i < points.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);

            var total = cumulative[^1];
            if (total < 0.001f)
            {
                outPts.Add(points[0]);
                while (outPts.Count < targetCount)
                    outPts.Add(points[^1]);
                return outPts;
            }

            for (var sample = 0; sample < targetCount; sample++)
            {
                var dist = sample * total / (targetCount - 1);
                var segment = 0;
                while (segment < points.Count - 2 && cumulative[segment + 1] < dist - 0.0001f)
                    segment++;

                if (segment >= points.Count - 1)
                {
                    outPts.Add(points[^1]);
                    continue;
                }

                var segLen = cumulative[segment + 1] - cumulative[segment];
                var frac = segLen > 0.0001f ? (dist - cumulative[segment]) / segLen : 0f;
                outPts.Add(Vector2.Lerp(points[segment], points[segment + 1], frac));
            }

            return outPts;
        }
    }
}
