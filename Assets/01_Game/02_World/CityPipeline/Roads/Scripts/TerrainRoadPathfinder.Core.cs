using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Terrain-cost A* used to reroute world-map road polylines through valleys.
    /// </summary>
    public static partial class TerrainRoadPathfinder
    {
        private static float[] _reuseGScore;
        private static float[] _reuseFScore;
        private static int[] _reuseCameFrom;
        private static bool[] _reuseClosed;
        private static int _reuseCapacity;

        private static readonly (int dx, int dz, float step)[] Neighbors =
        {
            (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
            (1, 1, 1.4142135f), (-1, 1, 1.4142135f), (1, -1, 1.4142135f), (-1, -1, 1.4142135f)
        };

        public static List<Vector2> RouteThroughWaypoints(
            TerrainRoadCostField field,
            IReadOnlyList<Vector2> waypoints,
            RoadClass roadClass,
            RoadNetworkSettings settings)
        {
            if (field == null || !field.IsValid || waypoints == null || waypoints.Count < 2 || settings == null)
                return null;

            var result = new List<Vector2>(waypoints.Count * 8);
            var minHop = Mathf.Max(field.CellSize * 2f, settings.pathfindingMinSegmentMeters);

            for (var i = 0; i < waypoints.Count - 1; i++)
            {
                var start = waypoints[i];
                var end = waypoints[i + 1];
                List<Vector2> segment;

                if (Vector2.Distance(start, end) <= minHop)
                    segment = new List<Vector2> { start, end };
                else
                    segment = FindPath(field, start, end, roadClass, settings);

                if (segment == null || segment.Count < 2)
                {
                    if (settings.fallbackToStraightPathWhenPathfindingFails)
                        segment = new List<Vector2> { start, end };
                    else
                        continue;
                }

                AppendSegment(result, segment);
            }

            if (result.Count < 2)
                return null;

            if (settings.removePathfindingLoops)
                result = RemoveSelfIntersectionLoops(result);

            return PostProcessPath(result, settings);
        }

        public static List<Vector2> PostProcessPath(List<Vector2> path, RoadNetworkSettings settings)
        {
            if (path == null || path.Count < 2 || settings == null)
                return path;

            var processed = SimplifyCollinear(path, 1f);
            if (settings.simplifySharpTurnsAfterPathfinding)
                processed = RemoveSharpTurns(processed, settings.maxPathTurnAngleDegrees);

            return processed.Count >= 2 ? processed : path;
        }

        public static List<Vector2> FindPath(
            TerrainRoadCostField field,
            Vector2 startXZ,
            Vector2 endXZ,
            RoadClass roadClass,
            RoadNetworkSettings settings)
        {
            return FindPath(field, startXZ, endXZ, roadClass, settings, pinEndpoints: false);
        }

        /// <summary>
        ///     When <paramref name="pinEndpoints"/> is true, start/end are not relocated by
        ///     exterior snap; caller must ensure endpoint cells are traversable (e.g. pin cells).
        /// </summary>
        public static List<Vector2> FindPath(
            TerrainRoadCostField field,
            Vector2 startXZ,
            Vector2 endXZ,
            RoadClass roadClass,
            RoadNetworkSettings settings,
            bool pinEndpoints)
        {
            if (field == null || !field.IsValid || settings == null) return null;

            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, roadClass);
            var maxSlopeRatio = maxSlopeDegrees > 0f ? Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) : float.MaxValue;

            var pinnedStart = startXZ;
            var pinnedEnd = endXZ;

            if (!pinEndpoints)
            {
                if (!TrySnapEndpointToTraversable(field, ref startXZ, maxSlopeRatio) ||
                    !TrySnapEndpointToTraversable(field, ref endXZ, maxSlopeRatio))
                    return null;
            }

            if (!field.TryWorldToCell(startXZ, out var startX, out var startZ) ||
                !field.TryWorldToCell(endXZ, out var endX, out var endZ))
                return null;

            if (!field.IsTraversable(startX, startZ) || !field.IsTraversable(endX, endZ))
                return null;

            if (startX == endX && startZ == endZ)
                return new List<Vector2> { pinEndpoints ? pinnedStart : startXZ, pinEndpoints ? pinnedEnd : endXZ };

            var path = FindPathWithRecursiveSplit(
                field, startXZ, endXZ, roadClass, settings, 0, pinEndpoints);
            if (path == null || path.Count < 2)
                return null;

            if (pinEndpoints)
                EnforcePinnedEnds(path, pinnedStart, pinnedEnd);
            return path;
        }

        public static void EnforcePinnedEnds(List<Vector2> path, Vector2 startXZ, Vector2 endXZ)
        {
            if (path == null || path.Count < 2)
                return;
            path[0] = startXZ;
            path[^1] = endXZ;
        }

        private static List<Vector2> FindPathWithRecursiveSplit(
            TerrainRoadCostField field,
            Vector2 startXZ,
            Vector2 endXZ,
            RoadClass roadClass,
            RoadNetworkSettings settings,
            int depth,
            bool pinEndpoints)
        {
            var direct = FindPathInternal(field, startXZ, endXZ, roadClass, settings);
            if (direct != null || depth >= 2)
                return direct;

            var mid = Vector2.Lerp(startXZ, endXZ, 0.5f);
            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, roadClass);
            var maxSlopeRatio = maxSlopeDegrees > 0f ? Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) : float.MaxValue;
            if (!TrySnapEndpointToTraversable(field, ref mid, maxSlopeRatio))
                return null;

            var first = FindPathWithRecursiveSplit(
                field, startXZ, mid, roadClass, settings, depth + 1, pinEndpoints);
            var second = FindPathWithRecursiveSplit(
                field, mid, endXZ, roadClass, settings, depth + 1, pinEndpoints);
            if (first == null || second == null)
                return null;

            var merged = new List<Vector2>(first.Count + second.Count);
            AppendSegment(merged, first);
            AppendSegment(merged, second);
            return merged.Count >= 2 ? merged : null;
        }

        private static bool TrySnapEndpointToTraversable(
            TerrainRoadCostField field,
            ref Vector2 worldXZ,
            float maxSlopeRatio)
        {
            if (field.TryWorldToCell(worldXZ, out var x, out var z) && field.IsTraversable(x, z))
            {
                if (field.GetSlopeRatio(x, z) <= maxSlopeRatio)
                    return true;
            }

            var bestScore = float.MaxValue;
            var bestWorld = worldXZ;
            var found = false;
            const int searchRadius = 10;

            if (!field.TryWorldToCell(worldXZ, out var centerX, out var centerZ))
                return false;

            for (var dz = -searchRadius; dz <= searchRadius; dz++)
            {
                for (var dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    var cx = centerX + dx;
                    var cz = centerZ + dz;
                    if (!field.IsTraversable(cx, cz)) continue;

                    var slope = field.GetSlopeRatio(cx, cz);
                    if (slope > maxSlopeRatio) continue;

                    var cellWorld = field.CellCenterWorld(cx, cz);
                    var dist = Vector2.Distance(cellWorld, worldXZ);
                    var score = dist + slope * 25f;
                    if (score >= bestScore) continue;

                    bestScore = score;
                    bestWorld = cellWorld;
                    found = true;
                }
            }

            if (!found) return false;

            worldXZ = bestWorld;
            return true;
        }

        private static List<Vector2> FindPathInternal(
            TerrainRoadCostField field,
            Vector2 startXZ,
            Vector2 endXZ,
            RoadClass roadClass,
            RoadNetworkSettings settings)
        {
            if (!field.TryWorldToCell(startXZ, out var startX, out var startZ) ||
                !field.TryWorldToCell(endXZ, out var endX, out var endZ))
                return null;

            if (!field.IsTraversable(startX, startZ) || !field.IsTraversable(endX, endZ))
                return null;

            if (startX == endX && startZ == endZ)
                return new List<Vector2> { startXZ, endXZ };

            var maxSlopeDegrees = ResolveMaxSlopeDegrees(settings, roadClass);
            var maxSlopeRatio = maxSlopeDegrees > 0f ? Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) : float.MaxValue;
            field.ComputeHeightRange(out var minHeight, out var maxHeight);
            var heightRange = Mathf.Max(0.01f, maxHeight - minHeight);

            var cellCount = field.Width * field.Height;
            EnsurePathBuffers(cellCount);
            var gScore = _reuseGScore;
            var fScore = _reuseFScore;
            var cameFrom = _reuseCameFrom;
            var closed = _reuseClosed;

            for (var i = 0; i < cellCount; i++)
            {
                gScore[i] = float.PositiveInfinity;
                fScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
                closed[i] = false;
            }

            var startIndex = field.ToIndex(startX, startZ);
            var goalIndex = field.ToIndex(endX, endZ);
            gScore[startIndex] = 0f;
            fScore[startIndex] = Heuristic(field, startX, startZ, endX, endZ);

            var open = new MinHeap(cellCount / 8 + 8);
            open.Push(startIndex, fScore[startIndex]);

            var expanded = 0;
            var maxExpanded = Mathf.Max(256, settings.pathfindingMaxExpandedNodes);

            while (open.Count > 0)
            {
                var current = open.Pop();
                if (closed[current]) continue;

                if (current == goalIndex)
                    return ReconstructPath(field, cameFrom, current, startXZ, endXZ);

                closed[current] = true;
                expanded++;
                if (expanded > maxExpanded) break;

                var cx = current % field.Width;
                var cz = current / field.Width;

                for (var n = 0; n < Neighbors.Length; n++)
                {
                    var nx = cx + Neighbors[n].dx;
                    var nz = cz + Neighbors[n].dz;
                    if (!field.IsTraversable(nx, nz)) continue;

                    var neighbor = field.ToIndex(nx, nz);
                    if (closed[neighbor]) continue;

                    var moveCost = field.GetTraversalCost(
                        cx, cz, nx, nz,
                        maxSlopeRatio,
                        settings.slopeCostWeight,
                        settings.elevationCostWeight,
                        settings.mountainProximityCostWeight,
                        minHeight,
                        heightRange);

                    if (float.IsPositiveInfinity(moveCost)) continue;

                    var tentativeG = gScore[current] + moveCost;
                    if (tentativeG >= gScore[neighbor]) continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(field, nx, nz, endX, endZ);
                    open.Push(neighbor, fScore[neighbor]);
                }
            }

            return null;
        }

        private static List<Vector2> RemoveSharpTurns(IReadOnlyList<Vector2> path, float maxTurnAngleDegrees)
        {
            if (path == null || path.Count < 3) return path == null ? null : new List<Vector2>(path);

            var minTurnDot = Mathf.Cos(Mathf.Clamp(maxTurnAngleDegrees, 90f, 175f) * Mathf.Deg2Rad);
            var simplified = new List<Vector2>(path.Count) { path[0] };

            for (var i = 1; i < path.Count - 1; i++)
            {
                var prev = simplified[simplified.Count - 1] - path[i];
                var next = path[i + 1] - path[i];
                if (prev.sqrMagnitude < 0.01f || next.sqrMagnitude < 0.01f)
                    continue;

                prev.Normalize();
                next.Normalize();
                if (Vector2.Dot(prev, next) < minTurnDot)
                    continue;

                simplified.Add(path[i]);
            }

            simplified.Add(path[path.Count - 1]);
            return simplified;
        }


        private static void EnsurePathBuffers(int cellCount)
        {
            if (_reuseCapacity >= cellCount &&
                _reuseGScore != null &&
                _reuseFScore != null &&
                _reuseCameFrom != null &&
                _reuseClosed != null)
                return;

            _reuseCapacity = cellCount;
            _reuseGScore = new float[cellCount];
            _reuseFScore = new float[cellCount];
            _reuseCameFrom = new int[cellCount];
            _reuseClosed = new bool[cellCount];
        }
        private static float Heuristic(TerrainRoadCostField field, int x, int z, int goalX, int goalZ)
        {
            var dx = Mathf.Abs(goalX - x);
            var dz = Mathf.Abs(goalZ - z);
            var steps = dx + dz + (1.4142135f - 2f) * Mathf.Min(dx, dz);
            // Scale by soft preference floor so pass-attract cells do not make h inadmissible.
            return steps * field.CellSize * field.HeuristicStepScale;
        }

        private static List<Vector2> ReconstructPath(
            TerrainRoadCostField field,
            int[] cameFrom,
            int goalIndex,
            Vector2 exactStart,
            Vector2 exactEnd)
        {
            var cells = new List<int>(64);
            var current = goalIndex;
            while (current >= 0)
            {
                cells.Add(current);
                current = cameFrom[current];
            }

            cells.Reverse();

            var path = new List<Vector2>(cells.Count + 2) { exactStart };
            for (var i = 1; i < cells.Count - 1; i++)
            {
                var idx = cells[i];
                var x = idx % field.Width;
                var z = idx / field.Width;
                path.Add(field.CellCenterWorld(x, z));
            }

            path.Add(exactEnd);
            return SimplifyCollinear(path, 0.5f);
        }

        private static void AppendSegment(List<Vector2> output, List<Vector2> segment)
        {
            if (segment == null || segment.Count == 0) return;

            if (output.Count == 0)
            {
                output.AddRange(segment);
                return;
            }

            var startIndex = 0;
            if ((segment[0] - output[output.Count - 1]).sqrMagnitude <= 0.25f)
                startIndex = 1;

            for (var i = startIndex; i < segment.Count; i++)
                output.Add(segment[i]);
        }

    }
}
