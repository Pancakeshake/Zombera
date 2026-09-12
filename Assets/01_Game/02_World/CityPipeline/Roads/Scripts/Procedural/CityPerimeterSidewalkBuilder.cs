using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Outer-city sidewalk on true perimeter block edges only — same width, material, and
    ///     lift as <see cref="ProceduralCityBlockSidewalkBuilder"/>. Breaks only at highway
    ///     ring exits with a gap equal to the highway road width.
    /// </summary>
    internal static class CityPerimeterSidewalkBuilder
    {
        private const float SharedEdgeEpsilonMeters = 0.5f;
        private const float MinEdgeSpanMeters = 1f;
        private const float MinBandSpanMeters = 0.25f;

        public static int Build(
            Transform networkRoot,
            IReadOnlyList<CityNamedArea> blocks,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralLayerMeshAccumulator accumulator = null)
        {
            if (networkRoot == null || blocks == null || blocks.Count == 0 || settings == null || resolveHeight == null)
                return 0;
            if (!settings.spawnProceduralSidewalkMeshes)
                return 0;

            var material = ResolveSidewalkMaterial(settings);
            if (material == null)
                return 0;

            var sidewalkRoot = EnsureChildFolder(networkRoot, ProceduralRoadNetworkNames.Sidewalks);
            var districtInset = CityMathBlockLayoutGenerator.ResolveDistrictBlockInsetMeters(settings);
            var roadHalf = ResolveRoadHalfWidth(settings);
            var bandWidth = Mathf.Max(MinBandSpanMeters, districtInset - roadHalf);
            var lift = Mathf.Max(0f, settings.proceduralSidewalkLiftMeters);
            var bevelWidth = Mathf.Max(0.05f, settings.proceduralSidewalkBevelWidthMeters);
            var outerEdgeDrop = Mathf.Max(0f, settings.proceduralSidewalkOuterEdgeDropMeters);
            var uvTile = Mathf.Max(0.25f, settings.footpathUvWorldUnitsPerTile);
            var highwayExitGaps = CityPerimeterSidewalkJunctionGapUtility.CollectHighwayExitGaps(
                roads, blocks, settings);
            var placed = 0;

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var blockCityCenter = ResolveBlockCityCenter(block);
                var cellRect = ExpandRect(block.boundsXZ, districtInset);
                var cellOutline = BuildCellOutline(block, districtInset);
                if (cellOutline.Count < 3 || !HasExteriorEdge(cellRect, cellOutline, blocks, block, districtInset))
                    continue;

                var spanRun = new List<Vector2>(32);
                for (var e = 0; e < cellOutline.Count; e++)
                {
                    var a = cellOutline[e];
                    var b = cellOutline[(e + 1) % cellOutline.Count];
                    if (!IsExteriorCellEdge(cellRect, a, b, blocks, block, districtInset) ||
                        CityPerimeterSidewalkHighwayCapUtility.IsOutboundHighwayEdge(a, b, roads, blockCityCenter))
                    {
                        placed += FlushRun(
                            sidewalkRoot, block.id, placed, spanRun, blockCityCenter, highwayExitGaps,
                            roadHalf, bandWidth, material, resolveHeight, lift, bevelWidth, outerEdgeDrop, uvTile,
                            accumulator);
                        continue;
                    }

                    AppendPoint(spanRun, a);
                    AppendPoint(spanRun, b);
                }

                placed += FlushRun(
                    sidewalkRoot, block.id, placed, spanRun, blockCityCenter, highwayExitGaps,
                    roadHalf, bandWidth, material, resolveHeight, lift, bevelWidth, outerEdgeDrop, uvTile,
                    accumulator);
            }

            return placed;
        }

        private static int FlushRun(
            Transform parent,
            int blockId,
            int stripIndex,
            List<Vector2> spanRun,
            Vector2 cityCenter,
            IReadOnlyList<CityPerimeterSidewalkJunctionGapUtility.HighwayExitGap> highwayExitGaps,
            float roadHalf,
            float bandWidth,
            Material material,
            Func<Vector2, float> resolveHeight,
            float lift,
            float bevelWidth,
            float outerEdgeDrop,
            float uvTile,
            ProceduralLayerMeshAccumulator accumulator)
        {
            if (spanRun.Count < 2)
            {
                spanRun.Clear();
                return 0;
            }

            DedupeConsecutive(spanRun);
            var spans = CityPerimeterSidewalkJunctionGapUtility.SplitSpanAtHighwayExitGaps(
                spanRun, highwayExitGaps, cityCenter);
            spanRun.Clear();
            var placed = 0;

            for (var s = 0; s < spans.Count; s++)
            {
                var span = spans[s];
                if (span == null || span.Count < 2 || PolylineLength(span) < MinEdgeSpanMeters)
                    continue;

                if (!BuildExteriorSidewalkBand(
                        parent,
                        $"Sidewalk_Perimeter_{blockId}_{stripIndex}_{s}",
                        span,
                        cityCenter,
                        roadHalf,
                        bandWidth,
                        material,
                        resolveHeight,
                        lift,
                        bevelWidth,
                        outerEdgeDrop,
                        uvTile,
                        accumulator))
                    continue;

                placed++;
            }

            return placed;
        }

        private static bool BuildExteriorSidewalkBand(
            Transform parent,
            string objectName,
            IReadOnlyList<Vector2> span,
            Vector2 cityCenter,
            float roadHalf,
            float bandWidth,
            Material material,
            Func<Vector2, float> resolveHeight,
            float lift,
            float bevelWidth,
            float outerEdgeDrop,
            float uvTile,
            ProceduralLayerMeshAccumulator accumulator)
        {
            var inner = new List<Vector2>(span.Count);
            var outer = new List<Vector2>(span.Count);
            for (var i = 0; i < span.Count; i++)
            {
                var prev = span[Mathf.Max(0, i - 1)];
                var next = span[Mathf.Min(span.Count - 1, i + 1)];
                var outward = ResolveOutwardNormal(prev, next, cityCenter);
                inner.Add(span[i] + outward * roadHalf);
                outer.Add(span[i] + outward * (roadHalf + bandWidth));
            }

            float SampleBase(Vector2 xz) => resolveHeight(xz);
            var mesh = RoadMeshBuilder.BuildBeveledBandMeshBetweenPolylines(
                outer, inner, SampleBase, uvTile, bevelWidth, lift, outerEdgeDrop);
            if (mesh == null)
                return false;

            return ProceduralMeshEmitUtility.Emit(
                parent,
                ProceduralRoadNetworkNames.Sidewalks,
                objectName,
                mesh,
                material,
                accumulator);
        }

        private static bool HasExteriorEdge(
            Rect cellRect,
            IReadOnlyList<Vector2> cellOutline,
            IReadOnlyList<CityNamedArea> blocks,
            CityNamedArea block,
            float districtInset)
        {
            for (var e = 0; e < cellOutline.Count; e++)
            {
                var a = cellOutline[e];
                var b = cellOutline[(e + 1) % cellOutline.Count];
                if (IsExteriorCellEdge(cellRect, a, b, blocks, block, districtInset))
                    return true;
            }

            return false;
        }

        private static List<Vector2> BuildCellOutline(CityNamedArea block, float districtInset)
        {
            var cellRect = ExpandRect(block.boundsXZ, districtInset);
            if (block.HasRoundedOutline)
            {
                return CityNamedAreaOutlineBuilder.BuildMeshOutline(
                    cellRect,
                    block.roundedCorners,
                    block.arterialCornerRadiusMeters,
                    0f);
            }

            return CityNamedAreaOutlineBuilder.BuildMeshOutline(
                cellRect,
                CityBlockCornerMask.None,
                0f,
                0f);
        }

        private static bool IsExteriorCellEdge(
            Rect cellRect,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            IReadOnlyList<CityNamedArea> blocks,
            CityNamedArea self,
            float districtInset)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var other = blocks[i];
                if (!IsSameCity(self, other))
                    continue;

                var otherCell = ExpandRect(other.boundsXZ, districtInset);
                if (CityBlockEdgeAdjacencyUtility.IsSharedStreetBlockEdge(
                        cellRect, otherCell, edgeStart, edgeEnd, SharedEdgeEpsilonMeters))
                    return false;
            }

            return true;
        }

        private static Vector2 ResolveOutwardNormal(Vector2 edgeStart, Vector2 edgeEnd, Vector2 cityCenter)
        {
            var tangent = (edgeEnd - edgeStart).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;

            var left = new Vector2(-tangent.y, tangent.x);
            var right = new Vector2(tangent.y, -tangent.x);
            var mid = (edgeStart + edgeEnd) * 0.5f;
            var toCenter = cityCenter - mid;
            if (toCenter.sqrMagnitude < 0.0001f)
                return -left;

            var inward = Vector2.Dot(left, toCenter) >= Vector2.Dot(right, toCenter) ? left : right;
            return -inward;
        }

        private static bool IsSameCity(CityNamedArea a, CityNamedArea b) =>
            (a.cityCenterXZ - b.cityCenterXZ).sqrMagnitude < 1f;

        private static Vector2 ResolveBlockCityCenter(CityNamedArea block) =>
            block.cityCenterXZ.sqrMagnitude > 0.01f ? block.cityCenterXZ : block.centerXZ;

        private static void AppendPoint(List<Vector2> run, Vector2 point)
        {
            if (run.Count > 0 && (run[^1] - point).sqrMagnitude < 0.0001f)
                return;
            run.Add(point);
        }

        private static void DedupeConsecutive(List<Vector2> points)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude < 0.0001f)
                    points.RemoveAt(i);
            }
        }

        private static float PolylineLength(IReadOnlyList<Vector2> polyline)
        {
            var length = 0f;
            for (var i = 1; i < polyline.Count; i++)
                length += Vector2.Distance(polyline[i - 1], polyline[i]);
            return length;
        }

        private static float ResolveRoadHalfWidth(RoadNetworkSettings settings)
        {
            var width = settings != null ? settings.ResolveWidthMeters(RoadClass.Local) : 6f;
            return Mathf.Max(1.5f, width * 0.5f);
        }

        private static Rect ExpandRect(Rect rect, float padding) =>
            Rect.MinMaxRect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.xMax + padding,
                rect.yMax + padding);

        private static Material ResolveSidewalkMaterial(RoadNetworkSettings settings)
        {
            if (settings.sidewalkMaterial != null)
                return settings.sidewalkMaterial;
            if (settings.kerbMaterial != null)
                return settings.kerbMaterial;

#if UNITY_EDITOR
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                RoadKitPrefabs.DefaultLotSidewalkConcreteMaterialPath);
            if (material != null)
                return material;
#endif
            return CityFootpathPlacer.ResolveFootpathMaterialForSettings(settings);
        }

        private static Transform EnsureChildFolder(Transform parent, string folderName)
        {
            var existing = parent.Find(folderName);
            if (existing != null)
                return existing;

            var folder = new GameObject(folderName);
            folder.transform.SetParent(parent, false);
            return folder.transform;
        }
    }
}
