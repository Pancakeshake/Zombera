#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    internal enum LotDecorationCategory
    {
        Tree = 0,
        Foliage = 1,
        Prop = 2
    }

    internal static partial class CityDistrictLotDecorationScatter
    {
        public const string LotDecorationsContainerName = "LotDecorations";

        private readonly struct PlacementRecord
        {
            public readonly Vector2 Position;

            public PlacementRecord(Vector2 position) => Position = position;
        }

        /// <summary>
        ///     Optional region-wide inputs shared across all areas of a hub build.
        /// </summary>
        public readonly struct RegionRoadsContext
        {
            public readonly IReadOnlyList<CityNamedArea> AllAreas;
            public readonly RoadNetworkSettings RoadSettings;

            public RegionRoadsContext(
                IReadOnlyList<CityNamedArea> allAreas,
                RoadNetworkSettings roadSettings)
            {
                AllAreas = allAreas;
                RoadSettings = roadSettings;
            }
        }

        /// <summary>
        ///     Per-area scatter request: bundles the marker, catalog, settings and
        ///     region context passed down from the road network builder.
        /// </summary>
        public readonly struct ScatterRequest
        {
            public readonly Transform AreaTransform;
            public readonly CityNamedAreaMarker Marker;
            public readonly CityLotDecorationCatalog Catalog;
            public readonly CityLotDecorationScatterSettings Settings;
            public readonly float FrontYardDepthMeters;
            public readonly System.Random Rng;
            public readonly RegionRoadsContext Region;
            public readonly DistrictLotTerrainLayout TerrainLayout;

            public ScatterRequest(
                Transform areaTransform,
                CityNamedAreaMarker marker,
                CityLotDecorationCatalog catalog,
                CityLotDecorationScatterSettings settings,
                float frontYardDepthMeters,
                System.Random rng,
                RegionRoadsContext region,
                DistrictLotTerrainLayout terrainLayout = null)
            {
                AreaTransform = areaTransform;
                Marker = marker;
                Catalog = catalog;
                Settings = settings;
                FrontYardDepthMeters = frontYardDepthMeters;
                Rng = rng;
                Region = region;
                TerrainLayout = terrainLayout;
            }
        }

        private readonly struct CategoryScatterContext
        {
            public readonly Transform Parent;
            public readonly Rect? BuildingBounds;
            public readonly CityLotDecorationScatterSettings Settings;
            public readonly float GroundY;
            public readonly System.Random Rng;

            public CategoryScatterContext(
                Transform parent,
                Rect? buildingBounds,
                CityLotDecorationScatterSettings settings,
                float groundY,
                System.Random rng)
            {
                Parent = parent;
                BuildingBounds = buildingBounds;
                Settings = settings;
                GroundY = groundY;
                Rng = rng;
            }
        }

        public static int ScatterArea(ScatterRequest request)
        {
            if (request.AreaTransform == null || request.Marker == null || !request.Catalog.HasAnyContent)
                return 0;

            var placedBuildings = request.AreaTransform.Find("PlacedBuildings");
            var lotsContainer = request.AreaTransform.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                ?? request.AreaTransform.Find("ResidentialLots");
            if (lotsContainer == null || lotsContainer.childCount == 0)
                return ScatterFullBlock(request, placedBuildings);

            var placed = 0;
            var regionsBuffer = new List<YardScatterRegion>(8);

            for (var i = 0; i < lotsContainer.childCount; i++)
            {
                var lotTransform = lotsContainer.GetChild(i);
                if (lotTransform.GetComponent<CityLotFacingMarker>() == null)
                    continue;

                ClearLotDecorations(lotTransform);
                var lotRect = CityDistrictLotPlacement.ExtractLotRect(lotTransform);
                if (lotRect.width < 4f || lotRect.height < 4f)
                    continue;

                var facing = lotTransform.GetComponent<CityLotFacingMarker>();
                var buildingBounds = FindBestBuildingBounds(placedBuildings, lotRect);
                CityLotYardRegionUtility.BuildScatterRegions(
                    lotRect,
                    buildingBounds,
                    facing.streetFace,
                    request.FrontYardDepthMeters,
                    request.Settings.buildingClearanceMeters,
                    request.Settings.lotEdgeMarginMeters,
                    regionsBuffer);

                if (regionsBuffer.Count == 0)
                    continue;

                var decorRoot = new GameObject(LotDecorationsContainerName);
                decorRoot.transform.SetParent(lotTransform, false);
                Undo.RegisterCreatedObjectUndo(decorRoot, "Place Lot Decorations");

                var ctx = new CategoryScatterContext(
                    decorRoot.transform,
                    buildingBounds,
                    request.Settings,
                    lotTransform.position.y,
                    new System.Random(request.Rng.Next()));
                var placements = new List<PlacementRecord>(32);
                var grassRegions = BuildGrassTreeRegions(request, lotRect, facing.streetFace, placedBuildings);
                placed += ScatterAllCategories(ctx, request.Catalog, request.Marker, regionsBuffer, grassRegions, placements);

                if (decorRoot.transform.childCount == 0)
                    Undo.DestroyObjectImmediate(decorRoot);
            }

            return placed;
        }

        private static int ScatterFullBlock(ScatterRequest request, Transform placedBuildings)
        {
            ClearLotDecorations(request.AreaTransform);

            var selfArea = CityLotBoundsUtility.FromMarker(request.Marker);
            var blockBounds = CityLotBoundsUtility.ComputeLotBoundsRect(
                request.Marker.BoundsXZ, request.Region.AllAreas, selfArea, request.Region.RoadSettings);
            if (blockBounds.width <= 0f || blockBounds.height <= 0f)
                blockBounds = request.Marker.BoundsXZ;
            if (blockBounds.width < 4f || blockBounds.height < 4f)
                return 0;

            var decorRoot = new GameObject(LotDecorationsContainerName);
            decorRoot.transform.SetParent(request.AreaTransform, false);
            Undo.RegisterCreatedObjectUndo(decorRoot, "Place Lot Decorations");

            var regionsBuffer = new List<YardScatterRegion>(1);
            var buildingBounds = FindBestBuildingBounds(placedBuildings, blockBounds);
            CityLotYardRegionUtility.BuildScatterRegions(
                blockBounds,
                buildingBounds,
                CityBuildingRoadFacingUtility.ResolveLotStreetFace(blockBounds, blockBounds),
                request.FrontYardDepthMeters,
                request.Settings.buildingClearanceMeters,
                request.Settings.lotEdgeMarginMeters,
                regionsBuffer);

            if (regionsBuffer.Count == 0)
            {
                Undo.DestroyObjectImmediate(decorRoot);
                return 0;
            }

            var ctx = new CategoryScatterContext(
                decorRoot.transform,
                buildingBounds,
                request.Settings,
                request.AreaTransform.position.y,
                new System.Random(request.Rng.Next()));
            var placements = new List<PlacementRecord>(32);
            var placed = ScatterAllCategories(ctx, request.Catalog, request.Marker, regionsBuffer, null, placements);

            if (decorRoot.transform.childCount == 0)
                Undo.DestroyObjectImmediate(decorRoot);

            return placed;
        }

        private static int ScatterAllCategories(
            in CategoryScatterContext ctx,
            CityLotDecorationCatalog catalog,
            CityNamedAreaMarker marker,
            List<YardScatterRegion> regions,
            List<YardScatterRegion> treeRegions,
            List<PlacementRecord> placements)
        {
            var counts = ctx.Settings.ResolveCounts(marker.DistrictType);
            var effectiveTreeRegions = treeRegions ?? regions;
            var placed = 0;
            placed += ScatterCategory(ctx, catalog.Trees, counts.trees, LotDecorationCategory.Tree, effectiveTreeRegions, placements);
            placed += ScatterCategory(ctx, catalog.Foliage, counts.foliage, LotDecorationCategory.Foliage, regions, placements);
            placed += ScatterCategory(ctx, catalog.Props, counts.props, LotDecorationCategory.Prop, regions, placements);
            return placed;
        }

        public static int ClearUnderAreasRoot(Transform areasRoot)
        {
            if (areasRoot == null)
                return 0;

            var cleared = 0;
            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var area = areasRoot.GetChild(i);
                cleared += ClearLotDecorations(area);

                var lotsContainer = area.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                    ?? area.Find("ResidentialLots");
                if (lotsContainer == null)
                    continue;

                for (var l = 0; l < lotsContainer.childCount; l++)
                    cleared += ClearLotDecorations(lotsContainer.GetChild(l));
            }

            return cleared;
        }

        private static int ClearLotDecorations(Transform lotTransform)
        {
            if (lotTransform == null)
                return 0;

            var existing = lotTransform.Find(LotDecorationsContainerName);
            if (existing == null)
                return 0;

            Undo.DestroyObjectImmediate(existing.gameObject);
            return 1;
        }

        private static int ScatterCategory(
            in CategoryScatterContext ctx,
            List<GameObject> prefabs,
            int targetCount,
            LotDecorationCategory category,
            List<YardScatterRegion> regions,
            List<PlacementRecord> placements)
        {
            if (prefabs == null || prefabs.Count == 0 || targetCount <= 0
                || regions == null || regions.Count == 0)
                return 0;

            var heightOffset = ResolveHeightOffset(category, ctx.Settings);
            var spacing = ctx.Settings.minSpacingMeters;
            var maxAttempts = targetCount * ctx.Settings.maxPlacementAttemptsPerItem;
            var placed = 0;

            for (var attempt = 0; attempt < maxAttempts && placed < targetCount; attempt++)
            {
                if (TryPlaceDecoration(ctx, category, prefabs, regions, spacing, heightOffset, placements))
                    placed++;
            }

            return placed;
        }

        private static bool TryPlaceDecoration(
            in CategoryScatterContext ctx,
            LotDecorationCategory category,
            List<GameObject> prefabs,
            List<YardScatterRegion> regions,
            float spacing,
            float heightOffset,
            List<PlacementRecord> placements)
        {
            var region = PickRegion(regions, category, ctx.Rng);
            if (!TryPickPoint(region.Bounds, ctx.Settings.positionJitterMeters, ctx.Rng, out var point))
                return false;

            if (ctx.BuildingBounds.HasValue &&
                IsInsideBuilding(point, ctx.BuildingBounds.Value, ctx.Settings.buildingClearanceMeters))
                return false;

            if (!HasClearance(point, spacing, placements))
                return false;

            var prefab = prefabs[ctx.Rng.Next(prefabs.Count)];
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, ctx.Parent) as GameObject;
            if (instance == null)
                return false;

            var yaw = ResolveDecorationYaw(ctx.Settings, ctx.Rng);
            instance.transform.SetPositionAndRotation(
                new Vector3(point.x, ctx.GroundY + heightOffset, point.y),
                Quaternion.Euler(0f, yaw, 0f));
            Undo.RegisterCreatedObjectUndo(instance, "Place Lot Decoration");

            placements.Add(new PlacementRecord(point));
            return true;
        }

        private static float ResolveDecorationYaw(CityLotDecorationScatterSettings settings, System.Random rng)
        {
            var yaw = (float)(rng.NextDouble() * 360d);
            if (settings.yawJitterDegrees > 0f)
                yaw += (float)((rng.NextDouble() * 2d - 1d) * settings.yawJitterDegrees);

            return yaw;
        }

        private static YardScatterRegion PickRegion(
            List<YardScatterRegion> regions,
            LotDecorationCategory category,
            System.Random rng)
        {
            if (regions.Count == 1)
                return regions[0];

            var frontCandidates = new List<YardScatterRegion>(regions.Count);
            var otherCandidates = new List<YardScatterRegion>(regions.Count);
            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region.Kind == YardRegionKind.FrontYard || region.Kind == YardRegionKind.FullLot)
                    frontCandidates.Add(region);
                else
                    otherCandidates.Add(region);
            }

            var preferFront = category is LotDecorationCategory.Tree or LotDecorationCategory.Foliage;
            if (preferFront && frontCandidates.Count > 0 && rng.NextDouble() < 0.7d)
                return frontCandidates[rng.Next(frontCandidates.Count)];

            var pool = otherCandidates.Count > 0 ? otherCandidates : frontCandidates;
            return pool[rng.Next(pool.Count)];
        }

        private static bool TryPickPoint(Rect region, float jitterMeters, System.Random rng, out Vector2 point)
        {
            point = default;
            if (region.width < 0.5f || region.height < 0.5f)
                return false;

            point = new Vector2(
                region.xMin + (float)rng.NextDouble() * region.width,
                region.yMin + (float)rng.NextDouble() * region.height);

            if (jitterMeters > 0f)
            {
                point.x += (float)((rng.NextDouble() * 2d - 1d) * jitterMeters);
                point.y += (float)((rng.NextDouble() * 2d - 1d) * jitterMeters);
            }

            return region.Contains(point);
        }

        private static bool IsInsideBuilding(Vector2 point, Rect buildingBounds, float clearanceMeters)
        {
            var inflated = new Rect(
                buildingBounds.xMin - clearanceMeters,
                buildingBounds.yMin - clearanceMeters,
                buildingBounds.width + clearanceMeters * 2f,
                buildingBounds.height + clearanceMeters * 2f);
            return inflated.Contains(point);
        }

        private static bool HasClearance(Vector2 point, float minSpacing, List<PlacementRecord> placements)
        {
            var minDistSq = minSpacing * minSpacing;
            for (var i = 0; i < placements.Count; i++)
            {
                var delta = point - placements[i].Position;
                if (delta.sqrMagnitude < minDistSq)
                    return false;
            }

            return true;
        }

        private static float ResolveHeightOffset(LotDecorationCategory category, CityLotDecorationScatterSettings settings) =>
            category switch
            {
                LotDecorationCategory.Tree => settings.treeHeightOffsetMeters,
                LotDecorationCategory.Foliage => settings.foliageHeightOffsetMeters,
                _ => settings.propHeightOffsetMeters
            };

        private static Rect? FindBestBuildingBounds(Transform placedBuildingsRoot, Rect lotRect)
        {
            if (placedBuildingsRoot == null)
                return null;

            Rect? bestBounds = null;
            var bestOverlap = 0f;
            for (var i = 0; i < placedBuildingsRoot.childCount; i++)
            {
                var child = placedBuildingsRoot.GetChild(i);
                if (!CityBuildingPrefabFootprintUtility.TryMeasureWorldBoundsXZ(child.gameObject, out var bounds))
                    continue;

                var overlap = ComputeOverlapArea(bounds, lotRect);
                if (overlap <= bestOverlap)
                    continue;

                bestOverlap = overlap;
                bestBounds = bounds;
            }

            return bestBounds;
        }

        private static float ComputeOverlapArea(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return 0f;

            return (xMax - xMin) * (yMax - yMin);
        }
    }
}
#endif
