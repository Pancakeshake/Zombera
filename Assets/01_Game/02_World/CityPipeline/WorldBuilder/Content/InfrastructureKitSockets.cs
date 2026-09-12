using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Travel sockets for modular road kits.
    ///     Mid: Socket_Start/End on local Z.
    ///     Portal (correct rot: forward toward approach): Socket_Approach on +Z road face,
    ///     Socket_Bore on −Z into the mountain. Socket_Mouth aliases Approach for snap.
    /// </summary>
    public static class InfrastructureKitSockets
    {
        public const string SocketStart = "Socket_Start";
        public const string SocketEnd = "Socket_End";
        public const string SocketMouth = "Socket_Mouth";
        public const string SocketCollar = "Socket_Collar";
        public const string SocketApproach = "Socket_Approach";
        public const string SocketBore = "Socket_Bore";
        public const string SocketDeck = "Socket_Deck";
        public const string SocketTop = "Socket_Top";
        public const string SocketApproachIn = "Socket_Approach_In";
        public const string SocketApproachOut = "Socket_Approach_Out";

        public static void EnsureMidSockets(GameObject root)
        {
            if (root == null || !TryEncapsulateRootLocalMesh(root, out var min, out var max))
                return;
            EnsureSocket(root, SocketStart, new Vector3(0f, 0f, min.z), preserveExisting: true);
            EnsureSocket(root, SocketEnd, new Vector3(0f, 0f, max.z), preserveExisting: true);
        }

        /// <summary>
        ///     Bridge abutment convention: local +Z faces the road approach and
        ///     local -Z faces the bridge deck. Authored sockets always win.
        /// </summary>
        public static void EnsureBridgeAbutmentSockets(GameObject root)
        {
            if (root == null || !TryEncapsulateRootLocalMesh(root, out var min, out var max))
                return;
            EnsureSocket(root, SocketApproach, new Vector3(0f, 0f, max.z), preserveExisting: true);
            EnsureSocket(root, SocketDeck, new Vector3(0f, 0f, min.z), preserveExisting: true);
        }

        public static void EnsureBridgePierSocket(GameObject root)
        {
            if (root == null || !TryEncapsulateRootLocalMesh(root, out _, out var max))
                return;
            EnsureSocket(root, SocketTop, new Vector3(0f, max.y, 0f), preserveExisting: true);
        }

        public static void EnsurePortalSockets(GameObject root)
        {
            if (root == null)
                return;

            var approachZ = 0f;
            var boreZ = 0f;
            if (TryEncapsulateRootLocalMesh(root, out var min, out var max))
            {
                // After placer +180, local +Z faces the road; mesh max.z is the approach face.
                approachZ = max.z;
                boreZ = min.z;
            }

            // Authored portal sockets are the kit contract. Only synthesize them for
            // legacy prefabs; never move existing sockets to a coarse mesh bound.
            EnsureSocket(root, SocketApproach, new Vector3(0f, 0f, approachZ), preserveExisting: true);
            EnsureSocket(root, SocketBore, new Vector3(0f, 0f, boreZ), preserveExisting: true);
            // Mouth / Collar kept for older callers; Mouth = approach abutment, Collar = bore.
            EnsureSocket(root, SocketMouth, new Vector3(0f, 0f, approachZ), preserveExisting: true);
            EnsureSocket(root, SocketCollar, new Vector3(0f, 0f, boreZ), preserveExisting: true);
        }

        /// <summary>
        ///     Resolves the authored portal's horizontal terrain-cut footprint.
        ///     Width comes from the root-local mesh bounds; inward depth comes from
        ///     Socket_Approach to Socket_Bore when the prefab authors those sockets.
        /// </summary>
        public static bool TryGetPortalCutFootprint(
            GameObject root,
            out float halfWidthMeters,
            out float inwardDepthMeters)
        {
            halfWidthMeters = 0f;
            inwardDepthMeters = 0f;
            if (root == null || !TryEncapsulateRootLocalMesh(root, out var min, out var max))
                return false;

            halfWidthMeters = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x));
            inwardDepthMeters = Mathf.Abs(max.z - min.z);

            var approach = FindSocket(root.transform, SocketApproach);
            var bore = FindSocket(root.transform, SocketBore);
            if (approach != null && bore != null)
            {
                var approachLocal = root.transform.InverseTransformPoint(approach.position);
                var boreLocal = root.transform.InverseTransformPoint(bore.position);
                inwardDepthMeters = Mathf.Abs(approachLocal.z - boreLocal.z);
            }

            return halfWidthMeters > 0.1f && inwardDepthMeters > 0.1f;
        }

        /// <summary>
        ///     Measures the lower clear opening of a tunnel shell, excluding its
        ///     outer walls and roof. This is the width a Terrain mouth hole needs.
        /// </summary>
        public static bool TryGetBoreClearHalfWidth(GameObject root, out float halfWidthMeters)
        {
            halfWidthMeters = 0f;
            if (root == null || !TryEncapsulateRootLocalMesh(root, out var min, out var max))
                return false;

            var height = Mathf.Max(0f, max.y - min.y);
            var sampleMinY = min.y + Mathf.Max(0.15f, height * 0.04f);
            var sampleMaxY = min.y + Mathf.Min(2f, height * 0.35f);
            var best = float.MaxValue;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
                AccumulateClearHalfWidth(root.transform, filters[i], sampleMinY, sampleMaxY, ref best);

            if (best == float.MaxValue)
                return false;
            halfWidthMeters = best;
            return halfWidthMeters > 0.1f;
        }

        public static Transform FindSocket(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindSocket(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static float LocalZLength(Transform start, Transform end)
        {
            if (start == null || end == null)
                return 0f;
            return Mathf.Abs(end.localPosition.z - start.localPosition.z);
        }

        public static void MoveSocketToWorld(Transform instance, Transform socket, Vector3 worldPosition)
        {
            if (instance == null || socket == null)
                return;
            instance.position += worldPosition - socket.position;
        }

        public static bool TryEncapsulateRootLocalMesh(GameObject root, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            if (root == null)
                return false;

            var found = false;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;
                AccumulateMeshBounds(root.transform, filter, ref min, ref max);
                found = true;
            }

            return found;
        }

        private static void AccumulateMeshBounds(
            Transform root,
            MeshFilter filter,
            ref Vector3 min,
            ref Vector3 max)
        {
            var bounds = filter.sharedMesh.bounds;
            var minB = bounds.min;
            var maxB = bounds.max;
            for (var ix = 0; ix < 2; ix++)
            for (var iy = 0; iy < 2; iy++)
            for (var iz = 0; iz < 2; iz++)
            {
                var corner = new Vector3(
                    ix == 0 ? minB.x : maxB.x,
                    iy == 0 ? minB.y : maxB.y,
                    iz == 0 ? minB.z : maxB.z);
                var local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        private static void AccumulateClearHalfWidth(
            Transform root,
            MeshFilter filter,
            float sampleMinY,
            float sampleMaxY,
            ref float best)
        {
            if (filter == null || filter.sharedMesh == null)
                return;

            var vertices = filter.sharedMesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                var world = filter.transform.TransformPoint(vertices[i]);
                var local = root.InverseTransformPoint(world);
                if (local.y < sampleMinY || local.y > sampleMaxY)
                    continue;
                var absoluteX = Mathf.Abs(local.x);
                if (absoluteX <= 0.1f || absoluteX >= best)
                    continue;
                best = absoluteX;
            }
        }

        private static void EnsureSocket(
            GameObject root,
            string name,
            Vector3 localPosition,
            bool preserveExisting = false)
        {
            var existing = FindSocket(root.transform, name);
            if (existing != null)
            {
                if (preserveExisting)
                    return;
                existing.SetParent(root.transform, false);
                existing.localPosition = localPosition;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
    }
}
