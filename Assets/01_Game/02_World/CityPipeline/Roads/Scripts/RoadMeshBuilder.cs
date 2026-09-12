using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static partial class RoadMeshBuilder
    {
        private static readonly List<Transform> DecalReuseBuffer = new(64);

        public static GameObject BuildRoadMeshObject(
            Transform parent,
            Terrain terrain,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect tileQueryRect)
        {
            if (parent == null || terrain == null || terrain.terrainData == null) return null;
            if (road == null || road.pointsXZ == null || road.pointsXZ.Count < 2 || settings == null ||
                !settings.spawnRoadMeshes)
                return null;

            var material = ResolveMaterial(road, settings);
            if (material == null) return null;

            var subPolylines = ExtractOverlappingSubPolylines(road.pointsXZ, tileQueryRect);
            if (subPolylines.Count == 0) return null;

            var root = new GameObject($"Road_{road.id}");
            root.transform.SetParent(parent, false);

            for (var i = 0; i < subPolylines.Count; i++)
            {
                var sub = subPolylines[i];
                var mesh = BuildStripMesh(terrain, sub, road.widthMeters);
                if (mesh == null) continue;

                var part = new GameObject($"Part_{i}");
                part.transform.SetParent(root.transform, false);
                var mf = part.AddComponent<MeshFilter>();
                var mr = part.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = material;

                SpawnOptionalRoadDecals(root.transform, terrain, sub, settings, i);
            }

            return root;
        }

        public static Material ResolveMaterial(RoadPolyline road, RoadNetworkSettings settings)
        {
            if (settings == null) return null;

            var baseMat = road?.roadClass switch
            {
                RoadClass.Highway => settings.highwayMaterial,
                RoadClass.Arterial => settings.arterialMaterial,
                RoadClass.Local => settings.localMaterial,
                _ => null
            };

            baseMat ??= settings.roadMaterial;
            if (baseMat == null) return null;

            // Keep class asphalt for highways so Combined_* meshes stay one continuous surface.
            if (road?.roadClass == RoadClass.Highway)
                return baseMat;

            if (road == null || road.pointsXZ == null || road.pointsXZ.Count < 3) return baseMat;
            if (settings.curveMaterial != null &&
                IsUsableCurveMaterial(settings.curveMaterial, baseMat) &&
                IsCurvy(road.pointsXZ, 28f))
                return settings.curveMaterial;
            return baseMat;
        }

        private static bool IsUsableCurveMaterial(Material curve, Material baseMat)
        {
            if (curve == null || baseMat == null)
                return false;
            // Reject third-party NYC kit curve atlas — it splits Combined meshes and mismatches asphalt.
            if (curve.name.IndexOf("Road_curve", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                curve != baseMat)
                return false;
            return true;
        }

        private static bool IsCurvy(IReadOnlyList<Vector2> pts, float minAvgTurnDegrees)
        {
            if (pts == null || pts.Count < 3) return false;

            var sum = 0f;
            var count = 0;
            for (var i = 1; i < pts.Count - 1; i++)
            {
                var a = pts[i] - pts[i - 1];
                var b = pts[i + 1] - pts[i];
                if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f) continue;

                sum += Vector2.Angle(a, b);
                count++;
            }

            if (count <= 0) return false;
            return (sum / count) >= Mathf.Max(0f, minAvgTurnDegrees);
        }

        private static float SampleClosestMarkerHeight(IReadOnlyList<Vector3> markers, Vector3 worldPos)
        {
            var bestDist = float.MaxValue;
            var bestY = 0f;
            for (var i = 0; i < markers.Count; i++)
            {
                var dx = markers[i].x - worldPos.x;
                var dz = markers[i].z - worldPos.z;
                var dist = dx * dx + dz * dz;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestY = markers[i].y;
                }
            }
            return bestY + 0.15f;
        }

        private static void SpawnOptionalRoadDecals(
            Transform parent,
            Terrain terrain,
            IReadOnlyList<Vector2> polylineXZ,
            RoadNetworkSettings settings,
            int partIndex)
        {
            if (parent == null || terrain == null || settings == null || polylineXZ == null || polylineXZ.Count < 2) return;
            if (!settings.spawnRoadDecals || settings.roadDecalPrefab == null) return;

            var spacing = Mathf.Max(1f, settings.roadDecalSpacingMeters);
            var nextDistance = spacing * 0.5f;
            var traveled = 0f;
            var terrainOriginY = terrain.transform.position.y;
            var decalIndex = 0;
            PrepareDecalReuse(parent, DecalReuseBuffer);
            var reuseIndex = 0;

            for (var i = 1; i < polylineXZ.Count; i++)
            {
                var a2 = polylineXZ[i - 1];
                var b2 = polylineXZ[i];
                var seg = b2 - a2;
                var segLen = seg.magnitude;
                if (segLen <= 0.001f)
                {
                    traveled += segLen;
                    continue;
                }

                while (traveled + segLen >= nextDistance)
                {
                    var localDistance = nextDistance - traveled;
                    var t = Mathf.Clamp01(localDistance / segLen);
                    var point2 = Vector2.Lerp(a2, b2, t);
                    var tangent2 = seg / segLen;

                    var worldPos = new Vector3(point2.x, 0f, point2.y);
                    worldPos.y = terrain.SampleHeight(worldPos) + terrainOriginY + 0.04f;

                    var rotation = Quaternion.LookRotation(new Vector3(tangent2.x, 0f, tangent2.y), Vector3.up);
                    GameObject instance;
                    if (reuseIndex < DecalReuseBuffer.Count)
                    {
                        instance = DecalReuseBuffer[reuseIndex].gameObject;
                        instance.transform.SetPositionAndRotation(worldPos, rotation);
                        instance.transform.SetParent(parent, true);
                        instance.SetActive(true);
                    }
                    else
                    {
                        instance = Object.Instantiate(settings.roadDecalPrefab, worldPos, rotation, parent);
                    }

                    instance.name = $"RoadDecal_{partIndex}_{decalIndex}";
                    decalIndex++;
                    reuseIndex++;

                    nextDistance += spacing;
                }

                traveled += segLen;
            }

            DisableUnusedDecals(DecalReuseBuffer, reuseIndex);
            DecalReuseBuffer.Clear();
        }

        private static void PrepareDecalReuse(Transform parent, List<Transform> reusableDecals)
        {
            reusableDecals.Clear();
            if (parent == null) return;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;
                if (!child.name.StartsWith("RoadDecal_")) continue;

                reusableDecals.Add(child);
                child.gameObject.SetActive(false);
            }
        }

        private static void DisableUnusedDecals(List<Transform> reusableDecals, int usedCount)
        {
            for (var i = usedCount; i < reusableDecals.Count; i++)
                if (reusableDecals[i] != null)
                    reusableDecals[i].gameObject.SetActive(false);
        }
    }
}
