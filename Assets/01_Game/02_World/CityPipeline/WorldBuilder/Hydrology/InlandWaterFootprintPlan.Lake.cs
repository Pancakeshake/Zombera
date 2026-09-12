using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Lake features are fitted as a medial spine: a single line of control points through the
    /// basin whose per-point half-width reproduces the measured cross-section. Every point keeps
    /// one constant <see cref="LakeRecord.SurfaceWorldY"/> so a lake can never render a step.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        private const int MaxLakeSpinePoints = 48;
        private const int LakeSpineScanSteps = 64;
        private const float LakeSpineSpacingMeters = 64f;

        private readonly struct SpineSample
        {
            public readonly Vector2 Center;
            public readonly float HalfWidth;

            public SpineSample(Vector2 center, float halfWidth)
            {
                Center = center;
                HalfWidth = halfWidth;
            }
        }

        private static void AddLakes(
            InlandWaterFootprintPlan result,
            HydrologyPlan plan,
            HydrologyProfile profile)
        {
            if (plan.Lakes == null)
                return;
            for (var i = 0; i < plan.Lakes.Length; i++)
            {
                var lake = plan.Lakes[i];
                if (lake == null)
                    continue;
                AddFeature(result, BuildLakeFeature(lake, profile));
            }
        }

        private static Feature BuildLakeFeature(LakeRecord lake, HydrologyProfile profile)
        {
            var feature = new Feature
            {
                StableId = lake.StableId,
                Kind = FeatureKind.Lake,
                Closed = false,
                SurfaceWorldY = lake.SurfaceWorldY
            };

            var spine = ResolveLakeSpine(lake, profile);
            var shoulder = Mathf.Max(0f, profile.CarveShoulderWidthMeters);
            // Lake beds are flooded cell-by-cell, so the requested clearance has to match the
            // clearance the bathymetry pass is able to guarantee.
            var clearance = Mathf.Max(0.5f, profile.MinLakeBedDepthBelowSea);
            for (var i = 0; i < spine.Count; i++)
            {
                feature.Points.Add(new Point
                {
                    CenterXZ = spine[i].Center,
                    SurfaceWorldY = lake.SurfaceWorldY,
                    TargetWetHalfWidthMeters = Mathf.Max(0.5f, spine[i].HalfWidth),
                    BankShoulderMeters = shoulder,
                    RequestedBedClearanceMeters = clearance,
                    Role = ResolveLakeEndpointRole(i, spine.Count)
                });
            }

            return feature;
        }

        private static PointRole ResolveLakeEndpointRole(int index, int count)
        {
            if (index == 0)
                return PointRole.Source | PointRole.LakeConnection;
            return index == count - 1 ? PointRole.Mouth | PointRole.LakeConnection : PointRole.None;
        }

        private static List<SpineSample> ResolveLakeSpine(LakeRecord lake, HydrologyProfile profile)
        {
            if (TryBuildOutlineSpine(lake, out var spine))
                return spine;
            if (TryBuildBasinSpine(lake, out spine))
                return spine;
            return BuildBoundsSpine(lake, profile);
        }

        /// <summary>Preferred: measure real cross-sections against the ordered shoreline loop.</summary>
        private static bool TryBuildOutlineSpine(LakeRecord lake, out List<SpineSample> spine)
        {
            spine = null;
            var outline = lake.OutlineXZ;
            if (outline == null || outline.Length < 4)
                return false;
            // The traced shoreline repeats its first vertex; that duplicate would skew the principal
            // axis and the sampled span, so fit against the de-duplicated ring.
            if (!TryResolveMajorAxis(WithoutClosingDuplicate(outline), out var centroid, out var axis))
                return false;

            var perpendicular = new Vector2(-axis.y, axis.x);
            ResolveAxisExtent(WithoutClosingDuplicate(outline), centroid, axis, out var min, out var max);
            var length = max - min;
            if (length < 1f)
                return false;

            var count = Mathf.Clamp(
                Mathf.CeilToInt(length / LakeSpineSpacingMeters) + 1, 2, MaxLakeSpinePoints);
            var scanStep = Mathf.Max(0.5f, length / (LakeSpineScanSteps * 2f));
            // Inset the sampled span so every origin sits strictly inside the shoreline. Sampling
            // exactly on the boundary makes the even-odd inside test arbitrary, which loses the end
            // points (the traced outline also repeats its first vertex, which skews the extremes).
            var inset = Mathf.Min(length * 0.25f, Mathf.Max(scanStep, length * 0.02f));
            if (length > inset * 4f)
            {
                min += inset;
                max -= inset;
            }

            var samples = new List<SpineSample>(count);
            for (var i = 0; i < count; i++)
            {
                var origin = centroid + axis * Mathf.Lerp(min, max, i / (float)(count - 1));
                var left = ScanInside(outline, origin, perpendicular, scanStep);
                var right = ScanInside(outline, origin, -perpendicular, scanStep);
                if (left < 0.5f && right < 0.5f)
                    continue;
                samples.Add(new SpineSample(
                    origin + perpendicular * ((left - right) * 0.5f),
                    (left + right) * 0.5f));
            }

            if (samples.Count < 2)
                return false;
            spine = samples;
            return true;
        }

        /// <summary>Fallback: derive widths from the basin cell raster when no shoreline loop exists.</summary>
        private static bool TryBuildBasinSpine(LakeRecord lake, out List<SpineSample> spine)
        {
            spine = null;
            var cells = lake.BasinCellCentersXZ;
            if (cells == null || cells.Length < 3)
                return false;
            if (!TryResolveMajorAxis(cells, out var centroid, out var axis))
                return false;

            var perpendicular = new Vector2(-axis.y, axis.x);
            ResolveAxisExtent(cells, centroid, axis, out var min, out var max);
            var length = max - min;
            if (length < 1f)
                return false;

            var count = Mathf.Clamp(
                Mathf.CeilToInt(length / LakeSpineSpacingMeters) + 1, 2, MaxLakeSpinePoints);
            var band = Mathf.Max(2f, length / (count - 1) * 0.75f);
            var samples = new List<SpineSample>(count);
            for (var i = 0; i < count; i++)
            {
                var t = Mathf.Lerp(min, max, i / (float)(count - 1));
                var origin = centroid + axis * t;
                var halfWidth = 0f;
                var found = false;
                for (var c = 0; c < cells.Length; c++)
                {
                    var offset = cells[c] - centroid;
                    if (Mathf.Abs(Vector2.Dot(offset, axis) - t) > band)
                        continue;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector2.Dot(offset, perpendicular)));
                    found = true;
                }

                if (!found)
                    continue;
                samples.Add(new SpineSample(origin, halfWidth));
            }

            if (samples.Count < 2)
                return false;
            spine = samples;
            return true;
        }

        /// <summary>
        /// Last resort: a straight spine with one constant width from the lake bounds. Always emits two
        /// distinct points so a bounds-only lake can never reach the hard-failing water stage degenerate.
        /// </summary>
        private static List<SpineSample> BuildBoundsSpine(LakeRecord lake, HydrologyProfile profile)
        {
            var bounds = lake.BoundsXZ;
            var horizontal = bounds.width >= bounds.height;
            var span = Mathf.Max(1f, horizontal ? bounds.width : bounds.height);
            var halfWidth = Mathf.Max(
                profile.MinRiverWidthMeters * 0.5f,
                (horizontal ? bounds.height : bounds.width) * 0.5f);
            var axis = horizontal ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            var halfSpan = Mathf.Max(span * 0.5f, halfWidth);
            return new List<SpineSample>(2)
            {
                new(bounds.center - axis * halfSpan, halfWidth),
                new(bounds.center + axis * halfSpan, halfWidth)
            };
        }

        /// <summary>
        /// Drops a trailing vertex that repeats the first one, which is how the lake outline tracer
        /// closes its ring.
        /// </summary>
        private static IReadOnlyList<Vector2> WithoutClosingDuplicate(Vector2[] ring)
        {
            if (ring.Length < 2)
                return ring;
            var last = ring[ring.Length - 1];
            var first = ring[0];
            if ((last - first).sqrMagnitude > 0.0001f)
                return ring;

            var trimmed = new Vector2[ring.Length - 1];
            System.Array.Copy(ring, trimmed, trimmed.Length);
            return trimmed;
        }

        private static bool TryResolveMajorAxis(
            IReadOnlyList<Vector2> points,
            out Vector2 centroid,
            out Vector2 axis)
        {
            centroid = Vector2.zero;
            axis = Vector2.right;
            if (points == null || points.Count == 0)
                return false;

            for (var i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            float sxx = 0f, sxz = 0f, szz = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].x - centroid.x;
                var dz = points[i].y - centroid.y;
                sxx += dx * dx;
                sxz += dx * dz;
                szz += dz * dz;
            }

            if (sxx + szz < 1e-4f)
                return false;

            var half = (sxx + szz) * 0.5f;
            var lambda = half + Mathf.Sqrt(((sxx - szz) * 0.5f) * ((sxx - szz) * 0.5f) + sxz * sxz);
            axis = Mathf.Abs(sxz) > 1e-6f
                ? new Vector2(lambda - szz, sxz)
                : (sxx >= szz ? Vector2.right : Vector2.up);
            axis = axis.sqrMagnitude < 1e-8f ? Vector2.right : axis.normalized;
            return true;
        }

        private static void ResolveAxisExtent(
            IReadOnlyList<Vector2> points,
            Vector2 centroid,
            Vector2 axis,
            out float min,
            out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (var i = 0; i < points.Count; i++)
            {
                var t = Vector2.Dot(points[i] - centroid, axis);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
            }
        }

        private static float ScanInside(
            IReadOnlyList<Vector2> polygon,
            Vector2 origin,
            Vector2 direction,
            float step)
        {
            var reach = step * LakeSpineScanSteps;
            var last = 0f;
            for (var distance = step; distance <= reach; distance += step)
            {
                if (!IsInsidePolygon(polygon, origin + direction * distance))
                    break;
                last = distance;
            }

            return last;
        }

        private static bool IsInsidePolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            var count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (a.y > point.y == b.y > point.y)
                    continue;
                var x = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < x)
                    inside = !inside;
            }

            return inside;
        }
    }
}
