using System;
using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {

        public void ClearNamedAreas()
        {
            DestroyAllAreaMarkersUnderHub();
        }

        public void ClearDistrictLots()
        {
            var container = transform.Find(AreasContainerName);
            if (container == null) return;

            for (var a = 0; a < container.childCount; a++)
            {
                var area = container.GetChild(a);
                ClearDistrictChildContainer(area, "DistrictLots");
                ClearDistrictChildContainer(area, "Fences");
            }
        }

        /// <summary>
        ///     Apply <see cref="CityBuildConfig.showDistrictFillColors"/> to every
        ///     fill MeshRenderer in the named-areas hierarchy —
        ///     both top-level DistrictFill and per-lot fills under DistrictLots.
        ///     Call after changing the toggle — no regeneration needed.
        /// </summary>
        [ContextMenu("Sync District Fill Visibility")]
        public void SyncDistrictFillVisibility()
        {
            var container = transform.Find(AreasContainerName);
            if (container == null) return;

            var showDistrict = buildConfig != null && buildConfig.showDistrictFillColors;
            var changed = ToggleFillVisibility(container, showDistrict);

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
#endif

            Debug.Log(
                $"[CityPrefabRoadNetworkBuilder] District fill visibility: {(showDistrict ? "ON" : "OFF")} — {changed} renderer(s) toggled.",
                this);
        }

        private static int ToggleFillVisibility(Transform container, bool show)
        {
            var changed = 0;
            for (var a = 0; a < container.childCount; a++)
            {
                var area = container.GetChild(a);

                // Top-level DistrictFill (area-colour overlay).
                var areaFill = area.Find("DistrictFill");
                if (areaFill != null)
                    changed += ToggleRendererEnabled(areaFill.GetComponent<MeshRenderer>(), show, "District Fill");

                // Per-lot fills under DistrictLots / ResidentialLots.
                var lotsRoot = area.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                    ?? area.Find("ResidentialLots");
                if (lotsRoot == null) continue;

                for (var l = 0; l < lotsRoot.childCount; l++)
                    changed += ToggleRendererEnabled(lotsRoot.GetChild(l).GetComponent<MeshRenderer>(), show, "Lot Fill");
            }
            return changed;
        }

        private static int ToggleRendererEnabled(MeshRenderer renderer, bool show, string label)
        {
            if (renderer == null || renderer.enabled == show) return 0;
#if UNITY_EDITOR
            Undo.RecordObject(renderer, show ? "Show " + label : "Hide " + label);
#else
            _ = label;
#endif
            renderer.enabled = show;
            return 1;
        }

        [ContextMenu("Clear District Lot Terrain")]
        public void ClearDistrictLotTerrain()
        {
            var container = transform.Find(AreasContainerName);
            if (container == null) return;

            for (var a = 0; a < container.childCount; a++)
            {
                var area = container.GetChild(a);
                var lotsRoot = area.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                    ?? area.Find("ResidentialLots");
                if (lotsRoot == null) continue;

                ClearSubZoneChildren(lotsRoot);
            }
        }

        // Destroy sub-zone children created by the lot terrain pipeline.
        // The parent Lot_XX container is left intact; old single-fill MeshFilter
        // / MeshRenderer components may have been removed — that is expected.
        private static void ClearSubZoneChildren(Transform lotsRoot)
        {
            for (var c = lotsRoot.childCount - 1; c >= 0; c--)
            {
                var child = lotsRoot.GetChild(c);
                if (IsSubZoneChildName(child.name))
                    DestroyDistrictObject(child.gameObject);
            }
        }

        private static bool IsSubZoneChildName(string name)
        {
            return name.EndsWith("_Backyard") || name.EndsWith("_FrontYard") || name.EndsWith("_Carpark")
                || name.EndsWith("_Driveway") || name.EndsWith("_SideYard_L") || name.EndsWith("_SideYard_R")
                || name.EndsWith("_SideYard_W") || name.EndsWith("_SideYard_E")
                || name.EndsWith("_SideYard_S") || name.EndsWith("_SideYard_N")
                || name.EndsWith("_FrontYard_Second")
                || name.EndsWith("_BuildingPad") || name.EndsWith("_Footpath");
        }

        public void ClearDistrictFences()
        {
            var container = transform.Find(AreasContainerName);
            if (container == null) return;

            for (var a = 0; a < container.childCount; a++)
                ClearDistrictChildContainer(container.GetChild(a), "Fences");
        }

        private static void ClearDistrictChildContainer(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
                DestroyDistrictObject(child.gameObject);
        }

        private static void DestroyDistrictObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
                return;
            }

#if UNITY_EDITOR
            // Hub Reset clears thousands of lots/props — Undo.Destroy on each
            // object dominated wall time (~20s+). Skip Undo for bulk clears.
            if (SuppressEditorDestroyUndo)
                UnityEngine.Object.DestroyImmediate(target);
            else
                Undo.DestroyObjectImmediate(target);
#else
            UnityEngine.Object.DestroyImmediate(target);
