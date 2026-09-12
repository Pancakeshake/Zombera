using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Length-aware ocean outlet selection for multi-km coast stems.</summary>
    public static partial class RiverNetworkBuilder
    {
        private readonly struct OutletScore
        {
            public readonly int Cell;
            public readonly float LengthMeters;
            public readonly float Score;

            public OutletScore(int cell, float lengthMeters, float score)
            {
                Cell = cell;
                LengthMeters = lengthMeters;
                Score = score;
            }
        }

        private static List<int> SelectOutlets(
            LandformField field,
            float[] accumulation,
            List<int>[] upstream,
            bool[] oceanMask,
            bool[] lakeMask,
            List<int> candidates,
            HydrologyProfile profile)
        {
            var selected = new List<int>(profile.MaxRiverSystems);
            if (field == null || candidates == null || candidates.Count == 0 || profile == null)
                return selected;

            var maxLandY = profile.SeaLevelWorldY + profile.MaxRiverLandElevationAboveSeaMeters;
            var scored = new List<OutletScore>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var cell = candidates[i];
                var path = TraceUpstream(
                    cell,
                    upstream,
                    accumulation,
                    oceanMask,
                    lakeMask,
                    field,
                    maxLandY,
                    profile.ValleyFlowPreference,
                    null,
                    out _);
                if (path.Count < 2)
                    continue;
                var length = EstimateCellLength(field, path);
                var flow = accumulation[cell];
                var score = length * Mathf.Log(1f + Mathf.Max(0f, flow));
                scored.Add(new OutletScore(cell, length, score));
            }

            if (scored.Count == 0)
                return selected;

            scored.Sort((a, b) =>
            {
                var compare = b.Score.CompareTo(a.Score);
                if (compare != 0) return compare;
                compare = b.LengthMeters.CompareTo(a.LengthMeters);
                return compare != 0 ? compare : a.Cell.CompareTo(b.Cell);
            });

            var targetLength = profile.PrimaryRiverMinimumLengthMeters;
            var separation = Mathf.Max(profile.PrimaryOutletSeparationMeters, profile.OutletClusterRadiusMeters);
            var separationSq = separation * separation;
            var dominantFlow = 0f;
            var usedFallback = false;

            for (var pass = 0; pass < 2 && selected.Count < profile.MaxRiverSystems; pass++)
            {
                var requireTarget = pass == 0;
                for (var i = 0; i < scored.Count && selected.Count < profile.MaxRiverSystems; i++)
                {
                    var entry = scored[i];
                    if (requireTarget && entry.LengthMeters < targetLength)
                        continue;
                    if (!requireTarget && entry.LengthMeters >= targetLength)
                        continue;

                    var candidate = entry.Cell;
                    if (selected.Count > 0 &&
                        accumulation[candidate] < dominantFlow * profile.SecondarySystemScoreRatio)
                        continue;
                    if (IsTooClose(field, candidate, selected, separationSq))
                        continue;

                    selected.Add(candidate);
                    dominantFlow = Mathf.Max(dominantFlow, accumulation[candidate]);
                    if (!requireTarget)
                        usedFallback = true;
                }
            }

            if (selected.Count == 0)
            {
                selected.Add(scored[0].Cell);
                usedFallback = scored[0].LengthMeters < targetLength;
            }

            if (usedFallback)
            {
                var kept = EstimateCellLength(
                    field,
                    TraceUpstream(
                        selected[0],
                        upstream,
                        accumulation,
                        oceanMask,
                        lakeMask,
                        field,
                        maxLandY,
                        profile.ValleyFlowPreference,
                        null,
                        out _));
                Debug.Log(
                    $"[RiverNetworkBuilder] primary below target length={kept:0}m " +
                    $"(target={targetLength:0}m); keeping longest low-land coast stem.");
            }

            return selected;
        }

        private static bool IsTooClose(
            LandformField field,
            int candidate,
            List<int> selected,
            float separationSq)
        {
            var point = field.CellCenterXZ(candidate % field.Width, candidate / field.Width);
            for (var s = 0; s < selected.Count; s++)
            {
                var other = selected[s];
                var otherPoint = field.CellCenterXZ(other % field.Width, other / field.Width);
                if ((point - otherPoint).sqrMagnitude < separationSq)
                    return true;
            }

            return false;
        }
    }
}
