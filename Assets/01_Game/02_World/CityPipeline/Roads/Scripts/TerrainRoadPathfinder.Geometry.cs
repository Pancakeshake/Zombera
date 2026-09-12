using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class TerrainRoadPathfinder
    {
        private static List<Vector2> SimplifyCollinear(List<Vector2> path, float epsilon)
        {
            if (path == null || path.Count <= 2) return path;

            var epsilonSq = epsilon * epsilon;
            var simplified = new List<Vector2>(path.Count) { path[0] };

            for (var i = 1; i < path.Count - 1; i++)
            {
                var prev = simplified[simplified.Count - 1];
                var next = path[i + 1];
                if (DistancePointToSegmentSq(path[i], prev, next) > epsilonSq)
                    simplified.Add(path[i]);
            }

            simplified.Add(path[path.Count - 1]);
            return simplified;
        }

        private static List<Vector2> RemoveSelfIntersectionLoops(List<Vector2> path)
        {
            if (path == null || path.Count < 4) return path;

            var working = new List<Vector2>(path);
            var guard = 0;
            while (guard++ < 32 && TryFindFirstIntersection(working, out var segA, out var segB, out var loopStart, out var loopEnd))
            {
                if (loopEnd <= loopStart + 1) break;
                working.RemoveRange(loopStart + 1, loopEnd - loopStart);
            }

            return working;
        }

        private static bool TryFindFirstIntersection(
            IReadOnlyList<Vector2> path,
            out int segA,
            out int segB,
            out int loopStart,
            out int loopEnd)
        {
            segA = 0;
            segB = 0;
            loopStart = 0;
            loopEnd = 0;

            for (var i = 0; i < path.Count - 1; i++)
            {
                for (var j = i + 2; j < path.Count - 1; j++)
                {
                    if (j == i + 1) continue;
                    if (!TryGetSegmentIntersectionXZ(path[i], path[i + 1], path[j], path[j + 1], 0.35f, out _, out _))
                        continue;

                    segA = i;
                    segB = j;
                    loopStart = i;
                    loopEnd = j;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSegmentIntersectionXZ(
            Vector2 a0,
            Vector2 a1,
            Vector2 b0,
            Vector2 b1,
            float endpointToleranceMeters,
            out float tA,
            out float tB)
        {
            tA = 0f;
            tB = 0f;

            var d1 = a1 - a0;
            var d2 = b1 - b0;
            var denom = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(denom) <= 0.00001f) return false;

            var t = ((b0.x - a0.x) * d2.y - (b0.y - a0.y) * d2.x) / denom;
            var u = ((b0.x - a0.x) * d1.y - (b0.y - a0.y) * d1.x) / denom;
            var tol = Mathf.Max(0.01f, endpointToleranceMeters);

            if (t < -tol || t > 1f + tol || u < -tol || u > 1f + tol) return false;

            tA = Mathf.Clamp01(t);
            tB = Mathf.Clamp01(u);
            return true;
        }

        private static float DistancePointToSegmentSq(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq <= 0.0001f) return (point - a).sqrMagnitude;

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            var projection = a + ab * t;
            return (point - projection).sqrMagnitude;
        }

        private static float ResolveMaxSlopeDegrees(RoadNetworkSettings settings, RoadClass roadClass)
        {
            if (!settings.usePerRoadClassSlopeLimits)
                return roadClass == RoadClass.Local ? settings.maxCityRoadSlopeDegrees : settings.maxArterialRoadSlopeDegrees;

            return roadClass switch
            {
                RoadClass.Highway => settings.ResolveHighwayPathfindingMaxSlopeDegrees(),
                RoadClass.Arterial => settings.maxArterialRoadSlopeDegrees,
                RoadClass.Local => settings.maxLocalRoadSlopeDegrees,
                _ => settings.maxCityRoadSlopeDegrees
            };
        }

        private sealed class MinHeap
        {
            private int[] _indices;
            private float[] _priorities;
            private int _count;

            public int Count => _count;

            public MinHeap(int capacity)
            {
                _indices = new int[Mathf.Max(4, capacity)];
                _priorities = new float[_indices.Length];
            }

            public void Push(int index, float priority)
            {
                if (_count >= _indices.Length)
                    Grow();

                var i = _count++;
                _indices[i] = index;
                _priorities[i] = priority;
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (_priorities[parent] <= _priorities[i]) break;

                    (_indices[parent], _indices[i]) = (_indices[i], _indices[parent]);
                    (_priorities[parent], _priorities[i]) = (_priorities[i], _priorities[parent]);
                    i = parent;
                }
            }

            public int Pop()
            {
                var result = _indices[0];
                _count--;
                if (_count > 0)
                {
                    _indices[0] = _indices[_count];
                    _priorities[0] = _priorities[_count];
                    HeapifyDown(0);
                }

                return result;
            }

            private void HeapifyDown(int i)
            {
                while (true)
                {
                    var left = (i << 1) + 1;
                    var right = left + 1;
                    var smallest = i;

                    if (left < _count && _priorities[left] < _priorities[smallest]) smallest = left;
                    if (right < _count && _priorities[right] < _priorities[smallest]) smallest = right;
                    if (smallest == i) break;

                    (_indices[i], _indices[smallest]) = (_indices[smallest], _indices[i]);
                    (_priorities[i], _priorities[smallest]) = (_priorities[smallest], _priorities[i]);
                    i = smallest;
                }
            }

            private void Grow()
            {
                var newCapacity = _indices.Length * 2;
                Array.Resize(ref _indices, newCapacity);
                Array.Resize(ref _priorities, newCapacity);
            }
        }
    }
}
