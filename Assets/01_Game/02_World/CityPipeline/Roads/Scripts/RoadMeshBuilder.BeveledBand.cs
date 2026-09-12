using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Beveled sidewalk/curb band meshes between two polylines (inner road edge, outer lot edge).
    /// </summary>
    public static partial class RoadMeshBuilder
    {
        private const int BeveledBandVerticesPerStation = 4;

        internal static Mesh BuildBeveledBandMeshBetweenPolylines(
            List<Vector2> outerPolyline,
            List<Vector2> innerPolyline,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile,
            float bevelWidthMeters,
            float topLiftMeters,
            float outerEdgeDropMeters)
        {
            if (!TryPrepareMatchedBandPolylines(outerPolyline, innerPolyline, out var outer, out var inner, out var avgWidth))
                return null;
            if (sampleHeight == null)
                return null;

            var bevel = Mathf.Clamp(bevelWidthMeters, 0.05f, avgWidth * 0.45f);
            var tile = Mathf.Max(0.25f, uvWorldUnitsPerTile);
            var stationCount = outer.Count;
            var verts = new List<Vector3>(stationCount * BeveledBandVerticesPerStation);
            var uvs = new List<Vector2>(stationCount * BeveledBandVerticesPerStation);
            var tris = new List<int>((stationCount - 1) * 18);
            var alongMeters = 0f;

            for (var i = 0; i < stationCount; i++)
            {
                var innerPoint = inner[i];
                var outerPoint = outer[i];
                var delta = outerPoint - innerPoint;
                var bandWidth = Mathf.Max(delta.magnitude, 0.001f);
                var across = delta.sqrMagnitude > 0.000001f ? delta / bandWidth : Vector2.right;
                var stationBevel = Mathf.Min(bevel, bandWidth * 0.45f);
                var innerTopPoint = innerPoint + across * stationBevel;
                var outerTopPoint = outerPoint - across * stationBevel;
                var innerBaseY = sampleHeight(innerPoint);
                var innerTopY = sampleHeight(innerTopPoint) + topLiftMeters;
                var outerTopY = sampleHeight(outerTopPoint) + topLiftMeters;
                var outerBaseY = sampleHeight(outerPoint) + topLiftMeters - outerEdgeDropMeters;

                if (i > 0)
                    alongMeters += Vector2.Distance(outer[i - 1], outerPoint);

                var uvAlong = alongMeters / tile;
                var uvInnerTop = stationBevel / tile;
                var uvOuterTop = (bandWidth - stationBevel) / tile;
                var uvOuterBase = bandWidth / tile;

                verts.Add(new Vector3(innerPoint.x, innerBaseY, innerPoint.y));
                verts.Add(new Vector3(innerTopPoint.x, innerTopY, innerTopPoint.y));
                verts.Add(new Vector3(outerTopPoint.x, outerTopY, outerTopPoint.y));
                verts.Add(new Vector3(outerPoint.x, outerBaseY, outerPoint.y));

                uvs.Add(new Vector2(0f, uvAlong));
                uvs.Add(new Vector2(uvInnerTop, uvAlong));
                uvs.Add(new Vector2(uvOuterTop, uvAlong));
                uvs.Add(new Vector2(uvOuterBase, uvAlong));
            }

            if (verts.Count < BeveledBandVerticesPerStation * 2)
                return null;

            var ringCount = verts.Count / BeveledBandVerticesPerStation;
            for (var i = 0; i < ringCount - 1; i++)
            {
                var i0 = i * BeveledBandVerticesPerStation;
                var i1 = (i + 1) * BeveledBandVerticesPerStation;
                AddQuad(tris, i0, i0 + 1, i1 + 1, i1);
                AddQuad(tris, i0 + 1, i0 + 2, i1 + 2, i1 + 1);
                AddQuad(tris, i0 + 2, i0 + 3, i1 + 3, i1 + 2);
            }

            var mesh = new Mesh { name = "BeveledSidewalkBand" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool TryPrepareMatchedBandPolylines(
            List<Vector2> outerPolyline,
            List<Vector2> innerPolyline,
            out List<Vector2> outer,
            out List<Vector2> inner,
            out float avgWidth)
        {
            outer = null;
            inner = null;
            avgWidth = 0f;
            if (outerPolyline == null || innerPolyline == null || outerPolyline.Count < 2 ||
                innerPolyline.Count < 2)
                return false;

            for (var i = 0; i < outerPolyline.Count; i++)
            {
                var innerIdx = Mathf.Min(i, innerPolyline.Count - 1);
                avgWidth += Vector2.Distance(outerPolyline[i], innerPolyline[innerIdx]);
            }

            avgWidth /= outerPolyline.Count;
            if (avgWidth < 0.25f)
                return false;

            var step = Mathf.Clamp(avgWidth * 0.25f, 0.75f, 2f);
            outer = ResamplePolyline(outerPolyline, step);
            inner = ResamplePolyline(innerPolyline, step);
            if (outer.Count < 2 || inner.Count < 2)
                return false;

            if (outer.Count != inner.Count)
            {
                var target = Mathf.Max(outer.Count, inner.Count);
                outer = ResamplePolylineUniformCount(outer, target);
                inner = ResamplePolylineUniformCount(inner, target);
            }

            return outer.Count >= 2 && inner.Count >= 2;
        }

        private static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
            tris.Add(a);
            tris.Add(c);
            tris.Add(d);
        }
    }
}
