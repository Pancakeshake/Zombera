using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public static partial class CityDistrictClusterPlanner
    {
        private const int PreferredCoreBlockCount = 3;

        private static int ResolveCoreBlockCount(int totalBlocks)
        {
            if (totalBlocks <= 1) return 1;
            if (totalBlocks <= 4) return Mathf.Min(2, totalBlocks);
            if (totalBlocks <= 12) return Mathf.Min(2, Mathf.RoundToInt(totalBlocks * 0.1f));

            var scaled = Mathf.RoundToInt(totalBlocks * 0.1f);
            return Mathf.Max(PreferredCoreBlockCount, scaled);
        }

        /// <summary>
        ///     Grows a compact center cluster (~3×3) from the block nearest the city center.
        /// </summary>
        private static void AssignCityCoreCluster(List<BlockNode> nodes, Vector2 cityCenter, int maxBlocks = -1)
        {
            var targetCount = ResolveCoreBlockCount(nodes.Count);
            if (maxBlocks >= 0)
                targetCount = Mathf.Min(targetCount, maxBlocks);
            if (targetCount <= 0)
                return;

            var seed = FindCoreSeedNode(nodes, cityCenter);
            if (seed == null)
                return;
            var grid = BuildGridLookup(nodes);
            var cluster = new List<BlockNode>(targetCount) { seed };
            seed.district = CityDistrictType.CityCore;
            seed.clusterName = "CityCore_Center";

            while (cluster.Count < targetCount)
            {
                var candidates = CollectUnassignedNeighborsForCluster(cluster, grid);
                if (candidates.Count == 0)
                    break;

                candidates.Sort((a, b) =>
                    SquaredDistanceToCenter(a, cityCenter).CompareTo(SquaredDistanceToCenter(b, cityCenter)));

                var added = false;
                for (var i = 0; i < candidates.Count && cluster.Count < targetCount; i++)
                {
                    var node = candidates[i];
                    node.district = CityDistrictType.CityCore;
                    node.clusterName = "CityCore_Center";
                    cluster.Add(node);
                    added = true;
                }

                if (!added)
                    break;
            }
        }

        private static BlockNode FindCoreSeedNode(List<BlockNode> nodes, Vector2 cityCenter)
        {
            BlockNode seed = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var dist = SquaredDistanceToCenter(node, cityCenter);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                seed = node;
            }

            return seed;
        }

        private static float SquaredDistanceToCenter(BlockNode node, Vector2 cityCenter)
        {
            var delta = node.block.centerXZ - cityCenter;
            return delta.sqrMagnitude;
        }

        // -----------------------------------------------------------------------
        // Hospital zone
        // -----------------------------------------------------------------------

        private static void AssignHospitalSingletons(List<BlockNode> nodes, System.Random rng, int seed)
        {
            var targetCount = ResolveHospitalCount(nodes.Count);
            if (targetCount <= 0)
                return;

            var candidates = CollectHospitalCandidates(nodes, preferLargeBlocks: true);
            if (candidates.Count == 0)
                candidates = CollectHospitalCandidates(nodes, preferLargeBlocks: false);

            if (candidates.Count == 0)
                return;

            var minSeparation = ResolveHospitalMinSeparationMeters(nodes);
            var placed = new List<BlockNode>(targetCount);

            while (placed.Count < targetCount)
            {
                if (!TryPickSpreadHospitalCandidate(candidates, placed, minSeparation, seed, rng, out var pick))
                    break;

                pick.district = CityDistrictType.Hospital;
                pick.clusterName = targetCount <= 1 ? "Hospital" : "Hospital_" + (placed.Count + 1).ToString("00");
                placed.Add(pick);
            }

            if (placed.Count > 0 || nodes.Count < ResolveMinimumBlocksForHospital())
                return;

            // Larger cities always get at least one hospital even if spread rules blocked every pick.
            candidates.Sort((a, b) =>
                ScoreHospitalCandidate(b, seed).CompareTo(ScoreHospitalCandidate(a, seed)));
            var fallback = candidates[0];
            fallback.district = CityDistrictType.Hospital;
            fallback.clusterName = "Hospital";
        }

        private static int ResolveMinimumBlocksForHospital()
        {
            return 8;
        }

        private static List<BlockNode> CollectHospitalCandidates(List<BlockNode> nodes, bool preferLargeBlocks)
        {
            var candidates = new List<BlockNode>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.district != CityDistrictType.Mixed)
                    continue;

                if (preferLargeBlocks && node.block.areaSquareMeters < 900f)
                    continue;

                candidates.Add(node);
            }

            return candidates;
        }

        private static bool TryPickSpreadHospitalCandidate(
            List<BlockNode> candidates,
            List<BlockNode> placed,
            float minSeparationMeters,
            int seed,
            System.Random rng,
            out BlockNode pick)
        {
            pick = null;
            var bestScore = float.MinValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.district != CityDistrictType.Mixed)
                    continue;

                if (placed.Count > 0 && !IsHospitalSeparated(candidate, placed, minSeparationMeters))
                    continue;

                var score = ScoreHospitalCandidate(candidate, seed);
                if (placed.Count > 0)
                {
                    var spread = MinSeparationMeters(candidate, placed);
                    score += Mathf.Clamp01(spread / minSeparationMeters) * 0.45f;
                }

                score += Hash01(seed, 11, candidate.block.id) * 0.06f;
                if (placed.Count > 0 && rng.NextDouble() < 0.08f)
                    score -= 0.05f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                pick = candidate;
            }

            return pick != null;
        }

        private static int ResolveHospitalCount(int blockCount)
        {
            return blockCount >= ResolveMinimumBlocksForHospital() ? 1 : 0;
        }

        private static float ResolveHospitalMinSeparationMeters(List<BlockNode> nodes)
        {
            var span = ResolveCityBoundsSpanMeters(nodes);
            return Mathf.Clamp(span * 0.32f, 100f, span * 0.5f);
        }

        private static bool IsHospitalSeparated(
            BlockNode candidate,
            List<BlockNode> placed,
            float minSeparationMeters)
        {
            return MinSeparationMeters(candidate, placed) >= minSeparationMeters - 0.01f;
        }

        private static float MinSeparationMeters(BlockNode candidate, List<BlockNode> placed)
        {
            var minDist = float.MaxValue;
            for (var i = 0; i < placed.Count; i++)
            {
                var delta = candidate.block.centerXZ - placed[i].block.centerXZ;
                minDist = Mathf.Min(minDist, delta.magnitude);
            }

            return minDist;
        }

        private static float ScoreHospitalCandidate(BlockNode node, int seed)
        {
            var accessibility = 1f - Mathf.Abs(node.centrality - 0.48f) * 2.2f;
            var sizeScore = Mathf.Clamp01(node.block.areaSquareMeters / 6000f);
            var jitter = Hash01(seed, 11, node.block.id) * 0.08f;
            return accessibility * 0.65f + sizeScore * 0.35f + jitter;
        }

        // -----------------------------------------------------------------------
        // Military zone
        // -----------------------------------------------------------------------

        /// <summary>
        ///     Military outposts can sit anywhere in the city — interior blocks
        ///     included (e.g. a base in the middle of a residential district).
        ///     Outposts prefer larger blocks and spread apart from each other.
        /// </summary>
        private static void AssignMilitaryOutposts(List<BlockNode> nodes, int seed, int maxBlocks = -1)
        {
            var outpostCount = ResolveMilitaryOutpostCount(nodes.Count);
            if (maxBlocks >= 0)
                outpostCount = Mathf.Min(outpostCount, maxBlocks);
            if (outpostCount <= 0)
                return;

            var candidates = CollectUnassigned(nodes);
            if (candidates.Count == 0)
                return;

            var span = ResolveCityBoundsSpanMeters(nodes);
            var placed = new List<BlockNode>(outpostCount);

            while (placed.Count < outpostCount)
            {
                BlockNode pick = null;
                var bestScore = float.MinValue;

                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate.district != CityDistrictType.Mixed)
                        continue;

                    var score = Mathf.Clamp01(candidate.block.areaSquareMeters / 6000f) * 0.45f
                              + Hash01(seed, 17, candidate.block.id) * 0.35f;
                    if (placed.Count > 0)
                        score += Mathf.Clamp01(MinSeparationMeters(candidate, placed) / span) * 0.45f;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    pick = candidate;
                }

                if (pick == null)
                    break;

                pick.district = CityDistrictType.Military;
                pick.clusterName = "Military_Outpost_" + (placed.Count + 1).ToString("00");
                placed.Add(pick);
            }
        }

        private static int ResolveMilitaryOutpostCount(int blockCount)
        {
            if (blockCount <= 8) return 1;
            return Mathf.Clamp(1 + blockCount / 24, 1, 2);
        }

        // -----------------------------------------------------------------------
        // Industrial zone
        // -----------------------------------------------------------------------

        private static void AssignIndustrialClusters(List<BlockNode> nodes, System.Random rng, int seed, int maxBlocks = -1)
        {
            if (maxBlocks == 0)
                return;

            var targetBlocks = Mathf.RoundToInt(nodes.Count * 0.16f);
            targetBlocks = Mathf.Clamp(targetBlocks, 2, Mathf.Max(2, nodes.Count / 3));
            if (maxBlocks > 0)
                targetBlocks = Mathf.Min(targetBlocks, maxBlocks);
            var clusterCount = Mathf.Clamp(1 + targetBlocks / 4, 1, 4);
            var clusterSize = Mathf.Max(2, Mathf.CeilToInt(targetBlocks / (float)clusterCount));

            var seeds = PickClusterSeeds(
                nodes,
                clusterCount,
                rng,
                node => node.perimeter * 0.75f + Hash01(seed, 29, node.block.id) * 0.25f);

            GrowClusters(
                nodes,
                seeds,
                CityDistrictType.Industrial,
                "Industrial",
                clusterSize,
                (a, b) => b.perimeter.CompareTo(a.perimeter));
        }

        // -----------------------------------------------------------------------
        // Commercial zone
        // -----------------------------------------------------------------------

        /// <summary>
        ///     Commercial blocks are spread across the city as standalone store
        ///     blocks (corner stores, diners, gas stations) instead of contiguous
        ///     strips — e.g. a single commercial block in the middle of a
        ///     residential neighborhood. One anchor lands near the city center;
        ///     the rest maximize distance from each other with an interior bias.
        /// </summary>
        private static void AssignCommercialSingletons(
            List<BlockNode> nodes, Vector2 cityCenter, System.Random rng, int seed, int maxBlocks = -1)
        {
            if (maxBlocks == 0)
                return;

            var remaining = CountUnassigned(nodes);
            var targetBlocks = Mathf.RoundToInt(remaining * 0.18f);
            targetBlocks = Mathf.Clamp(targetBlocks, 2, Mathf.Max(3, remaining / 3));
            if (maxBlocks > 0)
                targetBlocks = Mathf.Min(targetBlocks, maxBlocks);
            if (targetBlocks <= 0)
                return;

            var candidates = CollectUnassigned(nodes);
            if (candidates.Count == 0)
                return;

            var span = ResolveCityBoundsSpanMeters(nodes);
            var placed = new List<BlockNode>(targetBlocks);

            // Downtown anchor: the unassigned block nearest the city center.
            candidates.Sort((a, b) =>
                SquaredDistanceToCenter(a, cityCenter).CompareTo(SquaredDistanceToCenter(b, cityCenter)));
            CommitCommercialBlock(candidates[0], placed);

            // Spread the remaining stores: interior blocks score best (they end
            // up surrounded by residential), far from already-placed stores.
            while (placed.Count < targetBlocks)
            {
                var pick = PickSpreadCommercialCandidate(candidates, placed, span, seed, rng);
                if (pick == null)
                    break;

                CommitCommercialBlock(pick, placed);
            }
        }

        private static void CommitCommercialBlock(BlockNode node, List<BlockNode> placed)
        {
            node.district = CityDistrictType.Commercial;
            node.clusterName = "Commercial_" + (placed.Count + 1).ToString("00");
            placed.Add(node);
        }

        private static BlockNode PickSpreadCommercialCandidate(
            List<BlockNode> candidates, List<BlockNode> placed, float span, int seed, System.Random rng)
        {
            BlockNode pick = null;
            var bestScore = float.MinValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.district != CityDistrictType.Mixed)
                    continue;

                var score = Mathf.Clamp01(MinSeparationMeters(candidate, placed) / span) * 0.55f
                          + (1f - candidate.perimeter) * 0.30f
                          + Mathf.Clamp01(candidate.block.areaSquareMeters / 2500f) * 0.10f
                          + Hash01(seed, 37, candidate.block.id) * 0.05f;
                if (placed.Count >= 2 && rng.NextDouble() < 0.08f)
                    score -= 0.04f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                pick = candidate;
            }

            return pick;
        }

        // -----------------------------------------------------------------------
        // Residential zone
        // -----------------------------------------------------------------------

        private static void AssignResidentialClusters(List<BlockNode> nodes, System.Random rng, int seed, int maxBlocks = -1)
        {
            var unassigned = CollectUnassigned(nodes);
            if (unassigned.Count == 0 || maxBlocks == 0)
                return;

            var available = maxBlocks > 0 ? Mathf.Min(unassigned.Count, maxBlocks) : unassigned.Count;
            var clusterCount = Mathf.Clamp(1 + available / 6, 2, 6);
            var clusterSize = Mathf.Max(3, Mathf.CeilToInt(available / (float)clusterCount));

            // Residential seeds anywhere — mild suburban (interior) preference only.
            var seeds = PickClusterSeeds(
                nodes,
                clusterCount,
                rng,
                node => (1f - node.perimeter) * 0.35f + Hash01(seed, 43, node.block.id) * 0.65f);

            GrowClusters(
                nodes,
                seeds,
                CityDistrictType.Residential,
                "Residential",
                clusterSize,
                (a, b) =>
                {
                    var aSuburb = 1f - a.perimeter;
                    var bSuburb = 1f - b.perimeter;
                    return bSuburb.CompareTo(aSuburb);
                });
        }
    }
}
