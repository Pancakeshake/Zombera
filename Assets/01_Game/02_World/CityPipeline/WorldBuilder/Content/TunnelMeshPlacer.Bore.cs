using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     One socket-aligned mid stretched entry→exit (exact length, no tile seams).
    /// </summary>
    public sealed partial class TunnelMeshPlacer
    {
        private void PlaceStretchedBore(
            MountainTunnel tunnel,
            Transform group,
            RoadNetworkSettings settings,
            bool enterable)
        {
            if (settings.tunnelMidPrefab == null)
                return;
            if (!RoadInfrastructureSpanUtility.TryResolveSpan(
                    tunnel.EntryXZ,
                    tunnel.ExitXZ,
                    tunnel.EntryWorldY,
                    tunnel.ExitWorldY,
                    out var entryWorld,
                    out var spanRotation,
                    out var length))
                return;
            if (length < 0.5f)
                return;

            var authored = Mathf.Max(0.5f, ResolveMidAuthoredLength(settings.tunnelMidPrefab));
            SpawnMidTile(
                settings.tunnelMidPrefab,
                group,
                "Bore",
                entryWorld,
                spanRotation,
                authored,
                length,
                enterable,
                tunnel.StableId);

            Debug.Log(
                "[TunnelMeshPlacer] stretched bore=" + length.ToString("F1") + "m" +
                " authored=" + authored.ToString("F2") + "m" +
                " scaleZ=" + (length / authored).ToString("F3"));
        }

        private void SpawnMidTile(
            GameObject prefab,
            Transform group,
            string name,
            Vector3 worldStart,
            Quaternion rotation,
            float authoredLength,
            float placedLength,
            bool enterable,
            ulong tunnelId)
        {
            var go = UnityEngine.Object.Instantiate(prefab, group);
            go.name = name;
            InfrastructureKitSockets.EnsureMidSockets(go);
            go.transform.SetPositionAndRotation(worldStart, rotation);
            var scaleZ = authoredLength > 0.01f ? placedLength / authoredLength : 1f;
            go.transform.localScale = new Vector3(1f, 1f, scaleZ);
            var start = InfrastructureKitSockets.FindSocket(
                go.transform, InfrastructureKitSockets.SocketStart);
            if (start != null)
                InfrastructureKitSockets.MoveSocketToWorld(go.transform, start, worldStart);

            if (!enterable)
                return;
            EnsureNavFloorMarker(go, tunnelId);
            RegisterFloorColliders(go);
        }

        private static float ResolveMidAuthoredLength(GameObject prefab)
        {
            if (prefab == null)
                return RoadInfrastructureSpanUtility.AuthoredModuleLengthMeters;
            if (!InfrastructureKitSockets.TryEncapsulateRootLocalMesh(prefab, out var min, out var max))
                return RoadInfrastructureSpanUtility.AuthoredModuleLengthMeters;
            var length = Mathf.Abs(max.z - min.z);
            return length > 0.5f
                ? length
                : RoadInfrastructureSpanUtility.AuthoredModuleLengthMeters;
        }

        private static void SpawnPortal(
            GameObject prefab,
            Transform group,
            string name,
            Vector3 worldMouth,
            Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(prefab, group);
            go.name = name;
            InfrastructureKitSockets.EnsurePortalSockets(go);
            go.transform.SetPositionAndRotation(worldMouth, rotation);
            // Snap road abutment face (Approach) to the mouth point so asphalt can stop/resume here.
            var approach = InfrastructureKitSockets.FindSocket(
                go.transform, InfrastructureKitSockets.SocketApproach);
            if (approach == null)
            {
                approach = InfrastructureKitSockets.FindSocket(
                    go.transform, InfrastructureKitSockets.SocketMouth);
            }

            if (approach != null)
                InfrastructureKitSockets.MoveSocketToWorld(go.transform, approach, worldMouth);
        }
    }
}
