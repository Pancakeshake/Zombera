using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Presentation geometry and deterministic path shaping for river networks.</summary>
    public static partial class RiverNetworkBuilder
    {
        private static Dictionary<int, ulong> BuildLakeCellMap(LandformField field, LakeRecord[] lakes)
        {
            var result = new Dictionary<int, ulong>();
            if (lakes == null) return result;
            for (var l = 0; l < lakes.Length; l++)
            {
                var centers = lakes[l]?.BasinCellCentersXZ;
                if (centers == null) continue;
                for (var c = 0; c < centers.Length; c++)
                {
                    var x = Mathf.FloorToInt((centers[c].x - field.OriginXZ.x) / field.CellSize);
                    var z = Mathf.FloorToInt((centers[c].y - field.OriginXZ.y) / field.CellSize);
                    if (x >= 0 && z >= 0 && x < field.Width && z < field.Height)
                        result[field.Index(x, z)] = lakes[l].StableId;
                }
            }

            return result;
        }

        private static ulong FindAdjacentLake(int cell, List<int> candidates, Dictionary<int, ulong> lakeCells)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (lakeCells.TryGetValue(candidates[i], out var lakeId))
                    return lakeId;
            }

            return 0;
        }

        private static Vector2 FindOceanPoint(LandformField field, int outlet, int receiver, bool[] oceanMask)
        {
            if (receiver >= 0 && oceanMask != null && receiver < oceanMask.Length && oceanMask[receiver])
                return field.CellCenterXZ(receiver % field.Width, receiver / field.Width);
            if (!IsOceanAdjacent(field, outlet, oceanMask))
                return default;
            var x = outlet % field.Width;
            var z = outlet / field.Width;
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height)
                        continue;
                    var index = field.Index(nx, nz);
                    if (oceanMask[index])
                        return field.CellCenterXZ(nx, nz);
                }
            }
            return default;
        }

        private static bool IsOceanAdjacent(LandformField field, int cell, bool[] oceanMask)
        {
            if (field == null || oceanMask == null || cell < 0 || cell >= oceanMask.Length)
                return false;
            var x = cell % field.Width;
            var z = cell / field.Width;
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nx = x + dx;
                    var nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < field.Width && nz < field.Height &&
                        oceanMask[field.Index(nx, nz)])
                        return true;
                }
            }
            return false;
        }

        private static ulong HashSystem(int outlet)
        {
            var hasher = new StableHash64(0x525653595354454DUL);
            hasher.Append(outlet);
            return hasher.Finalize();
        }

        private static ulong HashRiver(ulong systemId, int branchIndex, RiverKind kind)
        {
            var hasher = new StableHash64(0x5256524956455253UL);
            hasher.Append(systemId);
            hasher.Append(branchIndex);
            hasher.Append((int)kind);
            return hasher.Finalize();
        }

        private static float EstimateCellLength(LandformField field, List<int> cells) =>
            Mathf.Max(0, cells.Count - 1) * field.CellSize;

        private static float SampleAccumulation(LandformField field, float[] accumulation, Vector2 worldXZ)
        {
            var fx = (worldXZ.x - field.OriginXZ.x) / field.CellSize - 0.5f;
            var fz = (worldXZ.y - field.OriginXZ.y) / field.CellSize - 0.5f;
            var x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, field.Width - 1);
            var z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, field.Height - 1);
            var x1 = Mathf.Min(x0 + 1, field.Width - 1);
            var z1 = Mathf.Min(z0 + 1, field.Height - 1);
            var tx = Mathf.Clamp01(fx - x0);
            var tz = Mathf.Clamp01(fz - z0);
            var a = Mathf.Lerp(accumulation[field.Index(x0, z0)], accumulation[field.Index(x1, z0)], tx);
            var b = Mathf.Lerp(accumulation[field.Index(x0, z1)], accumulation[field.Index(x1, z1)], tx);
            return Mathf.Lerp(a, b, tz);
        }

        private static void ApplyMeander(LandformField field, List<Vector2> points, HydrologyProfile profile, ulong stableId)
        {
            if (points.Count < 4 || profile.MeanderMaximumBankWidths <= 0f)
                return;
            var anchors = new List<Vector2>(points);
            var phase = (stableId & 0xFFFF) * 0.0001f;
            var distance = 0f;
            for (var i = 1; i < points.Count - 1; i++)
            {
                var previous = points[i - 1];
                var next = points[i + 1];
                var tangent = (next - previous).normalized;
                if (tangent.sqrMagnitude < 0.01f) continue;
                var normal = new Vector2(-tangent.y, tangent.x);
                distance += Vector2.Distance(previous, points[i]);
                var slope = EstimateSlope(field, points[i]);
                var valley = 1f - Mathf.InverseLerp(
                    profile.MeanderMinimumValleySlopeDegrees,
                    profile.MeanderMinimumValleySlopeDegrees * 4f,
                    slope);
                var width = Mathf.Max(4f, Vector2.Distance(points[i - 1], points[i])) * 0.5f;
                var amplitude = width * profile.MeanderMaximumBankWidths * valley;
                var offset = Mathf.Sin(
                    distance / Mathf.Max(1f, profile.MeanderWavelengthMeters) * Mathf.PI * 2f + phase) * amplitude;
                var candidate = points[i] + normal * offset;
                if (IsValidMeanderCandidate(field, points, anchors, i, candidate, amplitude))
                    points[i] = candidate;
            }
        }

        private static bool IsValidMeanderCandidate(
            LandformField field,
            List<Vector2> points,
            List<Vector2> anchors,
            int index,
            Vector2 candidate,
            float amplitude)
        {
            if (Vector2.Distance(candidate, anchors[index]) > Mathf.Max(1f, amplitude) + field.CellSize)
                return false;
            var previous = points[index - 1];
            var next = points[index + 1];
            var candidateHeight = LandformFieldSampling.SampleBilinear(field, candidate.x, candidate.y);
            var neighbourHeight = Mathf.Max(
                LandformFieldSampling.SampleBilinear(field, previous.x, previous.y),
                LandformFieldSampling.SampleBilinear(field, next.x, next.y));
            if (candidateHeight > neighbourHeight + field.CellSize * 0.75f)
                return false;

            for (var i = 0; i < index - 2; i++)
            {
                if (SegmentsIntersect(points[i], points[i + 1], previous, candidate))
                    return false;
            }

            return true;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var ab = Cross(b - a, c - a);
            var ab2 = Cross(b - a, d - a);
            var cd = Cross(d - c, a - c);
            var cd2 = Cross(d - c, b - c);
            return ((ab > 0f && ab2 < 0f) || (ab < 0f && ab2 > 0f)) &&
                   ((cd > 0f && cd2 < 0f) || (cd < 0f && cd2 > 0f));
        }

        private static float EstimateSlope(LandformField field, Vector2 point)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt((point.x - field.OriginXZ.x) / field.CellSize), 1, field.Width - 2);
            var z = Mathf.Clamp(Mathf.FloorToInt((point.y - field.OriginXZ.y) / field.CellSize), 1, field.Height - 2);
            var dx = field.WorldHeights[field.Index(x + 1, z)] - field.WorldHeights[field.Index(x - 1, z)];
            var dz = field.WorldHeights[field.Index(x, z + 1)] - field.WorldHeights[field.Index(x, z - 1)];
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / (2f * field.CellSize)) * Mathf.Rad2Deg;
        }

        private static void ApplyMouthFlare(float[] widths, HydrologyProfile profile)
        {
            var start = Mathf.Clamp01(1f - profile.RiverMouthFlareFraction);
            for (var i = 0; i < widths.Length; i++)
            {
                var t = widths.Length <= 1 ? 1f : i / (float)(widths.Length - 1);
                var flare = Mathf.SmoothStep(1f, profile.RiverMouthFlareMultiplier, Mathf.InverseLerp(start, 1f, t));
                widths[i] *= flare;
                if (i > 0)
                    widths[i] = Mathf.Max(widths[i], widths[i - 1]);
            }
        }

        private static List<Vector2> DouglasPeucker(List<Vector2> points, float tolerance)
        {
            if (points.Count < 3) return new List<Vector2>(points);
            var keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;
            DouglasRecursive(points, 0, points.Count - 1, tolerance, keep);
            var result = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
            return result;
        }

        private static void DouglasRecursive(List<Vector2> points, int start, int end, float tolerance, bool[] keep)
        {
            if (end <= start + 1) return;
            var maxDistance = 0f;
            var index = -1;
            for (var i = start + 1; i < end; i++)
            {
                var distance = PerpendicularDistance(points[i], points[start], points[end]);
                if (distance > maxDistance) { maxDistance = distance; index = i; }
            }
            if (index < 0 || maxDistance <= tolerance) return;
            keep[index] = true;
            DouglasRecursive(points, start, index, tolerance, keep);
            DouglasRecursive(points, index, end, tolerance, keep);
        }

        private static float PerpendicularDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-6f) return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }

        private static List<Vector2> Chaikin(List<Vector2> points, int passes)
        {
            var current = points;
            for (var pass = 0; pass < passes && current.Count > 1; pass++)
            {
                var next = new List<Vector2>(current.Count * 2) { current[0] };
                for (var i = 0; i < current.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(current[i], current[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(current[i], current[i + 1], 0.75f));
                }
                next.Add(current[current.Count - 1]);
                current = next;
            }
            return current;
        }

        private static List<Vector2> Resample(List<Vector2> points, float spacing)
        {
            if (points.Count < 2) return new List<Vector2>(points);
            var result = new List<Vector2>(points.Count) { points[0] };
            var carry = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var length = Vector2.Distance(a, b);
                if (length < 1e-5f) continue;
                var direction = (b - a) / length;
                var consumed = 0f;
                while (carry + length - consumed >= spacing)
                {
                    var step = spacing - carry;
                    consumed += step;
                    result.Add(a + direction * consumed);
                    carry = 0f;
                }
                carry += length - consumed;
            }
            var last = points[points.Count - 1];
            if (Vector2.Distance(result[result.Count - 1], last) > 0.01f)
                result.Add(last);
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
