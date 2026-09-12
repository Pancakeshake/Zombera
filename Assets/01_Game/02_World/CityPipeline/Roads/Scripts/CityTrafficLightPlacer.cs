using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static class CityTrafficLightPlacer
    {
        private const string ContainerName = "TrafficLights";

        public static int PlaceForHub(
            Transform networkRoot,
            ProceduralRoadSystem roadSystem,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            Func<Vector2, float> resolveHeight)
        {
            if (networkRoot == null || roadSystem == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnTrafficLights)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);
            var approaches = new List<CityJunctionApproach>(32);
            CityJunctionApproachUtility.CollectApproaches(
                roadSystem,
                approaches,
                streetscape.placeSignalsOnTerminatingBranch);

            var sidewalk = ResolveSidewalkWidth(roadSettings);
            var manifest = CityMathRoadLayoutGenerator.LastJunctionManifest;
            var placed = 0;
            var signalIndex = 0;
            var skipIndices = FindDeadStubIndices(approaches, manifest);
            for (var i = 0; i < approaches.Count; i++)
            {
                if (skipIndices.Contains(i))
                    continue;

                var approach = approaches[i];
                // Only place signals at junctions that received markings.
                if (!CityIntersectionMarkingBuilder.HasMarkings(approach.Connection))
                    continue;

                placed += TryPlaceSignal(container, approach, streetscape, sidewalk, resolveHeight, signalIndex++);
            }

            return placed;
        }

        public static int PlaceForProceduralHub(
            Transform networkRoot,
            IReadOnlyList<JunctionRecord> junctions,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            Func<Vector2, float> resolveHeight)
        {
            if (networkRoot == null || junctions == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnTrafficLights)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);
            var approaches = new List<JunctionApproach>(32);
            JunctionApproachUtility.CollectApproaches(junctions, roads, roadSettings, approaches);

            var sidewalk = roadSettings != null
                ? roadSettings.proceduralSidewalkWidthMeters
                : 1.5f;
            var placed = 0;
            for (var i = 0; i < approaches.Count; i++)
            {
                placed += TryPlaceProceduralSignal(
                    container, approaches[i], streetscape, sidewalk, resolveHeight, i);
            }

            return placed;
        }

        private static int TryPlaceProceduralSignal(
            Transform parent,
            JunctionApproach approach,
            CityStreetscapeConfig settings,
            float sidewalkWidth,
            Func<Vector2, float> resolveHeight,
            int index)
        {
            var halfRoad = Mathf.Max(0.5f, approach.RoadHalfWidth);
            var setback = settings.trafficSignalSetbackMeters;
            var approachPadding = halfRoad * 2f * 0.267f;
            var approachDir3 = new Vector3(approach.ApproachDir.x, 0f, approach.ApproachDir.y);
            var across = new Vector3(-approachDir3.z, 0f, approachDir3.x);
            var center = new Vector3(approach.JunctionCenterXZ.x, 0f, approach.JunctionCenterXZ.y);
            var pos = center
                      + approachDir3 * (setback + approachPadding)
                      + across * (halfRoad + sidewalkWidth);
            pos.y = resolveHeight(new Vector2(pos.x, pos.z));

            var rotation = Quaternion.LookRotation(approachDir3, Vector3.up)
                         * Quaternion.Euler(0f, 90f, 0f);
            var instance = UnityEngine.Object.Instantiate(
                CityPlacerPrefabResolver.TrafficSignal, pos, rotation, parent);
            instance.name = "TrafficSignal_" + index;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Traffic Signal");
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

        private static int TryPlaceSignal(
            Transform parent,
            CityJunctionApproach approach,
            CityStreetscapeConfig settings,
            float sidewalkWidth,
            Func<Vector2, float> resolveHeight,
            int index)
        {
            var halfRoad = Mathf.Max(0.5f, approach.RoadWidthMeters) * 0.5f;
            var setback = settings.trafficSignalSetbackMeters;

            // Place at sidewalk outer edge: setback from junction along approach,
            // offset across to road edge + sidewalk.
            // Scale the approach padding with road width — was hardcoded 2f for 7.5m roads.
            var approachPadding = approach.RoadWidthMeters * 0.267f;
            var approachDir = approach.ApproachDirection;
            var pos = approach.JunctionCenter
                    + approachDir * (setback + approachPadding)
                    + approach.AcrossDirection * (halfRoad + sidewalkWidth);
            pos.y = resolveHeight(new Vector2(pos.x, pos.z));

            // Face the approaching traffic; prefab forward is perpendicular, so rotate 90°.
            var rotation = Quaternion.LookRotation(approachDir, Vector3.up)
                         * Quaternion.Euler(0f, 90f, 0f);
            var instance = UnityEngine.Object.Instantiate(
                CityPlacerPrefabResolver.TrafficSignal, pos, rotation, parent);
            instance.name = "TrafficSignal_" + index;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Traffic Signal");
#endif
            return 1;
        }

        /// <summary>
        ///     For T-junctions on 4-port EasyRoads connections, identifies the dead stub
        ///     approach (the on-grid vertical column) by comparing approach direction
        ///     similarity against the junction manifest's branch direction.
        ///     Returns the set of approach indices to skip.
        /// </summary>
        private static HashSet<int> FindDeadStubIndices(
            List<CityJunctionApproach> approaches,
            Dictionary<Vector2Int, CityMathRoadLayoutGenerator.CityStreetJunctionEntry> manifest)
        {
            var skip = new HashSet<int>();
            if (manifest == null || approaches == null || approaches.Count < 4)
                return skip;

            const float quantizeScale = 2f;
            // Group by quantized junction center → list of indices
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

                // Find the two approaches with the most similar direction.
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

                // Of the similar pair, the dead stub is at the on-grid column
                // (closer to the manifest's recorded PositionXZ). The real branch
                // is offset sideways. Skip the one nearer the manifest position.
                var posA = new Vector2(approaches[pairA].PortPosition.x, approaches[pairA].PortPosition.z);
                var posB = new Vector2(approaches[pairB].PortPosition.x, approaches[pairB].PortPosition.z);
                var distA = Vector2.Distance(posA, entry.PositionXZ);
                var distB = Vector2.Distance(posB, entry.PositionXZ);
                skip.Add(distA > distB ? pairA : pairB);
            }

            return skip;
        }

        private static float ResolveSidewalkWidth(RoadNetworkSettings settings)
        {
            if (settings == null || !settings.spawnSidewalkMeshes)
                return 0f;
            return settings.ResolveSidewalkWidthMeters();
        }

        private static Transform GetOrCreateContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find(ContainerName);
            if (existing != null)
                return existing;

            var go = new GameObject(ContainerName);
            go.transform.SetParent(networkRoot, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Traffic Lights Container");
#endif
            return go.transform;
        }
    }
}