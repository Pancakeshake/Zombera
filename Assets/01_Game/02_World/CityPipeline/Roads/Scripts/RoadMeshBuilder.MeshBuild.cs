using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class RoadMeshBuilder
    {
        private static Mesh BuildStripMesh(Terrain terrain, List<Vector2> polylineXZ, float widthMeters)
        {
            if (terrain == null || terrain.terrainData == null) return null;
            if (polylineXZ == null || polylineXZ.Count < 2) return null;

            var originY = terrain.transform.position.y;
            var half = Mathf.Max(0.5f, widthMeters) * 0.5f;
            var samples = ResamplePolyline(polylineXZ, Mathf.Clamp(widthMeters * 0.35f, 2.5f, 8f));
            if (samples.Count < 2) return null;

            var verts = new List<Vector3>(samples.Count * 2);
            var uvs = new List<Vector2>(samples.Count * 2);
            var tris = new List<int>((samples.Count - 1) * 6);

            var totalLen = 0f;
            for (var i = 1; i < samples.Count; i++) totalLen += Vector2.Distance(samples[i - 1], samples[i]);
            totalLen = Mathf.Max(0.01f, totalLen);

            var u = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                var p = samples[i];
                var wpos = new Vector3(p.x, 0f, p.y);

                Vector3 tangent = i switch
                {
                    0 => (ToV3(samples[1]) - ToV3(samples[0])).normalized,
                    _ when i == samples.Count - 1 => (ToV3(samples[i]) - ToV3(samples[^2])).normalized,
                    _ => (ToV3(samples[i + 1]) - ToV3(samples[i - 1])).normalized
                };

                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;

                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                var leftOffset = -right * half;
                var rightOffset = right * half;

                var y = terrain.SampleHeight(wpos) + originY + 0.15f;
                var left = new Vector3(wpos.x + leftOffset.x, y, wpos.z + leftOffset.z);
                var rightV = new Vector3(wpos.x + rightOffset.x, y, wpos.z + rightOffset.z);

                if (i > 0) u += Vector2.Distance(samples[i - 1], p) / totalLen;

                verts.Add(left);
                verts.Add(rightV);
                uvs.Add(new Vector2(0f, u));
                uvs.Add(new Vector2(1f, u));
            }

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var i0 = i * 2;
                var i1 = i0 + 1;
                var i2 = i0 + 2;
                var i3 = i0 + 3;

                tris.Add(i0);
                tris.Add(i2);
                tris.Add(i1);

                tris.Add(i2);
                tris.Add(i3);
                tris.Add(i1);
            }

            var mesh = new Mesh { name = "RoadStrip" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToV3(Vector2 xz) => new Vector3(xz.x, 0f, xz.y);

        private static Mesh BuildStripMeshFromPolylines(
            IReadOnlyList<Vector3> sourceMarkers,
            List<Vector2> polylineXZ,
            float widthMeters)
        {
            if (sourceMarkers == null || sourceMarkers.Count < 2 || polylineXZ == null || polylineXZ.Count < 2) return null;

            var half = Mathf.Max(0.5f, widthMeters) * 0.5f;
            var samples = ResamplePolyline(polylineXZ, Mathf.Clamp(widthMeters * 0.35f, 2.5f, 8f));
            if (samples.Count < 2) return null;

            var verts = new List<Vector3>(samples.Count * 2);
            var uvs = new List<Vector2>(samples.Count * 2);
            var tris = new List<int>((samples.Count - 1) * 6);

            var totalLen = 0f;
            for (var i = 1; i < samples.Count; i++) totalLen += Vector2.Distance(samples[i - 1], samples[i]);
            totalLen = Mathf.Max(0.01f, totalLen);

            var u = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                var p = samples[i];
                var wpos = new Vector3(p.x, 0f, p.y);

                Vector3 tangent = i switch
                {
                    0 => (ToV3(samples[1]) - ToV3(samples[0])).normalized,
                    _ when i == samples.Count - 1 => (ToV3(samples[i]) - ToV3(samples[^2])).normalized,
                    _ => (ToV3(samples[i + 1]) - ToV3(samples[i - 1])).normalized
                };

                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;

                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                var leftOffset = -right * half;
                var rightOffset = right * half;

                var y = SampleClosestMarkerHeight(sourceMarkers, wpos);

                var left = new Vector3(wpos.x + leftOffset.x, y, wpos.z + leftOffset.z);
                var rightV = new Vector3(wpos.x + rightOffset.x, y, wpos.z + rightOffset.z);

                if (i > 0) u += Vector2.Distance(samples[i - 1], p) / totalLen;

                verts.Add(left);
                verts.Add(rightV);
                uvs.Add(new Vector2(0f, u));
                uvs.Add(new Vector2(1f, u));
            }

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var i0 = i * 2;
                var i1 = i0 + 1;
                var i2 = i0 + 2;
                var i3 = i0 + 3;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i2); tris.Add(i3); tris.Add(i1);
            }

            var mesh = new Mesh { name = "SidewalkStrip" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildStripMeshWithHeightSampler(
            List<Vector2> polylineXZ,
            float widthMeters,
            System.Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile = 0f)
        {
            if (polylineXZ == null || polylineXZ.Count < 2 || sampleHeight == null)
                return null;

            var half = Mathf.Max(0.5f, widthMeters) * 0.5f;
            return BuildStripMeshWithHeightSampler(polylineXZ, half, half, sampleHeight, uvWorldUnitsPerTile);
        }

        private static Mesh BuildStripMeshWithHeightSampler(
            List<Vector2> polylineXZ,
            float innerOffsetMeters,
            float outerOffsetMeters,
            System.Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile,
            int sideSign = 0)
        {
            if (polylineXZ == null || polylineXZ.Count < 2 || sampleHeight == null)
                return null;

            var symmetricHalf = innerOffsetMeters > 0f && Mathf.Approximately(innerOffsetMeters, outerOffsetMeters);
            var useCenterlineOffsets = sideSign != 0;
            var half = symmetricHalf ? innerOffsetMeters : (outerOffsetMeters - innerOffsetMeters) * 0.5f;
            var tile = Mathf.Max(0.25f, uvWorldUnitsPerTile);
            var useWorldTiling = tile > 0f;
            var useWorldPositionUvs = useWorldTiling && !useCenterlineOffsets;

            var samples = symmetricHalf
                ? ResamplePolyline(polylineXZ, Mathf.Clamp(half * 0.7f, 2.5f, 8f))
                : polylineXZ;
            if (samples.Count < 2)
                return null;

            var verts = new List<Vector3>(samples.Count * 2);
            var uvs = new List<Vector2>(samples.Count * 2);
            var tris = new List<int>((samples.Count - 1) * 6);

            var normalizedUvTotalLen = 0f;
            if (!useWorldPositionUvs && !useWorldTiling)
            {
                for (var j = 1; j < samples.Count; j++)
                    normalizedUvTotalLen += Vector2.Distance(samples[j - 1], samples[j]);
                normalizedUvTotalLen = Mathf.Max(0.01f, normalizedUvTotalLen);
            }

            var alongMeters = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                var p = samples[i];
                var y = sampleHeight(p);

                Vector3 tangent = i switch
                {
                    0 => (ToV3(samples[1]) - ToV3(samples[0])).normalized,
                    _ when i == samples.Count - 1 => (ToV3(samples[i]) - ToV3(samples[^2])).normalized,
                    _ => (ToV3(samples[i + 1]) - ToV3(samples[i - 1])).normalized
                };

                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = Vector3.forward;

                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 innerV;
                Vector3 outerV;
                if (useCenterlineOffsets)
                {
                    var sign = sideSign < 0 ? -1f : 1f;
                    innerV = new Vector3(
                        p.x + right.x * innerOffsetMeters * sign,
                        y,
                        p.y + right.z * innerOffsetMeters * sign);
                    outerV = new Vector3(
                        p.x + right.x * outerOffsetMeters * sign,
                        y,
                        p.y + right.z * outerOffsetMeters * sign);
                }
                else
                {
                    innerV = new Vector3(p.x - right.x * half, y, p.y - right.z * half);
                    outerV = new Vector3(p.x + right.x * half, y, p.y + right.z * half);
                }

                if (i > 0)
                    alongMeters += Vector2.Distance(samples[i - 1], p);

                float uvAlong;
                float uvAcrossInner;
                float uvAcrossOuter;
                if (useWorldPositionUvs)
                {
                    verts.Add(innerV);
                    verts.Add(outerV);
                    uvs.Add(new Vector2(innerV.x / tile, innerV.z / tile));
                    uvs.Add(new Vector2(outerV.x / tile, outerV.z / tile));
                    continue;
                }

                if (useWorldTiling)
                {
                    uvAlong = alongMeters / tile;
                    uvAcrossInner = 0f;
                    uvAcrossOuter = Mathf.Abs(outerOffsetMeters - innerOffsetMeters) / tile;
                }
                else
                {
                    uvAlong = alongMeters / normalizedUvTotalLen;
                    uvAcrossInner = 0f;
                    uvAcrossOuter = 1f;
                }

                verts.Add(innerV);
                verts.Add(outerV);
                uvs.Add(new Vector2(uvAcrossInner, uvAlong));
                uvs.Add(new Vector2(uvAcrossOuter, uvAlong));
            }

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var i0 = i * 2;
                tris.Add(i0);
                tris.Add(i0 + 2);
                tris.Add(i0 + 1);
                tris.Add(i0 + 2);
                tris.Add(i0 + 3);
                tris.Add(i0 + 1);
            }

            var mesh = new Mesh { name = "RoadStrip" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildAxisAlignedQuadXZ(
            Rect rect,
            System.Func<Vector2, float> sampleHeight,
            float liftMeters = 0f,
            float uvWorldUnitsPerTile = 2f)
        {
            if (sampleHeight == null || rect.width < 0.01f || rect.height < 0.01f)
                return null;

            var tile = Mathf.Max(0.25f, uvWorldUnitsPerTile);
            var corners = new Vector2[4]
            {
                new(rect.xMin, rect.yMin),
                new(rect.xMax, rect.yMin),
                new(rect.xMax, rect.yMax),
                new(rect.xMin, rect.yMax)
            };

            var verts = new Vector3[4];
            for (var i = 0; i < corners.Length; i++)
            {
                var c = corners[i];
                verts[i] = new Vector3(c.x, sampleHeight(c) + liftMeters, c.y);
            }

            var uSpan = rect.width / tile;
            var vSpan = rect.height / tile;
            var uvs = new Vector2[4]
            {
                new(0f, 0f),
                new(uSpan, 0f),
                new(uSpan, vSpan),
                new(0f, vSpan)
            };

            var tris = new[] { 0, 2, 1, 0, 3, 2 };
            var mesh = new Mesh { name = "AxisAlignedQuad" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
