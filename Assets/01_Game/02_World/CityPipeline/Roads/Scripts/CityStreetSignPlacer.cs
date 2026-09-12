using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Places street name signs at junction approaches, positioned near
    ///     traffic signal locations with cardinal (0/90/180/270) rotation.
    /// </summary>
    public static class CityStreetSignPlacer
    {
        private const string ContainerName = "StreetSigns";

        public static int PlaceForHub(
            Transform networkRoot,
            ProceduralRoadSystem roadSystem,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            CityMathRoadLayout layout,
            RoadNetworkRuntime roadNetwork,
            Func<Vector2, float> resolveHeight)
        {
            if (networkRoot == null || roadSystem == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnStreetSigns || CityPlacerPrefabResolver.StreetSign == null)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);

            var approaches = new List<CityJunctionApproach>(32);
            CityJunctionApproachUtility.CollectApproaches(
                roadSystem, approaches, streetscape.placeSignalsOnTerminatingBranch);

            if (approaches.Count == 0)
                return 0;

            var manifest = CityMathRoadLayoutGenerator.LastJunctionManifest;
            var skipIndices = FindDeadStubIndices(approaches, manifest);
            var sidewalk = ResolveSidewalkWidth(roadSettings);
            var roads = roadNetwork?.Roads;
            var placed = 0;
            var nameIndex = 0;

            for (var i = 0; i < approaches.Count; i++)
            {
                if (skipIndices.Contains(i))
                    continue;
                if (!CityIntersectionMarkingBuilder.HasMarkings(approaches[i].Connection))
                    continue;

                placed += TryPlaceSign(
                    container, approaches[i], streetscape, sidewalk,
                    resolveHeight, roads, layout, ref nameIndex);
            }

            return placed;
        }

        public static int PlaceForProceduralHub(
            Transform networkRoot,
            IReadOnlyList<JunctionRecord> junctions,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            CityMathRoadLayout layout,
            Func<Vector2, float> resolveHeight)
        {
            if (networkRoot == null || junctions == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnStreetSigns || CityPlacerPrefabResolver.StreetSign == null)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);
            var approaches = new List<JunctionApproach>(32);
            JunctionApproachUtility.CollectApproaches(junctions, roads, roadSettings, approaches);
            if (approaches.Count == 0)
                return 0;

            var sidewalk = roadSettings != null
                ? roadSettings.proceduralSidewalkWidthMeters
                : 1.5f;
            var placed = 0;
            var nameIndex = 0;
            for (var i = 0; i < approaches.Count; i++)
            {
                placed += TryPlaceProceduralSign(
                    container, approaches[i], streetscape, sidewalk,
                    resolveHeight, roads, layout, ref nameIndex);
            }

            return placed;
        }

        private static int TryPlaceProceduralSign(
            Transform parent,
            JunctionApproach approach,
            CityStreetscapeConfig settings,
            float sidewalkWidth,
            Func<Vector2, float> resolveHeight,
            IReadOnlyList<RoadPolyline> roads,
            CityMathRoadLayout layout,
            ref int nameIndex)
        {
            var halfRoad = Mathf.Max(0.5f, approach.RoadHalfWidth);
            var setback = settings.streetSignCornerOffsetMeters;
            var approachDir3 = new Vector3(approach.ApproachDir.x, 0f, approach.ApproachDir.y);
            var across = new Vector3(-approachDir3.z, 0f, approachDir3.x);
            var center = new Vector3(approach.JunctionCenterXZ.x, 0f, approach.JunctionCenterXZ.y);
            var pos = center
                      + approachDir3 * (setback + 1.5f)
                      - across * (halfRoad + sidewalkWidth);
            pos.y = resolveHeight(new Vector2(pos.x, pos.z));

            var look = Quaternion.LookRotation(approachDir3, Vector3.up);
            var cardinal = SnapToCardinal(look);
            var label = CityStreetNameUtility.ResolveRoadDisplayNameAtPoint(
                pos, roads, layout);

            var instance = UnityEngine.Object.Instantiate(
                CityPlacerPrefabResolver.StreetSign, pos, cardinal, parent);
            instance.name = "StreetSign_" + nameIndex++;
            ApplyLabel(instance, label);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Street Sign");
#endif
            return 1;
        }

        public static void Clear(Transform networkRoot)
        {
            if (networkRoot == null)
                return;

            var container = networkRoot.Find(ContainerName);
            if (container == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(container.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(container.gameObject);
        }

        private static int TryPlaceSign(
            Transform parent,
            CityJunctionApproach approach,
            CityStreetscapeConfig settings,
            float sidewalkWidth,
            Func<Vector2, float> resolveHeight,
            IReadOnlyList<RoadPolyline> roads,
            CityMathRoadLayout layout,
            ref int nameIndex)
        {
            var halfRoad = Mathf.Max(0.5f, approach.RoadWidthMeters) * 0.5f;
            var setback = settings.streetSignCornerOffsetMeters;

            // Place on opposite side of the approach from the traffic signal,
            // slightly set back from the junction.
            var approachDir = approach.ApproachDirection;
            var pos = approach.JunctionCenter
                    + approachDir * (setback + 1.5f)
                    - approach.AcrossDirection * (halfRoad + sidewalkWidth);
            pos.y = resolveHeight(new Vector2(pos.x, pos.z));

            // Snap rotation to nearest cardinal (0, 90, 180, 270).
            var look = Quaternion.LookRotation(approachDir, Vector3.up);
            var cardinal = SnapToCardinal(look);

            var label = CityStreetNameUtility.ResolveRoadDisplayNameAtPoint(
                approach.PortPosition, roads, layout);

            var instance = UnityEngine.Object.Instantiate(
                CityPlacerPrefabResolver.StreetSign, pos, cardinal, parent);
            instance.name = "StreetSign_" + nameIndex++;
            ApplyLabel(instance, label);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Street Sign");
#endif
            return 1;
        }

        private static void ApplyLabel(GameObject instance, string label)
        {
            var tmp = instance.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
                tmp.text = label;
        }

        private static Quaternion SnapToCardinal(Quaternion q)
        {
            var euler = q.eulerAngles;
            var snapped = Mathf.Round(euler.y / 90f) * 90f;
            return Quaternion.Euler(0f, snapped, 0f);
        }

        private static float ResolveSidewalkWidth(RoadNetworkSettings settings)
        {
            if (settings == null || !settings.spawnSidewalkMeshes)
                return 0f;
            return settings.ResolveSidewalkWidthMeters();
        }

        /// <summary>
        ///     Skips dead-stub approaches at T-junctions, matching the same
        ///     logic used by <see cref="CityTrafficLightPlacer"/>.
        /// </summary>
        private static HashSet<int> FindDeadStubIndices(
            List<CityJunctionApproach> approaches,
            Dictionary<Vector2Int, CityMathRoadLayoutGenerator.CityStreetJunctionEntry> manifest)
        {
            var skip = new HashSet<int>();
            if (manifest == null || approaches == null || approaches.Count < 4)
                return skip;

            const float quantizeScale = 2f;
            var groups = new Dictionary<Vector2Int, List<int>>();
            for (var i = 0; i < approaches.Count; i++)
            {
                var jc = approaches[i].JunctionCenter;
                var key = new Vector2Int(
                    Mathf.RoundToInt(jc.x * quantizeScale),
                    Mathf.RoundToInt(jc.z * quantizeScale));
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    groups[key] = list;
                }
                list.Add(i);
            }

            foreach (var kvp in groups)
            {
                var indices = kvp.Value;
                if (indices.Count != 4)
                    continue;
                if (!manifest.TryGetValue(kvp.Key, out var entry))
                    continue;
                if (entry.Kind != CityMathRoadLayoutGenerator.CityJunctionKind.T)
                    continue;

                var branchDir = new Vector3(
                    entry.BranchApproachDirection.x, 0f, entry.BranchApproachDirection.y);
                if (branchDir.sqrMagnitude < 0.0001f)
                    continue;
                branchDir.Normalize();

                var bestDot = -1f;
                var pairA = -1;
                var pairB = -1;
                for (var a = 0; a < indices.Count; a++)
                {
                    for (var b = a + 1; b < indices.Count; b++)
                    {
                        var dot = Vector3.Dot(
                            approaches[indices[a]].ApproachDirection,
                            approaches[indices[b]].ApproachDirection);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            pairA = indices[a];
                            pairB = indices[b];
                        }
                    }
                }

                if (pairA < 0 || bestDot < 0.5f)
                    continue;

                var posA = new Vector2(approaches[pairA].PortPosition.x, approaches[pairA].PortPosition.z);
                var posB = new Vector2(approaches[pairB].PortPosition.x, approaches[pairB].PortPosition.z);
                var distA = Vector2.Distance(posA, entry.PositionXZ);
                var distB = Vector2.Distance(posB, entry.PositionXZ);
                skip.Add(distA > distB ? pairA : pairB);
            }

            return skip;
        }

        private static Transform GetOrCreateContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find(ContainerName);
            if (existing != null)
                return existing;

            var go = new GameObject(ContainerName);
            go.transform.SetParent(networkRoot, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Street Signs Container");
#endif
            return go.transform;
        }
    }
}
