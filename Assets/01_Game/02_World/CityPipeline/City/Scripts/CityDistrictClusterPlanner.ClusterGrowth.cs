using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public static partial class CityDistrictClusterPlanner
    {
        private static List<BlockNode> PickClusterSeeds(
            List<BlockNode> nodes,
            int count,
            System.Random rng,
            Func<BlockNode, float> score)
        {
            var candidates = CollectUnassigned(nodes);
            candidates.Sort((a, b) => score(b).CompareTo(score(a)));

            var seeds = new List<BlockNode>(count);
            var minSeparation = 1.5f;
            for (var i = 0; i < candidates.Count && seeds.Count < count; i++)
            {
                var candidate = candidates[i];
                if (!IsSeparatedFromSeeds(candidate, seeds, minSeparation))
                    continue;

                if (seeds.Count > 0 && rng.NextDouble() < 0.18f)
                    continue;

                seeds.Add(candidate);
            }

            for (var i = 0; seeds.Count < count && i < candidates.Count; i++)
            {
                if (seeds.Contains(candidates[i]))
                    continue;

                seeds.Add(candidates[i]);
            }

            return seeds;
        }

        private static bool IsSeparatedFromSeeds(BlockNode candidate, List<BlockNode> seeds, float minSeparation)
        {
            for (var i = 0; i < seeds.Count; i++)
            {
                var dx = candidate.gridX - seeds[i].gridX;
                var dz = candidate.gridZ - seeds[i].gridZ;
                if (Mathf.Sqrt(dx * dx + dz * dz) < minSeparation)
                    return false;
            }

            return true;
        }

        private static void GrowClusters(
            List<BlockNode> nodes,
            List<BlockNode> seeds,
            CityDistrictType district,
            string districtPrefix,
            int targetClusterSize,
            Comparison<BlockNode> neighborPreference)
        {
            var grid = BuildGridLookup(nodes);
            for (var clusterIndex = 0; clusterIndex < seeds.Count; clusterIndex++)
            {
                var seed = seeds[clusterIndex];
                if (seed.district != CityDistrictType.Mixed)
                    continue;

                var direction = ResolveClusterDirectionLabel(seed.block.centerXZ, nodes);
                var clusterName = districtPrefix + "_" + direction + "_" + (clusterIndex + 1).ToString("00");
                GrowCluster(seed, district, clusterName, targetClusterSize, grid, neighborPreference);
            }
        }

        private static void GrowCluster(
            BlockNode seed,
            CityDistrictType district,
            string clusterName,
            int targetSize,
            Dictionary<(int x, int z), BlockNode> grid,
            Comparison<BlockNode> neighborPreference)
        {
            seed.district = district;
            seed.clusterName = clusterName;

            var frontier = new List<BlockNode> { seed };
            var clusterSize = 1;

            while (clusterSize < targetSize)
            {
                var expanded = new List<BlockNode>();
                for (var i = 0; i < frontier.Count; i++)
                {
                    var neighbors = CollectUnassignedNeighbors(frontier[i], grid);
                    if (neighbors.Count == 0)
                        continue;

                    neighbors.Sort(neighborPreference);
                    for (var n = 0; n < neighbors.Count && clusterSize < targetSize; n++)
                    {
                        var neighbor = neighbors[n];
                        neighbor.district = district;
                        neighbor.clusterName = clusterName;
                        expanded.Add(neighbor);
                        clusterSize++;
                    }
                }

                if (expanded.Count == 0)
                    break;

                frontier = expanded;
            }
        }

        private static List<BlockNode> CollectUnassignedNeighbors(
            BlockNode node,
            Dictionary<(int x, int z), BlockNode> grid)
        {
            var neighbors = new List<BlockNode>(4);
            TryAddNeighbor(node.gridX + 1, node.gridZ, grid, neighbors);
            TryAddNeighbor(node.gridX - 1, node.gridZ, grid, neighbors);
            TryAddNeighbor(node.gridX, node.gridZ + 1, grid, neighbors);
            TryAddNeighbor(node.gridX, node.gridZ - 1, grid, neighbors);
            return neighbors;
        }

        private static void TryAddNeighbor(
            int gridX,
            int gridZ,
            Dictionary<(int x, int z), BlockNode> grid,
            List<BlockNode> neighbors)
        {
            if (!grid.TryGetValue((gridX, gridZ), out var node))
                return;

            if (node.district != CityDistrictType.Mixed)
                return;

            neighbors.Add(node);
        }

        private static Dictionary<(int x, int z), BlockNode> BuildGridLookup(List<BlockNode> nodes)
        {
            var grid = new Dictionary<(int x, int z), BlockNode>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
                grid[(nodes[i].gridX, nodes[i].gridZ)] = nodes[i];

            return grid;
        }

        private static List<BlockNode> CollectUnassigned(List<BlockNode> nodes)
        {
            var result = new List<BlockNode>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].district == CityDistrictType.Mixed)
                    result.Add(nodes[i]);
            }

            return result;
        }

        private static int CountUnassigned(List<BlockNode> nodes)
        {
            var count = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].district == CityDistrictType.Mixed)
                    count++;
            }

            return count;
        }

        private static List<BlockNode> CollectUnassignedNeighborsForCluster(
            List<BlockNode> cluster,
            Dictionary<(int x, int z), BlockNode> grid)
        {
            var seen = new HashSet<BlockNode>();
            var neighbors = new List<BlockNode>();
            for (var i = 0; i < cluster.Count; i++)
            {
                var adjacent = CollectUnassignedNeighbors(cluster[i], grid);
                for (var n = 0; n < adjacent.Count; n++)
                {
                    var node = adjacent[n];
                    if (!seen.Add(node))
                        continue;

                    neighbors.Add(node);
                }
            }

            return neighbors;
        }

        private static string ResolveClusterDirectionLabel(Vector2 blockCenter, List<BlockNode> nodes)
        {
            var cityCenter = Vector2.zero;
            for (var i = 0; i < nodes.Count; i++)
                cityCenter += nodes[i].block.centerXZ;

            cityCenter /= Mathf.Max(1, nodes.Count);

            var delta = blockCenter - cityCenter;
            if (delta.sqrMagnitude < 0.01f)
                return "Center";

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0f ? "East" : "West";

            return delta.y >= 0f ? "North" : "South";
        }
    }
}
