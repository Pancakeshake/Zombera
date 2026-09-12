using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Mouth placement refinements reverse-engineered from capsule markers:
    ///     normal portals sit on the daylight face near natural surface. City-side
    ///     portals follow the highway outward until the terrain settles to pad level.
    /// </summary>
    public static partial class MountainTunnelScanner
    {
        /// <summary>How far below natural surface the portal roadbed sits into the face.</summary>
        private const float MouthSurfaceSinkMeters = 2f;
        private const float PadSurfaceToleranceMeters = 0.5f;
        private const float PadMouthSearchStepMeters = 1f;

        private static void RefineCandidateMouths(in ScanContext ctx, ref TunnelCandidate candidate)
        {
            RefineOneMouth(ctx, ref candidate.Entry, ref candidate.EntryY, towardStart: true);
            RefineOneMouth(ctx, ref candidate.Exit, ref candidate.ExitY, towardStart: false);
            candidate.SpanLength = Mathf.Max(
                candidate.SpanLength,
                Vector2.Distance(candidate.Entry, candidate.Exit));
        }

        private static void RefineOneMouth(
            in ScanContext ctx,
            ref Vector2 mouthXZ,
            ref float mouthY,
            bool towardStart)
        {
            if (RawCoverAt(ctx, mouthXZ) > ctx.CoverThreshold + MouthSurfaceSinkMeters &&
                TryFindSettledPadMouth(ctx, mouthXZ, towardStart, out var padMouth, out var padHeight))
            {
                mouthXZ = padMouth;
                mouthY = padHeight;
                return;
            }

            mouthXZ = ResolveDaylightMouth(ctx, mouthXZ, towardStart);
            mouthY = ResolveMouthWorldY(ctx.Landforms, ctx.BedProfile, mouthXZ);
        }

        private static Vector2 ResolveDaylightMouth(
            in ScanContext ctx,
            Vector2 mouth,
            bool towardStart) =>
            PullIntoDaylight(ctx, mouth, towardStart);

        private static bool TryFindSettledPadMouth(
            in ScanContext ctx,
            Vector2 mouth,
            bool towardStart,
            out Vector2 result,
            out float padHeight)
        {
            result = mouth;
            padHeight = 0f;
            var points = ctx.Road?.pointsXZ;
            if (points == null || points.Count < 2 || ctx.Pads == null || ctx.Pads.Count == 0)
                return false;
            if (!TryProjectToPolyline(points, mouth, out var segmentIndex, out var projected))
                return false;

            var maxDistance = DistanceToPolylineEnd(points, segmentIndex, projected, towardStart);
            for (var distance = 0f; distance <= maxDistance; distance += PadMouthSearchStepMeters)
            {
                var sample = WalkAlongPolyline(points, segmentIndex, projected, distance, towardStart);
                if (!TryFindContainingPad(ctx.Pads, sample, out var pad))
                    continue;

                var surfaceY = SampleVisibleSurfaceY(ctx.Landforms, sample);
                if (!IsSettledPadSurface(surfaceY, pad.TargetHeightWorldY))
                    continue;

                result = sample;
                padHeight = pad.TargetHeightWorldY;
                return true;
            }

            return false;
        }

        private static bool TryFindContainingPad(
            IReadOnlyList<CityFlattenPad> pads,
            Vector2 point,
            out CityFlattenPad result)
        {
            result = null;
            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null || !pad.OuterBoundsXZ.Contains(point))
                    continue;
                result = pad;
                return true;
            }

            return false;
        }

        private static bool IsSettledPadSurface(float surfaceY, float padY) =>
            Mathf.Abs(surfaceY - padY) <= PadSurfaceToleranceMeters;

        private static float RawCoverAt(in ScanContext ctx, Vector2 point) =>
            SampleVisibleSurfaceY(ctx.Landforms, point) -
            ctx.BedProfile.SampleHeightAlongPath(point);

        private static float ResolveMouthWorldY(
            LandformField landforms,
            PolylineHeightProfile bedProfile,
            Vector2 mouthXZ)
        {
            var bedY = bedProfile.SampleHeightAlongPath(mouthXZ);
            var naturalY = SampleVisibleSurfaceY(landforms, mouthXZ);
            var surfaceY = naturalY - MouthSurfaceSinkMeters;
            // Capsule lesson: never bury the portal under deep cover when bed underestimates.
            return Mathf.Max(bedY, surfaceY);
        }

        private static float SampleVisibleSurfaceY(LandformField landforms, Vector2 point)
        {
            if (CityTerrainFootprintFlattener.TrySampleTerrainHeightmap(point, out var heightmapY))
                return heightmapY;
            return LandformFieldSampling.SampleBilinear(landforms, point.x, point.y);
        }

        private static Vector2 PullIntoDaylight(in ScanContext ctx, Vector2 mouth, bool towardStart)
        {
            var points = ctx.Road?.pointsXZ;
            if (points == null || points.Count < 2 || ctx.Daylight < 1f)
                return mouth;
            if (!TryProjectToPolyline(points, mouth, out var segmentIndex, out var projected))
                return mouth;

            return WalkAlongPolyline(points, segmentIndex, projected, ctx.Daylight, towardStart);
        }

        private static Vector2 WalkAlongPolyline(
            IReadOnlyList<Vector2> points,
            int segmentIndex,
            Vector2 start,
            float distance,
            bool towardStart)
        {
            var remaining = Mathf.Max(0f, distance);
            var cursor = start;
            var vertexIndex = towardStart ? segmentIndex : segmentIndex + 1;
            var step = towardStart ? -1 : 1;

            while (vertexIndex >= 0 && vertexIndex < points.Count)
            {
                var target = points[vertexIndex];
                var segmentLength = Vector2.Distance(cursor, target);
                if (segmentLength < 0.01f)
                {
                    cursor = target;
                    vertexIndex += step;
                    continue;
                }

                if (remaining <= segmentLength)
                    return Vector2.Lerp(cursor, target, remaining / segmentLength);

                remaining -= segmentLength;
                cursor = target;
                vertexIndex += step;
            }

            return cursor;
        }

        private static float DistanceToPolylineEnd(
            IReadOnlyList<Vector2> points,
            int segmentIndex,
            Vector2 start,
            bool towardStart)
        {
            var distance = 0f;
            var cursor = start;
            var vertexIndex = towardStart ? segmentIndex : segmentIndex + 1;
            var step = towardStart ? -1 : 1;
            while (vertexIndex >= 0 && vertexIndex < points.Count)
            {
                distance += Vector2.Distance(cursor, points[vertexIndex]);
                cursor = points[vertexIndex];
                vertexIndex += step;
            }

            return distance;
        }

        private static bool TryProjectToPolyline(
            IReadOnlyList<Vector2> points,
            Vector2 target,
            out int segmentIndex,
            out Vector2 projected)
        {
            segmentIndex = -1;
            projected = target;
            var bestDistanceSq = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var delta = points[i] - a;
                var lengthSq = delta.sqrMagnitude;
                if (lengthSq < 0.0001f)
                    continue;

                var t = Mathf.Clamp01(Vector2.Dot(target - a, delta) / lengthSq);
                var candidate = a + delta * t;
                var distanceSq = (candidate - target).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;
                bestDistanceSq = distanceSq;
                segmentIndex = i - 1;
                projected = candidate;
            }

            return segmentIndex >= 0;
        }
    }
}
