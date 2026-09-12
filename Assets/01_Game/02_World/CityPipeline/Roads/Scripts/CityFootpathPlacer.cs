using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Builds continuous footpath mesh strips outside sidewalks along road centerlines.
    ///     Used by the City Prefab Hub after road generation (step 2).
    /// </summary>
    public static class CityFootpathPlacer
    {
        public static int PlaceFootpathsForRoad(
            Transform parent,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect boundRect,
            Func<Vector2, float> resolveHeight,
            int roadIndex)
        {
            if (parent == null || road == null || road.pointsXZ == null || road.pointsXZ.Count < 2)
                return 0;
            if (settings == null || !settings.spawnFootpathMeshes || resolveHeight == null)
                return 0;

            var material = ResolveFootpathMaterial(settings);
            if (material == null)
                return 0;

            var footpathWidth = Mathf.Max(0.5f, settings.footpathWidthMeters);
            var halfRoad = Mathf.Max(0.5f, road.widthMeters) * 0.5f;
            var sidewalkWidth = ResolveEffectiveSidewalkWidthMeters(settings);
            var curbExtra = Mathf.Max(0f, settings.footpathCurbExtraMeters);
            var innerOffset = halfRoad + sidewalkWidth + curbExtra;
            var outerOffset = innerOffset + footpathWidth;
            var lift = Mathf.Max(0f, settings.footpathSurfaceLiftMeters);
            var aboveFill = Mathf.Max(0f, settings.footpathAboveDistrictFillMeters);
            var totalLift = lift + aboveFill;
            var setback = Mathf.Max(0f, settings.footpathJunctionSetbackMeters);
            var uvTile = Mathf.Max(0.25f, settings.footpathUvWorldUnitsPerTile);

            var polyline = new List<Vector2>(road.pointsXZ);
            polyline = RoadMeshBuilder.TrimPolylineEnds(polyline, setback);
            if (polyline == null || polyline.Count < 2)
                return 0;

            float SampleY(Vector2 xz) => resolveHeight(xz) + totalLift;

            var placed = 0;
            placed += TryCreateStrip(parent, polyline, innerOffset, outerOffset, -1, material, boundRect,
                SampleY, uvTile, $"Footpath_L_Road{road.id}_{roadIndex}");
            placed += TryCreateStrip(parent, polyline, innerOffset, outerOffset, 1, material, boundRect,
                SampleY, uvTile, $"Footpath_R_Road{road.id}_{roadIndex}");
            return placed;
        }

        private static int TryCreateStrip(
            Transform parent,
            List<Vector2> centerPolylineXZ,
            float innerOffsetMeters,
            float outerOffsetMeters,
            int sideSign,
            Material material,
            Rect boundRect,
            Func<Vector2, float> sampleHeight,
            float uvWorldUnitsPerTile,
            string objectName)
        {
            if (!PolylineOverlapsBounds(centerPolylineXZ, boundRect, innerOffsetMeters))
                return 0;

            var mesh = RoadMeshBuilder.BuildAsymmetricStripMeshWithHeightSampler(
                centerPolylineXZ,
                innerOffsetMeters,
                outerOffsetMeters,
                sideSign,
                sampleHeight,
                uvWorldUnitsPerTile);
            if (mesh == null)
                return 0;

            var part = new GameObject(objectName);
            part.transform.SetParent(parent, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(part, "Create Footpath Strip");
#endif
            var meshFilter = part.AddComponent<MeshFilter>();
            var meshRenderer = part.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.sharedMaterial.renderQueue = 2002;
            return 1;
        }

        private static bool PolylineOverlapsBounds(
            List<Vector2> polylineXZ,
            Rect boundRect,
            float lateralOffsetMeters)
        {
            if (polylineXZ == null || polylineXZ.Count == 0)
                return false;

            var expanded = boundRect;
            expanded.xMin -= lateralOffsetMeters;
            expanded.xMax += lateralOffsetMeters;
            expanded.yMin -= lateralOffsetMeters;
            expanded.yMax += lateralOffsetMeters;

            for (var i = 0; i < polylineXZ.Count; i++)
            {
                if (expanded.Contains(polylineXZ[i]))
                    return true;
            }

            return false;
        }

        private static float ResolveEffectiveSidewalkWidthMeters(RoadNetworkSettings settings)
        {
            if (settings == null)
                return 0f;

            if (ProceduralRoadSystem.NativeSidewalksConfigured || settings.spawnSidewalkMeshes)
                return settings.ResolveSidewalkWidthMeters();

            return 0f;
        }

        private static Material ResolveFootpathMaterial(RoadNetworkSettings settings)
        {
            if (settings.footpathMaterial != null)
                return settings.footpathMaterial;
            return settings.localMaterial ?? settings.roadMaterial;
        }

        public static Material ResolveFootpathMaterialForSettings(RoadNetworkSettings settings) =>
            settings != null ? ResolveFootpathMaterial(settings) : null;
    }
}