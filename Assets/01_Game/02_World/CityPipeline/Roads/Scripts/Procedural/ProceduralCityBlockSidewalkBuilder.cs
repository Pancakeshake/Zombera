using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Fills city block cells with sidewalk rings that follow the same rounded outlines as
    ///     named district areas.
    /// </summary>
    public static class ProceduralCityBlockSidewalkBuilder
    {
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
            {
                Debug.LogWarning(
                    "[ProceduralCityBlockSidewalkBuilder] Sidewalk material missing — skipping block sidewalks.",
                    networkRoot);
                return 0;
            }

            var sidewalkRoot = EnsureChildFolder(networkRoot, ProceduralRoadNetworkNames.Sidewalks);
            var districtInset = CityMathBlockLayoutGenerator.ResolveDistrictBlockInsetMeters(settings);
            var roadHalf = ResolveRoadHalfWidth(settings);
            var bandWidth = Mathf.Max(MinBandSpanMeters, districtInset - roadHalf);
            var lift = Mathf.Max(0f, settings.proceduralSidewalkLiftMeters);
            var bevelWidth = Mathf.Max(0.05f, settings.proceduralSidewalkBevelWidthMeters);
            var outerEdgeDrop = Mathf.Max(0f, settings.proceduralSidewalkOuterEdgeDropMeters);
            var uvTile = Mathf.Max(0.25f, settings.footpathUvWorldUnitsPerTile);
            var placed = 0;

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block.boundsXZ.width < MinBandSpanMeters || block.boundsXZ.height < MinBandSpanMeters)
                    continue;

                placed += PlaceBlockRing(
                    sidewalkRoot,
                    block,
                    districtInset,
                    roadHalf,
                    bandWidth,
                    material,
                    resolveHeight,
                    lift,
                    bevelWidth,
                    outerEdgeDrop,
                    uvTile,
                    accumulator);
            }

            return placed;
        }

        private static int PlaceBlockRing(
            Transform parent,
            CityNamedArea block,
            float districtInset,
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
            var cellOutline = BuildCellOutline(block, districtInset);
            if (cellOutline.Count < 3)
                return 0;

            var outerRing = CityNamedAreaOutlineBuilder.InsetOutlineUniform(cellOutline, roadHalf);
            var innerRing = CityNamedAreaOutlineBuilder.InsetOutlineUniform(cellOutline, roadHalf + bandWidth);
            if (outerRing.Count < 3 || innerRing.Count < 3)
                return 0;

            var outerRun = new List<Vector2>(outerRing.Count + 1);
            var innerRun = new List<Vector2>(innerRing.Count + 1);
            for (var i = 0; i < cellOutline.Count; i++)
            {
                var next = (i + 1) % cellOutline.Count;
                AppendRingPoint(outerRun, outerRing[i]);
                AppendRingPoint(outerRun, outerRing[next]);
                AppendRingPoint(innerRun, innerRing[i]);
                AppendRingPoint(innerRun, innerRing[next]);
            }

            return FlushRun(
                parent, block.id, outerRun, innerRun, material, resolveHeight, lift, bevelWidth, outerEdgeDrop, uvTile,
                accumulator);
        }

        private static int FlushRun(
            Transform parent,
            int blockId,
            List<Vector2> outerRun,
            List<Vector2> innerRun,
            Material material,
            Func<Vector2, float> resolveHeight,
            float lift,
            float bevelWidth,
            float outerEdgeDrop,
            float uvTile,
            ProceduralLayerMeshAccumulator accumulator)
        {
            if (outerRun.Count < 2 || innerRun.Count < 2)
                return 0;

            DedupeConsecutive(outerRun);
            DedupeConsecutive(innerRun);
            if (outerRun.Count < 2 || innerRun.Count < 2)
                return 0;

            var mesh = RoadMeshBuilder.BuildBeveledBandMeshBetweenPolylines(
                outerRun, innerRun, resolveHeight, uvTile, bevelWidth, lift, outerEdgeDrop);
            if (mesh == null)
                return 0;

            return ProceduralMeshEmitUtility.Emit(
                parent,
                ProceduralRoadNetworkNames.Sidewalks,
                $"Sidewalk_Block_{blockId}",
                mesh,
                material,
                accumulator)
                ? 1
                : 0;
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

        private static void AppendRingPoint(List<Vector2> run, Vector2 point)
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
