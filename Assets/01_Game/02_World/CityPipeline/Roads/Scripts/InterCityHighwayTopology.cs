using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Minimum spanning tree over city sites for inter-city highway links.</summary>
    public static class InterCityHighwayTopology
    {
        private readonly struct ExtraLinkCandidate
        {
            public readonly int IndexA;
            public readonly int IndexB;
            public readonly float DistanceSquared;
            public readonly uint TieBreaker;

            public ExtraLinkCandidate(int indexA, int indexB, float distanceSquared, uint tieBreaker)
            {
                IndexA = indexA;
                IndexB = indexB;
                DistanceSquared = distanceSquared;
                TieBreaker = tieBreaker;
            }
        }

        public static List<(int IndexA, int IndexB)> BuildMinimumSpanningTree(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<int> connectedIndices,
            float minLinkDistanceMeters)
        {
            var edges = new List<(int, int)>();
            if (positions == null || connectedIndices == null || connectedIndices.Count < 2)
                return edges;

            var inTree = new bool[positions.Count];
            inTree[connectedIndices[0]] = true;
            var minLinkSq = Mathf.Max(50f, minLinkDistanceMeters);
            minLinkSq *= minLinkSq;

            for (var e = 0; e < connectedIndices.Count - 1; e++)
            {
                var bestFrom = -1;
                var bestTo = -1;
                var bestDist = float.MaxValue;

                for (var i = 0; i < connectedIndices.Count; i++)
                {
                    var indexI = connectedIndices[i];
                    if (!inTree[indexI])
                        continue;

                    for (var j = 0; j < connectedIndices.Count; j++)
                    {
                        var indexJ = connectedIndices[j];
                        if (inTree[indexJ])
                            continue;

                        var dist = (positions[indexI] - positions[indexJ]).sqrMagnitude;
                        if (dist < minLinkSq || dist >= bestDist)
                            continue;

                        bestDist = dist;
                        bestFrom = indexI;
                        bestTo = indexJ;
                    }
                }

                if (bestFrom < 0 || bestTo < 0)
                    break;

                inTree[bestTo] = true;
                edges.Add((bestFrom, bestTo));
            }

            return edges;
        }

        /// <summary>
        /// Builds the backbone MST plus a deterministic set of shorter non-tree links.
        /// Extra links make city connectivity feel like a network rather than a sparse tree.
        /// </summary>
        public static List<(int IndexA, int IndexB)> BuildMinimumSpanningTreeWithExtraLinks(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<int> connectedIndices,
            float minLinkDistanceMeters,
            float extraLoopChance,
            int seed,
            out int mstEdgeCount)
        {
            var links = BuildMinimumSpanningTree(positions, connectedIndices, minLinkDistanceMeters);
            mstEdgeCount = links.Count;
            var extraCount = ResolveExtraLinkCount(connectedIndices, extraLoopChance);
            if (extraCount == 0 || positions == null)
                return links;

            var used = new HashSet<ulong>();
            for (var i = 0; i < links.Count; i++)
                used.Add(ToPairKey(links[i].IndexA, links[i].IndexB));

            var candidates = CollectExtraLinkCandidates(
                positions,
                connectedIndices,
                minLinkDistanceMeters,
                seed,
                used);
            candidates.Sort(CompareExtraLinkCandidates);

            for (var i = 0; i < candidates.Count && extraCount > 0; i++)
            {
                var candidate = candidates[i];
                if (!used.Add(ToPairKey(candidate.IndexA, candidate.IndexB)))
                    continue;

                links.Add((candidate.IndexA, candidate.IndexB));
                extraCount--;
            }

            return links;
        }

        public static List<int> CollectConnectedIndices(int siteCount, System.Func<int, bool> includeSite)
        {
            var connected = new List<int>(siteCount);
            for (var i = 0; i < siteCount; i++)
            {
                if (includeSite == null || includeSite(i))
                    connected.Add(i);
            }

            return connected;
        }

        private static int ResolveExtraLinkCount(
            IReadOnlyList<int> connectedIndices,
            float extraLoopChance)
        {
            if (connectedIndices == null || connectedIndices.Count < 3)
                return 0;

            var potential = connectedIndices.Count - 1;
            return Mathf.CeilToInt(potential * Mathf.Clamp01(extraLoopChance));
        }

        private static List<ExtraLinkCandidate> CollectExtraLinkCandidates(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<int> connectedIndices,
            float minLinkDistanceMeters,
            int seed,
            HashSet<ulong> used)
        {
            var candidates = new List<ExtraLinkCandidate>();
            if (connectedIndices == null)
                return candidates;

            var minLink = Mathf.Max(50f, minLinkDistanceMeters);
            var minLinkSquared = minLink * minLink;
            for (var i = 0; i < connectedIndices.Count - 1; i++)
            {
                var indexA = connectedIndices[i];
                if (indexA < 0 || indexA >= positions.Count)
                    continue;

                for (var j = i + 1; j < connectedIndices.Count; j++)
                {
                    var indexB = connectedIndices[j];
                    if (indexB < 0 || indexB >= positions.Count ||
                        used.Contains(ToPairKey(indexA, indexB)))
                        continue;

                    var distanceSquared = (positions[indexA] - positions[indexB]).sqrMagnitude;
                    if (distanceSquared < minLinkSquared)
                        continue;

                    candidates.Add(new ExtraLinkCandidate(
                        indexA,
                        indexB,
                        distanceSquared,
                        BuildTieBreaker(indexA, indexB, seed)));
                }
            }

            return candidates;
        }

        private static int CompareExtraLinkCandidates(ExtraLinkCandidate a, ExtraLinkCandidate b)
        {
            var distance = a.DistanceSquared.CompareTo(b.DistanceSquared);
            return distance != 0 ? distance : a.TieBreaker.CompareTo(b.TieBreaker);
        }

        private static ulong ToPairKey(int indexA, int indexB)
        {
            var low = Mathf.Min(indexA, indexB);
            var high = Mathf.Max(indexA, indexB);
            return ((ulong)(uint)low << 32) | (uint)high;
        }

        private static uint BuildTieBreaker(int indexA, int indexB, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)Mathf.Min(indexA, indexB) * 0x9E3779B9u;
                hash = (hash << 13) | (hash >> 19);
                hash ^= (uint)Mathf.Max(indexA, indexB) * 0x85EBCA6Bu;
                return hash ^ (hash >> 16);
            }
        }
    }
}
