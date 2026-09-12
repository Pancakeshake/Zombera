using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Replaces ridge-winding highway interiors with straight tunnel chords when
    ///     path length greatly exceeds chord length and cover along the chord is sufficient.
    ///     Runs at polyline ownership time (before the first corridor carve).
    /// </summary>
    public static class HighwayTunnelChordRewriter
    {
        private struct ChordCandidate
        {
            public int EntryIndex;
            public int ExitIndex;
            public float PathLength;
            public float ChordLength;
            public float PathSaved;
            public float PeakCover;
            public float PeakNaturalY;
            public float EntryY;
            public float ExitY;
        }

        /// <summary>Rewrites highway polylines in-place. Returns number of roads shortened.</summary>
        public static int Rewrite(
            RoadNetworkRuntime network,
            LandformField landforms,
            RoadNetworkSettings settings,
            IReadOnlyList<CityFlattenPad> pads,
            IReadOnlyList<WaterCrossing> crossings,
            float seaLevelWorldY)
        {
            if (network?.Roads == null || landforms == null || settings == null || !settings.enableMountainTunnels)
                return 0;

            var rewritten = 0;
            for (var r = 0; r < network.Roads.Count; r++)
            {
                var road = network.Roads[r];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 4)
                    continue;
                if (!TryRewriteHighway(road, landforms, settings, pads, crossings, seaLevelWorldY))
                    continue;
                rewritten++;
            }

            if (rewritten > 0)
            {
                Debug.Log(
                    "[HighwayTunnelChordRewriter] Rewrote " + rewritten +
                    " highway(s) to tunnel chords (algorithm v" +
                    settings.tunnelChordAlgorithmVersion + ").");
            }

            return rewritten;
        }

        private static bool TryRewriteHighway(
            RoadPolyline road,
            LandformField landforms,
            RoadNetworkSettings settings,
            IReadOnlyList<CityFlattenPad> pads,
            IReadOnlyList<WaterCrossing> crossings,
            float seaLevelWorldY)
        {
            var points = road.pointsXZ;
            BuildPrefixLengths(points, out var prefix);
            if (!TryFindBestChord(
                    points,
                    prefix,
                    landforms,
                    settings,
                    pads,
                    crossings,
                    seaLevelWorldY,
                    out var best))
                return false;

            ApplyRewrite(points, best.EntryIndex, best.ExitIndex);
            Debug.Log(
                "[HighwayTunnelChordRewriter] road=" + road.id +
                " saved=" + best.PathSaved.ToString("F0") + "m" +
                " chord=" + best.ChordLength.ToString("F0") + "m" +
                " cover=" + best.PeakCover.ToString("F1") + "m" +
                " entry=" + best.EntryIndex + " exit=" + best.ExitIndex);
            return true;
        }

        private static bool TryFindBestChord(
            IReadOnlyList<Vector2> points,
            float[] prefix,
            LandformField landforms,
            RoadNetworkSettings settings,
            IReadOnlyList<CityFlattenPad> pads,
            IReadOnlyList<WaterCrossing> crossings,
            float seaLevelWorldY,
            out ChordCandidate best)
        {
            best = default;
            var found = false;
            var n = points.Count;
            var minLength = Mathf.Max(settings.tunnelMinLengthMeters, landforms.CellSize * 3f);
            var maxLength = Mathf.Max(minLength, settings.tunnelMaxLengthMeters);
            var coverThreshold = Mathf.Max(8f, settings.tunnelMinCoverMeters);
            var minPeakAboveSea = Mathf.Max(0f, settings.tunnelMinPeakElevationAboveSeaMeters);
            var padExclude = Mathf.Max(0f, settings.tunnelPadExclusionMeters);
            var minRatio = Mathf.Max(1.05f, settings.tunnelChordMinPathToChordRatio);
            var sampleStep = Mathf.Max(8f, settings.tunnelChordSampleStepMeters);
            var maxSlopeRatio = Mathf.Tan(Mathf.Max(1f, settings.maxHighwayRoadSlopeDegrees) * Mathf.Deg2Rad);
            var resample = Mathf.Max(8f, landforms.CellSize);
            var maxIndexSpan = Mathf.Max(2, Mathf.CeilToInt(maxLength / resample) + 1);

            for (var i = 1; i < n - 2; i++)
            {
                var jMax = Mathf.Min(n - 2, i + maxIndexSpan);
                for (var j = i + 2; j <= jMax; j++)
                {
                    var chordLength = Vector2.Distance(points[i], points[j]);
                    if (chordLength < minLength || chordLength > maxLength)
                        continue;

                    var pathLength = prefix[j] - prefix[i];
                    if (pathLength < chordLength * minRatio)
                        continue;

                    if (!TryEvaluateChordCover(
                            points[i],
                            points[j],
                            landforms,
                            pads,
                            padExclude,
                            coverThreshold,
                            minPeakAboveSea,
                            seaLevelWorldY,
                            maxSlopeRatio,
                            sampleStep,
                            out var peakCover,
                            out var peakNaturalY,
                            out var entryY,
                            out var exitY))
                        continue;

                    if (OverlapsWaterCrossing(points[i], points[j], crossings))
                        continue;

                    var pathSaved = pathLength - chordLength;
                    if (found && !IsBetter(pathSaved, chordLength, points, i, j, best))
                        continue;

                    best = new ChordCandidate
                    {
                        EntryIndex = i,
                        ExitIndex = j,
                        PathLength = pathLength,
                        ChordLength = chordLength,
                        PathSaved = pathSaved,
                        PeakCover = peakCover,
                        PeakNaturalY = peakNaturalY,
                        EntryY = entryY,
                        ExitY = exitY
                    };
                    found = true;
                }
            }

            return found;
        }

        private static bool IsBetter(
            float pathSaved,
            float chordLength,
            IReadOnlyList<Vector2> points,
            int entryIndex,
            int exitIndex,
            in ChordCandidate current)
        {
            if (pathSaved > current.PathSaved + 0.01f)
                return true;
            if (pathSaved < current.PathSaved - 0.01f)
                return false;
            if (chordLength < current.ChordLength - 0.01f)
                return true;
            if (chordLength > current.ChordLength + 0.01f)
                return false;

            var e = points[entryIndex];
            var x = points[exitIndex];
            var ce = points[current.EntryIndex];
            var cx = points[current.ExitIndex];
            if (e.x != ce.x) return e.x < ce.x;
            if (e.y != ce.y) return e.y < ce.y;
            if (x.x != cx.x) return x.x < cx.x;
            return x.y < cx.y;
        }

        private static bool TryEvaluateChordCover(
            Vector2 entry,
            Vector2 exit,
            LandformField landforms,
            IReadOnlyList<CityFlattenPad> pads,
            float padExclude,
            float coverThreshold,
            float minPeakAboveSea,
            float seaLevelWorldY,
            float maxSlopeRatio,
            float sampleStep,
            out float peakCover,
            out float peakNaturalY,
            out float entryY,
            out float exitY)
        {
            peakCover = 0f;
            peakNaturalY = float.NegativeInfinity;
            entryY = LandformFieldSampling.SampleBilinear(landforms, entry.x, entry.y);
            exitY = LandformFieldSampling.SampleBilinear(landforms, exit.x, exit.y);
            var chordLength = Vector2.Distance(entry, exit);
            if (chordLength < 0.01f)
                return false;

            var grade = Mathf.Abs(exitY - entryY) / chordLength;
            if (grade > maxSlopeRatio)
                return false;

            if (IsNearPad(pads, entry, padExclude) || IsNearPad(pads, exit, padExclude))
                return false;

            var samples = Mathf.Max(2, Mathf.CeilToInt(chordLength / sampleStep));
            var minInteriorCover = float.PositiveInfinity;
            var interiorHits = 0;
            for (var s = 0; s <= samples; s++)
            {
                var t = s / (float)samples;
                var xz = Vector2.Lerp(entry, exit, t);
                if (IsNearPad(pads, xz, padExclude))
                    return false;

                var naturalY = LandformFieldSampling.SampleBilinear(landforms, xz.x, xz.y);
                var bedY = Mathf.Lerp(entryY, exitY, t);
                var cover = naturalY - bedY;
                if (cover > peakCover)
                    peakCover = cover;
                if (naturalY > peakNaturalY)
                    peakNaturalY = naturalY;

                // Mouths are daylight — only require cover on the bore interior.
                if (t < 0.12f || t > 0.88f)
                    continue;
                interiorHits++;
                if (cover < minInteriorCover)
                    minInteriorCover = cover;
            }

            if (interiorHits <= 0 || minInteriorCover < coverThreshold)
                return false;
            if (peakNaturalY < seaLevelWorldY + minPeakAboveSea)
                return false;
            return true;
        }

        private static void ApplyRewrite(List<Vector2> points, int entryIndex, int exitIndex)
        {
            if (exitIndex <= entryIndex + 1)
                return;

            var keepTail = points.Count - exitIndex;
            var newCount = entryIndex + 1 + keepTail;
            var rewritten = new List<Vector2>(newCount);
            for (var i = 0; i <= entryIndex; i++)
                rewritten.Add(points[i]);
            for (var i = exitIndex; i < points.Count; i++)
                rewritten.Add(points[i]);

            points.Clear();
            points.AddRange(rewritten);
        }

        private static void BuildPrefixLengths(IReadOnlyList<Vector2> points, out float[] prefix)
        {
            prefix = new float[points.Count];
            prefix[0] = 0f;
            for (var i = 1; i < points.Count; i++)
                prefix[i] = prefix[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        private static bool OverlapsWaterCrossing(
            Vector2 entry,
            Vector2 exit,
            IReadOnlyList<WaterCrossing> crossings)
        {
            if (crossings == null || crossings.Count == 0)
                return false;

            for (var i = 0; i < crossings.Count; i++)
            {
                var c = crossings[i];
                if (SegmentsNear(entry, exit, c.EntryXZ, c.ExitXZ, 24f))
                    return true;
            }

            return false;
        }

        private static bool SegmentsNear(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float maxDist)
        {
            if (Vector2.Distance(a0, b0) <= maxDist || Vector2.Distance(a0, b1) <= maxDist)
                return true;
            if (Vector2.Distance(a1, b0) <= maxDist || Vector2.Distance(a1, b1) <= maxDist)
                return true;
            return Vector2.Distance((a0 + a1) * 0.5f, (b0 + b1) * 0.5f) <= maxDist;
        }

        private static bool IsNearPad(IReadOnlyList<CityFlattenPad> pads, Vector2 worldXZ, float excludeMeters)
        {
            if (pads == null || pads.Count == 0 || excludeMeters <= 0f)
                return false;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                var outer = pad.OuterBoundsXZ;
                var expanded = Rect.MinMaxRect(
                    outer.xMin - excludeMeters,
                    outer.yMin - excludeMeters,
                    outer.xMax + excludeMeters,
                    outer.yMax + excludeMeters);
                if (expanded.Contains(worldXZ))
                    return true;
            }

            return false;
        }
    }
}
