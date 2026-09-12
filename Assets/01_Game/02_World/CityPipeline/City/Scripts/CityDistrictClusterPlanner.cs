using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Assigns city blocks into named district clusters: CityCore center, R/I neighborhoods,
    ///     scattered commercial storefront blocks, military outposts (anywhere in the city),
    ///     and standalone hospital blocks.
    /// </summary>
    public static partial class CityDistrictClusterPlanner
    {
        private sealed class BlockNode
        {
            public CityNamedArea block;
            public int gridX;
            public int gridZ;
            public float centrality;
            public float perimeter;
            public CityDistrictType district = CityDistrictType.Mixed;
            public string clusterName = string.Empty;
        }

        public static void AssignClusters(
            IReadOnlyList<CityNamedArea> blocks,
            Vector2 cityCenter,
            int seed,
            IReadOnlyList<CityDistrictWeight> districtMix = null)
        {
            if (blocks == null || blocks.Count == 0)
                return;

            var nodes = BuildNodes(blocks, cityCenter);
            if (nodes.Count == 0)
                return;

            // Blocks remember their city center so commercial block layouts can
            // open toward downtown regardless of which step runs later.
            for (var i = 0; i < nodes.Count; i++)
                nodes[i].block.cityCenterXZ = cityCenter;

            var rng = new System.Random(MixSeed(seed, 4242));
            var targets = ResolveDistrictTargets(nodes.Count, districtMix);

            AssignCityCoreCluster(nodes, cityCenter, CapFor(targets, CityDistrictType.CityCore));
            if (targets == null || targets[(int)CityDistrictType.Hospital] > 0)
                AssignHospitalSingletons(nodes, rng, seed);
            AssignMilitaryOutposts(nodes, seed, CapFor(targets, CityDistrictType.Military));
            AssignIndustrialClusters(nodes, rng, seed, CapFor(targets, CityDistrictType.Industrial));
            AssignCommercialSingletons(nodes, cityCenter, rng, seed, CapFor(targets, CityDistrictType.Commercial));
            AssignResidentialClusters(nodes, rng, seed, CapFor(targets, CityDistrictType.Residential));
            AssignParkBlocks(nodes, rng, seed, CapFor(targets, CityDistrictType.Park));

            for (var i = 0; i < nodes.Count; i++)
                ApplyNodeToBlock(nodes[i]);
        }

        /// <summary>
        ///     Turns a weighted district mix into per-district target block counts.
        ///     Returns null when no mix is supplied (or it sums to zero), meaning the
        ///     legacy fixed-ratio distribution is used. 'Mixed' weight is ignored —
        ///     Mixed fills whatever blocks remain unassigned.
        /// </summary>
        private static int[] ResolveDistrictTargets(int totalBlocks, IReadOnlyList<CityDistrictWeight> districtMix)
        {
            if (districtMix == null || districtMix.Count == 0)
                return null;

            var totalWeight = 0f;
            for (var i = 0; i < districtMix.Count; i++)
                totalWeight += Mathf.Max(0f, districtMix[i].weight);

            if (totalWeight <= 0f)
                return null;

            var targets = new int[System.Enum.GetValues(typeof(CityDistrictType)).Length];
            for (var i = 0; i < districtMix.Count; i++)
            {
                var entry = districtMix[i];
                if (entry.district == CityDistrictType.Mixed || entry.weight <= 0f)
                    continue;

                targets[(int)entry.district] = Mathf.RoundToInt(totalBlocks * entry.weight / totalWeight);
            }

            return targets;
        }

        /// <summary>
        ///     Block cap for one district, or -1 when no mix is active (legacy
        ///     distribution uses its own internal ratios).
        /// </summary>
        private static int CapFor(int[] targets, CityDistrictType district) =>
            targets == null ? -1 : targets[(int)district];

        private static List<BlockNode> BuildNodes(IReadOnlyList<CityNamedArea> blocks, Vector2 cityCenter)
        {
            var maxDist = 1f;
            for (var i = 0; i < blocks.Count; i++)
            {
                var delta = blocks[i].centerXZ - cityCenter;
                maxDist = Mathf.Max(maxDist, delta.magnitude);
            }

            var nodes = new List<BlockNode>(blocks.Count);
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var delta = block.centerXZ - cityCenter;
                nodes.Add(new BlockNode
                {
                    block = block,
                    gridX = block.gridX,
                    gridZ = block.gridZ,
                    centrality = 1f - Mathf.Clamp01(delta.magnitude / maxDist),
                    perimeter = EstimatePerimeterScore(block.boundsXZ, blocks)
                });
            }

            return nodes;
        }

        private static void ApplyNodeToBlock(BlockNode node)
        {
            if (node.block == null)
                return;

            if (node.district == CityDistrictType.Mixed)
            {
                node.block.districtType = CityDistrictType.Residential;
                node.block.clusterName = "Residential_Remaining";
                node.block.displayName = node.block.clusterName;
                return;
            }

            node.block.districtType = node.district;
            node.block.clusterName = string.IsNullOrWhiteSpace(node.clusterName)
                ? node.district.ToString()
                : node.clusterName;
            node.block.displayName = node.block.clusterName;
        }

        private static float ResolveCityBoundsSpanMeters(List<BlockNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return 200f;

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;

            for (var i = 0; i < nodes.Count; i++)
            {
                var center = nodes[i].block.centerXZ;
                if (center.x < minX) minX = center.x;
                if (center.x > maxX) maxX = center.x;
                if (center.y < minZ) minZ = center.y;
                if (center.y > maxZ) maxZ = center.y;
            }

            return Mathf.Max(maxX - minX, maxZ - minZ, 120f);
        }

        private static float EstimatePerimeterScore(Rect rect, IReadOnlyList<CityNamedArea> all)
        {
            if (all == null || all.Count == 0) return 0f;

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            for (var i = 0; i < all.Count; i++)
            {
                var bounds = all[i].boundsXZ;
                if (bounds.xMin < minX) minX = bounds.xMin;
                if (bounds.xMax > maxX) maxX = bounds.xMax;
                if (bounds.yMin < minZ) minZ = bounds.yMin;
                if (bounds.yMax > maxZ) maxZ = bounds.yMax;
            }

            var spanX = Mathf.Max(1f, maxX - minX);
            var spanZ = Mathf.Max(1f, maxZ - minZ);
            var edgeDistX = Mathf.Min(rect.center.x - minX, maxX - rect.center.x) / (spanX * 0.5f);
            var edgeDistZ = Mathf.Min(rect.center.y - minZ, maxZ - rect.center.y) / (spanZ * 0.5f);
            return 1f - Mathf.Clamp01(Mathf.Min(edgeDistX, edgeDistZ));
        }

        internal static float Hash01(int seed, int salt, int id)
        {
            unchecked
            {
                var hash = seed * 73856093 ^ salt * 19349663 ^ id * 83492791;
                return (hash & 0xFFFF) / 65535f;
            }
        }

        private static int MixSeed(int seed, int salt)
        {
            unchecked
            {
                return seed * 73856093 ^ salt * 19349663;
            }
        }
    }
}
