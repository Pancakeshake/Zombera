using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Bit-faithful reproduction of Crest's spline-to-ribbon sampling so a footprint can be
    /// validated against the real ribbon before any Crest object exists.
    /// <para>
    /// Mirrors <c>Crest.Spline.SplineInterpolation</c> (Catmull-Rom hull, tangent scale 0.39)
    /// and <c>Crest.ShapeGerstnerSplineHandling.GenerateMeshFromSpline</c>: uniform normalized-t
    /// sampling at <c>spacing = 16 / 2^(subdivisions + 1)</c>, centred offset, overlap resolution,
    /// then five edge-smoothing passes.
    /// </para>
    /// <para>
    /// Full ribbon width is <c>Spline.Radius * RadiusMultiplier</c>, which is what makes
    /// <c>RadiusMultiplier = targetWetWidthMeters / Spline.Radius</c> the only correct mapping.
    /// </para>
    /// </summary>
    public sealed class CrestRibbonSampler
    {
        /// <summary>Crest's tangent handle scale (shape of the resulting curve).</summary>
        public const float TangentScale = 0.39f;

        /// <summary>Crest's base sampling spacing in metres before subdivision scaling.</summary>
        public const float BaseSpacingMeters = 16f;

        private const int SmoothingIterations = 5;
        private const int MaxSubdivisions = 12;

        private Vector3[] _controls = Array.Empty<Vector3>();
        private float[] _controlMultipliers = Array.Empty<float>();
        private Vector3[] _hull = Array.Empty<Vector3>();
        private Vector3[] _centers = Array.Empty<Vector3>();
        private Vector3[] _left = Array.Empty<Vector3>();
        private Vector3[] _right = Array.Empty<Vector3>();
        private Vector3[] _scratch = Array.Empty<Vector3>();
        private float[] _multipliers = Array.Empty<float>();
        private int _controlCount;

        /// <summary>Number of ribbon samples produced by the last successful <see cref="TrySample"/>.</summary>
        public int Count { get; private set; }

        /// <summary>Number of authored control points used by the last successful sample.</summary>
        public int ControlCount { get; private set; }

        /// <summary>Spline radius used to convert multipliers into metres.</summary>
        public float SplineRadius { get; private set; }

        public Vector3 Center(int index) => _centers[index];

        public Vector3 Left(int index) => _left[index];

        public Vector3 Right(int index) => _right[index];

        public float RadiusMultiplier(int index) => _multipliers[index];

        /// <summary>Half of the rendered ribbon width at a sample, in metres.</summary>
        public float HalfWidthMeters(int index) => SplineRadius * _multipliers[index] * 0.5f;

        /// <summary>Crest's sample spacing for a spline subdivision count.</summary>
        public static float ResolveSpacingMeters(int subdivisions) =>
            BaseSpacingMeters / Mathf.Pow(2f, Mathf.Clamp(subdivisions, 0, MaxSubdivisions) + 1);

        /// <summary>Sample count Crest derives from the control-polyline length estimate.</summary>
        public static int ResolveSampleCount(float lengthEstimateMeters, int subdivisions)
        {
            var spacing = ResolveSpacingMeters(subdivisions);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(lengthEstimateMeters, 1f) / spacing));
        }

        /// <summary>Control-polyline length estimate, matching Crest's <c>lengthEst</c>.</summary>
        public static float EstimateLengthMeters(IReadOnlyList<Vector3> controls)
        {
            if (controls == null || controls.Count < 2)
                return 0f;

            var length = 0f;
            for (var i = 1; i < controls.Count; i++)
                length += (controls[i] - controls[i - 1]).magnitude;
            return length;
        }

        /// <summary>
        /// Samples the ribbon Crest would render. Returns false when there are too few controls
        /// to form a ribbon.
        /// </summary>
        public bool TrySample(
            IReadOnlyList<Vector3> controls,
            IReadOnlyList<float> controlRadiusMultipliers,
            float splineRadius,
            int subdivisions,
            bool closed)
        {
            Count = 0;
            ControlCount = 0;
            SplineRadius = Mathf.Max(0.01f, splineRadius);
            if (controls == null || controls.Count < 2)
                return false;
            if (closed && controls.Count < 3)
                return false;

            var controlCount = controls.Count;
            _controlCount = controlCount;
            EnsureControls(controlCount);
            var lengthEstimate = CopyControls(controls, controlRadiusMultipliers, controlCount);

            var pointCount = ResolveSampleCount(lengthEstimate, subdivisions);
            EnsureSamples(pointCount);

            var splinePointCount = closed ? controlCount + 1 : controlCount;
            var hullLength = (splinePointCount - 1) * 3 + 1;
            EnsureHull(hullLength);
            BuildHull(controlCount, hullLength, closed);

            _centers[0] = _hull[0];
            _multipliers[0] = _controlMultipliers[0];
            for (var i = 1; i < pointCount; i++)
            {
                var t = i / (float)(pointCount - 1);
                _centers[i] = InterpolateCubicPosition(splinePointCount, t);
                _multipliers[i] = InterpolateMultiplier(t, controlCount);
            }

            BuildEdges(pointCount, closed);
            // Crest fixes the closed-seam edge before resolving overlaps, then smooths both edges.
            if (closed)
            {
                var midpoint = Vector3.Lerp(_right[0], _right[pointCount - 1], 0.5f);
                _right[0] = midpoint;
                _right[pointCount - 1] = midpoint;
            }

            ResolveOverlaps(_left, pointCount);
            ResolveOverlaps(_right, pointCount);
            SmoothEdges(_left, pointCount);
            SmoothEdges(_right, pointCount);

            ControlCount = controlCount;
            Count = pointCount;
            return true;
        }

        /// <summary>
        /// Local perpendicular (bank) direction of the control polyline at a control point,
        /// matching Crest's ribbon normal orientation so left/right map to the sampled edges.
        /// </summary>
        public static Vector2 ControlPerpendicular(IReadOnlyList<Vector3> controls, int index)
        {
            if (controls == null || controls.Count < 2)
                return Vector2.right;

            var before = Mathf.Max(0, index - 1);
            var after = Mathf.Min(controls.Count - 1, index + 1);
            var tangent = controls[after] - controls[before];
            var normal = new Vector2(tangent.z, -tangent.x);
            return normal.sqrMagnitude < 1e-6f ? Vector2.right : normal.normalized;
        }

        private float CopyControls(
            IReadOnlyList<Vector3> controls,
            IReadOnlyList<float> controlRadiusMultipliers,
            int controlCount)
        {
            var lengthEstimate = 0f;
            for (var i = 0; i < controlCount; i++)
            {
                _controls[i] = controls[i];
                _controlMultipliers[i] = controlRadiusMultipliers != null && i < controlRadiusMultipliers.Count
                    ? Mathf.Max(0f, controlRadiusMultipliers[i])
                    : 1f;
                if (i > 0)
                    lengthEstimate += (_controls[i] - _controls[i - 1]).magnitude;
            }

            return lengthEstimate;
        }

        private void BuildHull(int controlCount, int hullLength, bool closed)
        {
            for (var i = 0; i < hullLength; i++)
            {
                var spi = (i / 3) % controlCount;
                var spiNext = (spi + 1) % controlCount;
                if (i % 3 == 0)
                {
                    _hull[i] = _controls[spi];
                    continue;
                }

                var span = _controls[spiNext] - _controls[spi];
                var magnitude = span.magnitude;
                if (i % 3 == 1)
                {
                    // Crest mirrors TangentBefore(1), NOT TangentAfter(0): for a curved open spline
                    // those point in different directions and shape the first segment differently.
                    var outTangent = i == 1 && !closed
                        ? MirrorFirstOutTangent(span, magnitude)
                        : ResolveTangent(spi, closed).normalized * magnitude;
                    _hull[i] = _controls[spi] + TangentScale * outTangent;
                    continue;
                }

                var inTangent = i == hullLength - 2 && !closed
                    ? MirrorLastInTangent(span, magnitude)
                    : ResolveTangent(spiNext, closed).normalized * magnitude;
                _hull[i] = _controls[spiNext] - TangentScale * inTangent;
            }
        }

        /// <summary>Crest mirrors <c>TangentBefore(1)</c> through the chord for the first out-tangent.</summary>
        private Vector3 MirrorFirstOutTangent(Vector3 span, float magnitude)
        {
            if (_controlCount < 2)
                return span.normalized * magnitude;

            var tangent = ResolveTangent(1, closed: false).normalized * magnitude;
            return MirrorThrough(tangent, span.normalized, magnitude);
        }

        /// <summary>Crest mirrors <c>TangentAfter(n - 2)</c> through the chord for the last in-tangent.</summary>
        private Vector3 MirrorLastInTangent(Vector3 span, float magnitude)
        {
            var lastIndex = _controlCount - 1;
            var previousIndex = lastIndex - 1;
            if (previousIndex < 0)
                return span.normalized * magnitude;

            var tangent = ResolveTangent(previousIndex, closed: false).normalized * magnitude;
            var toPrevious = (_controls[previousIndex] - _controls[lastIndex]).normalized;
            return MirrorThrough(tangent, toPrevious, magnitude);
        }

        /// <summary>Crest's end-tangent reflection: reflect through the axis of the given direction.</summary>
        private static Vector3 MirrorThrough(Vector3 tangent, Vector3 axis, float magnitude)
        {
            var nearest = Vector3.Dot(tangent, axis) * axis;
            var mirrored = tangent + 2f * (nearest - tangent);
            return mirrored.normalized * magnitude;
        }

        /// <summary>Crest's <c>TangentAfter</c> / <c>TangentBefore</c> (identical bodies).</summary>
        private Vector3 ResolveTangent(int index, bool closed)
        {
            var controlCount = _controlCount;
            var tangent = Vector3.zero;
            var weight = 0f;

            var before = index - 1;
            if (before < 0 && closed)
                before += controlCount;
            var after = index + 1;
            if (after >= controlCount && closed)
                after -= controlCount;

            if (before >= 0)
            {
                tangent += _controls[index] - _controls[before];
                weight += 1f;
            }

            if (after < controlCount)
            {
                tangent += _controls[after] - _controls[index];
                weight += 1f;
            }

            return weight <= 0f ? tangent : tangent / weight;
        }

        private Vector3 InterpolateCubicPosition(int splinePointCount, float t)
        {
            var tpts = t * (splinePointCount - 1f);
            var spidx = Mathf.FloorToInt(tpts);
            var alpha = tpts - spidx;
            if (spidx == splinePointCount - 1)
            {
                spidx -= 1;
                alpha = 1f;
            }

            var pidx = spidx * 3;
            var oneMinusAlpha = 1f - alpha;
            return oneMinusAlpha * oneMinusAlpha * oneMinusAlpha * _hull[pidx]
                   + 3f * alpha * oneMinusAlpha * oneMinusAlpha * _hull[pidx + 1]
                   + 3f * alpha * alpha * oneMinusAlpha * _hull[pidx + 2]
                   + alpha * alpha * alpha * _hull[pidx + 3];
        }

        private float InterpolateMultiplier(float t, int controlCount)
        {
            var tpts = t * (controlCount - 1f);
            var spidx = Mathf.FloorToInt(tpts);
            var alpha = tpts - spidx;
            var first = _controlMultipliers[Mathf.Clamp(spidx, 0, controlCount - 1)];
            var second = _controlMultipliers[Mathf.Min(spidx + 1, controlCount - 1)];
            return Mathf.Lerp(first, second, Mathf.SmoothStep(0f, 1f, alpha));
        }

        private void BuildEdges(int pointCount, bool closed)
        {
            var halfRadius = 0.5f * SplineRadius;
            for (var i = 0; i < pointCount; i++)
            {
                var before = i - 1;
                var after = i + 1;
                if (closed)
                {
                    if (before < 0)
                        before += pointCount;
                    after %= pointCount;
                }
                else
                {
                    before = Mathf.Max(before, 0);
                    after = Mathf.Min(after, pointCount - 1);
                }

                var tangent = _centers[after] - _centers[before];
                // Crest zeroes y, then rotates and normalizes; a degenerate tangent therefore
                // collapses the ribbon to the centreline exactly as Crest renders it.
                var normal = new Vector3(tangent.z, 0f, -tangent.x).normalized;
                var offset = halfRadius * _multipliers[i] * normal;
                _left[i] = _centers[i] - offset;
                _right[i] = _centers[i] + offset;
            }
        }

        /// <summary>Crest's <c>ResolveOverlaps</c>: keeps edges strictly forward-moving.</summary>
        private void ResolveOverlaps(Vector3[] points, int pointCount)
        {
            if (pointCount < 2)
                return;

            _scratch[0] = points[0];
            var lastGood = points[1];
            for (var i = 1; i < pointCount; i++)
            {
                var splineTangent = _centers[i] - _centers[i - 1];
                var tangent = points[i] - lastGood;
                tangent.y = 0f;
                splineTangent.y = 0f;
                if (Vector3.Dot(tangent, splineTangent) > 0f)
                {
                    _scratch[i] = points[i];
                    lastGood = points[i];
                    continue;
                }

                _scratch[i] = lastGood;
                _scratch[i].y = points[i].y;
            }

            Array.Copy(_scratch, points, pointCount);
        }

        private void SmoothEdges(Vector3[] points, int pointCount)
        {
            for (var pass = 0; pass < SmoothingIterations; pass++)
            {
                for (var i = 1; i < pointCount - 1; i++)
                    _scratch[i] = 0.5f * (points[i - 1] + points[i + 1]);
                for (var i = 1; i < pointCount - 1; i++)
                    points[i] = _scratch[i];
            }
        }

        private void EnsureControls(int count)
        {
            if (_controls.Length >= count)
                return;
            _controls = new Vector3[count];
            _controlMultipliers = new float[count];
        }

        private void EnsureSamples(int count)
        {
            if (_centers.Length >= count)
                return;
            _centers = new Vector3[count];
            _left = new Vector3[count];
            _right = new Vector3[count];
            _scratch = new Vector3[count];
            _multipliers = new float[count];
        }

        private void EnsureHull(int count)
        {
            if (_hull.Length < count)
                _hull = new Vector3[count];
        }
    }
}
