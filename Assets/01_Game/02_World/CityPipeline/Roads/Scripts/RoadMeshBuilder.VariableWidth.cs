using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Variable-width road strips used to taper highway asphalt into tunnel apertures.</summary>
    public static partial class RoadMeshBuilder
    {
        public static Mesh BuildVariableWidthStripMeshWithHeightSampler(
            List<Vector2> polylineXZ,
            Func<Vector2, float> sampleWidth,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile = 4f)
        {
            if (polylineXZ == null || polylineXZ.Count < 2 || sampleWidth == null || sampleHeight == null)
                return null;

            var samples = ResamplePolyline(polylineXZ, 4f);
            if (samples.Count < 2)
                return null;

            return BuildVariableWidthStrip(samples, sampleWidth, sampleHeight, uvWorldUnitsPerTile);
        }

        private static Mesh BuildVariableWidthStrip(
            IReadOnlyList<Vector2> samples,
            Func<Vector2, float> sampleWidth,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile)
        {
            var verts = new List<Vector3>(samples.Count * 2);
            var uvs = new List<Vector2>(samples.Count * 2);
            var tris = new List<int>((samples.Count - 1) * 6);
            var tile = Mathf.Max(0.25f, uvWorldUnitsPerTile);

            for (var i = 0; i < samples.Count; i++)
                AppendVariableWidthPair(samples, i, sampleWidth, sampleHeight, tile, verts, uvs);
            AppendStripTriangles(samples.Count, tris);

            var mesh = new Mesh { name = "RoadStripVariableWidth" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendVariableWidthPair(
            IReadOnlyList<Vector2> samples,
            int index,
            Func<Vector2, float> sampleWidth,
            Func<Vector2, float> sampleHeight,
            float tile,
            List<Vector3> verts,
            List<Vector2> uvs)
        {
            var point = samples[index];
            var tangent = ResolveTangent(samples, index);
            var right = new Vector2(tangent.y, -tangent.x);
            var halfWidth = Mathf.Max(0.5f, sampleWidth(point)) * 0.5f;
            var y = sampleHeight(point);
            var leftXZ = point - right * halfWidth;
            var rightXZ = point + right * halfWidth;
            verts.Add(new Vector3(leftXZ.x, y, leftXZ.y));
            verts.Add(new Vector3(rightXZ.x, y, rightXZ.y));
            uvs.Add(leftXZ / tile);
            uvs.Add(rightXZ / tile);
        }

        private static Vector2 ResolveTangent(IReadOnlyList<Vector2> samples, int index)
        {
            Vector2 tangent;
            if (index == 0)
                tangent = samples[1] - samples[0];
            else if (index == samples.Count - 1)
                tangent = samples[index] - samples[index - 1];
            else
                tangent = samples[index + 1] - samples[index - 1];
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.up;
        }

        private static void AppendStripTriangles(int sampleCount, List<int> tris)
        {
            for (var i = 0; i < sampleCount - 1; i++)
            {
                var i0 = i * 2;
                tris.Add(i0);
                tris.Add(i0 + 2);
                tris.Add(i0 + 1);
                tris.Add(i0 + 2);
                tris.Add(i0 + 3);
                tris.Add(i0 + 1);
            }
        }
    }
}
