using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private const string CityNamedAreasContainerName = "CityNamedAreas";
        private const string PlacedBuildingsContainerName = "PlacedBuildings";

        /// <summary>Bound by world-build stages for water-aware lot placement.</summary>
        public IWorldTerrainQuery LotTerrainQuery { get; set; }

        public float LotDeepWaterDepthMeters { get; set; }
        public float LotMinDistanceToWaterMeters { get; set; } =
            WorldWaterPlacementGate.DefaultMinDistanceToWaterMeters;
        public float LotMaxReclaimDepthMeters { get; set; }
        public bool LotRequireWaterGate { get; set; }

        // ── Resolved building config from BuildConfig SO ──

        private bool ResolvedUseProxyPrefabs =>
            buildConfig?.useProxyPrefabs ?? true;
        private int ResolvedBuildingLayoutSeed =>
            buildConfig?.buildingLayoutSeed ?? 12345;
        private CityNamedAreaBuildingLayoutSettings ResolvedBuildingLayoutSettings =>
            buildConfig?.buildingLayoutSettings ?? CityNamedAreaBuildingLayoutSettings.CreateDefault();
        private string ResolvedAssembledFolder =>
            streetscapeConfig != null ? streetscapeConfig.assembledPrefabFolder : "Assets/02_Shared/Prefabs/Building/Buildings_Modular_Complete";
        private string ResolvedProxyFolder =>
            streetscapeConfig != null ? streetscapeConfig.proxyPrefabFolder : "Assets/02_Shared/Proxies/Buildings_Complete";

        // ── Public API ──

        /// <summary>
        ///     Step 11: Place building prefabs on district lots and in named areas.
        ///     Requires lots to be generated first (District Lots step).
        /// </summary>
        public void PlaceBuildingsInAreas()
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

            var buildingCatalog = CityAssembledBuildingCatalogLoader.Load(ResolvedAssembledFolder, ResolvedProxyFolder);
            if (buildingCatalog.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No assembled building prefabs found in '" + ResolvedAssembledFolder + "'.", this);
                return;
            }

            var settings = ResolvedBuildingLayoutSettings;
            settings.Clamp();
            settings.streetSetbackMeters = Mathf.Max(
                settings.streetSetbackMeters,
                ResolveMinimumStreetSetbackMeters());

            var allAreas = CityLotBoundsUtility.CollectFromAreasRoot(areasRoot);
            var roadSettings = ResolveBuildingRoadNetworkSettings();
            var roadMeshes = CollectRoadMeshesForPlacement();

            // Region mode places buildings for every site in one pass, each city
            // seeded from its own region-derived seed so every town gets a stable,
            // independent building layout. Single-city mode keeps the template seed.
            var placeWatch = System.Diagnostics.Stopwatch.StartNew();
            int placedCount;
            string seedLabel;
            if (RegionModeActive)
            {
                placedCount = PlaceBuildingsForAllSites(
                    areasRoot, buildingCatalog, settings, roadSettings, roadMeshes, allAreas);
                seedLabel = "region-per-site";
            }
            else
            {
                var wrapperSeed = Layout != null && Layout.layoutSeed != 0
                    ? Layout.layoutSeed
                    : ResolvedBuildingLayoutSeed;
                var areaTransforms = CollectAreaTransforms(areasRoot);
                placedCount = PlaceBuildingsForAreas(
                    areaTransforms, buildingCatalog, settings, roadSettings, roadMeshes, allAreas,
                    new System.Random(wrapperSeed));
                seedLabel = wrapperSeed.ToString();
            }

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Placed buildings=" + placedCount +
                " in " + placeWatch.ElapsedMilliseconds + "ms (seed=" + seedLabel + ").", this);

            Undo.CollapseUndoOperations(undoGroup);
#endif
        }

