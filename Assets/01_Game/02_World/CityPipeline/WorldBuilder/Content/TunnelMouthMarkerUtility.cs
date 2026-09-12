using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Optional debug override: root-level Capsule* markers can snap mouths for a seed.
    ///     Procedural placement comes from <see cref="MountainTunnelScanner"/> mouth refine math.
    /// </summary>
    public static class TunnelMouthMarkerUtility
    {
        private const float SnapMaxHorizontalMeters = 300f;

        public static MountainTunnel ApplySceneMarkers(in MountainTunnel tunnel)
        {
            var list = new List<MountainTunnel>(1) { tunnel };
            ApplySceneMarkersInPlace(list);
            return list[0];
        }

        public static void ApplySceneMarkersInPlace(IList<MountainTunnel> tunnels)
        {
            if (tunnels == null || tunnels.Count == 0)
                return;

            CollectCapsuleMarkers(out var markers);
            if (markers.Count == 0)
                return;

            var used = new bool[markers.Count];
            // Greedy best-pair first so multi-tunnel scenes don't steal mouths.
            var order = BuildTunnelOrderByBestPair(tunnels, markers);
            for (var o = 0; o < order.Length; o++)
            {
                var i = order[o];
                if (!TryPickExclusivePair(tunnels[i], markers, used, out var entry, out var exit))
                    continue;
                tunnels[i] = RebuildWithMouths(tunnels[i], entry, exit);
            }
        }

        private static int[] BuildTunnelOrderByBestPair(
            IList<MountainTunnel> tunnels,
            List<Vector3> markers)
        {
            var order = new int[tunnels.Count];
            var costs = new float[tunnels.Count];
            for (var i = 0; i < tunnels.Count; i++)
            {
                order[i] = i;
                costs[i] = BestPairCost(tunnels[i], markers);
            }

            System.Array.Sort(order, (a, b) => costs[a].CompareTo(costs[b]));
            return order;
        }

        private static float BestPairCost(in MountainTunnel tunnel, List<Vector3> markers)
        {
            var best = float.MaxValue;
            for (var a = 0; a < markers.Count; a++)
            for (var b = 0; b < markers.Count; b++)
            {
                if (a == b)
                    continue;
                var cost = PairCost(tunnel, markers[a], markers[b]);
                if (cost < best)
                    best = cost;
            }

            return best;
        }

        private static bool TryPickExclusivePair(
            in MountainTunnel tunnel,
            List<Vector3> markers,
            bool[] used,
            out Vector3 entry,
            out Vector3 exit)
        {
            entry = default;
            exit = default;
            var bestCost = float.MaxValue;
            var bestA = -1;
            var bestB = -1;

            for (var a = 0; a < markers.Count; a++)
            {
                if (used[a])
                    continue;
                for (var b = 0; b < markers.Count; b++)
                {
                    if (a == b || used[b])
                        continue;
                    var cost = PairCost(tunnel, markers[a], markers[b]);
                    if (cost >= bestCost)
                        continue;
                    bestCost = cost;
                    bestA = a;
                    bestB = b;
                }
            }

            if (bestA < 0 || bestCost > SnapMaxHorizontalMeters * 2f)
                return false;

            used[bestA] = true;
            used[bestB] = true;
            entry = markers[bestA];
            exit = markers[bestB];
            return true;
        }

        private static float PairCost(in MountainTunnel tunnel, Vector3 a, Vector3 b)
        {
            var entry = new Vector2(tunnel.EntryXZ.x, tunnel.EntryXZ.y);
            var exit = new Vector2(tunnel.ExitXZ.x, tunnel.ExitXZ.y);
            var dIn = HorizontalDistance(a, entry);
            var dOut = HorizontalDistance(b, exit);
            if (dIn > SnapMaxHorizontalMeters || dOut > SnapMaxHorizontalMeters)
                return float.MaxValue;
            return dIn + dOut;
        }

        private static MountainTunnel RebuildWithMouths(
            in MountainTunnel tunnel,
            Vector3 entry,
            Vector3 exit)
        {
            var entryXZ = new Vector2(entry.x, entry.z);
            var exitXZ = new Vector2(exit.x, exit.z);
            var length = Mathf.Max(
                Vector3.Distance(entry, exit),
                Vector2.Distance(entryXZ, exitXZ));
            return new MountainTunnel(
                tunnel.StableId,
                tunnel.RoadId,
                entryXZ,
                exitXZ,
                entry.y,
                exit.y,
                length,
                tunnel.PeakCoverMeters,
                tunnel.DaylightMeters);
        }

        private static float HorizontalDistance(Vector3 world, Vector2 xz) =>
            Vector2.Distance(new Vector2(world.x, world.z), xz);

        private static void CollectCapsuleMarkers(out List<Vector3> markers)
        {
            markers = new List<Vector3>(4);
            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var tr = transforms[i];
                if (tr == null || tr.parent != null)
                    continue;
                if (tr.name.IndexOf("Capsule", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                markers.Add(tr.position);
            }
        }
    }
}
