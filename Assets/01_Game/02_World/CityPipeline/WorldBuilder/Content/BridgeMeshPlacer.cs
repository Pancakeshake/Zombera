using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Socket-aligns and fuses one modular bridge kit per water crossing.</summary>
    public sealed class BridgeMeshPlacer
    {
        private readonly List<GameObject> _spawned = new();
        private readonly List<ResolvedBridgeApproach> _resolved = new();

        public IReadOnlyList<ResolvedBridgeApproach> Place(
            IReadOnlyList<WaterCrossing> crossings,
            Transform parent,
            RoadNetworkSettings settings)
        {
            Clear();
            if (crossings == null || crossings.Count == 0 || settings == null || parent == null)
                return _resolved;
            ValidateKit(crossings, settings);

            var root = EnsureChild(parent, "WaterCrossings");
            for (var i = 0; i < crossings.Count; i++)
            {
                var crossing = crossings[i];
                if (!IsBridge(crossing.Policy))
                    continue;
                PlaceOne(crossing, root, settings);
            }

            if (_resolved.Count > 0)
                Debug.Log($"[BridgeMeshPlacer] Placed {_resolved.Count} fused bridge kit(s).");
            return _resolved;
        }

        public void Clear()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    UnityEngine.Object.Destroy(_spawned[i]);
            }

            _spawned.Clear();
            _resolved.Clear();
        }

        private static void ValidateKit(
            IReadOnlyList<WaterCrossing> crossings,
            RoadNetworkSettings settings)
        {
            if (!RequiresBridgeKit(crossings))
                return;
            if (settings.bridgeAbutmentPrefab == null)
                throw new InvalidOperationException("missing_bridge_abutment: Bridges require an abutment prefab.");
            if (settings.bridgeMidPrefab == null)
                throw new InvalidOperationException("missing_bridge_mid: Bridges require a bridge-mid prefab.");
        }

        private void PlaceOne(
            WaterCrossing crossing,
            Transform waterRoot,
            RoadNetworkSettings settings)
        {
            if (!RoadInfrastructureSpanUtility.TryResolveSpan(
                    crossing.EntryXZ,
                    crossing.ExitXZ,
                    crossing.DeckWorldY,
                    crossing.DeckWorldY,
                    out _,
                    out var spanRotation,
                    out _))
                return;

            var direction = (crossing.ExitXZ - crossing.EntryXZ).normalized;
            var extension = Mathf.Max(0f, settings.bridgeBankExtensionMeters);
            var entryApproachXZ = crossing.EntryXZ - direction * extension;
            var exitApproachXZ = crossing.ExitXZ + direction * extension;
            var bridgeRoot = CreateBridgeRoot(waterRoot, crossing);

            var entryAbutment = PlaceAbutment(
                settings.bridgeAbutmentPrefab,
                bridgeRoot,
                "Abutment_In",
                entryApproachXZ,
                crossing.DeckWorldY,
                spanRotation * Quaternion.Euler(0f, 180f, 0f));
            var exitAbutment = PlaceAbutment(
                settings.bridgeAbutmentPrefab,
                bridgeRoot,
                "Abutment_Out",
                exitApproachXZ,
                crossing.DeckWorldY,
                spanRotation);

            var entryDeck = RequireSocket(entryAbutment, InfrastructureKitSockets.SocketDeck);
            var exitDeck = RequireSocket(exitAbutment, InfrastructureKitSockets.SocketDeck);
            PlaceDeck(settings.bridgeMidPrefab, bridgeRoot, entryDeck, exitDeck, spanRotation);
            PlacePier(crossing, bridgeRoot, settings, spanRotation);
            PlaceGates(settings, bridgeRoot, entryApproachXZ, exitApproachXZ, crossing.DeckWorldY, spanRotation);

            var entryApproach = PromoteSocket(
                bridgeRoot,
                RequireSocket(entryAbutment, InfrastructureKitSockets.SocketApproach),
                InfrastructureKitSockets.SocketApproachIn);
            var exitApproach = PromoteSocket(
                bridgeRoot,
                RequireSocket(exitAbutment, InfrastructureKitSockets.SocketApproach),
                InfrastructureKitSockets.SocketApproachOut);

            if (!InfrastructureMeshCombiner.FuseIntoRoot(
                    bridgeRoot.gameObject,
                    $"Bridge_R{crossing.RoadId}_{crossing.StableId:X}_Combined"))
                throw new InvalidOperationException($"combine_bridge_failed: {bridgeRoot.name}");

            _resolved.Add(new ResolvedBridgeApproach(
                crossing.StableId,
                crossing.RoadId,
                crossing.RoadClass,
                entryApproach.position,
                exitApproach.position));
        }

        private GameObject PlaceAbutment(
            GameObject prefab,
            Transform root,
            string name,
            Vector2 approachXZ,
            float deckY,
            Quaternion rotation)
        {
            var instance = SpawnPrefab(prefab, root, name, approachXZ, deckY, rotation);
            InfrastructureKitSockets.EnsureBridgeAbutmentSockets(instance);
            var socket = RequireSocket(instance, InfrastructureKitSockets.SocketApproach);
            InfrastructureKitSockets.MoveSocketToWorld(
                instance.transform,
                socket,
                new Vector3(approachXZ.x, socket.position.y, approachXZ.y));
            return instance;
        }

        private GameObject PlaceDeck(
            GameObject prefab,
            Transform root,
            Transform entryDeck,
            Transform exitDeck,
            Quaternion rotation)
        {
            var entryXZ = new Vector2(entryDeck.position.x, entryDeck.position.z);
            var instance = SpawnPrefab(prefab, root, "Deck", entryXZ, entryDeck.position.y, rotation);
            InfrastructureKitSockets.EnsureMidSockets(instance);
            var start = RequireSocket(instance, InfrastructureKitSockets.SocketStart);
            var end = RequireSocket(instance, InfrastructureKitSockets.SocketEnd);
            var length = Vector3.Distance(entryDeck.position, exitDeck.position);
            var authoredLength = InfrastructureKitSockets.LocalZLength(start, end);
            if (authoredLength < 0.1f)
                throw new InvalidOperationException("invalid_bridge_mid_sockets: Start/End overlap.");

            RoadInfrastructureSpanUtility.StretchAndTileUvs(instance, length);
            InfrastructureKitSockets.MoveSocketToWorld(instance.transform, start, entryDeck.position);
            if (Vector3.Distance(end.position, exitDeck.position) > 0.05f)
                throw new InvalidOperationException("bridge_mid_socket_alignment_failed: End did not meet abutment.");
            return instance;
        }

        private void PlacePier(
            WaterCrossing crossing,
            Transform root,
            RoadNetworkSettings settings,
            Quaternion rotation)
        {
            if (settings.bridgePierPrefab == null ||
                crossing.WidthMeters <= RoadInfrastructureSpanUtility.AuthoredModuleLengthMeters * 2.5f)
                return;

            var pier = SpawnPrefab(
                settings.bridgePierPrefab,
                root,
                "Pier",
                crossing.PositionXZ,
                crossing.DeckWorldY,
                rotation);
            InfrastructureKitSockets.EnsureBridgePierSocket(pier);
        }

        private void PlaceGates(
            RoadNetworkSettings settings,
            Transform root,
            Vector2 entryXZ,
            Vector2 exitXZ,
            float deckY,
            Quaternion spanRotation)
        {
            if (settings.bridgePortalGatePrefab == null)
                return;
            SpawnPrefab(
                settings.bridgePortalGatePrefab,
                root,
                "Gate_In",
                entryXZ,
                deckY,
                spanRotation * Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(
                settings.bridgePortalGatePrefab,
                root,
                "Gate_Out",
                exitXZ,
                deckY,
                spanRotation);
        }

        private Transform CreateBridgeRoot(Transform parent, WaterCrossing crossing)
        {
            var go = new GameObject($"Bridge_R{crossing.RoadId}_{crossing.StableId:X}");
            go.transform.SetParent(parent, false);
            _spawned.Add(go);
            return go.transform;
        }

        private static Transform PromoteSocket(Transform parent, Transform source, string name)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.SetPositionAndRotation(source.position, source.rotation);
            return socket;
        }

        private static Transform RequireSocket(GameObject root, string socketName)
        {
            var socket = InfrastructureKitSockets.FindSocket(root.transform, socketName);
            if (socket == null)
                throw new InvalidOperationException($"missing_bridge_socket: {root.name}/{socketName}");
            return socket;
        }

        private static GameObject SpawnPrefab(
            GameObject prefab,
            Transform root,
            string name,
            Vector2 xz,
            float y,
            Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(prefab, root);
            go.name = name;
            go.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.y), rotation);
            return go;
        }

        private static bool RequiresBridgeKit(IReadOnlyList<WaterCrossing> crossings)
        {
            for (var i = 0; i < crossings.Count; i++)
            {
                if (IsBridge(crossings[i].Policy))
                    return true;
            }
            return false;
        }

        private static bool IsBridge(WaterCrossingPolicy policy) =>
            policy == WaterCrossingPolicy.Bridge || policy == WaterCrossingPolicy.Causeway;

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
