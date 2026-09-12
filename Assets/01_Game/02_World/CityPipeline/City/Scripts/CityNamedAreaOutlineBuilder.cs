using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Builds XZ outlines for city blocks with optional corner fillets that trim the block rect
    ///     to match rounded arterial road corners.
    /// </summary>
    public static class CityNamedAreaOutlineBuilder
    {
        public const int ArcSegments = 8;
        public const float MeshArcSegmentLengthMeters = 1.25f;
        public const int MeshArcSegmentMin = 12;
        public const int MeshArcSegmentMax = 48;

        public static int ResolveArcSegmentCount(
            float arcLengthMeters,
            float segmentLengthMeters = MeshArcSegmentLengthMeters)
        {
            return Mathf.Clamp(
                Mathf.CeilToInt(arcLengthMeters / Mathf.Max(0.5f, segmentLengthMeters)),
                MeshArcSegmentMin,
                MeshArcSegmentMax);
        }

        public static List<Vector2> BuildOutline(
            Rect rect,
            CityBlockCornerMask corners,
            float arterialCornerRadius,
            float inset)
        {
            if (corners == CityBlockCornerMask.None || arterialCornerRadius < 0.5f)
                return RectOutline(rect);

            var filletRadius = ResolveFilletRadius(rect, arterialCornerRadius, inset);
            return BuildRoundedRectOutline(rect, corners, filletRadius, ArcSegments);
        }

        /// <summary>
        ///     Higher-density outline for procedural sidewalk/footpath meshes along filleted corners.
        /// </summary>
        public static List<Vector2> BuildMeshOutline(
            Rect rect,
            CityBlockCornerMask corners,
            float arterialCornerRadius,
            float inset)
        {
            if (corners == CityBlockCornerMask.None || arterialCornerRadius < 0.5f)
                return RectOutline(rect);

            var filletRadius = ResolveFilletRadius(rect, arterialCornerRadius, inset);
            var arcSegments = ResolveArcSegmentCount(Mathf.PI * 0.5f * filletRadius);
            return BuildRoundedRectOutline(rect, corners, filletRadius, arcSegments);
        }

        public static float ResolveFilletRadius(Rect rect, float arterialCornerRadius, float inset)
        {
            var target = Mathf.Max(0.25f, arterialCornerRadius - inset);
            return Mathf.Min(target, rect.width * 0.5f - 0.05f, rect.height * 0.5f - 0.05f);
        }

        public static float ComputePolygonArea(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
                return 0f;

            var area = 0d;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }

            return Mathf.Abs((float)(area * 0.5d));
        }

        public static Vector2 ComputePolygonCentroid(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
                return Vector2.zero;

            if (points.Count < 3)
            {
                var sum = Vector2.zero;
                for (var i = 0; i < points.Count; i++)
                    sum += points[i];
                return sum / points.Count;
            }

            var area = 0d;
            var cx = 0d;
            var cz = 0d;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var cross = (double)a.x * b.y - (double)b.x * a.y;
                area += cross;
                cx += (a.x + b.x) * cross;
                cz += (a.y + b.y) * cross;
            }

            area *= 0.5d;
            if (Mathf.Abs((float)area) < 0.0001f)
                return points[0];

            var inv = 1d / (6d * area);
            return new Vector2((float)(cx * inv), (float)(cz * inv));
        }

        public static List<Vector2> ExpandOutlineStreetFacingEdges(
            IReadOnlyList<Vector2> outline,
            float expandMeters,
            Func<Vector2, Vector2, bool> isSharedEdge)
        {
            var count = outline?.Count ?? 0;
            var result = new List<Vector2>(count);
            if (count < 3 || expandMeters <= 0.0001f || isSharedEdge == null)
            {
                if (outline != null)
                    result.AddRange(outline);
                return result;
            }

            var sharedEdges = new bool[count];
            var outwardOffsets = new Vector2[count];
            var centroid = ComputePolygonCentroid(outline);

            for (var e = 0; e < count; e++)
            {
                var a = outline[e];
                var b = outline[(e + 1) % count];
                sharedEdges[e] = isSharedEdge(a, b);
                outwardOffsets[e] = sharedEdges[e]
                    ? Vector2.zero
                    : ResolveOutwardNormal(a, b, centroid) * expandMeters;
            }

            const float miterLimit = 2.5f;

            for (var i = 0; i < count; i++)
            {
                var prevEdge = (i - 1 + count) % count;
                var currEdge = i;
                var prevShared = sharedEdges[prevEdge];
                var currShared = sharedEdges[currEdge];
                var point = outline[i];

                if (prevShared && currShared)
                {
                    result.Add(point);
                    continue;
                }

                if (!prevShared && !currShared)
                {
                    var prevTangent = (point - outline[prevEdge]).normalized;
                    var currTangent = (outline[(i + 1) % count] - point).normalized;
                    var offsetPrev = point + outwardOffsets[prevEdge];
                    var offsetCurr = point + outwardOffsets[currEdge];

                    if (TryMiterPoint(offsetPrev, prevTangent, offsetCurr, currTangent, point, expandMeters, miterLimit, out var miter))
                        result.Add(miter);
                    else
                        result.Add(offsetPrev);
                    continue;
                }

                result.Add(point + (prevShared ? outwardOffsets[currEdge] : outwardOffsets[prevEdge]));
            }

            for (var i = result.Count - 1; i > 0; i--)
            {
                if ((result[i] - result[i - 1]).sqrMagnitude < 0.0001f)
                    result.RemoveAt(i);
            }

            return result;
        }

        /// <summary>
        ///     Offsets every edge of a closed outline by a uniform signed distance (negative = inset).
        /// </summary>
        public static List<Vector2> OffsetOutlineUniform(IReadOnlyList<Vector2> outline, float signedMeters)
        {
            var count = outline?.Count ?? 0;
            var result = new List<Vector2>(count);
            if (count < 3 || Mathf.Abs(signedMeters) < 0.0001f)
            {
                if (outline != null)
                    result.AddRange(outline);
                return result;
            }

            var centroid = ComputePolygonCentroid(outline);
            var outwardOffsets = new Vector2[count];
            for (var e = 0; e < count; e++)
            {
                var a = outline[e];
                var b = outline[(e + 1) % count];
                outwardOffsets[e] = ResolveOutwardNormal(a, b, centroid) * signedMeters;
            }

            const float miterLimit = 2.5f;
            var expandMeters = signedMeters;

            for (var i = 0; i < count; i++)
            {
                var prevEdge = (i - 1 + count) % count;
                var point = outline[i];
                var prevTangent = (point - outline[prevEdge]).normalized;
                var currTangent = (outline[(i + 1) % count] - point).normalized;
                var offsetPrev = point + outwardOffsets[prevEdge];
                var offsetCurr = point + outwardOffsets[i];

                if (TryMiterPoint(offsetPrev, prevTangent, offsetCurr, currTangent, point, expandMeters, miterLimit,
                        out var miter))
                    result.Add(miter);
                else
                    result.Add(offsetPrev);
            }

            for (var i = result.Count - 1; i > 0; i--)
            {
                if ((result[i] - result[i - 1]).sqrMagnitude < 0.0001f)
                    result.RemoveAt(i);
            }

            return result;
        }

        public static List<Vector2> InsetOutlineUniform(IReadOnlyList<Vector2> outline, float insetMeters) =>
            OffsetOutlineUniform(outline, -Mathf.Abs(insetMeters));

        public static Mesh CreateFlatMesh(IReadOnlyList<Vector2> outline, Vector3 parentWorldPosition, float localYOffset)
        {
            var mesh = new Mesh { name = "DistrictFillMesh" };
            if (outline == null || outline.Count < 3)
                return mesh;

            var centroid = ComputePolygonCentroid(outline);
            var vertexCount = outline.Count + 1;
            var vertices = new Vector3[vertexCount];
            vertices[0] = ToLocal(centroid, parentWorldPosition, localYOffset);
            for (var i = 0; i < outline.Count; i++)
                vertices[i + 1] = ToLocal(outline[i], parentWorldPosition, localYOffset);

            var triangles = new int[outline.Count * 3];
            for (var i = 0; i < outline.Count; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == outline.Count - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToLocal(Vector2 worldXZ, Vector3 parentWorldPosition, float localYOffset)
        {
            return new Vector3(
                worldXZ.x - parentWorldPosition.x,
                localYOffset,
                worldXZ.y - parentWorldPosition.z);
        }

        private static List<Vector2> BuildRoundedRectOutline(
            Rect rect,
            CityBlockCornerMask corners,
            float filletRadius,
            int arcSegments)
        {
            var x0 = rect.xMin;
            var x1 = rect.xMax;
            var z0 = rect.yMin;
            var z1 = rect.yMax;
            var points = new List<Vector2>(32);

            // Clockwise from top-left (viewed from above).
            if (Has(corners, CityBlockCornerMask.TopLeft))
                AddPoint(points, new Vector2(x0, z1 - filletRadius));
            else
                AddPoint(points, new Vector2(x0, z1));

            if (Has(corners, CityBlockCornerMask.TopLeft))
                AppendArc(points, x0 + filletRadius, z1 - filletRadius, filletRadius, Mathf.PI, Mathf.PI * 0.5f, arcSegments);

            if (Has(corners, CityBlockCornerMask.TopRight))
                AddPoint(points, new Vector2(x1 - filletRadius, z1));
            else
                AddPoint(points, new Vector2(x1, z1));

            if (Has(corners, CityBlockCornerMask.TopRight))
                AppendArc(points, x1 - filletRadius, z1 - filletRadius, filletRadius, Mathf.PI * 0.5f, 0f, arcSegments);

            if (Has(corners, CityBlockCornerMask.BottomRight))
                AddPoint(points, new Vector2(x1, z0 + filletRadius));
            else
                AddPoint(points, new Vector2(x1, z0));

            if (Has(corners, CityBlockCornerMask.BottomRight))
                AppendArc(points, x1 - filletRadius, z0 + filletRadius, filletRadius, 0f, -Mathf.PI * 0.5f, arcSegments);

            if (Has(corners, CityBlockCornerMask.BottomLeft))
                AddPoint(points, new Vector2(x0 + filletRadius, z0));
            else
                AddPoint(points, new Vector2(x0, z0));

            if (Has(corners, CityBlockCornerMask.BottomLeft))
                AppendArc(points, x0 + filletRadius, z0 + filletRadius, filletRadius, -Mathf.PI * 0.5f, -Mathf.PI, arcSegments);

            return points;
        }

        private static Vector2 ResolveOutwardNormal(Vector2 edgeStart, Vector2 edgeEnd, Vector2 centroid)
        {
            var tangent = (edgeEnd - edgeStart).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;

            var left = new Vector2(-tangent.y, tangent.x);
            var right = new Vector2(tangent.y, -tangent.x);
            var mid = (edgeStart + edgeEnd) * 0.5f;
            var toCentroid = centroid - mid;
            if (toCentroid.sqrMagnitude < 0.0001f)
                return -left;

            var inward = Vector2.Dot(left, toCentroid) >= Vector2.Dot(right, toCentroid) ? left : right;
            return -inward;
        }

        private static bool TryMiterPoint(
            Vector2 offsetPrev,
            Vector2 prevTangent,
            Vector2 offsetCurr,
            Vector2 currTangent,
            Vector2 corner,
            float expandMeters,
            float miterLimit,
            out Vector2 miterPoint)
        {
            miterPoint = corner;
            if (prevTangent.sqrMagnitude < 0.0001f || currTangent.sqrMagnitude < 0.0001f)
                return false;

            if (Vector2.Dot(prevTangent, currTangent) > 0.999f)
            {
                miterPoint = (offsetPrev + offsetCurr) * 0.5f;
                return true;
            }

            if (!LineIntersection2D(offsetPrev, prevTangent, offsetCurr, currTangent, out miterPoint))
                return false;

            var miterLen = (miterPoint - corner).magnitude;
            var maxMiter = Mathf.Abs(expandMeters) * miterLimit;
            if (miterLen > maxMiter)
            {
                miterPoint = offsetPrev;
                return true;
            }

            return true;
        }

        private static bool LineIntersection2D(
            Vector2 originA,
            Vector2 directionA,
            Vector2 originB,
            Vector2 directionB,
            out Vector2 intersection)
        {
            intersection = Vector2.zero;

            var cross = directionA.x * directionB.y - directionA.y * directionB.x;
            if (Mathf.Abs(cross) < 0.000001f)
                return false;

            var delta = originB - originA;
            var t = (delta.x * directionB.y - delta.y * directionB.x) / cross;
            intersection = originA + directionA * t;
            return true;
        }

        private static List<Vector2> RectOutline(Rect rect)
        {
            return new List<Vector2>(4)
            {
                new(rect.xMin, rect.yMax),
                new(rect.xMax, rect.yMax),
                new(rect.xMax, rect.yMin),
                new(rect.xMin, rect.yMin)
            };
        }

        private static bool Has(CityBlockCornerMask mask, CityBlockCornerMask flag)
        {
            return (mask & flag) != 0;
        }

        private static void AddPoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.0001f)
                points.Add(point);
        }

        private static void AppendArc(
            List<Vector2> points,
            float centerX,
            float centerZ,
            float radius,
            float startAngle,
            float endAngle,
            int arcSegments)
        {
            radius = Mathf.Max(0.25f, radius);
            arcSegments = Mathf.Max(2, arcSegments);
            for (var i = 1; i <= arcSegments; i++)
            {
                var t = i / (float)arcSegments;
                var angle = Mathf.Lerp(startAngle, endAngle, t);
                AddPoint(points, new Vector2(
                    centerX + Mathf.Cos(angle) * radius,
                    centerZ + Mathf.Sin(angle) * radius));
            }
        }

        private static void AppendArc(
            List<Vector2> points,
            float centerX,
            float centerZ,
            float radius,
            float startAngle,
            float endAngle)
        {
            AppendArc(points, centerX, centerZ, radius, startAngle, endAngle, ArcSegments);
        }
    }
}
