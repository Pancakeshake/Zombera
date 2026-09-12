using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Places utility poles on the sidewalk strip in front of district lots and
    ///     strings power wires between them. Candidate positions come from each
    ///     lot's street frontage offset onto the footpath (see the
    ///     <c>CityPowerLinePlacer.Lots</c> partial) — the pole chain skips the
    ///     terrain painted for each building's driveway and front-door path, so
    ///     poles land on the footpath on either side of those crossings.
    /// </summary>
    public static partial class CityPowerLinePlacer
    {
        private const string ContainerName = "PowerLines";
        private const string WiresContainerName = "Wires";
        private const float WireTopOffsetMeters = 6f;
        private const float RoadSurfaceClearanceMeters = 4f;
        private const float DuplicateSlotEpsilonMeters = 0.5f;

        /// <summary>
        ///     A resolved pole position on a lot's sidewalk line.
        /// </summary>
        internal readonly struct PoleCandidate
        {
            public readonly Vector2 PositionXZ;
            public readonly Quaternion Facing;

            public PoleCandidate(Vector2 positionXZ, Quaternion facing)
            {
                PositionXZ = positionXZ;
                Facing = facing;
            }
        }

        // ── Hub entry point ──

        public static int PlaceForHub(
            Transform networkRoot,
            Transform areasRoot,
            IReadOnlyList<RoadPolyline> roads,
            CityStreetscapeConfig streetscape,
            DistrictLotTerrainLayout lotTerrainLayout,
            Func<Vector2, float> resolveHeight)
        {
            if (networkRoot == null || areasRoot == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnPowerLines || CityPlacerPrefabResolver.UtilityPole == null)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);
            var wiresRoot = GetOrCreateChild(container, WiresContainerName);

            var spacing = Mathf.Max(1f, streetscape.utilityPoleSpacingMeters);
            var sidewalkOffset = Mathf.Max(0.1f, streetscape.utilityPoleLateralOffsetMeters);
            var crossingClearance = Mathf.Max(0f, streetscape.utilityPoleCrossingClearanceMeters);

            var candidates = new List<PoleCandidate>(128);
            CollectSidewalkCandidates(areasRoot, lotTerrainLayout,
                sidewalkOffset, crossingClearance, spacing, candidates);
            if (candidates.Count == 0)
                return 0;

            // Defensive: never let a pole stand on the road surface itself — the
            // sidewalk line already lives between the lot edge and the road edge.
            candidates.RemoveAll(c => TooCloseToAnyRoad(c.PositionXZ, roads, RoadSurfaceClearanceMeters));
            if (candidates.Count == 0)
                return 0;

            // Grid-major ordering (Z then X) so wires chain along streets instead
            // of jumping across blocks.
            candidates.Sort(CompareByGridPosition);

            var poleTops = new List<Vector3>(candidates.Count);
            var placed = 0;

            for (var i = 0; i < candidates.Count; i++)
            {
                var pos = candidates[i].PositionXZ;
                if (poleTops.Count > 0 &&
                    Vector2.Distance(pos, ToXZ(poleTops[poleTops.Count - 1])) < DuplicateSlotEpsilonMeters)
                    continue; // same grid slot claimed by two neighbouring lots

                var world = new Vector3(pos.x, resolveHeight(pos), pos.y);
                var instance = UnityEngine.Object.Instantiate(
                    CityPlacerPrefabResolver.UtilityPole, world, candidates[i].Facing, container);
                instance.name = "UtilityPole_B" + i;
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Utility Pole");
#endif
                poleTops.Add(world + Vector3.up * WireTopOffsetMeters);
                placed++;
            }

            ConnectPolesWithWires(wiresRoot, poleTops, streetscape);
            return placed;
        }

        private static Vector2 ToXZ(Vector3 v) => new(v.x, v.z);

        // ── Spatial ordering ──

        private static int CompareByGridPosition(PoleCandidate a, PoleCandidate b)
        {
            const float cellSize = 20f;
            var az = Mathf.RoundToInt(a.PositionXZ.y / cellSize);
            var bz = Mathf.RoundToInt(b.PositionXZ.y / cellSize);
            if (az != bz)
                return az.CompareTo(bz);

            var ax = Mathf.RoundToInt(a.PositionXZ.x / cellSize);
            var bx = Mathf.RoundToInt(b.PositionXZ.x / cellSize);
            if (ax != bx)
                return ax.CompareTo(bx);

            return a.PositionXZ.x.CompareTo(b.PositionXZ.x);
        }

        // ── Wire connection ──

        private static void ConnectPolesWithWires(
            Transform wiresRoot,
            List<Vector3> poleTops,
            CityStreetscapeConfig settings)
        {
            if (wiresRoot == null || poleTops.Count < 2)
                return;

            for (var i = 1; i < poleTops.Count; i++)
            {
                var a = poleTops[i - 1];
                var b = poleTops[i];
                if (Vector3.Distance(a, b) > settings.utilityPoleSpacingMeters * 1.75f)
                    continue;

                var wireGo = new GameObject("PowerWire_" + i);
                wireGo.transform.SetParent(wiresRoot, false);
                var lr = wireGo.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                lr.startWidth = settings.powerLineWireWidthMeters;
                lr.endWidth = settings.powerLineWireWidthMeters;
                lr.material = CityPlacerPrefabResolver.PowerLineWireMaterial != null
                    ? CityPlacerPrefabResolver.PowerLineWireMaterial
                    : new Material(Shader.Find("Sprites/Default"));
                lr.useWorldSpace = true;
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(wireGo, "Create Power Wire");
#endif
            }
        }

        // ── Container management ──

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

        private static Transform GetOrCreateContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find(ContainerName);
            if (existing != null)
                return existing;

            var go = new GameObject(ContainerName);
            go.transform.SetParent(networkRoot, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Power Lines Container");
#endif
            return go.transform;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
                return existing;

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
#endif
            return go.transform;
        }

        // ── Road proximity check ──

        /// <summary>
        ///     Returns true when <paramref name="point"/> is within
        ///     <paramref name="minDist"/> of any road centerline segment.
        /// </summary>
        private static bool TooCloseToAnyRoad(
            Vector2 point,
            IReadOnlyList<RoadPolyline> roads,
            float minDist)
        {
            if (roads == null || roads.Count == 0) return false;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2) continue;

                for (var i = 1; i < road.pointsXZ.Count; i++)
                {
                    var a = road.pointsXZ[i - 1];
                    var b = road.pointsXZ[i];
                    var closest = ClosestPointOnSegment(point, a, b);
                    if (Vector2.Distance(point, closest) < minDist)
                        return true;
                }
            }

            return false;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab);
            t = Mathf.Clamp01(t);
            return a + t * ab;
        }
    }
}
