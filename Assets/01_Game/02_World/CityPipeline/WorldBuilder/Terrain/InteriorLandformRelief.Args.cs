using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parameter bundles for <see cref="InteriorLandformRelief"/> (Sonar S107).</summary>
    public static partial class InteriorLandformRelief
    {
        public readonly struct MountainRangeSpine
        {
            public readonly Vector2 Start;
            public readonly Vector2 Mid;
            public readonly Vector2 End;

            public MountainRangeSpine(Vector2 start, Vector2 mid, Vector2 end)
            {
                Start = start;
                Mid = mid;
                End = end;
            }
        }

        public readonly struct MountainRangeMetrics
        {
            public readonly float WidthMeters;
            public readonly float PeakMeters;
            public readonly float Ruggedness;
            public readonly float PassAlong01;
            public readonly float PassWidth01;
            public readonly float FoothillSkirtMeters;

            public MountainRangeMetrics(
                float widthMeters,
                float peakMeters,
                float ruggedness,
                float passAlong01,
                float passWidth01,
                float foothillSkirtMeters)
            {
                WidthMeters = widthMeters;
                PeakMeters = peakMeters;
                Ruggedness = ruggedness;
                PassAlong01 = passAlong01;
                PassWidth01 = passWidth01;
                FoothillSkirtMeters = foothillSkirtMeters;
            }
        }

        public readonly struct MountainRangeArgs
        {
            public readonly MountainRangeSpine Spine;
            public readonly MountainRangeMetrics Metrics;

            public MountainRangeArgs(in MountainRangeSpine spine, in MountainRangeMetrics metrics)
            {
                Spine = spine;
                Metrics = metrics;
            }
        }

        public readonly struct OrogenWorldQuery
        {
            public readonly float WorldX;
            public readonly float WorldZ;
            public readonly Rect Bounds;
            public readonly WorldMapBoundaryLayout Layout;
            public readonly float LandMask;
            public readonly bool FastLandforms;

            public OrogenWorldQuery(
                float worldX,
                float worldZ,
                Rect bounds,
                WorldMapBoundaryLayout layout,
                float landMask,
                bool fastLandforms)
            {
                WorldX = worldX;
                WorldZ = worldZ;
                Bounds = bounds;
                Layout = layout;
                LandMask = landMask;
                FastLandforms = fastLandforms;
            }
        }

        public readonly struct OrogenSampleArgs
        {
            public readonly OrogenWorldQuery Query;
            public readonly LandformProfile Profile;
            public readonly MountainRange[] Ranges;
            public readonly DeterministicNoise2D RidgeNoise;
            public readonly DeterministicNoise2D RollingNoise;

            public OrogenSampleArgs(
                in OrogenWorldQuery query,
                LandformProfile profile,
                MountainRange[] ranges,
                DeterministicNoise2D ridgeNoise,
                DeterministicNoise2D rollingNoise)
            {
                Query = query;
                Profile = profile;
                Ranges = ranges;
                RidgeNoise = ridgeNoise;
                RollingNoise = rollingNoise;
            }
        }

        public readonly struct OrogenComposeMasks
        {
            public readonly float LandMask;
            public readonly float InteriorMask;
            public readonly bool FastLandforms;

            public OrogenComposeMasks(float landMask, float interiorMask, bool fastLandforms)
            {
                LandMask = landMask;
                InteriorMask = interiorMask;
                FastLandforms = fastLandforms;
            }
        }

        public readonly struct OrogenComposeSampleArgs
        {
            public readonly float WorldX;
            public readonly float WorldZ;
            public readonly LandformComposeParams Profile;
            public readonly MountainRange[] Ranges;
            public readonly DeterministicNoise2D RidgeNoise;
            public readonly DeterministicNoise2D RollingNoise;
            public readonly OrogenComposeMasks Masks;

            public OrogenComposeSampleArgs(
                float worldX,
                float worldZ,
                in LandformComposeParams profile,
                MountainRange[] ranges,
                DeterministicNoise2D ridgeNoise,
                DeterministicNoise2D rollingNoise,
                in OrogenComposeMasks masks)
            {
                WorldX = worldX;
                WorldZ = worldZ;
                Profile = profile;
                Ranges = ranges;
                RidgeNoise = ridgeNoise;
                RollingNoise = rollingNoise;
                Masks = masks;
            }
        }

        private readonly struct EvaluateRangeArgs
        {
            public readonly float WorldX;
            public readonly float WorldZ;
            public readonly MountainRange Range;
            public readonly DeterministicNoise2D RidgeNoise;
            public readonly LandformComposeParams Profile;
            public readonly float InteriorMask;
            public readonly bool FastLandforms;

            public EvaluateRangeArgs(
                float worldX,
                float worldZ,
                MountainRange range,
                DeterministicNoise2D ridgeNoise,
                in LandformComposeParams profile,
                float interiorMask,
                bool fastLandforms)
            {
                WorldX = worldX;
                WorldZ = worldZ;
                Range = range;
                RidgeNoise = ridgeNoise;
                Profile = profile;
                InteriorMask = interiorMask;
                FastLandforms = fastLandforms;
            }
        }

        private readonly struct CoreHeightPosition
        {
            public readonly float Along01;
            public readonly float AlongMeters;
            public readonly float Dist;
            public readonly float RidgeScale;

            public CoreHeightPosition(float along01, float alongMeters, float dist, float ridgeScale)
            {
                Along01 = along01;
                AlongMeters = alongMeters;
                Dist = dist;
                RidgeScale = ridgeScale;
            }
        }

        private readonly struct CoreHeightShape
        {
            public readonly float AlongPeak;
            public readonly float CrossPeak;
            public readonly float EndFade;
            public readonly float Rugged;

            public CoreHeightShape(float alongPeak, float crossPeak, float endFade, float rugged)
            {
                AlongPeak = alongPeak;
                CrossPeak = crossPeak;
                EndFade = endFade;
                Rugged = rugged;
            }
        }

        private readonly struct CoreHeightSample
        {
            public readonly CoreHeightPosition Position;
            public readonly CoreHeightShape Shape;

            public CoreHeightSample(in CoreHeightPosition position, in CoreHeightShape shape)
            {
                Position = position;
                Shape = shape;
            }
        }

        private struct RangeEvalResult
        {
            public float Height;
            public float CoreMask;
            public float FoothillMask;
            public float Dist;
        }
    }
}
