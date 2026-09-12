using UnityEngine;
using Zombera.World.City;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private const string ParkContentContainerName = "ParkContent";
        private const string DefaultTreeFolder = "Assets/02_Shared/Prefabs/Props/Nature";

        private CityParkScatterSettings ResolvedParkScatterSettings =>
            buildConfig?.parkScatterSettings ?? new CityParkScatterSettings();

        private string ResolvedParkTreeFolder =>
            streetscapeConfig != null ? streetscapeConfig.treePrefabFolder : DefaultTreeFolder;

        public void BuildParks()
        {
#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            var settings = ResolvedParkScatterSettings;
            settings.Clamp();

            var areasRoot = transform.Find(CityNamedAreasContainerName);
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas first.", this);
                return;
            }

            var catalog = CityLotDecorationCatalogLoader.Load(ResolvedParkTreeFolder, ResolvedParkTreeFolder, string.Empty);
            var benchPrefab = CityPlacerPrefabResolver.Bench;
            var roadSettings = HubRoadNetworkSettings;
            var seed = ResolvedBuildingLayoutSeed;
            var wrapperSeed = Layout != null && Layout.layoutSeed != 0 ? Layout.layoutSeed : seed;
            var rng = new System.Random(wrapperSeed);
            var built = 0;

            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var area = areasRoot.GetChild(i);
                var marker = area.GetComponent<CityNamedAreaMarker>();
                if (marker == null || marker.DistrictType != CityDistrictType.Park)
                    continue;

                ClearParkContent(area);
                var contentRoot = new GameObject(ParkContentContainerName);
                contentRoot.transform.SetParent(area, false);
                Undo.RegisterCreatedObjectUndo(contentRoot, "Create Park Content");

                built += ScatterTrees(contentRoot.transform, marker, catalog, settings, rng);
                built += ScatterBenches(contentRoot.transform, marker, benchPrefab, settings, rng);
            }

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (built > 0
                ? "Built park content in " + built + " element group(s)."
                : "No Park district blocks found."), this);
#endif
        }

        public void ClearParks()
        {
            var areasRoot = transform.Find(CityNamedAreasContainerName);
            if (areasRoot == null) return;

            for (var i = 0; i < areasRoot.childCount; i++)
                ClearParkContent(areasRoot.GetChild(i));
        }

        private static void ClearParkContent(Transform area)
        {
            var existing = area.Find(ParkContentContainerName);
            if (existing == null) return;

            if (Application.isPlaying)
                Object.Destroy(existing.gameObject);
            else
                Object.DestroyImmediate(existing.gameObject);
        }

        private int ScatterTrees(
            Transform parent, CityNamedAreaMarker marker,
            CityLotDecorationCatalog catalog, CityParkScatterSettings settings,
            System.Random rng)
        {
            if (catalog.Trees.Count == 0) return 0;

            var count = rng.Next(settings.treeCountMin, settings.treeCountMax + 1);
            var rect = InsetRect(marker.BoundsXZ, settings.loopPathInsetMeters + 1f);
            var placed = 0;
            for (var i = 0; i < count * 8 && placed < count; i++)
            {
                var x = (float)(rect.xMin + rng.NextDouble() * rect.width);
                var z = (float)(rect.yMin + rng.NextDouble() * rect.height);
                var prefab = catalog.Trees[rng.Next(catalog.Trees.Count)];
                if (prefab == null) continue;

                var pos = new Vector3(x, 0f, z);
                var instance = Instantiate(prefab, pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), parent);
                instance.name = "ParkTree_" + placed;
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(instance, "Place Park Tree");
#endif
                placed++;
            }

            return placed > 0 ? 1 : 0;
        }

        private int ScatterBenches(
            Transform parent, CityNamedAreaMarker marker,
            GameObject benchPrefab, CityParkScatterSettings settings,
            System.Random rng)
        {
            if (benchPrefab == null) return 0;

            var count = rng.Next(settings.benchCountMin, settings.benchCountMax + 1);
            var rect = InsetRect(marker.BoundsXZ, settings.loopPathInsetMeters + 0.5f);
            for (var i = 0; i < count; i++)
            {
                var x = (float)(rect.xMin + rng.NextDouble() * rect.width);
                var z = (float)(rect.yMin + rng.NextDouble() * rect.height);
                var pos = new Vector3(x, 0f, z);
                var instance = Instantiate(benchPrefab, pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), parent);
                instance.name = "ParkBench_" + i;
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(instance, "Place Park Bench");
#endif
            }

            return count > 0 ? 1 : 0;
        }

        private static Rect InsetRect(Rect r, float inset)
        {
            return new Rect(
                r.xMin + inset, r.yMin + inset,
                Mathf.Max(0f, r.width - inset * 2f),
                Mathf.Max(0f, r.height - inset * 2f));
        }
    }
}
