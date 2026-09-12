using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     One socket-aligned mid stretched entry→exit, plus flipped portal caps.
    ///     Hierarchy: MountainTunnels / Tunnel_{id} / {Bore, Mouth_In, Mouth_Out, Gate_*}.
    ///     Gates block agents unless <see cref="RoadNetworkSettings.tunnelEnterable"/>.
    /// </summary>
    public sealed partial class TunnelMeshPlacer
    {
        private const float FallbackClearHalfWidthMeters = 4.25f;
        private const float ClearHeightMeters = 5.5f;

        private readonly List<GameObject> _spawned = new();

        public void Place(
            IReadOnlyList<MountainTunnel> tunnels,
            Transform parent,
            RoadNetworkSettings settings)
        {
            Clear();
            if (tunnels == null || tunnels.Count == 0 || settings == null || parent == null)
                return;
            if (settings.tunnelMidPrefab == null)
            {
                throw new InvalidOperationException(
                    "missing_tunnel_mid: Mountain tunnels require tunnelMidPrefab.");
            }

            var root = EnsureChild(parent, "MountainTunnels");
            DestroyExistingBores(root);

            var snapped = new List<MountainTunnel>(tunnels.Count);
            for (var i = 0; i < tunnels.Count; i++)
                snapped.Add(tunnels[i]);
            TunnelMouthMarkerUtility.ApplySceneMarkersInPlace(snapped);

            for (var i = 0; i < snapped.Count; i++)
                PlaceOne(snapped[i], root, settings);
        }

        public void Clear()
        {
            TunnelRuntimeRegistry.Clear();
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    UnityEngine.Object.Destroy(_spawned[i]);
            }

            _spawned.Clear();
        }

        private void PlaceOne(MountainTunnel tunnel, Transform root, RoadNetworkSettings settings)
        {
            var group = CreateTunnelGroup(root, tunnel);
            var enterable = settings.tunnelEnterable;

            if (!RoadInfrastructureSpanUtility.TryResolveSpan(
                    tunnel.EntryXZ,
                    tunnel.ExitXZ,
                    tunnel.EntryWorldY,
                    tunnel.ExitWorldY,
                    out _,
                    out var spanRotation,
                    out _))
                return;

            // Kit portal faces the opposite way from older docs: +180 so the arch faces the road.
            var entryRot = spanRotation * Quaternion.Euler(0f, 180f, 0f);
            var exitRot = spanRotation;
            PlaceStretchedBore(tunnel, group, settings, enterable);

            if (settings.tunnelPortalPrefab != null)
            {
                var entryWorld = new Vector3(tunnel.EntryXZ.x, tunnel.EntryWorldY, tunnel.EntryXZ.y);
                var exitWorld = new Vector3(tunnel.ExitXZ.x, tunnel.ExitWorldY, tunnel.ExitXZ.y);
                SpawnPortal(
                    settings.tunnelPortalPrefab,
                    group,
                    "Mouth_In",
                    entryWorld,
                    entryRot);
                SpawnPortal(
                    settings.tunnelPortalPrefab,
                    group,
                    "Mouth_Out",
                    exitWorld,
                    exitRot);
            }

            if (!enterable && settings.tunnelPortalGatePrefab != null)
            {
                SpawnPrefab(
                    settings.tunnelPortalGatePrefab,
                    group,
                    "Gate_In",
                    tunnel.EntryXZ,
                    tunnel.EntryWorldY,
                    entryRot);
                SpawnPrefab(
                    settings.tunnelPortalGatePrefab,
                    group,
                    "Gate_Out",
                    tunnel.ExitXZ,
                    tunnel.ExitWorldY,
                    exitRot);
            }

            if (enterable)
            {
                var clearHalfWidth = InfrastructureKitSockets.TryGetBoreClearHalfWidth(
                    settings.tunnelMidPrefab,
                    out var measuredHalfWidth)
                    ? measuredHalfWidth
                    : FallbackClearHalfWidthMeters;
                TunnelRuntimeRegistry.RegisterBore(tunnel, clearHalfWidth, ClearHeightMeters);
            }
        }

        private Transform CreateTunnelGroup(Transform root, MountainTunnel tunnel)
        {
            var go = new GameObject($"Tunnel_R{tunnel.RoadId}_{tunnel.StableId:X}");
            go.transform.SetParent(root, false);
            // The group is organizational only. Keeping it identity prevents parent
            // translation/rotation/scale from shifting world-space portal endpoints.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _spawned.Add(go);
            return go.transform;
        }

        private static void EnsureNavFloorMarker(GameObject go, ulong tunnelId)
        {
            var marker = go.GetComponent<TunnelNavFloor>();
            if (marker == null)
                marker = go.AddComponent<TunnelNavFloor>();
            marker.TunnelStableId = tunnelId;
        }

        private static void RegisterFloorColliders(GameObject go)
        {
            EnsureMeshColliders(go);
            var colliders = go.GetComponentsInChildren<MeshCollider>(true);
            for (var i = 0; i < colliders.Length; i++)
                TunnelRuntimeRegistry.RegisterFloor(colliders[i]);
        }

        private static void EnsureMeshColliders(GameObject go)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var col = filter.GetComponent<MeshCollider>();
                if (col == null)
                    col = filter.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = filter.sharedMesh;
                col.convex = false;
            }
        }

        private static void SpawnPrefab(
            GameObject prefab,
            Transform group,
            string name,
            Vector2 xz,
            float y,
            Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(prefab, group);
            go.name = name;
            go.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.y), rotation);
        }

        private static void DestroyExistingBores(Transform root)
        {
            if (root == null)
                return;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isEditor)
                    UnityEngine.Object.DestroyImmediate(child);
                else
                    UnityEngine.Object.Destroy(child);
            }
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
