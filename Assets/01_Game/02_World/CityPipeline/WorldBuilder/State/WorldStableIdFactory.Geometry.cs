using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStableIdFactory
    {
        private enum GeometryKind
        {
            None = 0,
            Point = 1,
            Box = 2,
            Building = 3,
            Polyline = 4,
            Polygon = 5
        }

        private static GeometryPayload Point2(Vector2 positionXZ, float yawDegrees)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Point,
                Center = QuantizePoint(positionXZ),
                YawCentidegrees = QuantizeYaw(yawDegrees)
            };
        }

        private static GeometryPayload Box2(Vector2 centerXZ, Vector2 extentsMeters)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Box,
                Center = QuantizePoint(centerXZ),
                Extents = QuantizePoint(extentsMeters)
            };
        }

        private static GeometryPayload OrientedBox2(
            Vector2 centerXZ,
            Vector2 extentsMeters,
            float yawDegrees)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Box,
                Center = QuantizePoint(centerXZ),
                Extents = QuantizePoint(extentsMeters),
                YawCentidegrees = QuantizeYaw(yawDegrees)
            };
        }

        private static GeometryPayload BuildingFootprint(
            Vector2 centerXZ,
            Vector2 extentsMeters,
            float yawDegrees,
            Vector3 scale)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Building,
                Center = QuantizePoint(centerXZ),
                Extents = QuantizePoint(extentsMeters),
                YawCentidegrees = QuantizeYaw(yawDegrees),
                Scale = QuantizeScale(scale)
            };
        }

        private static GeometryPayload Road(IReadOnlyList<Vector2> polylineXZ)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Polyline,
                Points = NormalizeRoad(polylineXZ)
            };
        }

        private static GeometryPayload Polygon(IReadOnlyList<Vector2> polygonXZ)
        {
            return new GeometryPayload
            {
                Kind = GeometryKind.Polygon,
                Points = NormalizePolygon(polygonXZ)
            };
        }

        private static QuantizedPoint2[] NormalizeRoad(IReadOnlyList<Vector2> source)
        {
            var forward = QuantizePoints(source);
            var reversed = ReversedCopy(forward);
            return CompareSequences(reversed, forward) < 0 ? reversed : forward;
        }

        private static QuantizedPoint2[] NormalizePolygon(IReadOnlyList<Vector2> source)
        {
            var points = new List<QuantizedPoint2>(QuantizePoints(source));
            RemoveDuplicateClosingPoint(points);
            if (SignedAreaTwice(points) < 0)
                points.Reverse();
            RotateToSmallestLexicographicSequence(points);
            return points.ToArray();
        }

        private static QuantizedPoint2[] QuantizePoints(IReadOnlyList<Vector2> source)
        {
            var count = source != null ? source.Count : 0;
            var points = new QuantizedPoint2[count];
            for (var i = 0; i < count; i++)
                points[i] = QuantizePoint(source[i]);
            return points;
        }

        private static QuantizedPoint2[] ReversedCopy(IReadOnlyList<QuantizedPoint2> source)
        {
            var copy = new QuantizedPoint2[source.Count];
            for (var i = 0; i < source.Count; i++)
                copy[i] = source[source.Count - 1 - i];
            return copy;
        }

        private static void RemoveDuplicateClosingPoint(List<QuantizedPoint2> points)
        {
            if (points.Count > 1 && points[0].Equals(points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);
        }

        private static long SignedAreaTwice(IReadOnlyList<QuantizedPoint2> points)
        {
            var area = 0L;
            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                area += points[i].X * points[next].Z - points[next].X * points[i].Z;
            }

            return area;
        }

        private static void RotateToSmallestLexicographicSequence(List<QuantizedPoint2> points)
        {
            if (points.Count < 2)
                return;

            var best = 0;
            for (var i = 1; i < points.Count; i++)
            {
                if (CompareRotations(points, i, best) < 0)
                    best = i;
            }

            if (best > 0)
                Rotate(points, best);
        }

        private static int CompareRotations(IReadOnlyList<QuantizedPoint2> points, int leftStart, int rightStart)
        {
            for (var offset = 0; offset < points.Count; offset++)
            {
                var left = points[(leftStart + offset) % points.Count];
                var right = points[(rightStart + offset) % points.Count];
                var compare = ComparePoint(left, right);
                if (compare != 0)
                    return compare;
            }

            return 0;
        }

        private static void Rotate(List<QuantizedPoint2> points, int start)
        {
            var rotated = new QuantizedPoint2[points.Count];
            for (var i = 0; i < points.Count; i++)
                rotated[i] = points[(start + i) % points.Count];
            points.Clear();
            points.AddRange(rotated);
        }

        private static int CompareSequences(
            IReadOnlyList<QuantizedPoint2> left,
            IReadOnlyList<QuantizedPoint2> right)
        {
            var countCompare = left.Count.CompareTo(right.Count);
            var count = Math.Min(left.Count, right.Count);
            for (var i = 0; i < count; i++)
            {
                var compare = ComparePoint(left[i], right[i]);
                if (compare != 0)
                    return compare;
            }

            return countCompare;
        }

        private static int ComparePoint(QuantizedPoint2 left, QuantizedPoint2 right)
        {
            var xCompare = left.X.CompareTo(right.X);
            return xCompare != 0 ? xCompare : left.Z.CompareTo(right.Z);
        }

        private static QuantizedPoint2 QuantizePoint(Vector2 point) =>
            new(QuantizeMillimetres(point.x), QuantizeMillimetres(point.y));

        private static QuantizedScale3 QuantizeScale(Vector3 scale) =>
            new(QuantizeScaleUnit(scale.x), QuantizeScaleUnit(scale.y), QuantizeScaleUnit(scale.z));

        private static long QuantizeMillimetres(float value) =>
            (long)Math.Round(value * 1000d, MidpointRounding.AwayFromZero);

        private static long QuantizeScaleUnit(float value) =>
            (long)Math.Round(value * 10000d, MidpointRounding.AwayFromZero);

        private static int QuantizeYaw(float yawDegrees)
        {
            var normalized = yawDegrees % 360f;
            if (normalized < 0f)
                normalized += 360f;
            return (int)Math.Round(normalized * 100d, MidpointRounding.AwayFromZero);
        }

        private static void AppendGeometrySignature(StringBuilder builder, GeometryPayload geometry)
        {
            builder.Append((int)geometry.Kind).Append('|');
            AppendPointSignature(builder, geometry.Center);
            AppendPointSignature(builder, geometry.Extents);
            builder.Append(geometry.YawCentidegrees).Append('|');
            AppendScaleSignature(builder, geometry.Scale);
            AppendPointListSignature(builder, geometry.Points);
        }

        private static void AppendPointListSignature(
            StringBuilder builder,
            IReadOnlyList<QuantizedPoint2> points)
        {
            var count = points != null ? points.Count : 0;
            builder.Append(count).Append('|');
            for (var i = 0; i < count; i++)
                AppendPointSignature(builder, points[i]);
        }

        private static void AppendPointSignature(StringBuilder builder, QuantizedPoint2 point)
        {
            builder.Append(point.X).Append(',').Append(point.Z).Append('|');
        }

        private static void AppendScaleSignature(StringBuilder builder, QuantizedScale3 scale)
        {
            builder.Append(scale.X).Append(',').Append(scale.Y).Append(',').Append(scale.Z).Append('|');
        }

        private struct GeometryPayload
        {
            public GeometryKind Kind;
            public QuantizedPoint2 Center;
            public QuantizedPoint2 Extents;
            public int YawCentidegrees;
            public QuantizedScale3 Scale;
            public QuantizedPoint2[] Points;
        }

        private readonly struct QuantizedPoint2 : IEquatable<QuantizedPoint2>
        {
            public readonly long X;
            public readonly long Z;

            public QuantizedPoint2(long x, long z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(QuantizedPoint2 other) => X == other.X && Z == other.Z;
        }

        private readonly struct QuantizedScale3
        {
            public readonly long X;
            public readonly long Y;
            public readonly long Z;

            public QuantizedScale3(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
}
