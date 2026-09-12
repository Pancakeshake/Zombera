#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.World.City
{
    internal static partial class CityPrefabResidentialLotBuilder
    {
        /// <summary>Reused edge buffer — fence placement is single-threaded and non-reentrant.</summary>
        private static readonly List<CityLotFenceEdgeUtility.FenceEdgeSegment> FenceEdgeScratch = new();

        /// <summary>Piece length per resolved fence prefab (socket span, else renderer-bounds estimate).</summary>
        private static readonly Dictionary<GameObject, float> FencePieceLengthCache = new();

        private static GameObject ResolveResidentialFencePrefab(GameObject fencePrefab)
        {
            var resolvedFence = fencePrefab;
            if (resolvedFence == null)
            {
                resolvedFence = AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefabPath);
                if (resolvedFence == null)
                    Debug.LogWarning("[CityPrefabResidentialLotBuilder] Fence prefab not found.");
            }

            var proxyFence = ResolveProxyFence(resolvedFence);
            return proxyFence != null ? proxyFence : resolvedFence;
        }

        private static int PlaceFencesForLot(
            LotPlacement lot, GameObject fence, Transform parent, Rect blockBounds,
            float floorVisualHeight, HashSet<string> placedEdges,
            IReadOnlyList<Vector2> outline, bool fenceAllEdges = false)
        {
            var groundY = parent.position.y + floorVisualHeight + 0.01f;

            if ((lot.IsCurvedLot || lot.IsCornerLot) && lot.ClippedOutline != null && lot.ClippedOutline.Count >= 3)
                return PlaceCurvedLotFences(lot.ClippedOutline, outline, fence, parent, groundY, placedEdges, fenceAllEdges);

            return PlaceRectLotFences(lot.Bounds, blockBounds, fence, parent, groundY, placedEdges, fenceAllEdges);
        }

        private static int PlaceCurvedLotFences(
            List<Vector2> clip, IReadOnlyList<Vector2> outline,
            GameObject fence, Transform parent, float groundY, HashSet<string> placedEdges,
            bool fenceAllEdges)
        {
            var edges = FenceEdgeScratch;
            edges.Clear();
            CityLotFenceEdgeUtility.AppendCurvedLotFenceEdges(clip, outline, edges, fenceAllEdges);

            var pieces = 0;
            for (var i = 0; i < edges.Count; i++)
                pieces += TryPlaceFenceEdge(edges[i], fence, parent, groundY, placedEdges);

            return pieces;
        }

        private static int PlaceRectLotFences(
            Rect lotRect, Rect blockBounds,
            GameObject fence, Transform parent, float groundY, HashSet<string> placedEdges,
            bool fenceAllEdges)
        {
            var edges = FenceEdgeScratch;
            edges.Clear();
            CityLotFenceEdgeUtility.AppendRectLotFenceEdges(lotRect, blockBounds, edges, fenceAllEdges);

            var pieces = 0;
            for (var i = 0; i < edges.Count; i++)
                pieces += TryPlaceFenceEdge(edges[i], fence, parent, groundY, placedEdges);

            return pieces;
        }

        private static int TryPlaceFenceEdge(
            CityLotFenceEdgeUtility.FenceEdgeSegment edge, GameObject fencePrefab, Transform parent, float groundY, HashSet<string> placedEdges)
        {
            var ax = edge.X0 < edge.X1 ? edge.X0 : edge.X1;
            var bx = edge.X0 < edge.X1 ? edge.X1 : edge.X0;
            var az = edge.Z0 < edge.Z1 ? edge.Z0 : edge.Z1;
            var bz = edge.Z0 < edge.Z1 ? edge.Z1 : edge.Z0;
            var key = Mathf.RoundToInt(ax * 10f) + "," + Mathf.RoundToInt(az * 10f) + "-"
                    + Mathf.RoundToInt(bx * 10f) + "," + Mathf.RoundToInt(bz * 10f);

            if (!placedEdges.Add(key)) return 0;

            return PlaceFenceEdge(edge, fencePrefab, parent, groundY);
        }

        private static int PlaceFenceEdge(
            CityLotFenceEdgeUtility.FenceEdgeSegment edge, GameObject fencePrefab, Transform parent, float groundY)
        {
            var x0 = edge.X0; var z0 = edge.Z0;
            var x1 = edge.X1; var z1 = edge.Z1;
            var totalLength = Mathf.Max(0.5f, Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1)));
            var dir = new Vector3(x1 - x0, 0f, z1 - z0).normalized;
            var quat = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);

            // Cached per prefab — socket-span lookup and renderer-bounds scan used to
            // re-run for every edge even though the fence prefab never changes mid-pass.
            var pieceLength = ResolveFencePieceLength(fencePrefab);

            var fullPieces = Mathf.FloorToInt(totalLength / pieceLength);
            var remainder = totalLength - fullPieces * pieceLength;
            var start = new Vector3(x0, groundY, z0);

            // No per-piece undo: the parent Fences / DistrictLots container is registered
            // once per area, and undo of that container covers all fence children.
            var pieces = 0;
            for (var i = 0; i < fullPieces; i++)
            {
                var pos = start + dir * pieceLength * (i + 0.5f);
                var instance = PrefabUtility.InstantiatePrefab(fencePrefab, parent) as GameObject;
                if (instance == null) continue;
                instance.transform.SetPositionAndRotation(pos, quat);
                pieces++;
            }

            if (remainder > 0.05f)
            {
                var pos = start + dir * (fullPieces * pieceLength + remainder * 0.5f);
                var instance = PrefabUtility.InstantiatePrefab(fencePrefab, parent) as GameObject;
                if (instance != null)
                {
                    instance.transform.SetPositionAndRotation(pos, quat);
                    var scale = instance.transform.localScale;
                    scale.x = remainder / pieceLength;
                    instance.transform.localScale = scale;
                    pieces++;
                }
            }

            return pieces;
        }

        private static float ResolveFencePieceLength(GameObject fencePrefab)
        {
            if (fencePrefab == null) return 0f;

            if (FencePieceLengthCache.TryGetValue(fencePrefab, out var cached))
                return cached;

            var length = MeasureFenceSocketSpan(fencePrefab);
            if (length < 0.01f) length = EstimateFencePrefabLength(fencePrefab);
            if (length < 0.01f) length = 1f;

            FencePieceLengthCache[fencePrefab] = length;
            return length;
        }

        private static float MeasureFenceSocketSpan(GameObject prefab)
        {
            if (prefab == null) return 0f;

            var left = prefab.transform.Find("SnapSocket_Left");
            var right = prefab.transform.Find("SnapSocket_Right");
            if (left == null || right == null) return 0f;

            return Mathf.Abs(right.localPosition.x - left.localPosition.x);
        }

        private static GameObject ResolveProxyFence(GameObject original)
        {
            if (original == null) return null;

            var originalPath = AssetDatabase.GetAssetPath(original);
            if (string.IsNullOrEmpty(originalPath)) return null;

            var dir = System.IO.Path.GetDirectoryName(originalPath).Replace('\\', '/');
            var name = System.IO.Path.GetFileNameWithoutExtension(originalPath);
            var proxyPath = dir + "/Proxies/" + name + "_Proxy.prefab";

            return AssetDatabase.LoadAssetAtPath<GameObject>(proxyPath);
        }

        private static float EstimateFencePrefabLength(GameObject prefab)
        {
            if (prefab == null) return 1f;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1f;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return Mathf.Max(0.5f, bounds.size.x);
        }
    }
}
#endif
