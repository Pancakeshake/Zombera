using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Extracts and ranks substantial basins from priority-flood output.</summary>
    public static class LakeExtractor
    {
        public static LakeRecord[] Extract(
            LandformField field,
            float[] filledHeights,
            HydrologyProfile profile) =>
            Extract(field, filledHeights, profile, null);
        public static LakeRecord[] Extract(
            LandformField field,
            float[] filledHeights,
            HydrologyProfile profile,
            bool[] exclusionMask,
            bool limitCount = true)
        {
            if (field == null || filledHeights == null || profile == null)
                return Array.Empty<LakeRecord>();

            var candidates = CollectCandidates(field, filledHeights, profile, exclusionMask);
            candidates.Sort((a, b) =>
            {
                var score = (b.EstimatedVolumeMetersCubed * Mathf.Max(1f, b.MaxDepthMeters))
                    .CompareTo(a.EstimatedVolumeMetersCubed * Mathf.Max(1f, a.MaxDepthMeters));
                return score != 0 ? score : a.StableId.CompareTo(b.StableId);
            });

            if (!limitCount)
                return candidates.ToArray();
            var count = Mathf.Min(profile.MaxVisibleLakes, candidates.Count);
            if (count == candidates.Count)
                return candidates.ToArray();
            var result = new LakeRecord[count];
            for (var i = 0; i < count; i++) result[i] = candidates[i];
            return result;
        }

        private static List<LakeRecord> CollectCandidates(
            LandformField field,
            float[] filledHeights,
            HydrologyProfile profile,
            bool[] exclusionMask)
        {
            var lakes = new List<LakeRecord>(profile.MaxVisibleLakes);
            var visited = new bool[field.Width * field.Height];
            var minFillDepth = Mathf.Max(0.01f, profile.LakeMinFillDepthMeters);
            var minArea = Mathf.Max(1f, profile.LakeMinAreaMetersSq);
            var cellArea = field.CellSize * field.CellSize;

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var index = field.Index(x, z);
                    if (visited[index] || IsBlocked(exclusionMask, index))
                        continue;
                    if (filledHeights[index] - field.WorldHeights[index] < minFillDepth)
                        continue;

                    var cells = FloodBasin(field, filledHeights, visited, x, z, minFillDepth, exclusionMask);
                    var candidate = BuildCandidate(field, filledHeights, profile, cells, minArea, cellArea);
                    if (candidate != null)
                        lakes.Add(candidate);
                }
            }

            return lakes;
        }

        /// <summary>Builds a lake candidate from a flooded basin, or null when it fails the profile gates.</summary>
        private static LakeRecord BuildCandidate(
            LandformField field,
            float[] filledHeights,
            HydrologyProfile profile,
            List<int> cells,
            float minAreaMetersSq,
            float cellArea)
        {
            var area = cells.Count * cellArea;
            // Oversized basins survive here — ConnectedLakeSelector budgets the total area.
            if (area < minAreaMetersSq)
                return null;
            var candidate = BuildRecord(field, filledHeights, cells, area, cellArea);
            if (candidate.MaxDepthMeters < profile.LakeMinimumDepthMeters ||
                candidate.EstimatedVolumeMetersCubed < profile.LakeMinimumVolumeMetersCubed)
                return null;
            return IsAspectAllowed(candidate.BoundsXZ, profile.LakeMaximumAspectRatio) ? candidate : null;
        }

        private static LakeRecord BuildRecord(
            LandformField field,
            float[] filledHeights,
            List<int> cells,
            float area,
            float cellArea)
        {
            var sum = Vector2.zero;
            var maxDepth = 0f;
            var surfaceY = float.NegativeInfinity;
            var volume = 0f;
            var shore = new List<Vector2>(cells.Count);
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                var center = field.CellCenterXZ(cell % field.Width, cell / field.Width);
                var depth = Mathf.Max(0f, filledHeights[cell] - field.WorldHeights[cell]);
                sum += center;
                shore.Add(center);
                maxDepth = Mathf.Max(maxDepth, depth);
                surfaceY = Mathf.Max(surfaceY, filledHeights[cell]);
                volume += depth * cellArea;
            }

            var centerXZ = sum / Mathf.Max(1, cells.Count);
            var bounds = ComputeBasinBounds(field, cells);
            var outlet = FindOutlet(field, filledHeights, cells, surfaceY);
            var hasher = new StableHash64(0x4C414B4552454300UL);
            hasher.Append(Mathf.FloorToInt(centerXZ.x / field.CellSize));
            hasher.Append(Mathf.FloorToInt(centerXZ.y / field.CellSize));
            hasher.Append(cells.Count);

            return new LakeRecord
            {
                StableId = hasher.Finalize(),
                CenterXZ = centerXZ,
                SurfaceWorldY = surfaceY,
                AreaMetersSq = area,
                MaxDepthMeters = maxDepth,
                BoundsXZ = bounds,
                OutlineXZ = BuildOutline(field, centerXZ, cells),
                EstimatedVolumeMetersCubed = volume,
                HasOutlet = outlet.HasValue,
                OutletXZ = outlet ?? default,
                BasinCellCentersXZ = shore.ToArray()
            };
        }

        private static bool IsAspectAllowed(Rect bounds, float maximumAspect)
        {
            var shortest = Mathf.Max(1f, Mathf.Min(bounds.width, bounds.height));
            var longest = Mathf.Max(bounds.width, bounds.height);
            return longest / shortest <= Mathf.Max(1f, maximumAspect);
        }

        private static Vector2[] BuildOutline(LandformField field, Vector2 center, List<int> cells)
        {
            var basin = new HashSet<int>(cells);
            var adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var x = cell % field.Width;
                var z = cell / field.Width;
                AddBoundaryEdge(field, basin, adjacency, x, z, Vector2Int.left);
                AddBoundaryEdge(field, basin, adjacency, x, z, Vector2Int.right);
                AddBoundaryEdge(field, basin, adjacency, x, z, Vector2Int.down);
                AddBoundaryEdge(field, basin, adjacency, x, z, Vector2Int.up);
            }

            var outline = TraceLargestBoundary(field, adjacency);
            if (outline.Count < 3)
                return Array.Empty<Vector2>();

            outline = SimplifyOutline(outline, field.CellSize * 0.2f);
            var smoothed = SmoothOutline(outline, center, field.CellSize);
            smoothed.Add(smoothed[0]);
            return smoothed.ToArray();
        }

        private static void AddBoundaryEdge(
            LandformField field,
            HashSet<int> basin,
            Dictionary<Vector2Int, List<Vector2Int>> adjacency,
            int x,
            int z,
            Vector2Int direction)
        {
            var nx = x + direction.x;
            var nz = z + direction.y;
            if (nx >= 0 && nz >= 0 && nx < field.Width && nz < field.Height &&
                basin.Contains(field.Index(nx, nz)))
                return;

            var bottomLeft = new Vector2Int(x, z);
            var bottomRight = new Vector2Int(x + 1, z);
            var topLeft = new Vector2Int(x, z + 1);
            var topRight = new Vector2Int(x + 1, z + 1);
            Vector2Int a;
            Vector2Int b;
            if (direction == Vector2Int.left)
            {
                a = topLeft;
                b = bottomLeft;
            }
            else if (direction == Vector2Int.right)
            {
                a = bottomRight;
                b = topRight;
            }
            else if (direction == Vector2Int.down)
            {
                a = bottomLeft;
                b = bottomRight;
            }
            else
            {
                a = topRight;
                b = topLeft;
            }

            AddBoundaryLink(adjacency, a, b);
            AddBoundaryLink(adjacency, b, a);
        }

        private static void AddBoundaryLink(
            Dictionary<Vector2Int, List<Vector2Int>> adjacency,
            Vector2Int from,
            Vector2Int to)
        {
            if (!adjacency.TryGetValue(from, out var neighbours))
            {
                neighbours = new List<Vector2Int>(2);
                adjacency.Add(from, neighbours);
            }

            if (!neighbours.Contains(to))
                neighbours.Add(to);
        }

        private static List<Vector2> TraceLargestBoundary(
            LandformField field,
            Dictionary<Vector2Int, List<Vector2Int>> adjacency)
        {
            var visited = new HashSet<BoundaryEdge>();
            var largest = new List<Vector2>();
            foreach (var pair in adjacency)
            {
                for (var n = 0; n < pair.Value.Count; n++)
                {
                    var edge = new BoundaryEdge(pair.Key, pair.Value[n]);
                    if (visited.Contains(edge))
                        continue;

                    var loop = TraceBoundaryLoop(field, adjacency, visited, pair.Key, pair.Value[n]);
                    if (Mathf.Abs(SignedArea(loop)) > Mathf.Abs(SignedArea(largest)))
                        largest = loop;
                }
            }

            return largest;
        }

        private static List<Vector2> TraceBoundaryLoop(
            LandformField field,
            Dictionary<Vector2Int, List<Vector2Int>> adjacency,
            HashSet<BoundaryEdge> visited,
            Vector2Int start,
            Vector2Int next)
        {
            var loop = new List<Vector2>(adjacency.Count);
            var current = start;
            Vector2Int previous;
            var guard = 0;
            while (guard++ < adjacency.Count * 4)
            {
                loop.Add(GridPoint(field, current));
                var edge = new BoundaryEdge(current, next);
                if (!visited.Add(edge))
                    break;
                previous = current;
                current = next;
                if (current == start)
                    break;

                if (!adjacency.TryGetValue(current, out var neighbours) || neighbours.Count == 0)
                    break;
                next = ChooseNextBoundaryVertex(neighbours, previous, current);
            }

            return loop;
        }

        private static Vector2Int ChooseNextBoundaryVertex(
            List<Vector2Int> neighbours,
            Vector2Int previous,
            Vector2Int current)
        {
            for (var i = 0; i < neighbours.Count; i++)
            {
                if (neighbours[i] != previous)
                    return neighbours[i];
            }

            return current;
        }

        private static Vector2 GridPoint(LandformField field, Vector2Int grid)
        {
            return new Vector2(
                field.OriginXZ.x + grid.x * field.CellSize,
                field.OriginXZ.y + grid.y * field.CellSize);
        }

        private static List<Vector2> SimplifyOutline(List<Vector2> points, float tolerance)
        {
            if (points.Count < 4)
                return points;
            var result = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var previous = points[(i + points.Count - 1) % points.Count];
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                if (Vector2.Distance(previous, current) < tolerance)
                    continue;
                var cross = Mathf.Abs(Cross(current - previous, next - current));
                if (cross < tolerance * tolerance)
                    continue;
                result.Add(current);
            }

            return result.Count >= 3 ? result : points;
        }

        private static List<Vector2> SmoothOutline(List<Vector2> points, Vector2 center, float cellSize)
        {
            _ = center;
            if (points.Count < 4)
                return points;
            var smoothed = new List<Vector2>(points.Count * 2);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                smoothed.Add(Vector2.Lerp(a, b, 0.25f));
                smoothed.Add(Vector2.Lerp(a, b, 0.75f));
            }

            var limit = Mathf.Max(cellSize * 0.5f, cellSize * 1.5f);
            for (var i = 0; i < smoothed.Count; i++)
            {
                var source = points[i / 2];
                var delta = smoothed[i] - source;
                if (delta.magnitude > limit)
                    smoothed[i] = source + delta.normalized * limit;
            }

            return smoothed;
        }

        private static float SignedArea(List<Vector2> points)
        {
            if (points == null || points.Count < 3)
                return 0f;
            var area = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private readonly struct BoundaryEdge : IEquatable<BoundaryEdge>
        {
            private readonly Vector2Int _a;
            private readonly Vector2Int _b;

            public BoundaryEdge(Vector2Int a, Vector2Int b)
            {
                if (a.x < b.x || (a.x == b.x && a.y <= b.y))
                {
                    _a = a;
                    _b = b;
                }
                else
                {
                    _a = b;
                    _b = a;
                }
            }

            public bool Equals(BoundaryEdge other) => _a == other._a && _b == other._b;
            public override bool Equals(object obj) => obj is BoundaryEdge other && Equals(other);
            public override int GetHashCode() => (_a.GetHashCode() * 397) ^ _b.GetHashCode();
        }

        private static Vector2? FindOutlet(
            LandformField field,
            float[] filledHeights,
            List<int> cells,
            float surfaceY)
        {
            var basin = new HashSet<int>(cells);
            var best = -1;
            var bestHeight = float.PositiveInfinity;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var x = cell % field.Width;
                var z = cell / field.Width;
                var neighbours = new[] { new Vector2Int(x + 1, z), new Vector2Int(x - 1, z), new Vector2Int(x, z + 1), new Vector2Int(x, z - 1) };
                for (var n = 0; n < neighbours.Length; n++)
                {
                    var nx = neighbours[n].x;
                    var nz = neighbours[n].y;
                    if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height)
                        continue;
                    var neighbour = field.Index(nx, nz);
                    if (basin.Contains(neighbour))
                        continue;
                    if (filledHeights[neighbour] < bestHeight && filledHeights[neighbour] <= surfaceY + field.CellSize)
                    {
                        best = neighbour;
                        bestHeight = filledHeights[neighbour];
                    }
                }
            }

            return best < 0 ? (Vector2?)null : field.CellCenterXZ(best % field.Width, best / field.Width);
        }

        private static Rect ComputeBasinBounds(LandformField field, List<int> cells)
        {
            var first = field.CellCenterXZ(cells[0] % field.Width, cells[0] / field.Width);
            var xMin = first.x;
            var xMax = first.x;
            var zMin = first.y;
            var zMax = first.y;
            for (var i = 1; i < cells.Count; i++)
            {
                var center = field.CellCenterXZ(cells[i] % field.Width, cells[i] / field.Width);
                xMin = Mathf.Min(xMin, center.x);
                xMax = Mathf.Max(xMax, center.x);
                zMin = Mathf.Min(zMin, center.y);
                zMax = Mathf.Max(zMax, center.y);
            }

            var halfCell = field.CellSize * 0.5f;
            return Rect.MinMaxRect(xMin - halfCell, zMin - halfCell, xMax + halfCell, zMax + halfCell);
        }

        private static List<int> FloodBasin(
            LandformField field,
            float[] filledHeights,
            bool[] visited,
            int startX,
            int startZ,
            float minDepth,
            bool[] exclusionMask)
        {
            var result = new List<int>(64);
            var queue = new Queue<int>();
            var start = field.Index(startX, startZ);
            queue.Enqueue(start);
            visited[start] = true;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                result.Add(cell);
                var x = cell % field.Width;
                var z = cell / field.Width;
                TryEnqueue(x + 1, z);
                TryEnqueue(x - 1, z);
                TryEnqueue(x, z + 1);
                TryEnqueue(x, z - 1);
            }
            return result;

            void TryEnqueue(int x, int z)
            {
                if (x < 0 || z < 0 || x >= field.Width || z >= field.Height)
                    return;
                var index = field.Index(x, z);
                if (visited[index] || IsBlocked(exclusionMask, index))
                    return;
                if (filledHeights[index] - field.WorldHeights[index] < minDepth)
                    return;
                visited[index] = true;
                queue.Enqueue(index);
            }
        }

        private static bool IsBlocked(bool[] mask, int index)
        {
            return mask != null && index >= 0 && index < mask.Length && mask[index];
        }
    }
}
