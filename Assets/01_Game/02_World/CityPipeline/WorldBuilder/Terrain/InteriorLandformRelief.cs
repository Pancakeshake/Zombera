using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Sole interior mountain/orogen height authority: warped polylines, foothill skirts,
    /// crest saddles (passes), and structural valley deepen between ridge limbs.
    /// </summary>
    public static partial class InteriorLandformRelief
    {
        public readonly struct MountainRange
        {
            public readonly Vector2 Start;
            public readonly Vector2 Mid;
            public readonly Vector2 End;
            public readonly float WidthMeters;
            public readonly float PeakMeters;
            public readonly float Ruggedness;
            public readonly float PassAlong01;
            public readonly float PassWidth01;
            public readonly float FoothillSkirtMeters;

            public MountainRange(in MountainRangeArgs args)
            {
                Start = args.Spine.Start;
                Mid = args.Spine.Mid;
                End = args.Spine.End;
                WidthMeters = args.Metrics.WidthMeters;
                PeakMeters = args.Metrics.PeakMeters;
                Ruggedness = args.Metrics.Ruggedness;
                PassAlong01 = args.Metrics.PassAlong01;
                PassWidth01 = args.Metrics.PassWidth01;
                FoothillSkirtMeters = args.Metrics.FoothillSkirtMeters;
            }

            public Vector2 EvaluatePassCenter()
            {
                return EvaluateBezier(Start, Mid, End, Mathf.Clamp01(PassAlong01));
            }
        }

        public readonly struct OrogenSample
        {
            public readonly float Height;
            public readonly float OrogenMask;
            public readonly float FoothillMask;
            public readonly float ValleyDeepenMeters;

            public OrogenSample(float height, float orogenMask, float foothillMask, float valleyDeepenMeters)
            {
                Height = height;
                OrogenMask = orogenMask;
                FoothillMask = foothillMask;
                ValleyDeepenMeters = valleyDeepenMeters;
            }
        }

        public static MountainRange[] BuildRanges(
            int landformSeed,
            LandformProfile profile,
            Rect bounds,
            WorldMapBoundaryLayout layout)
        {
            if (profile == null || !profile.InteriorMountainsEnabled)
                return System.Array.Empty<MountainRange>();

            var count = Mathf.Clamp(profile.InteriorMountainRangeCount, 0, 4);
            if (count == 0)
                return System.Array.Empty<MountainRange>();

            var rng = new DeterministicRng(landformSeed + profile.InteriorMountainSeedOffset);
            // Keep spines on the map; do not force a large inland-only band.
            var inset = Mathf.Max(40f, profile.InteriorReliefInsetMeters);
            var inner = new Rect(
                bounds.xMin + inset,
                bounds.yMin + inset,
                bounds.width - inset * 2f,
                bounds.height - inset * 2f);

            if (inner.width < 100f || inner.height < 100f)
                return System.Array.Empty<MountainRange>();

            // Only keep range anchors out of the wet trench/shelf — beaches are allowed.
            ShrinkAwayFromOcean(ref inner, bounds, layout, ResolveOceanKeepOutMeters(profile));
            if (inner.width < 100f || inner.height < 100f)
                return System.Array.Empty<MountainRange>();

            var ranges = new MountainRange[count];
            var width = profile.InteriorMountainWidthMeters;
            var peak = profile.InteriorMountainPeakMeters;
            var rugged = profile.InteriorMountainRuggedness;
            var skirt = Mathf.Max(60f, profile.FoothillSkirtMeters);
            var warpFrac = Mathf.Clamp01(profile.OrogenWarpFraction);

            for (var i = 0; i < count; i++)
            {
                var alongX = rng.NextFloat01() > 0.25f;
                Vector2 start;
                Vector2 end;
                if (alongX)
                {
                    var z = Mathf.Lerp(inner.yMin, inner.yMax, 0.05f + rng.NextFloat01() * 0.9f);
                    start = new Vector2(
                        inner.xMin + inner.width * (0.02f + rng.NextFloat01() * 0.1f),
                        z);
                    end = new Vector2(
                        inner.xMax - inner.width * (0.02f + rng.NextFloat01() * 0.1f),
                        z + (rng.NextFloat01() - 0.5f) * inner.height * 0.12f);
                }
                else
                {
                    var x = Mathf.Lerp(inner.xMin, inner.xMax, 0.05f + rng.NextFloat01() * 0.9f);
                    start = new Vector2(
                        x,
                        inner.yMin + inner.height * (0.02f + rng.NextFloat01() * 0.1f));
                    end = new Vector2(
                        x + (rng.NextFloat01() - 0.5f) * inner.width * 0.12f,
                        inner.yMax - inner.height * (0.02f + rng.NextFloat01() * 0.1f));
                }

                var chord = end - start;
                var len = chord.magnitude;
                var dir = len > 1f ? chord / len : Vector2.right;
                // Perpendicular (CCW) without swapped ctor-arg identifiers (Sonar S2234).
                var perpendicularX = -dir.y;
                var perpendicularY = dir.x;
                var perp = new Vector2(perpendicularX, perpendicularY);
                var mid = (start + end) * 0.5f;
                mid += perp * ((rng.NextFloat01() - 0.5f) * 2f * len * warpFrac);
                mid += dir * ((rng.NextFloat01() - 0.5f) * len * 0.08f);

                var peakVar = peak * (0.75f + rng.NextFloat01() * 0.5f);
                var widthVar = width * (0.7f + rng.NextFloat01() * 0.6f);
                var passAlong = 0.35f + rng.NextFloat01() * 0.3f;
                var passWidth = 0.08f + rng.NextFloat01() * 0.08f;
                ranges[i] = new MountainRange(new MountainRangeArgs(
                    new MountainRangeSpine(start, mid, end),
                    new MountainRangeMetrics(
                        widthVar,
                        peakVar,
                        rugged,
                        passAlong,
                        passWidth,
                        skirt * (0.85f + rng.NextFloat01() * 0.3f))));
            }

            return ranges;
        }

        public static OrogenPlan BuildOrogenPlan(MountainRange[] ranges)
        {
            if (ranges == null || ranges.Length == 0)
                return new OrogenPlan(System.Array.Empty<MountainRange>(), System.Array.Empty<Vector2>());

            var passes = new Vector2[ranges.Length];
            for (var i = 0; i < ranges.Length; i++)
                passes[i] = ranges[i].EvaluatePassCenter();
            return new OrogenPlan(ranges, passes);
        }

        public static OrogenSample Sample(in OrogenSampleArgs args)
        {
            if (args.Profile == null || args.Query.LandMask < 0.01f)
                return default;

            var compose = LandformComposeParams.Capture(args.Profile, bakeMountainMaskCurve: false);
            var interiorMask = EvaluateInteriorMask(
                args.Query.WorldX,
                args.Query.WorldZ,
                args.Query.Bounds,
                args.Query.Layout,
                compose.InteriorReliefInsetMeters,
                compose.OceanKeepOutMeters,
                compose.EdgeBarrierDepthMeters);
            return Sample(new OrogenComposeSampleArgs(
                args.Query.WorldX,
                args.Query.WorldZ,
                in compose,
                args.Ranges,
                args.RidgeNoise,
                args.RollingNoise,
                new OrogenComposeMasks(
                    args.Query.LandMask, interiorMask, args.Query.FastLandforms)));
        }

        /// <summary>
        ///     Thread-safe sample using a main-thread snapshot of profile values.
        /// </summary>
        public static OrogenSample Sample(in OrogenComposeSampleArgs args)
        {
            if (args.Masks.LandMask < 0.01f || args.Masks.InteriorMask < 0.001f)
                return default;

            var rollOctaves = args.Masks.FastLandforms ? 3 : 5;
            var rollRidgeOctaves = args.Masks.FastLandforms ? 2 : 3;
            var height = 0f;
            var rollScale = Mathf.Max(100f, args.Profile.InteriorRollingScaleMeters);
            var roll = args.RollingNoise.Fbm(
                args.WorldX / rollScale, args.WorldZ / rollScale, rollOctaves);
            var rollRidge = args.RollingNoise.Ridged(
                args.WorldX / (rollScale * 0.5f),
                args.WorldZ / (rollScale * 0.5f),
                rollRidgeOctaves);
            height += (roll - 0.5f) * 2f * args.Profile.InteriorRollingAmplitudeMeters * args.Masks.InteriorMask;
            height += rollRidge * args.Profile.InteriorRollingAmplitudeMeters * 0.45f * args.Masks.InteriorMask;

            var orogenMask = 0f;
            var foothillMask = 0f;
            if (args.Ranges != null && args.Ranges.Length > 0)
                AccumulateRangeHeights(in args, ref height, out orogenMask, out foothillMask);

            return new OrogenSample(
                height * args.Masks.LandMask,
                Mathf.Clamp01(orogenMask * args.Masks.InteriorMask),
                Mathf.Clamp01(foothillMask * args.Masks.InteriorMask),
                0f);
        }

        private static void AccumulateRangeHeights(
            in OrogenComposeSampleArgs args,
            ref float height,
            out float orogenMask,
            out float foothillMask)
        {
            orogenMask = 0f;
            foothillMask = 0f;
            var nearest = float.MaxValue;
            var second = float.MaxValue;
            for (var i = 0; i < args.Ranges.Length; i++)
            {
                var composeProfile = args.Profile;
                var rangeEval = EvaluateRange(new EvaluateRangeArgs(
                    args.WorldX,
                    args.WorldZ,
                    args.Ranges[i],
                    args.RidgeNoise,
                    in composeProfile,
                    args.Masks.InteriorMask,
                    args.Masks.FastLandforms));
                height += rangeEval.Height;

                orogenMask = Mathf.Max(
                    orogenMask, Mathf.Max(rangeEval.CoreMask, rangeEval.FoothillMask * 0.85f));
                foothillMask = Mathf.Max(foothillMask, rangeEval.FoothillMask);
                TrackNearestDistances(rangeEval.Dist, ref nearest, ref second);
            }

            height -= EvaluateValleyDeepen(
                args.Profile.StructuralValleyDepthMeters,
                nearest,
                second,
                orogenMask,
                args.Masks.InteriorMask);
        }

        private static void TrackNearestDistances(float dist, ref float nearest, ref float second)
        {
            if (dist < nearest)
            {
                second = nearest;
                nearest = dist;
                return;
            }

            if (dist < second)
                second = dist;
        }

        /// <summary>Legacy entry used by older call sites that only need height.</summary>
        public static float Evaluate(in OrogenSampleArgs args) =>
            Sample(in args).Height;

        public static float EvaluateInteriorMask(
            float worldX,
            float worldZ,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            LandformProfile profile)
        {
            if (profile == null)
                return 0f;

            var compose = LandformComposeParams.Capture(profile, bakeMountainMaskCurve: false);
            return EvaluateInteriorMask(
                worldX,
                worldZ,
                bounds,
                layout,
                compose.InteriorReliefInsetMeters,
                compose.OceanKeepOutMeters,
                compose.EdgeBarrierDepthMeters);
        }

        public static float EvaluateInteriorMask(
            float worldX,
            float worldZ,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            float interiorReliefInsetMeters,
            float oceanKeepOutMeters,
            float edgeBarrierDepthMeters)
        {
            var edgeDist = WorldMapBoundaryUtility.DistanceToRectEdge(worldX, worldZ, bounds);
            var inset = Mathf.Max(40f, interiorReliefInsetMeters);
            // Soft map-edge only — allow full strength once a short distance inland.
            var fromRect = Smooth01((edgeDist - inset * 0.15f) / Mathf.Max(1f, inset * 0.85f));

            WorldMapBoundaryUtility.TryGetNearestEdgeSide(
                worldX,
                worldZ,
                bounds,
                out var side,
                out var nearestEdgeDist);

            if (layout.Get(side) == WorldMapBoundaryKind.Ocean)
            {
                // Suppress only in the wet trench/shelf; beaches and dry shoulder keep orogen.
                var keepOut = oceanKeepOutMeters;
                var oceanFade = Smooth01(
                    (nearestEdgeDist - keepOut * 0.35f) / Mathf.Max(1f, keepOut * 0.65f));
                fromRect = Mathf.Min(fromRect, oceanFade);
            }

            if (layout.North == WorldMapBoundaryKind.Mountains)
            {
                var northDist = bounds.yMax - worldZ;
                var northFade = Smooth01(
                    (northDist - edgeBarrierDepthMeters * 0.2f) /
                    Mathf.Max(1f, edgeBarrierDepthMeters * 0.65f));
                fromRect = Mathf.Min(fromRect, northFade);
            }

            return fromRect;
        }

        public static void SampleRangeMasks(
            float worldX,
            float worldZ,
            MountainRange range,
            out float coreMask,
            out float foothillMask)
        {
            ClosestOnRange(worldX, worldZ, range, out _, out var dist, out _);
            var halfWidth = Mathf.Max(60f, range.WidthMeters * 0.48f);
            var skirt = Mathf.Max(40f, range.FoothillSkirtMeters);
            var outer = halfWidth + skirt;
            var crossCore = 1f - Mathf.Clamp01(dist / halfWidth);
            coreMask = Smooth01(crossCore);
            var skirtT = 1f - Mathf.Clamp01((dist - halfWidth) / Mathf.Max(1f, skirt));
            foothillMask = dist <= halfWidth ? 0f : Smooth01(skirtT);
            if (dist > outer)
            {
                coreMask = 0f;
                foothillMask = 0f;
            }
        }
    }
}
