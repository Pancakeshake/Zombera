#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.World.Spawning
{
    public sealed partial class WorldBuildingSpawner
    {
        /// <summary>
        ///     MapMagic spline product extraction lives in Legacy. World returns false (no-op).
        /// </summary>
        private bool TryExtractSplines(Terrain terrain, List<SplineSampleLine> output)
        {
            _ = terrain;
            _ = output;
            _ = splineResPerMeter;
            _ = splineMinSamples;
            _ = splineMaxSamples;
            return false;
        }

        /// <summary>Pre-computed world-space polyline with per-segment arc-length for distance-based sampling.</summary>
        private sealed class SplineSampleLine
        {
            public readonly Vector3[] Points;
            public readonly float TotalLength;

            private readonly float[] _cumulativeLengths;

            public SplineSampleLine(Vector3[] worldPoints)
            {
                Points = worldPoints;
                _cumulativeLengths = new float[worldPoints.Length];
                _cumulativeLengths[0] = 0f;
                var total = 0f;

                for (var i = 1; i < worldPoints.Length; i++)
                {
                    total += Vector3.Distance(worldPoints[i - 1], worldPoints[i]);
                    _cumulativeLengths[i] = total;
                }

                TotalLength = total;
            }

            public Vector3 SampleAtDistance(float distance)
            {
                if (Points.Length == 0) return Vector3.zero;
                if (distance <= 0f) return Points[0];
                if (distance >= TotalLength) return Points[Points.Length - 1];

                for (var i = 1; i < _cumulativeLengths.Length; i++)
                {
                    if (distance > _cumulativeLengths[i]) continue;

                    var segmentStart = _cumulativeLengths[i - 1];
                    var segmentLen = _cumulativeLengths[i] - segmentStart;
                    var t = segmentLen > 0.0001f ? (distance - segmentStart) / segmentLen : 0f;
                    return Vector3.Lerp(Points[i - 1], Points[i], t);
                }

                return Points[Points.Length - 1];
            }

            public Vector3 TangentAtDistance(float distance)
            {
                if (Points.Length < 2) return Vector3.forward;
                if (distance <= 0f) return (Points[1] - Points[0]).normalized;

                for (var i = 1; i < _cumulativeLengths.Length; i++)
                {
                    if (distance > _cumulativeLengths[i]) continue;
                    return (Points[i] - Points[i - 1]).normalized;
                }

                return (Points[Points.Length - 1] - Points[Points.Length - 2]).normalized;
            }
        }
    }
}
