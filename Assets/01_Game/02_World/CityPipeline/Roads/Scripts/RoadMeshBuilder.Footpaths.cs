using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Footpath strip/band mesh helpers (junction trim, one-sided strips, dual-polyline bands).
    /// </summary>
    public static partial class RoadMeshBuilder
    {
        internal static List<Vector2> TrimPolylineEnds(List<Vector2> pts, float trimMeters)
        {
            if (pts == null || pts.Count < 3 || trimMeters <= 0f) return pts;

            var accumulated = 0f;
            for (var i = 1; i < pts.Count; i++)
            {
                accumulated += Vector2.Distance(pts[i - 1], pts[i]);
                if (accumulated < trimMeters)
                    continue;

                var excess = accumulated - trimMeters;
                var t = 1f - excess / Vector2.Distance(pts[i - 1], pts[i]);
                var cutPt = Vector2.Lerp(pts[i - 1], pts[i], t);
                var result = new List<Vector2>(pts.Count - i + 1) { cutPt };
                for (var j = i; j < pts.Count; j++)
                    result.Add(pts[j]);

                accumulated = 0f;
                for (var j = result.Count - 2; j >= 1; j--)
                {
                    accumulated += Vector2.Distance(result[j + 1], result[j]);
                    if (accumulated < trimMeters)
                        continue;

                    var excessE = accumulated - trimMeters;
                    var tE = excessE / Vector2.Distance(result[j + 1], result[j]);
                    result[j] = Vector2.Lerp(result[j + 1], result[j], tE);
                    result.RemoveRange(j + 1, result.Count - (j + 1));
                    break;
                }

                return result.Count >= 2 ? result : null;
            }

            return null;
        }

        /// <summary>
        ///     Builds a strip on one side of a road centerline. Inner/outer edges stay at fixed
        ///     lateral distance from the centerline so corners cannot fold onto the pavement.
        /// </summary>
        public static Mesh BuildAsymmetricStripMeshWithHeightSampler(
            List<Vector2> centerPolylineXZ,
            float innerOffsetMeters,
            float outerOffsetMeters,
            int sideSign,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile)
        {
            if (centerPolylineXZ == null || centerPolylineXZ.Count < 2 || sampleHeight == null)
                return null;
            if (sideSign == 0)
                return null;

            innerOffsetMeters = Mathf.Max(0f, innerOffsetMeters);
            outerOffsetMeters = Mathf.Max(0f, outerOffsetMeters);
            if (innerOffsetMeters <= outerOffsetMeters)
                outerOffsetMeters = Mathf.Max(innerOffsetMeters + 0.25f, outerOffsetMeters);
            var stripWidth = Mathf.Abs(outerOffsetMeters - innerOffsetMeters);
            var samples = ResamplePolyline(centerPolylineXZ, Mathf.Clamp(stripWidth * 0.35f, 2.5f, 8f));
            if (samples.Count < 2)
                return null;

            return BuildStripMeshWithHeightSampler(
                samples,
                innerOffsetMeters,
                outerOffsetMeters,
                sampleHeight,
                uvWorldUnitsPerTile,
                sideSign);
        }

        internal static List<Vector2> OffsetPolylineTowardPoint(
            IReadOnlyList<Vector2> polyline,
            Vector2 targetPoint,
            float offsetMeters)
        {
            var output = new List<Vector2>(polyline?.Count ?? 0);
            if (polyline == null || polyline.Count == 0)
                return output;

            for (var i = 0; i < polyline.Count; i++)
            {
                var point = polyline[i];
                var toTarget = targetPoint - point;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    output.Add(point);
                    continue;
                }

                output.Add(point + toTarget.normalized * offsetMeters);
            }

            return output;
        }

        internal static Mesh BuildBandMeshBetweenPolylines(
            List<Vector2> outerPolyline,
            List<Vector2> innerPolyline,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile,
            float outerEdgeDrop = 0f)
        {
            if (outerPolyline == null || innerPolyline == null || outerPolyline.Count < 2 ||
                innerPolyline.Count < 2 || sampleHeight == null)
                return null;

            var avgWidth = 0f;
            for (var i = 0; i < outerPolyline.Count; i++)
            {
                var innerIdx = Mathf.Min(i, innerPolyline.Count - 1);
                avgWidth += Vector2.Distance(outerPolyline[i], innerPolyline[innerIdx]);
            }

            avgWidth /= outerPolyline.Count;
            if (avgWidth < 0.25f)
                return null;

            var step = Mathf.Clamp(avgWidth * 0.25f, 0.75f, 2f);
            var outer = ResamplePolyline(outerPolyline, step);
            if (outer.Count < 2)
                return null;

            var inner = ResamplePolyline(innerPolyline, step);
            if (inner.Count < 2)
                return null;

            if (outer.Count != inner.Count)
            {
                var target = Mathf.Max(outer.Count, inner.Count);
                outer = ResamplePolylineUniformCount(outer, target);
                inner = ResamplePolylineUniformCount(inner, target);
            }

            var tile = Mathf.Max(0.25f, uvWorldUnitsPerTile);
            var verts = new List<Vector3>(outer.Count * 2);
            var uvs = new List<Vector2>(outer.Count * 2);
            var tris = new List<int>((outer.Count - 1) * 6);
            var alongMeters = 0f;

            for (var i = 0; i < outer.Count; i++)
            {
                var outerPoint = outer[i];
                var innerPoint = inner[i];
                var outerY = sampleHeight(outerPoint) - outerEdgeDrop;
                var innerY = sampleHeight(innerPoint);

                if (i > 0)
                    alongMeters += Vector2.Distance(outer[i - 1], outerPoint);

                var uvAlong = alongMeters / tile;
                var bandWidth = Vector2.Distance(outerPoint, innerPoint);
                var uvAcrossOuter = bandWidth / tile;

                verts.Add(new Vector3(innerPoint.x, innerY, innerPoint.y));
                verts.Add(new Vector3(outerPoint.x, outerY, outerPoint.y));
                uvs.Add(new Vector2(0f, uvAlong));
                uvs.Add(new Vector2(uvAcrossOuter, uvAlong));
            }

            for (var i = 0; i < outer.Count - 1; i++)
            {
                var i0 = i * 2;
                tris.Add(i0);
                tris.Add(i0 + 1);
                tris.Add(i0 + 2);
                tris.Add(i0 + 1);
                tris.Add(i0 + 3);
                tris.Add(i0 + 2);
            }

            var mesh = new Mesh { name = "FootpathBand" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
