using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>D8 steepest-descent flow and accumulation (descending filled-height order).</summary>
    public static class FlowAccumulationSolver
    {
        // Clockwise from north: N, NE, E, SE, S, SW, W, NW
        private static readonly Vector2Int[] Neighbors =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1)
        };

        public static void Compute(float[] filledHeights, int width, int height, float[] accumulation, int[] flowTo)
        {
            Compute(filledHeights, width, height, accumulation, flowTo, null);
        }

        public static void Compute(
            float[] filledHeights,
            int width,
            int height,
            float[] accumulation,
            int[] flowTo,
            bool[] oceanMask)
        {
            if (filledHeights == null || accumulation == null || flowTo == null) return;
            if (width <= 0 || height <= 0) return;
            if (filledHeights.Length < width * height) return;

            var count = width * height;
            for (var i = 0; i < count; i++)
            {
                flowTo[i] = -1;
                accumulation[i] = 1f;
            }

            var oceanDistance = BuildOceanDistance(width, height, oceanMask);
            AssignFlowDirections(filledHeights, width, height, flowTo, oceanMask, oceanDistance);

            var order = BuildDescendingOrder(filledHeights, width, height, oceanDistance);
            for (var o = 0; o < order.Length; o++)
            {
                var i = order[o];
                var dest = flowTo[i];
                if (dest < 0) continue;
                accumulation[dest] += accumulation[i];
            }
        }

        private static void AssignFlowDirections(
            float[] filledHeights,
            int width,
            int height,
            int[] flowTo,
            bool[] oceanMask,
            int[] oceanDistance)
        {
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = z * width + x;
                    if (oceanMask != null && i < oceanMask.Length && oceanMask[i])
                        continue;
                    var h = filledHeights[i];
                    var bestDrop = 0f;
                    var best = -1;
                    var bestDistance = GetDistance(oceanDistance, i);

                    for (var n = 0; n < Neighbors.Length; n++)
                    {
                        var nx = x + Neighbors[n].x;
                        var nz = z + Neighbors[n].y;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;

                        var ni = nz * width + nx;
                        var drop = h - filledHeights[ni];
                        if (drop > bestDrop + 0.0001f)
                        {
                            bestDrop = drop;
                            best = ni;
                            continue;
                        }

                        if (drop < -0.0001f)
                            continue;
                        if (bestDrop > 0.0001f)
                            continue;
                        var candidateDistance = GetDistance(oceanDistance, ni);
                        if (best >= 0 &&
                            (candidateDistance > bestDistance ||
                             candidateDistance == bestDistance && ni > best))
                            continue;
                        if (candidateDistance < bestDistance || best < 0)
                        {
                            best = ni;
                            bestDistance = candidateDistance;
                        }
                    }

                    flowTo[i] = best;
                }
            }
        }

        private static int[] BuildDescendingOrder(float[] heights, int width, int height, int[] oceanDistance)
        {
            var count = width * height;
            var order = new int[count];
            for (var i = 0; i < count; i++)
                order[i] = i;

            Array.Sort(order, (a, b) =>
            {
                var cmp = heights[b].CompareTo(heights[a]);
                if (cmp != 0) return cmp;
                cmp = GetDistance(oceanDistance, b).CompareTo(GetDistance(oceanDistance, a));
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            return order;
        }

        private static int[] BuildOceanDistance(int width, int height, bool[] oceanMask)
        {
            var count = width * height;
            var distance = new int[count];
            for (var i = 0; i < count; i++) distance[i] = int.MaxValue;
            var queue = new System.Collections.Generic.Queue<int>(count);
            for (var i = 0; i < count; i++)
            {
                var isOcean = oceanMask != null && i < oceanMask.Length && oceanMask[i];
                if (!isOcean && oceanMask != null) continue;
                var x = i % width;
                var z = i / width;
                if (oceanMask == null && x != 0 && z != 0 && x != width - 1 && z != height - 1)
                    continue;
                distance[i] = 0;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var x = current % width;
                var z = current / width;
                for (var n = 0; n < Neighbors.Length; n++)
                {
                    var nx = x + Neighbors[n].x;
                    var nz = z + Neighbors[n].y;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                    var next = nz * width + nx;
                    if (distance[next] <= distance[current] + 1) continue;
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }

            return distance;
        }

        private static int GetDistance(int[] distances, int index) =>
            distances == null || index < 0 || index >= distances.Length ? int.MaxValue : distances[index];
    }
}
