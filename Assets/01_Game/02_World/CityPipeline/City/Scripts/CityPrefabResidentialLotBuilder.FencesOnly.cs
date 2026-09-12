using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Static residential lot subdivision and fence placement for city blocks.
    ///     Extracted from <see cref="CityPrefabDistrictBuilder"/>.
    /// </summary>
    internal static partial class CityPrefabResidentialLotBuilder
    {

#if UNITY_EDITOR
        private static bool TryGetLotRectForFences(Transform lotTransform, out Rect lotRect)
        {
            lotRect = default;
            if (lotTransform == null) return false;

            // Primary: parent MeshFilter (legacy single-quad lots).
            var mf = lotTransform.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var localBounds = mf.sharedMesh.bounds;
                var pos = lotTransform.position;
                lotRect = Rect.MinMaxRect(
                    pos.x + localBounds.min.x, pos.z + localBounds.min.z,
                    pos.x + localBounds.max.x, pos.z + localBounds.max.z);
                return lotRect.width > 0.1f && lotRect.height > 0.1f;
            }

            // Fallback: reconstruct from sub-zone children.
            var hasAny = false;
            for (var c = 0; c < lotTransform.childCount; c++)
            {
                var child = lotTransform.GetChild(c);
                var childMf = child.GetComponent<MeshFilter>();
                if (childMf == null || childMf.sharedMesh == null) continue;

                var childBounds = childMf.sharedMesh.bounds;
                var childPos = child.position;
                var cr = Rect.MinMaxRect(
                    childPos.x + childBounds.min.x, childPos.z + childBounds.min.z,
                    childPos.x + childBounds.max.x, childPos.z + childBounds.max.z);

                if (!hasAny) { lotRect = cr; hasAny = true; }
                else
                {
                    lotRect.xMin = Mathf.Min(lotRect.xMin, cr.xMin);
                    lotRect.xMax = Mathf.Max(lotRect.xMax, cr.xMax);
                    lotRect.yMin = Mathf.Min(lotRect.yMin, cr.yMin);
                    lotRect.yMax = Mathf.Max(lotRect.yMax, cr.yMax);
                }
            }

            return hasAny && lotRect.width > 0.1f && lotRect.height > 0.1f;
        }

        /// <summary>
        ///     Fence-only pass: clears and re-places fences on existing lot GameObjects.
        ///     Uses mesh bounds from lot fill visuals to reconstruct lot rects.
        /// </summary>
        public static int PlaceFencesOnly(Transform areasRoot, Config config)
        {
            if (areasRoot == null) return 0;

            var resolvedFence = ResolveResidentialFencePrefab(config.FencePrefab);
            if (resolvedFence == null) return 0;

            var placedEdges = new HashSet<string>();
            var fencesPlaced = 0;
            var piecesPlaced = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (var a = 0; a < areasRoot.childCount; a++)
                fencesPlaced += PlaceFencesForArea(
                    areasRoot.GetChild(a), resolvedFence, config, placedEdges, ref piecesPlaced);

            watch.Stop();
            Debug.Log(
                "[CityPrefabResidentialLotBuilder] Fences placed on " + fencesPlaced +
                " lots (" + piecesPlaced + " pieces) in " + watch.ElapsedMilliseconds + "ms.");
            return fencesPlaced;
        }

        private static int PlaceFencesForArea(
            Transform areaTransform,
            GameObject resolvedFence,
            Config config,
            HashSet<string> placedEdges,
            ref int piecesPlaced)
        {
            var marker = areaTransform.GetComponent<CityNamedAreaMarker>();
            if (marker == null || !SupportsDistrictLots(marker.DistrictType))
                return 0;

            // CityCore and Commercial: lot fills only, no fences.
            if (marker.DistrictType is CityDistrictType.CityCore or CityDistrictType.Commercial)
                return 0;

            // Industrial encloses the whole district: all four lot edges fenced,
            // road-facing sides included.
            var fenceAllEdges = marker.DistrictType == CityDistrictType.Industrial;

            var lotsContainer = areaTransform.Find(DistrictLotsContainerName);
            if (lotsContainer == null)
                return 0;

            ClearOldFenceContainer(areaTransform);
            var fenceRoot = CreateFenceContainer(areaTransform);

            // Use the actual named area bounds for open-edge detection, not the shrunk
            // ComputeLotBoundsRect result — lots were placed at marker.BoundsXZ positions.
            // Region mode: lots live at world (hub-shifted) positions while BoundsXZ is
            // template-local — comparing against the unshifted rect makes every edge
            // look non-open and fences every side of every lot (full enclosures with
            // buildings sitting on the fence lines).
            var block = marker.GetHubShiftedBoundsXZ();
            var placed = 0;
            for (var l = 0; l < lotsContainer.childCount; l++)
            {
                var pieces = PlaceFenceForLotChild(
                    lotsContainer.GetChild(l), resolvedFence, fenceRoot, block, config,
                    placedEdges, fenceAllEdges);
                piecesPlaced += pieces;
                if (pieces > 0) placed++;
            }

            return placed;
        }

        private static void ClearOldFenceContainer(Transform areaTransform)
        {
            var oldFences = areaTransform.Find("Fences");
            if (oldFences != null) Undo.DestroyObjectImmediate(oldFences.gameObject);
        }

        private static Transform CreateFenceContainer(Transform areaTransform)
        {
            var fenceRoot = new GameObject("Fences");
            fenceRoot.transform.SetParent(areaTransform, false);
            Undo.RegisterCreatedObjectUndo(fenceRoot, "Create Fence Container");
            return fenceRoot.transform;
        }

        private static int PlaceFenceForLotChild(
            Transform lotGo,
            GameObject resolvedFence,
            Transform fenceRoot,
            Rect block,
            Config config,
            HashSet<string> placedEdges,
            bool fenceAllEdges)
        {
            // Reconstruct lot rect: try parent mesh first, then fall back
            // to sub-zone children (set by the lot terrain sub-zone pipeline).
            if (!TryGetLotRectForFences(lotGo, out var lotRect))
                return 0;

            var isCurved = lotRect.width < 0.5f || lotRect.height < 0.5f;
            var lot = new LotPlacement
            {
                Bounds = lotRect,
                IsCornerLot = false,
                IsCurvedLot = isCurved,
                ClippedOutline = null
            };

            return PlaceFencesForLot(lot, resolvedFence, fenceRoot, block,
                config.FloorVisualHeight, placedEdges, config.Outline, fenceAllEdges);
        }
#endif
    }
}
