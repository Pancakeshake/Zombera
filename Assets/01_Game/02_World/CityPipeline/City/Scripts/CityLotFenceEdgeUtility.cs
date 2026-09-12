using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Shared fence-edge enumeration for lot placement and footpath corridor exclusion.
    /// </summary>
    internal static class CityLotFenceEdgeUtility
    {
        internal const string DistrictLotsContainerName = "DistrictLots";
        private const string LegacyLotsContainerName = "ResidentialLots";

        public const float ResolveFootpathCorridorToleranceMeters = 2.2f;

        private const float RectEdgeSnap = 0.05f;
        private const float RoadSetback = 3f;
        private const float OutlineSnap = 0.15f;

        public readonly struct FenceEdgeSegment
        {
            public readonly float X0, Z0, X1, Z1;

            public FenceEdgeSegment(float x0, float z0, float x1, float z1)
            {
                X0 = x0;
                Z0 = z0;
                X1 = x1;
                Z1 = z1;
            }

            public Vector2 Start => new(X0, Z0);
            public Vector2 End => new(X1, Z1);
        }

        public struct LotFenceSource
        {
            public Rect Bounds;
            public List<Vector2> ClippedOutline;
            public bool IsCurvedLot;
        }

        public static List<Rect> CollectLotRectsFromArea(Transform areaTransform)
        {
            var rects = new List<Rect>();
            if (areaTransform == null)
                return rects;

            var lotsRoot = FindLotsContainer(areaTransform);
            if (lotsRoot == null)
                return rects;

            for (var i = 0; i < lotsRoot.childCount; i++)
            {
                if (TryGetLotRect(lotsRoot.GetChild(i), out var lotRect))
                    rects.Add(lotRect);
            }

            return rects;
        }

        public static List<LotFenceSource> CollectLotSourcesFromArea(Transform areaTransform)
        {
            var sources = new List<LotFenceSource>();
            if (areaTransform == null)
                return sources;

            var lotsRoot = FindLotsContainer(areaTransform);
            if (lotsRoot == null)
                return sources;

            for (var i = 0; i < lotsRoot.childCount; i++)
            {
                if (TryBuildLotFenceSource(lotsRoot.GetChild(i), out var source))
                    sources.Add(source);
            }

            return sources;
        }

        public static void CollectPredictedFenceEdgesForBlock(
            Rect blockBounds,
            IReadOnlyList<LotFenceSource> lots,
            IReadOnlyList<Vector2> outline,
            List<FenceEdgeSegment> edges)
        {
            if (lots == null || edges == null)
                return;

            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot.IsCurvedLot && lot.ClippedOutline != null && lot.ClippedOutline.Count >= 3)
                    AppendCurvedLotFenceEdges(lot.ClippedOutline, outline, edges);
                else
                    AppendRectLotFenceEdges(lot.Bounds, blockBounds, edges);
            }
        }

        public static void AppendRectLotFenceEdges(
            Rect lotRect, Rect blockBounds, List<FenceEdgeSegment> edges, bool fenceRoadEdges = false)
        {
            if (edges == null)
                return;

            // Industrial-style full enclosure: every edge gets a fence, road-facing
            // sides included, no 3m corner setbacks. Outer lots' road edges form the
            // district perimeter; interior edges form the between-lot fences.
            if (fenceRoadEdges)
            {
                edges.Add(new FenceEdgeSegment(lotRect.xMin, lotRect.yMin, lotRect.xMax, lotRect.yMin));
                edges.Add(new FenceEdgeSegment(lotRect.xMax, lotRect.yMin, lotRect.xMax, lotRect.yMax));
                edges.Add(new FenceEdgeSegment(lotRect.xMax, lotRect.yMax, lotRect.xMin, lotRect.yMax));
                edges.Add(new FenceEdgeSegment(lotRect.xMin, lotRect.yMax, lotRect.xMin, lotRect.yMin));
                return;
            }

            var openBottom = Mathf.Abs(lotRect.yMin - blockBounds.yMin) < RectEdgeSnap;
            var openTop = Mathf.Abs(lotRect.yMax - blockBounds.yMax) < RectEdgeSnap;
            var openLeft = Mathf.Abs(lotRect.xMin - blockBounds.xMin) < RectEdgeSnap;
            var openRight = Mathf.Abs(lotRect.xMax - blockBounds.xMax) < RectEdgeSnap;

            if (!openBottom)
            {
                edges.Add(new FenceEdgeSegment(
                    openLeft ? lotRect.xMin + RoadSetback : lotRect.xMin, lotRect.yMin,
                    openRight ? lotRect.xMax - RoadSetback : lotRect.xMax, lotRect.yMin));
            }

            if (!openRight)
            {
                edges.Add(new FenceEdgeSegment(
                    lotRect.xMax, openBottom ? lotRect.yMin + RoadSetback : lotRect.yMin,
                    lotRect.xMax, openTop ? lotRect.yMax - RoadSetback : lotRect.yMax));
            }

            if (!openTop)
            {
                edges.Add(new FenceEdgeSegment(
                    openRight ? lotRect.xMax - RoadSetback : lotRect.xMax, lotRect.yMax,
                    openLeft ? lotRect.xMin + RoadSetback : lotRect.xMin, lotRect.yMax));
            }

            if (!openLeft)
            {
                edges.Add(new FenceEdgeSegment(
                    lotRect.xMin, openTop ? lotRect.yMax - RoadSetback : lotRect.yMax,
                    lotRect.xMin, openBottom ? lotRect.yMin + RoadSetback : lotRect.yMin));
            }
        }

        public static void AppendCurvedLotFenceEdges(
            IReadOnlyList<Vector2> clip,
            IReadOnlyList<Vector2> outline,
            List<FenceEdgeSegment> edges,
            bool fenceRoadEdges = false)
        {
            if (clip == null || edges == null || clip.Count < 3)
                return;

            for (var e = 0; e < clip.Count; e++)
            {
                var a = clip[e];
                var b = clip[(e + 1) % clip.Count];
                // Road-facing edges are open unless full enclosure (industrial).
                if (!fenceRoadEdges &&
                    CityNamedAreaPolygonUtility.IsEdgeOnOuterOutline(a.x, a.y, b.x, b.y, outline, OutlineSnap))
                    continue;
                // Rounded corner edges are NOT road-facing — they get fences.
                edges.Add(new FenceEdgeSegment(a.x, a.y, b.x, b.y));
            }
        }

        private static Transform FindLotsContainer(Transform areaTransform)
        {
            var lotsRoot = areaTransform.Find(DistrictLotsContainerName);
            if (lotsRoot != null)
                return lotsRoot;

            return areaTransform.Find(LegacyLotsContainerName);
        }

        private static bool TryGetLotRect(Transform lotTransform, out Rect lotRect)
        {
            lotRect = default;
            if (lotTransform == null)
                return false;

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

            // Fallback: reconstruct lot rect from sub-zone children when the parent
            // has no mesh of its own (sub-zone pipeline removed the single fill quad).
            return TryReconstructLotRectFromChildren(lotTransform, out lotRect);
        }

        /// <summary>
        ///     Reconstructs the lot bounding rect from sub-zone child GameObjects
        ///     (e.g. Lot_XX_FrontYard, Lot_XX_Driveway) when the parent has no
        ///     MeshFilter of its own.
        /// </summary>
        private static bool TryReconstructLotRectFromChildren(Transform lotTransform, out Rect lotRect)
        {
            lotRect = default;
            var hasAny = false;

            for (var c = 0; c < lotTransform.childCount; c++)
            {
                var child = lotTransform.GetChild(c);
                var childMf = child.GetComponent<MeshFilter>();
                if (childMf == null || childMf.sharedMesh == null)
                    continue;

                var childBounds = childMf.sharedMesh.bounds;
                var childPos = child.position;
                var childRect = Rect.MinMaxRect(
                    childPos.x + childBounds.min.x, childPos.z + childBounds.min.z,
                    childPos.x + childBounds.max.x, childPos.z + childBounds.max.z);

                if (!hasAny)
                {
                    lotRect = childRect;
                    hasAny = true;
                }
                else
                {
                    lotRect.xMin = Mathf.Min(lotRect.xMin, childRect.xMin);
                    lotRect.xMax = Mathf.Max(lotRect.xMax, childRect.xMax);
                    lotRect.yMin = Mathf.Min(lotRect.yMin, childRect.yMin);
                    lotRect.yMax = Mathf.Max(lotRect.yMax, childRect.yMax);
                }
            }

            return hasAny && lotRect.width > 0.1f && lotRect.height > 0.1f;
        }

        private static bool TryBuildLotFenceSource(Transform lotTransform, out LotFenceSource source)
        {
            source = default;
            if (!TryGetLotRect(lotTransform, out var lotRect))
                return false;

            source.Bounds = lotRect;
            source.ClippedOutline = null;
            source.IsCurvedLot = false;

            // Curved-lot detection: check the parent for a curved/clipped mesh.
            // Sub-zoned lots (no parent mesh) are treated as rect lots — the
            // union of sub-zone rects captures the original bounds adequately.
            var mf = lotTransform.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return true;

            var mesh = mf.sharedMesh;
            if (mesh.vertexCount < 5 && mesh.name != "LotClipped")
                return true;

            var verts = mesh.vertices;
            var pos = lotTransform.position;
            var outline = new List<Vector2>(verts.Length);
            for (var v = 0; v < verts.Length; v++)
                outline.Add(new Vector2(pos.x + verts[v].x, pos.z + verts[v].z));

            if (outline.Count >= 3)
            {
                source.ClippedOutline = outline;
                source.IsCurvedLot = true;
                source.Bounds = CityNamedAreaPolygonUtility.ComputeBounds(outline);
            }

            return true;
        }
    }
}