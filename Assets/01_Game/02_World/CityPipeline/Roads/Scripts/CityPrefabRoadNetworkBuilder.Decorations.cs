using UnityEngine;
using Zombera.World.City;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ── Resolved decoration config from BuildConfig SO ──

        private CityLotDecorationScatterSettings ResolvedScatterSettings =>
            buildConfig?.scatterSettings ?? CityLotDecorationScatterSettings.CreateDefault();

        private string ResolvedTreeFolder =>
            streetscapeConfig != null ? streetscapeConfig.treePrefabFolder : "Assets/02_Shared/Prefabs/Props/Nature";

        private float ResolvedLotFrontYardDepth =>
            ResolvedResidentialLotSize.min * (buildConfig?.lotFrontYardFraction ?? 0f);

        // ── Public API ──

        public void PlaceTrees()
        {
#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            var areasRoot = transform.Find(CityNamedAreasContainerName);
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas first.", this);
                return;
            }

            var treeCatalog = CityLotDecorationCatalogLoader.LoadTrees(ResolvedTreeFolder);
            if (!treeCatalog.HasAnyContent)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No tree prefabs found in '" + ResolvedTreeFolder + "'.", this);
                return;
            }

            var settings = ResolvedScatterSettings;
            settings.Clamp();
            var seed = ResolvedBuildingLayoutSeed;
            var wrapperSeed = Layout != null && Layout.layoutSeed != 0 ? Layout.layoutSeed : seed;
            var rng = new System.Random(wrapperSeed);
            var allAreas = CityLotBoundsUtility.CollectFromAreasRoot(areasRoot);
            var roadSettings = HubRoadNetworkSettings
                               ?? Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            var placed = 0;

            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var areaTransform = areasRoot.GetChild(i);
                var marker = areaTransform.GetComponent<CityNamedAreaMarker>();
                if (marker == null)
                    continue;

                placed += CityDistrictLotDecorationScatter.ScatterArea(
                    new CityDistrictLotDecorationScatter.ScatterRequest(
                        areaTransform, marker, treeCatalog, settings,
                        ResolvedLotFrontYardDepth, rng,
                        new CityDistrictLotDecorationScatter.RegionRoadsContext(allAreas, roadSettings),
                        districtLotTerrainLayout));
            }

            Debug.Log("[CityPrefabRoadNetworkBuilder] Placed trees=" + placed + " (seed=" + wrapperSeed + ").", this);

            Undo.CollapseUndoOperations(undoGroup);
#endif
        }

        public void ClearTrees()
        {
            ClearLotDecorations();
        }

        public void ClearLotDecorations()
        {
            var areasRoot = transform.Find(CityNamedAreasContainerName);
            var cleared = CityDistrictLotDecorationScatter.ClearUnderAreasRoot(areasRoot);
            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (cleared > 0
                ? "Cleared lot decoration roots=" + cleared + "."
                : "Cleared lot decorations."), this);
        }
    }
}
