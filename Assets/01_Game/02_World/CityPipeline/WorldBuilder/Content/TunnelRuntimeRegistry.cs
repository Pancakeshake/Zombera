using System.Collections.Generic;
using UnityEngine;
using Zombera.Systems;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Session-scoped enterable tunnel floors + bore volumes for NavMesh and grounding.
    ///     Cleared on reset / SetTunnels(null) / placer Clear.
    /// </summary>
    public static class TunnelRuntimeRegistry
    {
        private struct BoreVolume
        {
            public Vector2 EntryXZ;
            public Vector2 ExitXZ;
            public float MinY;
            public float MaxY;
            public float HalfWidth;
        }

        private static readonly List<MeshCollider> Floors = new(64);
        private static readonly List<BoreVolume> Bores = new(16);

        public static int FloorCount => Floors.Count;

        public static void Clear()
        {
            Floors.Clear();
            Bores.Clear();
            SyncUndergroundGate();
        }

        public static void RegisterBore(MountainTunnel tunnel, float clearHalfWidthMeters, float clearHeightMeters)
        {
            var bedMin = Mathf.Min(tunnel.EntryWorldY, tunnel.ExitWorldY) - 1f;
            var bedMax = Mathf.Max(tunnel.EntryWorldY, tunnel.ExitWorldY) + Mathf.Max(2f, clearHeightMeters);
            Bores.Add(new BoreVolume
            {
                EntryXZ = tunnel.EntryXZ,
                ExitXZ = tunnel.ExitXZ,
                MinY = bedMin,
                MaxY = bedMax,
                HalfWidth = Mathf.Max(1f, clearHalfWidthMeters)
            });
            SyncUndergroundGate();
        }

        public static void RegisterFloor(MeshCollider collider)
        {
            if (collider == null)
                return;
            Floors.Add(collider);
        }

        public static bool IsInsideEnterableBore(Vector3 worldPos)
        {
            if (Bores.Count == 0)
                return false;

            var xz = new Vector2(worldPos.x, worldPos.z);
            for (var i = 0; i < Bores.Count; i++)
            {
                var bore = Bores[i];
                if (worldPos.y < bore.MinY || worldPos.y > bore.MaxY)
                    continue;

                var closest = ClosestPointOnSegment(xz, bore.EntryXZ, bore.ExitXZ);
                if (Vector2.Distance(xz, closest) <= bore.HalfWidth)
                    return true;
            }

            return false;
        }

        public static void CollectFloorsIntersecting(Bounds worldBounds, List<MeshCollider> results)
        {
            if (results == null)
                return;

            for (var i = 0; i < Floors.Count; i++)
            {
                var col = Floors[i];
                if (col == null)
                    continue;
                if (!col.bounds.Intersects(worldBounds))
                    continue;
                results.Add(col);
            }
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        private static void SyncUndergroundGate()
        {
            UndergroundTraversalGate.IsInsideUndergroundVolume =
                Bores.Count == 0 ? null : IsInsideEnterableBore;
        }
    }
}
