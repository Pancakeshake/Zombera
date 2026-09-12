using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Range SDF evaluation, valley deepen, and geometry helpers for interior orogens.</summary>
    public static partial class InteriorLandformRelief
    {
        private static RangeEvalResult EvaluateRange(in EvaluateRangeArgs args)
        {
            ClosestOnRange(
                args.WorldX, args.WorldZ, args.Range, out var along01, out var dist, out var alongMeters);
            var halfWidth = Mathf.Max(60f, args.Range.WidthMeters * 0.48f);
            var skirt = Mathf.Max(40f, args.Range.FoothillSkirtMeters);
            // Max width jitter is 0.82 + 0.36 = 1.18; cull before octave noise when clearly outside.
            var maxOuter = halfWidth * 1.18f + skirt;
            if (dist > maxOuter)
                return new RangeEvalResult { Dist = dist };

            var widthNoiseScale = Mathf.Max(80f, args.Range.WidthMeters * 0.18f);
            var widthJitter = 0.82f + args.RidgeNoise.Fbm(alongMeters / widthNoiseScale, 0.17f, 2) * 0.36f;
            halfWidth *= widthJitter;

            var sharpness = Mathf.Max(1.2f, args.Profile.CrestSharpness);
            var crossT = 1f - Mathf.Clamp01(dist / halfWidth);
            var crossPeak = Mathf.Pow(Mathf.Max(0f, crossT), sharpness);

            var skirtT = 1f - Mathf.Clamp01((dist - halfWidth) / Mathf.Max(1f, skirt));
            var foothillMask = dist <= halfWidth ? 0f : Smooth01(skirtT);
            var coreMask = Smooth01(Mathf.Pow(
                Mathf.Max(0f, crossT), Mathf.Lerp(1f, 1.65f, (sharpness - 1.2f) / 2.8f)));

            var endFade = SoftEndFade(along01);
            var saddle = EvaluateSaddle(
                along01, args.Range.PassAlong01, args.Range.PassWidth01, args.Profile.SaddleDepth);
            var alongPeak = endFade * saddle;
            var ridgeScale = Mathf.Max(80f, args.Range.WidthMeters * 0.1f);
            var rugged = Mathf.Lerp(0.65f, 1f, args.Range.Ruggedness);

            var coreHeight = BuildCoreHeight(
                in args,
                new CoreHeightSample(
                    new CoreHeightPosition(along01, alongMeters, dist, ridgeScale),
                    new CoreHeightShape(alongPeak, crossPeak, endFade, rugged)),
                ref coreMask);

            var foothillHeight = EvaluateFoothillHeight(
                in args, foothillMask, endFade, rugged, ridgeScale);

            return new RangeEvalResult
            {
                Height = (coreHeight + foothillHeight) * args.InteriorMask,
                CoreMask = coreMask,
                FoothillMask = foothillMask,
                Dist = dist
            };
        }

        private static float BuildCoreHeight(
            in EvaluateRangeArgs args,
            in CoreHeightSample sample,
            ref float coreMask)
        {
            var pos = sample.Position;
            var shape = sample.Shape;
            var ridgeOctaves = args.FastLandforms ? 3 : 5;
            var fineOctaves = args.FastLandforms ? 2 : 3;
            var ridged = args.RidgeNoise.Ridged(
                pos.AlongMeters / pos.RidgeScale, pos.Dist / (pos.RidgeScale * 0.38f), ridgeOctaves);
            var ridgedFine = args.RidgeNoise.Ridged(
                pos.AlongMeters / (pos.RidgeScale * 0.55f),
                pos.Dist / (pos.RidgeScale * 0.28f),
                fineOctaves);
            var macro = args.RidgeNoise.Fbm(
                pos.AlongMeters / (pos.RidgeScale * 2.5f), pos.Dist / pos.RidgeScale, 2);

            var coreHeight = args.Range.PeakMeters * shape.Rugged * shape.AlongPeak * shape.CrossPeak;
            coreHeight *= 0.3f + ridged * 0.5f + ridgedFine * 0.2f;
            coreHeight += (macro - 0.5f) * coreHeight * 0.18f;

            // Flatten a corridor through the saddle so highways can cross at playable grades.
            var passCorridor = EvaluatePassCorridor(
                pos.Along01, pos.Dist, args.Range, args.Profile.PassCorridorHalfWidthMeters);
            if (passCorridor <= 0.01f)
                return coreHeight;

            var corridorFloor = args.Range.PeakMeters * shape.Rugged * 0.06f * shape.EndFade;
            coreHeight = Mathf.Lerp(coreHeight, corridorFloor, Mathf.Clamp01(passCorridor * 1.15f));
            coreMask = Mathf.Lerp(coreMask, 0.08f, passCorridor * 0.9f);
            return coreHeight;
        }

        private static float EvaluateFoothillHeight(
            in EvaluateRangeArgs args,
            float foothillMask,
            float endFade,
            float rugged,
            float ridgeScale)
        {
            if (foothillMask <= 0.001f)
                return 0f;

            // Cubic ramp: short soft toe, then steep into the core (sharp mountain start).
            var ramp = foothillMask * foothillMask * foothillMask;
            var foothillHeight = args.Range.PeakMeters * 0.12f * rugged * endFade * ramp;
            foothillHeight *= 0.55f +
                              args.RidgeNoise.Fbm(
                                  args.WorldX / ridgeScale, args.WorldZ / ridgeScale, 2) * 0.45f;
            return foothillHeight;
        }

        private static float EvaluateValleyDeepen(
            float structuralValleyDepthMeters,
            float nearestDist,
            float secondDist,
            float orogenMask,
            float interiorMask)
        {
            var depth = structuralValleyDepthMeters;
            if (depth <= 0.01f || orogenMask > 0.35f || interiorMask < 0.2f)
                return 0f;
            if (secondDist >= float.MaxValue * 0.5f)
                return 0f;

            var gap = nearestDist + secondDist;
            if (gap < 200f || gap > 4200f)
                return 0f;

            var balance = 1f - Mathf.Abs(nearestDist - secondDist) / Mathf.Max(1f, gap);
            balance = Mathf.Clamp01(balance);
            var midGap = Smooth01((gap - 350f) / 900f) * (1f - Smooth01((gap - 2800f) / 1200f));
            var awayFromCore = 1f - Mathf.Clamp01(orogenMask / 0.35f);
            return depth * balance * midGap * awayFromCore * interiorMask;
        }

        private static void ClosestOnRange(
            float worldX,
            float worldZ,
            MountainRange range,
            out float along01,
            out float dist,
            out float alongMeters)
        {
            var point = new Vector2(worldX, worldZ);
            ClosestOnSegment(point, range.Start, range.Mid, out var d0, out var t0, out var len0);
            ClosestOnSegment(point, range.Mid, range.End, out var d1, out var t1, out var len1);

            var total = Mathf.Max(1f, len0 + len1);
            if (d0 <= d1)
            {
                dist = d0;
                alongMeters = t0 * len0;
                along01 = alongMeters / total;
            }
            else
            {
                dist = d1;
                alongMeters = len0 + t1 * len1;
                along01 = alongMeters / total;
            }
        }

        private static void ClosestOnSegment(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float dist,
            out float t,
            out float length)
        {
            var ab = b - a;
            length = ab.magnitude;
            if (length < 1f)
            {
                dist = Vector2.Distance(point, a);
                t = 0f;
                length = 1f;
                return;
            }

            t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / (length * length));
            var closest = a + ab * t;
            dist = Vector2.Distance(point, closest);
        }

        private static Vector2 EvaluateBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float SoftEndFade(float along01)
        {
            along01 = Mathf.Clamp01(along01);
            // Softer termini so spines keep height near ends (less mid-map condensation).
            var fadeIn = Smooth01(along01 / 0.10f);
            var fadeOut = Smooth01((1f - along01) / 0.10f);
            return Mathf.Max(0.35f, fadeIn * fadeOut);
        }

        private static float EvaluateSaddle(float along01, float passAlong, float passWidth, float saddleDepth)
        {
            var w = Mathf.Max(0.04f, passWidth);
            var x = (along01 - passAlong) / w;
            var gauss = Mathf.Exp(-x * x);
            return Mathf.Clamp01(1f - Mathf.Clamp01(saddleDepth) * gauss);
        }

        private static float EvaluatePassCorridor(
            float along01,
            float dist,
            MountainRange range,
            float corridorHalfWidthMeters)
        {
            var alongW = Mathf.Max(0.04f, range.PassWidth01 * 1.35f);
            var alongT = 1f - Mathf.Clamp01(Mathf.Abs(along01 - range.PassAlong01) / alongW);
            alongT = alongT * alongT * (3f - 2f * alongT);
            var half = Mathf.Max(40f, corridorHalfWidthMeters);
            var crossT = 1f - Mathf.Clamp01(dist / half);
            crossT = crossT * crossT * (3f - 2f * crossT);
            return alongT * crossT;
        }

        private static void ShrinkAwayFromOcean(
            ref Rect inner,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            float margin)
        {
            margin = Mathf.Max(0f, margin);
            if (layout.West == WorldMapBoundaryKind.Ocean)
                inner.xMin = Mathf.Max(inner.xMin, bounds.xMin + margin);
            if (layout.East == WorldMapBoundaryKind.Ocean)
                inner.xMax = Mathf.Min(inner.xMax, bounds.xMax - margin);
            if (layout.South == WorldMapBoundaryKind.Ocean)
                inner.yMin = Mathf.Max(inner.yMin, bounds.yMin + margin);
            if (layout.North == WorldMapBoundaryKind.Ocean)
                inner.yMax = Mathf.Min(inner.yMax, bounds.yMax - margin);

            inner.width = Mathf.Max(0f, inner.xMax - inner.xMin);
            inner.height = Mathf.Max(0f, inner.yMax - inner.yMin);
        }

        /// <summary>
        /// Wet coast keep-out (trench + shelf). Beach band is intentionally excluded so
        /// orogens may terminate at / rise from beaches.
        /// </summary>
        private static float ResolveOceanKeepOutMeters(LandformProfile profile)
        {
            if (profile == null)
                return 120f;

            var wet = profile.OceanOffshoreWidthMeters + profile.OceanShoreShelfWidthMeters;
            return Mathf.Clamp(wet, 60f, 400f);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