#if UNITY_EDITOR
        private static List<Transform> CollectAreaTransforms(Transform areasRoot)
        {
            var transforms = new List<Transform>(areasRoot.childCount);
            for (var i = 0; i < areasRoot.childCount; i++)
                transforms.Add(areasRoot.GetChild(i));

            return transforms;
        }

        /// <summary>
        ///     Region buildings: buckets named areas by their nearest site and places
        ///     each city's buildings with that site's own seed (mirrors the per-site
        ///     lot flow in <see cref="BuildLotsForAllSites" />).
        /// </summary>
        private int PlaceBuildingsForAllSites(
            Transform areasRoot,
            List<CityAssembledBuildingCatalogEntry> buildingCatalog,
            CityNamedAreaBuildingLayoutSettings settings,
            RoadNetworkSettings roadSettings,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes,
            IReadOnlyList<CityNamedArea> allAreas)
        {
            var region = ActiveRegionAsset;
            if (region == null)
                return 0;

            var buckets = BucketAreaChildrenBySite(region, areasRoot);
            var total = 0;
            for (var i = 0; i < region.SiteCount; i++)
            {
                if (buckets[i].Count == 0)
                    continue;

                var site = region.GetSite(i);
                if (site == null)
                    continue;

                var seed = ResolveSiteSeed(site, ResolveBuiltRegionSeed(), i);
                total += PlaceBuildingsForAreas(
                    buckets[i], buildingCatalog, settings, roadSettings, roadMeshes, allAreas,
                    new System.Random(seed));
            }

            return total;
        }

        private int PlaceBuildingsForAreas(
            IReadOnlyList<Transform> areaTransforms,
            List<CityAssembledBuildingCatalogEntry> buildingCatalog,
            CityNamedAreaBuildingLayoutSettings settings,
            RoadNetworkSettings roadSettings,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes,
            IReadOnlyList<CityNamedArea> allAreas,
            System.Random rng)
        {
            var placedCount = 0;
            try
            {
                for (var i = 0; i < areaTransforms.Count; i++)
                {
                    if ((i & 31) == 0)
                        EditorUtility.DisplayProgressBar(
                            "Placing Buildings", "Area " + (i + 1) + " / " + areaTransforms.Count,
                            (float)i / Mathf.Max(1, areaTransforms.Count));

                    placedCount += PlaceBuildingsForArea(
                        areaTransforms[i], buildingCatalog, settings, roadSettings, roadMeshes, allAreas, rng);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return placedCount;
        }

        private int PlaceBuildingsForArea(
            Transform areaTransform,
            List<CityAssembledBuildingCatalogEntry> buildingCatalog,
            CityNamedAreaBuildingLayoutSettings settings,
            RoadNetworkSettings roadSettings,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes,
            IReadOnlyList<CityNamedArea> allAreas,
            System.Random rng)
        {
            var marker = areaTransform.GetComponent<CityNamedAreaMarker>();
            if (marker == null || marker.DistrictType == CityDistrictType.Park)
                return 0;

            CityBuildingPrefabUtility.ClearPlacedBuildingsUnder(areaTransform);
            var groundY = areaTransform.position.y;

            var lotsContainer = areaTransform.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                ?? areaTransform.Find("ResidentialLots");
            if (lotsContainer != null && lotsContainer.childCount > 0)
            {
                return CityDistrictLotPlacement.PlaceDistrictLotBuildings(
                    new CityDistrictLotPlacement.DistrictLotBuildArgs
                    {
                        AreaTransform = areaTransform, LotsContainer = lotsContainer,
                        Marker = marker, Catalog = buildingCatalog, Settings = settings,
                        Rng = rng, UseProxy = ResolvedUseProxyPrefabs, RoadMeshes = roadMeshes,
                        AllAreas = allAreas, RoadSettings = roadSettings,
                        StateSink = GeneratedBuildingStateSink,
                        TerrainQuery = LotTerrainQuery,
                        DeepWaterDepthMeters = LotDeepWaterDepthMeters,
                        MinDistanceToWaterMeters = LotMinDistanceToWaterMeters,
                        MaxReclaimDepthMeters = LotMaxReclaimDepthMeters,
                        RequireWaterGate = LotRequireWaterGate
                    });
            }

            if (marker.DistrictType is CityDistrictType.Residential or CityDistrictType.Commercial
                or CityDistrictType.Industrial or CityDistrictType.CityCore)
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] '" + areaTransform.name +
                    "' has no DistrictLots child — run Step 4 first.", this);

            return PlaceGridBuildings(
                areaTransform, marker, groundY, buildingCatalog, settings, rng);
        }
