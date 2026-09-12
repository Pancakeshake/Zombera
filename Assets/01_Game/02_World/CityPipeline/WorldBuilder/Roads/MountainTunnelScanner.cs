using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Detects highway spans where natural cover greatly exceeds an endpoint-lerp design bed
    ///     (valley-to-valley grade under ridges) and peak elevation reaches the snow line.
    ///     Ignores pad aprons, short spans, water∩tunnel overlaps, and mid-elevation hills below snow.
    /// </summary>
    public static partial class MountainTunnelScanner
    {
        private struct TunnelCandidate
        {
            public RoadPolyline Road;
            public Vector2 Entry;
            public Vector2 Exit;
            public float EntryY;
            public float ExitY;
            public float SpanLength;
            public float PeakCover;
            public float PeakNaturalY;
            public float PeakOrogenCoreMask;
            public float MinLength;
            public float MinPeakElevationAboveSea;
            public bool RequireMajorOrogenCore;
            public float MinOrogenCoreMask;
            public float SeaLevelWorldY;
            public float Daylight;
        }

        private struct SpanTracker
        {
            public bool InSpan;
            public bool RejectUntilDaylight;
            public Vector2 Entry;
            public float EntryY;
            public float EntryDistance;
            public float PeakCover;
            public float PeakNaturalY;
            public float PeakOrogenCoreMask;
            public float DistanceAlong;
            public Vector2 Previous;
            public float PreviousCover;
        }

        private struct ScanContext
        {
            public RoadPolyline Road;
            public PolylineHeightProfile BedProfile;
            public LandformField Landforms;
            public OrogenPlan Orogen;
            public IReadOnlyList<CityFlattenPad> Pads;
            public IReadOnlyList<WaterCrossing> Crossings;
            public float CoverThreshold;
            public float MaxLength;
            public float MinLength;
            public float MinPeakElevationAboveSea;
            public bool RequireMajorOrogenCore;
            public float MinOrogenCoreMask;
            public float SeaLevelWorldY;
            public float Daylight;
            public float PadExclude;
            public List<MountainTunnel> Results;
        }

        public static List<MountainTunnel> Scan(
            RoadNetworkRuntime roads,
            LandformField landforms,
            RoadNetworkSettings settings,
            IReadOnlyList<CityFlattenPad> pads,
            IReadOnlyList<WaterCrossing> crossings,
            float seaLevelWorldY,
            OrogenPlan orogen = null)
        {
            var results = new List<MountainTunnel>(8);
            if (roads?.Roads == null || landforms == null || settings == null || !settings.enableMountainTunnels)
                return results;

            for (var r = 0; r < roads.Roads.Count; r++)
            {
                var road = roads.Roads[r];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;
                ScanHighway(road, landforms, settings, pads, crossings, seaLevelWorldY, orogen, results);
            }

            return results;
        }

        private static void ScanHighway(
            RoadPolyline road,
            LandformField landforms,
            RoadNetworkSettings settings,
            IReadOnlyList<CityFlattenPad> pads,
            IReadOnlyList<WaterCrossing> crossings,
            float seaLevelWorldY,
            OrogenPlan orogen,
            List<MountainTunnel> results)
        {
            var coverThreshold = Mathf.Max(8f, settings.tunnelMinCoverMeters);
            var minLength = Mathf.Max(settings.tunnelMinLengthMeters, landforms.CellSize * 3f);
            var maxLength = Mathf.Max(minLength, settings.tunnelMaxLengthMeters);
            var padExclude = Mathf.Max(0f, settings.tunnelPadExclusionMeters);
            var daylight = Mathf.Max(2f, settings.tunnelPortalDaylightMeters);
            var minPeakAboveSea = Mathf.Max(0f, settings.tunnelMinPeakElevationAboveSeaMeters);
            var requireMajorOrogenCore = settings.tunnelRequireMajorOrogenCore;
            var minOrogenCoreMask = Mathf.Clamp01(settings.tunnelMinOrogenCoreMask);

            // Endpoint-lerp design bed vs natural: positive cover ⇒ under-ridge bore (tunnel candidate).
            // Terrain-follow / slope-clamp beds climb the ridge and shrink mouths to mid-slope stubs.
            var bedProfile = PolylineHeightProfile.BuildEndpointLerpBed(
                road.pointsXZ,
                xz => LandformFieldSampling.SampleBilinear(landforms, xz.x, xz.y),
                resampleSpacingMeters: Mathf.Max(8f, landforms.CellSize));
            if (bedProfile == null || !bedProfile.IsValid)
                return;

            var ctx = new ScanContext
            {
                Road = road,
                BedProfile = bedProfile,
                Landforms = landforms,
                Orogen = orogen,
                Pads = pads,
                Crossings = crossings,
                CoverThreshold = coverThreshold,
                MaxLength = maxLength,
                MinLength = minLength,
                MinPeakElevationAboveSea = minPeakAboveSea,
                RequireMajorOrogenCore = requireMajorOrogenCore,
                MinOrogenCoreMask = minOrogenCoreMask,
                SeaLevelWorldY = seaLevelWorldY,
                Daylight = daylight,
                PadExclude = padExclude,
                Results = results
            };

            var samples = bedProfile.PointsXZ;
            var tracker = new SpanTracker
            {
                Previous = samples[0],
                PreviousCover = CoverAt(samples[0], bedProfile, landforms, pads, padExclude)
            };

            for (var i = 1; i < samples.Count; i++)
                AdvanceSpanSample(ctx, samples[i], ref tracker);

            if (!tracker.InSpan)
                return;

            TryAddTunnel(ctx, BuildCandidate(
                ctx,
                tracker.Entry,
                samples[^1],
                tracker.EntryY,
                bedProfile.SampleHeightAlongPath(samples[^1]),
                tracker.DistanceAlong - tracker.EntryDistance,
                tracker.PeakCover,
                tracker.PeakNaturalY,
                tracker.PeakOrogenCoreMask));
        }

        private static void AdvanceSpanSample(
            in ScanContext ctx,
            Vector2 current,
            ref SpanTracker tracker)
        {
            var segmentLength = Vector2.Distance(tracker.Previous, current);
            var currentCover = CoverAt(
                current, ctx.BedProfile, ctx.Landforms, ctx.Pads, ctx.PadExclude);
            var wasCovered = tracker.PreviousCover >= ctx.CoverThreshold;
            var isCovered = currentCover >= ctx.CoverThreshold;
            var previousExcluded = !IsFinite(tracker.PreviousCover);
            var currentExcluded = !IsFinite(currentCover);

            if (previousExcluded && !currentExcluded && isCovered)
            {
                // Pad exclusion is not daylight, but it is a valid mouth once the
                // sample is outside the pad. Skipping until uncover dropped whole
                // snow-ridge bores on city-to-city chords.
                BeginSpan(ctx, current, currentCover, ref tracker);
            }
            else if (!wasCovered && isCovered && !tracker.RejectUntilDaylight)
            {
                BeginSpan(ctx, current, currentCover, ref tracker);
            }
            else if (tracker.InSpan && isCovered)
            {
                ExtendOrRejectSpan(ctx, current, currentCover, segmentLength, ref tracker);
            }

            if (wasCovered && !isCovered)
            {
                if (tracker.InSpan)
                    EndSpan(ctx, current, currentCover, ref tracker);
                else
                    tracker.RejectUntilDaylight = currentExcluded;
            }

            tracker.DistanceAlong += segmentLength;
            tracker.Previous = current;
            tracker.PreviousCover = currentCover;
        }

        private static void BeginSpan(
            in ScanContext ctx,
            Vector2 current,
            float currentCover,
            ref SpanTracker tracker)
        {
            tracker.Entry = InterpolateThreshold(
                tracker.Previous, current, tracker.PreviousCover, currentCover, ctx.CoverThreshold);
            tracker.EntryY = ctx.BedProfile.SampleHeightAlongPath(tracker.Entry);
            tracker.EntryDistance = tracker.DistanceAlong + Vector2.Distance(tracker.Previous, tracker.Entry);
            tracker.PeakCover = currentCover;
            tracker.PeakNaturalY = LandformFieldSampling.SampleBilinear(
                ctx.Landforms, current.x, current.y);
            tracker.PeakOrogenCoreMask = SampleOrogenCoreMask(ctx.Orogen, current);
            tracker.InSpan = true;
        }

        private static void ExtendOrRejectSpan(
            in ScanContext ctx,
            Vector2 current,
            float currentCover,
            float segmentLength,
            ref SpanTracker tracker)
        {
            tracker.PeakCover = Mathf.Max(tracker.PeakCover, currentCover);
            tracker.PeakNaturalY = Mathf.Max(
                tracker.PeakNaturalY,
                LandformFieldSampling.SampleBilinear(ctx.Landforms, current.x, current.y));
            tracker.PeakOrogenCoreMask = Mathf.Max(
                tracker.PeakOrogenCoreMask,
                SampleOrogenCoreMask(ctx.Orogen, current));
            var spanLen = tracker.DistanceAlong + segmentLength - tracker.EntryDistance;
            if (spanLen <= ctx.MaxLength)
                return;

            // Emit a capped tunnel rather than discarding a long valid cover span.
            var overshoot = spanLen - ctx.MaxLength;
            var t = 1f - overshoot / Mathf.Max(0.01f, segmentLength);
            var exit = Vector2.Lerp(tracker.Previous, current, Mathf.Clamp01(t));
            TryAddTunnel(ctx, BuildCandidate(
                ctx,
                tracker.Entry,
                exit,
                tracker.EntryY,
                ctx.BedProfile.SampleHeightAlongPath(exit),
                ctx.MaxLength,
                tracker.PeakCover,
                tracker.PeakNaturalY,
                tracker.PeakOrogenCoreMask));

            tracker.InSpan = false;
            tracker.RejectUntilDaylight = true;
        }

        private static void EndSpan(
            in ScanContext ctx,
            Vector2 current,
            float currentCover,
            ref SpanTracker tracker)
        {
            var exit = InterpolateThreshold(
                tracker.Previous, current, tracker.PreviousCover, currentCover, ctx.CoverThreshold);
            if (tracker.InSpan)
            {
                var spanLength = tracker.DistanceAlong + Vector2.Distance(tracker.Previous, exit) -
                                 tracker.EntryDistance;
                TryAddTunnel(ctx, BuildCandidate(
                    ctx,
                    tracker.Entry,
                    exit,
                    tracker.EntryY,
                    ctx.BedProfile.SampleHeightAlongPath(exit),
                    spanLength,
                    tracker.PeakCover,
                    tracker.PeakNaturalY,
                    tracker.PeakOrogenCoreMask));
            }

            tracker.InSpan = false;
            tracker.RejectUntilDaylight = false;
        }

        private static TunnelCandidate BuildCandidate(
            in ScanContext ctx,
            Vector2 entry,
            Vector2 exit,
            float entryY,
            float exitY,
            float spanLength,
            float peakCover,
            float peakNaturalY,
            float peakOrogenCoreMask) =>
            new()
            {
                Road = ctx.Road,
                Entry = entry,
                Exit = exit,
                EntryY = entryY,
                ExitY = exitY,
                SpanLength = spanLength,
                PeakCover = peakCover,
                PeakNaturalY = peakNaturalY,
                PeakOrogenCoreMask = peakOrogenCoreMask,
                MinLength = ctx.MinLength,
                MinPeakElevationAboveSea = ctx.MinPeakElevationAboveSea,
                RequireMajorOrogenCore = ctx.RequireMajorOrogenCore,
                MinOrogenCoreMask = ctx.MinOrogenCoreMask,
                SeaLevelWorldY = ctx.SeaLevelWorldY,
                Daylight = ctx.Daylight
            };

        private static float SampleOrogenCoreMask(OrogenPlan orogen, Vector2 worldXZ) =>
            orogen != null ? orogen.SampleOrogenCoreMask(worldXZ.x, worldXZ.y) : 0f;

        private static float CoverAt(
            Vector2 point,
            PolylineHeightProfile bedProfile,
            LandformField landforms,
            IReadOnlyList<CityFlattenPad> pads,
            float padExclude)
        {
            if (IsNearPadExclusion(pads, point, padExclude))
                return float.NegativeInfinity;
            return LandformFieldSampling.SampleBilinear(landforms, point.x, point.y) -
                   bedProfile.SampleHeightAlongPath(point);
        }

        private static Vector2 InterpolateThreshold(
            Vector2 a,
            Vector2 b,
            float coverA,
            float coverB,
            float threshold)
        {
            if (!IsFinite(coverA))
                return b;
            if (!IsFinite(coverB))
                return a;

            var delta = coverB - coverA;
            if (Mathf.Abs(delta) < 0.0001f)
                return (a + b) * 0.5f;
            return Vector2.Lerp(a, b, Mathf.Clamp01((threshold - coverA) / delta));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void TryAddTunnel(in ScanContext ctx, TunnelCandidate candidate)
        {
            var length = Mathf.Max(candidate.SpanLength, Vector2.Distance(candidate.Entry, candidate.Exit));
            if (length < candidate.MinLength)
                return;
            if (candidate.PeakNaturalY < candidate.SeaLevelWorldY + candidate.MinPeakElevationAboveSea)
                return;
            if (candidate.RequireMajorOrogenCore &&
                candidate.PeakOrogenCoreMask < candidate.MinOrogenCoreMask)
                return;
            if (OverlapsWaterCrossing(candidate.Entry, candidate.Exit, ctx.Crossings))
                return;

            RefineCandidateMouths(ctx, ref candidate);

            var hasher = new StableHash64();
            hasher.Append(candidate.Road.id);
            hasher.Append(candidate.Entry.x);
            hasher.Append(candidate.Entry.y);
            hasher.Append(candidate.Exit.x);
            hasher.Append(candidate.Exit.y);

            length = Mathf.Max(candidate.SpanLength, Vector2.Distance(candidate.Entry, candidate.Exit));
            ctx.Results.Add(new MountainTunnel(
                hasher.Finalize(),
                candidate.Road.id,
                candidate.Entry,
                candidate.Exit,
                candidate.EntryY,
                candidate.ExitY,
                length,
                candidate.PeakCover,
                candidate.Daylight));
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
            var midA = (a0 + a1) * 0.5f;
            var midB = (b0 + b1) * 0.5f;
            return Vector2.Distance(midA, midB) <= maxDist;
        }

        private static bool IsNearPadExclusion(
            IReadOnlyList<CityFlattenPad> pads,
            Vector2 worldXZ,
            float excludeMeters)
        {
            if (pads == null || pads.Count == 0)
                return false;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null)
                    continue;
                var outer = pad.OuterBoundsXZ;
                var expanded = Rect.MinMaxRect(
                    outer.xMin - excludeMeters,
                    outer.yMin - excludeMeters,
                    outer.xMax + excludeMeters,
                    outer.yMax + excludeMeters);
                if (worldXZ.x >= expanded.xMin && worldXZ.x <= expanded.xMax &&
                    worldXZ.y >= expanded.yMin && worldXZ.y <= expanded.yMax)
                    return true;
            }

            return false;
        }
    }
}
