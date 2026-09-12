using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Shared road-mesh distance queries used by both door-path generation
    ///     and building-facing resolution.  Single source of truth for "how far
    ///     is this world point from the nearest road surface?"
    /// </summary>
    public static class CityRoadMeshQuery
    {
        public struct RoadMeshInfo
        {
            public Mesh mesh;
            public Transform transform;

            // Acceleration data built once at collect time: world-space bounds plus
            // a downsampled world-XZ sample set. Vertex-by-vertex scans of dense
            // road meshes per query were the dominant placement cost.
            public Vector2 boundsMin;
            public Vector2 boundsMax;
            public Vector2[] samplesXZ;
        }

        private const int SampleStride = 16;

        /// <summary>
        ///     Collects every MeshFilter+MeshRenderer under <paramref name="roadObjectsRoot"/>
        ///     that is not a highway marker.
        ///     Descends into layer folders (Asphalt / Sidewalks / Footpaths / Driveways) so
        ///     passing the network root still yields the road surfaces. Combined layer
        ///     meshes are preferred: a combined mesh already represents its strips, so
        ///     collecting it means the individual strips are skipped.
        /// </summary>
        public static List<RoadMeshInfo> CollectRoadMeshes(Transform roadObjectsRoot)
        {
            var list = new List<RoadMeshInfo>();
            if (roadObjectsRoot == null) return list;

            CollectRoadMeshesRecursive(roadObjectsRoot, list);
            return list;
        }

        private static void CollectRoadMeshesRecursive(Transform parent, List<RoadMeshInfo> list)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf != null && mf.sharedMesh != null && mr != null)
                {
                    if (IsAlreadyCollected(mf, list))
                        continue;

                    var info = new RoadMeshInfo { mesh = mf.sharedMesh, transform = mf.transform };
                    BuildSamples(mf, ref info);
                    list.Add(info);
                    continue;
                }

                // Layer folder (or any grouping node) — descend into it.
                CollectRoadMeshesRecursive(child, list);
            }
        }

        private static bool IsAlreadyCollected(MeshFilter candidate, List<RoadMeshInfo> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].transform == candidate.transform)
                    return true;
            }

            return false;
        }

        private static void BuildSamples(MeshFilter mf, ref RoadMeshInfo info)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable)
                return;

            var verts = mesh.vertices;
            if (verts.Length < 2)
                return;

            var sampleCount = (verts.Length + SampleStride - 1) / SampleStride;
            var samples = new Vector2[sampleCount];
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var v = 0; v < verts.Length; v += SampleStride)
            {
                var p = mf.transform.TransformPoint(verts[v]);
                var sample = new Vector2(p.x, p.z);
                samples[v / SampleStride] = sample;
                if (sample.x < min.x) min.x = sample.x;
                if (sample.x > max.x) max.x = sample.x;
                if (sample.y < min.y) min.y = sample.y;
                if (sample.y > max.y) max.y = sample.y;
            }

            info.samplesXZ = samples;
            info.boundsMin = min;
            info.boundsMax = max;
        }

        /// <summary>
        ///     Finds the closest vertex on <paramref name="roadMesh"/> to <paramref name="targetPoint"/>
        ///     (XZ distance only).  Returns the world-space point and squared distance.
        /// </summary>
        public static bool TryFindClosestRoadPoint(
            RoadMeshInfo roadMesh,
            Vector3 targetPoint,
            out Vector3 closestPoint,
            out float distSq)
        {
            closestPoint = Vector3.zero;
            distSq = float.MaxValue;

            var mesh = roadMesh.mesh;
            var t = roadMesh.transform;
            if (mesh == null || !mesh.isReadable) return false;

            var verts = mesh.vertices;
            if (verts.Length < 2) return false;

            var bestDist = float.MaxValue;
            var bestVert = Vector3.zero;

            for (var v = 0; v < verts.Length; v++)
            {
                var worldVert = t.TransformPoint(verts[v]);
                var delta = targetPoint - worldVert;
                delta.y = 0f;
                var dSq = delta.sqrMagnitude;
                if (dSq < bestDist)
                {
                    bestDist = dSq;
                    bestVert = worldVert;
                }
            }

            if (bestDist >= float.MaxValue) return false;

            closestPoint = bestVert;
            distSq = bestDist;
            return true;
        }

        /// <summary>
        ///     Returns the minimum squared XZ distance from <paramref name="targetPoint"/>
        ///     to any vertex in <paramref name="roadMeshes"/>. Uses the collect-time
        ///     downsampled samples + bounds prefilter — no per-query full-mesh scans.
        /// </summary>
        public static float GetMinRoadDistanceSq(
            IReadOnlyList<RoadMeshInfo> roadMeshes,
            Vector3 targetPoint)
        {
            var target = new Vector2(targetPoint.x, targetPoint.z);
            var best = float.MaxValue;
            for (var r = 0; r < roadMeshes.Count; r++)
            {
                var info = roadMeshes[r];
                var samples = info.samplesXZ;
                if (samples == null || samples.Length == 0)
                    continue;

                // Bounds reject: distance to the sample bounds is a lower bound of the
                // distance to any sample — skip meshes that cannot beat the current best.
                if (SqrDistanceToRect(target, info.boundsMin, info.boundsMax) >= best)
                    continue;

                for (var s = 0; s < samples.Length; s++)
                {
                    var delta = target - samples[s];
                    var dSq = delta.x * delta.x + delta.y * delta.y;
                    if (dSq < best)
                        best = dSq;
                }
            }

            return best;
        }

        private static float SqrDistanceToRect(Vector2 point, Vector2 min, Vector2 max)
        {
            var dx = Math.Max(min.x - point.x, Math.Max(0f, point.x - max.x));
            var dz = Math.Max(min.y - point.y, Math.Max(0f, point.y - max.y));
            return dx * dx + dz * dz;
        }
    }
}