#endif
        }

        public void CollectNamedAreaBounds(List<Rect> results)
        {
            if (results == null) return;
            var container = transform.Find(AreasContainerName);
            if (container == null) return;

            for (var i = 0; i < container.childCount; i++)
            {
                var marker = container.GetChild(i).GetComponent<CityNamedAreaMarker>();
                if (marker == null) continue;
                var bounds = marker.BoundsXZ;
                if (bounds.width <= 0f || bounds.height <= 0f) continue;
                results.Add(bounds);
            }
        }

        // ── Private helpers ──

        private RoadNetworkSettings ResolveDistrictRoadNetworkSettings()
        {
            var settings = roadNetworkSettings
                           ?? HubRoadNetworkSettings
                           ?? Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
#if UNITY_EDITOR
            if (settings == null)
                settings = AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(
                    "Assets/02_Shared/ScriptableObjects/World/RoadNetworkSettings.asset");
#endif
            return settings;
        }

        private GameObject ResolveFencePrefab()
        {
            if (streetscapeConfig != null && streetscapeConfig.fencePrefab != null)
                return streetscapeConfig.fencePrefab;
            return null;
        }

        /// <summary>
        ///     Root whose children are searched for road meshes. Returns the procedural
        ///     network root; the legacy EasyRoads "Road Network" layout has no creator
        ///     left, so the old lookup always returned null and every road-driven
        ///     facing/decoration query silently fell back to its geometric heuristic.
        /// </summary>
        private Transform ResolveRoadObjectsRoot() =>
            ProceduralCityRoadBuilder.FindNetworkRoot(transform);

        private void DestroyAllAreaMarkersUnderHub()
        {
#if UNITY_EDITOR
            DestroyAllAreaMarkersUnderHubEditor();
#else
            var container = transform.Find(AreasContainerName);
            if (container != null)
                Destroy(container.gameObject);
#endif
        }

#if UNITY_EDITOR
        private void DestroyDetachedAreaMarkersForHub()
        {
            var markers = FindObjectsByType<CityNamedAreaMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || IsUnderHubHierarchy(marker.transform))
                    continue;
                DestroyDistrictObject(marker.gameObject);
            }
        }

        private void DestroyAllAreaMarkersUnderHubEditor()
        {
            DestroyDetachedAreaMarkersForHub();
            var markers = GetComponentsInChildren<CityNamedAreaMarker>(true);
            for (var i = markers.Length - 1; i >= 0; i--)
            {
                if (markers[i] != null)
                    DestroyDistrictObject(markers[i].gameObject);
            }

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == AreasContainerName)
                    DestroyDistrictObject(child.gameObject);
            }
        }

        private bool IsUnderHubHierarchy(Transform candidate)
        {
            if (candidate == null) return false;
            return candidate == transform || candidate.IsChildOf(transform);
        }
#endif

        private void SpawnNamedAreas(
            IReadOnlyList<CityNamedArea> areas,
            Func<Vector2, float> resolveHeight)
        {
            if (areas == null || areas.Count == 0) return;

            var container = new GameObject(AreasContainerName);
            container.transform.SetParent(transform, false);

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(container, "Create City Named Areas");
#endif

            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                var groundY = resolveHeight(area.centerXZ);
                var go = new GameObject("Area_" + area.displayName + "_" + area.id);
                go.transform.SetParent(container.transform, false);
                go.transform.position = area.WorldCenter(groundY);

                var marker = go.AddComponent<CityNamedAreaMarker>();
                marker.Apply(area);

#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(go, "Create Named Area");

                if (ResolvedCreateEditorFloorVisuals)
                    CreateEditorFloorVisual(go.transform, area, ResolvedFloorVisualHeight);
#endif
            }

            // Apply the current show/hide toggle to freshly-created fills.
            SyncDistrictFillVisibility();
        }

#if UNITY_EDITOR
        private static void CreateEditorFloorVisual(Transform parent, CityNamedArea area, float floorHeight)
        {
            var visual = new GameObject("DistrictFill");
            visual.transform.SetParent(parent, false);

            var color = CityNamedAreaMarker.DistrictColor(area.districtType);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = new Color(color.r, color.g, color.b, 0.42f);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }

            var meshFilter = visual.AddComponent<MeshFilter>();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            var outline = area.outlineXZ != null && area.outlineXZ.Length >= 3
                ? new List<Vector2>(area.outlineXZ)
                : new List<Vector2>(BuildRectOutline(area.boundsXZ));

            meshFilter.sharedMesh = CityNamedAreaOutlineBuilder.CreateFlatMesh(outline, parent.position, floorHeight);
            Undo.RegisterCreatedObjectUndo(visual, "Create District Fill Visual");
        }

        private static Vector2[] BuildRectOutline(Rect rect)
        {
            return new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };
        }
#endif

        private static string BuildDistrictSummary(IReadOnlyList<CityNamedArea> areas)
        {
            if (areas == null || areas.Count == 0) return "Named areas=0.";

            var clusters = new HashSet<string>();
            var coreBlocks = 0;
            var hospitalBlocks = 0;
            var militaryBlocks = 0;
            var residentialClusters = new HashSet<string>();
            var commercialClusters = new HashSet<string>();
            var industrialClusters = new HashSet<string>();

            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                var label = string.IsNullOrWhiteSpace(area.clusterName) ? area.displayName : area.clusterName;
                clusters.Add(label);

                switch (area.districtType)
                {
                    case CityDistrictType.CityCore: coreBlocks++; break;
                    case CityDistrictType.Hospital: hospitalBlocks++; break;
                    case CityDistrictType.Military: militaryBlocks++; break;
                    case CityDistrictType.Residential: residentialClusters.Add(label); break;
                    case CityDistrictType.Commercial: commercialClusters.Add(label); break;
                    case CityDistrictType.Industrial: industrialClusters.Add(label); break;
                }
            }

            return "Named areas=" + areas.Count +
                   ", clusters=" + clusters.Count +
                   ", CityCore=" + coreBlocks +
                   ", Residential=" + residentialClusters.Count +
                   ", Commercial=" + commercialClusters.Count +
                   ", Industrial=" + industrialClusters.Count +
                   ", Military=" + militaryBlocks +
                   ", Hospital=" + hospitalBlocks + ".";
        }

    }
}
