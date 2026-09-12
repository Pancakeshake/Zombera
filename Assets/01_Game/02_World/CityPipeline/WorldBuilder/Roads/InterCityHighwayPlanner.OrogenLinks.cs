using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Extra inter-city chords that bore snow-elevation orogen instead of routing around it.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        internal static void AppendSnowOrogenTunnelLinks(
            List<(int IndexA, int IndexB)> links,
            IReadOnlyList<WorldCitySite> cities,
            IWorldTerrainQuery terrainQuery,
            OrogenPlan orogen,
            RoadNetworkSettings settings,
            HashSet<ulong> forceChordKeys)
        {
            if (links == null || cities == null || terrainQuery == null || settings == null)
                return;
            var extraCount = Mathf.Max(0, settings.highwaySnowTunnelExtraLinks);
            if (extraCount == 0 || cities.Count < 2)
                return;

            var used = new HashSet<ulong>();
            for (var i = 0; i < links.Count; i++)
                used.Add(ToPairKey(links[i].IndexA, links[i].IndexB));

            var candidates = CollectSnowTunnelCandidates(
                cities, terrainQuery, orogen, settings, used);
            candidates.Sort(CompareSnowTunnelCandidates);

            var added = 0;
            for (var i = 0; i < candidates.Count && added < extraCount; i++)
            {
                var key = ToPairKey(candidates[i].IndexA, candidates[i].IndexB);
                if (!used.Add(key))
                    continue;
                links.Add((candidates[i].IndexA, candidates[i].IndexB));
                forceChordKeys?.Add(key);
                added++;
            }

            if (added > 0)
            {
                Debug.Log(
                    "[InterCityHighwayPlanner] snow-orogen tunnel extras=" + added +
                    " minPeak=" + settings.highwaySnowTunnelMinPeakHeightMeters.ToString("F0") + "m");
            }
        }

        internal static bool ShouldForceInfrastructureChord(
            HashSet<ulong> forceChordKeys,
            int indexA,
            int indexB)
        {
            return forceChordKeys != null && forceChordKeys.Contains(ToPairKey(indexA, indexB));
        }

        internal static ulong ToPairKey(int indexA, int indexB)
        {
            var low = Mathf.Min(indexA, indexB);
            var high = Mathf.Max(indexA, indexB);
            return ((ulong)(uint)low << 32) | (uint)high;
        }

        private struct SnowTunnelCandidate
        {
            public int IndexA;
            public int IndexB;
            public float Distance;
            public float PeakHeight;
            public float PeakCover;
        }

        private static List<SnowTunnelCandidate> CollectSnowTunnelCandidates(
            IReadOnlyList<WorldCitySite> cities,
            IWorldTerrainQuery terrainQuery,
            OrogenPlan orogen,
            RoadNetworkSettings settings,
            HashSet<ulong> used)
        {
            var results = new List<SnowTunnelCandidate>(8);
            var minPeak = Mathf.Max(80f, settings.highwaySnowTunnelMinPeakHeightMeters);
            var minCover = Mathf.Max(8f, settings.tunnelMinCoverMeters);
            var minCore = settings.tunnelRequireMajorOrogenCore
                ? Mathf.Clamp01(settings.tunnelMinOrogenCoreMask)
                : 0f;
            var minLink = Mathf.Max(50f, settings.highwayMinLinkDistanceMeters);

            for (var i = 0; i < cities.Count - 1; i++)
            {
                if (cities[i] == null)
                    continue;
                for (var j = i + 1; j < cities.Count; j++)
                {
                    if (cities[j] == null || used.Contains(ToPairKey(i, j)))
                        continue;
                    var a = cities[i].CenterXZ;
                    var b = cities[j].CenterXZ;
                    var distance = Vector2.Distance(a, b);
                    if (distance < minLink)
                        continue;
                    if (!TrySampleChordSnow(
                            a, b, terrainQuery, orogen, minPeak, minCover, minCore,
                            out var peakHeight, out var peakCover))
                        continue;
                    results.Add(new SnowTunnelCandidate
                    {
                        IndexA = i,
                        IndexB = j,
                        Distance = distance,
                        PeakHeight = peakHeight,
                        PeakCover = peakCover
                    });
                }
            }

            return results;
        }

        private static bool TrySampleChordSnow(
            Vector2 a,
            Vector2 b,
            IWorldTerrainQuery terrainQuery,
            OrogenPlan orogen,
            float minPeak,
            float minCover,
            float minCore,
            out float peakHeight,
            out float peakCover)
        {
            peakHeight = 0f;
            peakCover = 0f;
            if (!terrainQuery.TrySampleHeight(a, out var aY) ||
                !terrainQuery.TrySampleHeight(b, out var bY))
                return false;

            var distance = Vector2.Distance(a, b);
            var steps = Mathf.Max(4, Mathf.CeilToInt(distance / 48f));
            var peakCore = 0f;
            for (var s = 1; s < steps; s++)
            {
                var t = s / (float)steps;
                var p = Vector2.Lerp(a, b, t);
                if (!terrainQuery.TrySampleHeight(p, out var h))
                    continue;
                peakHeight = Mathf.Max(peakHeight, h);
                peakCover = Mathf.Max(peakCover, h - Mathf.Lerp(aY, bY, t));
                if (orogen != null)
                    peakCore = Mathf.Max(peakCore, orogen.SampleOrogenCoreMask(p.x, p.y));
            }

            return peakHeight >= minPeak && peakCover >= minCover && peakCore >= minCore;
        }

        private static int CompareSnowTunnelCandidates(SnowTunnelCandidate a, SnowTunnelCandidate b)
        {
            var d = a.Distance.CompareTo(b.Distance);
            if (d != 0)
                return d;
            return b.PeakCover.CompareTo(a.PeakCover);
        }
    }
}