#endif

        public void ClearPlacedBuildings()
        {
            var areasRoot = transform.Find(CityNamedAreasContainerName);
            if (areasRoot == null) return;

            var cleared = 0;
            for (var i = 0; i < areasRoot.childCount; i++)
                cleared += CityBuildingPrefabUtility.ClearPlacedBuildingsUnder(areasRoot.GetChild(i));

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (cleared > 0
                ? "Cleared placed buildings=" + cleared + "."
                : "Cleared placed buildings."), this);
        }

        // ── Private helpers ──

        private int PlaceGridBuildings(
            Transform areaTransform, CityNamedAreaMarker marker, float groundY,
            List<CityAssembledBuildingCatalogEntry> buildingCatalog,
            CityNamedAreaBuildingLayoutSettings settings, System.Random rng)
        {
            var outline = CityNamedAreaPolygonUtility.ResolveOutlineXZ(marker);
            var placements = CityNamedAreaBuildingLayout.BuildPlacements(
                new CityNamedAreaBuildingLayoutRequest(
                    new CityNamedAreaBuildingLayoutArea(
                        marker.BoundsXZ, outline, marker.RoundedCorners, groundY, marker.DistrictType),
                    new CityNamedAreaBuildingLayoutCatalog(buildingCatalog, settings, ResolvedUseProxyPrefabs, rng)));
            if (placements.Count == 0) return 0;

            var placed = 0;
            var container = new GameObject(PlacedBuildingsContainerName);
            container.transform.SetParent(areaTransform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(container, "Place District Buildings");
#endif

            for (var p = 0; p < placements.Count; p++)
            {
                var placement = placements[p];
                var prefab = placement.UseProxy && placement.Entry.proxyPrefab != null
                    ? placement.Entry.proxyPrefab : placement.Entry.prefab;
                if (prefab == null) continue;

                var centerOffset = placement.Entry.ResolvePlacementCenterOffset(placement.UseProxy);
                var rootPosition = CityBuildingPrefabFootprintUtility.ResolveRootPosition(
                    placement.WorldPosition, placement.Rotation, centerOffset);
                var instance = Object.Instantiate(prefab, rootPosition, placement.Rotation, container.transform);
                if (instance == null) continue;

                if (!CityBuildingPrefabPlacementValidator.FitsInsideOutline(
                        instance, outline, settings.polygonSafetyMarginMeters, settings.gridCellMeters))
                {
                    CityPipelineFailureMarkers.Add(
                        new Vector3(placement.WorldPosition.x, groundY, placement.WorldPosition.y),
                        "Building rejected: does not fit outline");
#if UNITY_EDITOR
                    Undo.DestroyObjectImmediate(instance);
#else
                    Object.Destroy(instance);
#endif
                    continue;
                }

                var placedBounds = default(Rect);
                if (GeneratedBuildingStateSink != null &&
                    !CityBuildingPrefabFootprintUtility.TryMeasureWorldBoundsXZ(instance, out placedBounds))
                {
                    DestroyFailedStateBuilding(instance);
                    continue;
                }

                if (!TryPublishGridBuildingState(
                        instance, marker, placement, placedBounds, p))
                    continue;

                placed++;
            }

            return placed;
        }

        private bool TryPublishGridBuildingState(
            GameObject instance,
            CityNamedAreaMarker marker,
            CityNamedAreaBuildingPlacement placement,
            Rect footprintXZ,
            int ordinal)
        {
            var sink = GeneratedBuildingStateSink;
            if (sink == null)
                return true;

            if (!WorldStateEntityViewBinding.TryGetBoundId(
                    marker, WorldEntityKind.District, out var districtId))
                return DestroyFailedStateBuilding(instance);

            var data = CreateGridBuildingPlacementData(
                marker, placement, districtId, footprintXZ, ordinal);
            if (!sink.TryStageBuilding(data, out var id, out _))
                return DestroyFailedStateBuilding(instance);

            WorldStateEntityViewBinding.Bind(instance, id);
            sink.RegisterProvisionalView(instance);
            return true;
        }

        private static GeneratedBuildingPlacementData CreateGridBuildingPlacementData(
            CityNamedAreaMarker marker,
            CityNamedAreaBuildingPlacement placement,
            WorldEntityId districtId,
            Rect footprintXZ,
            int ordinal)
        {
            var archetypeId = ResolveBuildingArchetypeId(placement.Entry);
            return new GeneratedBuildingPlacementData
            {
                SourceId = archetypeId,
                ArchetypeId = archetypeId,
                TypeId = placement.Entry?.id ?? string.Empty,
                DistrictId = districtId,
                DistrictType = marker != null ? marker.DistrictType : CityDistrictType.Mixed,
                Position = placement.WorldPosition,
                Rotation = placement.Rotation,
                FootprintXZ = footprintXZ,
                StreetFace = BlockFace.North,
                Ordinal = ordinal
            };
        }

        private static bool DestroyFailedStateBuilding(GameObject instance)
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(instance);
#else
            Object.Destroy(instance);
#endif
            return false;
        }

        private static string ResolveBuildingArchetypeId(CityAssembledBuildingCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.assetPath))
                return entry.assetPath;
            if (!string.IsNullOrWhiteSpace(entry.id))
                return entry.id;
            return entry.prefab != null ? entry.prefab.name : string.Empty;
        }

        private float ResolveMinimumStreetSetbackMeters()
        {
            var roadSettings = HubRoadNetworkSettings
                               ?? Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            if (roadSettings == null) return 2f;

            var settings = ResolvedBuildingLayoutSettings;
            var roadHalfWidth = Mathf.Max(1.5f, roadSettings.ResolveWidthMeters(RoadClass.Local) * 0.5f);
            return roadHalfWidth * 0.35f + settings.polygonSafetyMarginMeters;
        }

        private IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> CollectRoadMeshesForPlacement()
        {
            // Read-only query: never EnsureNetworkRoot here, so a placement pass cannot
            // create a stray empty road-network root.
            var roadObjects = ResolveRoadObjectsRoot();
            if (roadObjects == null) return null;

            return CityRoadMeshQuery.CollectRoadMeshes(roadObjects);
        }

        private RoadNetworkSettings ResolveBuildingRoadNetworkSettings()
        {
            return roadNetworkSettings
                   ?? HubRoadNetworkSettings
                   ?? Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
        }
    }
}
