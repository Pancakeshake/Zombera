using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Object = UnityEngine.Object;

namespace Zombera.World.City
{
    public static class CityNamedAreaBuildingSpawnUtility
    {
        public const string PlacedBuildingsContainerName = "PlacedBuildings";

        public static List<CityAssembledBuildingCatalogEntry> BuildCatalogFromStreamed(StreamedCityCatalog catalog)
        {
            var results = new List<CityAssembledBuildingCatalogEntry>();
            if (catalog?.Entries == null)
                return results;

            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry?.prefab == null)
                    continue;

                var assembled = new CityAssembledBuildingCatalogEntry
                {
                    id = string.IsNullOrWhiteSpace(entry.id) ? entry.prefab.name : entry.id,
                    prefab = entry.prefab,
                    proxyPrefab = entry.proxyPrefab,
                    yawOffsetDegrees = entry.yawOffsetDegrees,
                    footprintWidthMeters = entry.footprintWidthMeters,
                    footprintDepthMeters = entry.footprintDepthMeters
                };

                if (!CityBuildingPrefabNaming.TryParseDistrictFromPrefabName(entry.prefab.name, out assembled.districtType))
                    assembled.districtType = CityDistrictType.Mixed;

                if (CityBuildingPrefabFootprintUtility.TryMeasureFootprint(entry.prefab, out var fp))
                {
                    assembled.doorYawOffsetDegrees = CityBuildingRoadFacingUtility.GetDoorYawOffset(entry.prefab, assembled.yawOffsetDegrees, fp);
                    assembled.doorYawResolved = true;
                }

                if (entry.proxyPrefab != null)
                {
                    assembled.hasProxyFootprint = true;
                    assembled.proxyFootprintWidthMeters = entry.footprintWidthMeters;
                    assembled.proxyFootprintDepthMeters = entry.footprintDepthMeters;
                }

                results.Add(assembled);
            }

            return results;
        }

        public static int SpawnPlacements(
            Transform areasRoot,
            IReadOnlyList<CityAssembledBuildingCatalogEntry> catalog,
            CityNamedAreaBuildingLayoutSettings layoutSettings,
            bool useProxyPrefabs,
            int layoutSeed,
            float minimumStreetSetbackMeters,
            RuntimePlacedStructureFixer structureFixer,
            out string summary)
        {
            summary = string.Empty;
            if (areasRoot == null || catalog == null || catalog.Count == 0)
            {
                summary = "No catalog entries.";
                return 0;
            }

            var settings = layoutSettings;
            settings.Clamp();
            settings.streetSetbackMeters = Mathf.Max(settings.streetSetbackMeters, minimumStreetSetbackMeters);

            var seed = layoutSeed != 0 ? layoutSeed : 12345;
            var rng = new System.Random(seed);
            var placedCount = 0;

            for (var i = 0; i < areasRoot.childCount; i++)
            {
                placedCount += SpawnPlacementsForArea(
                    areasRoot.GetChild(i),
                    catalog,
                    settings,
                    useProxyPrefabs,
                    rng,
                    structureFixer);
            }

            summary = "Placed buildings=" + placedCount + " (seed=" + seed + ").";
            return placedCount;
        }

        /// <summary>
        ///     Spawns buildings for a single named area. <paramref name="settings" /> must already be clamped.
        ///     Allows callers to spread placement across frames (one area per slice).
        /// </summary>
        public static int SpawnPlacementsForArea(
            Transform areaTransform,
            IReadOnlyList<CityAssembledBuildingCatalogEntry> catalog,
            CityNamedAreaBuildingLayoutSettings settings,
            bool useProxyPrefabs,
            System.Random rng,
            RuntimePlacedStructureFixer structureFixer)
        {
            if (areaTransform == null || catalog == null || catalog.Count == 0)
                return 0;

            var marker = areaTransform.GetComponent<CityNamedAreaMarker>();
            if (marker == null || marker.DistrictType == CityDistrictType.Park)
                return 0;

            ClearPlacedBuildingsUnder(areaTransform);

            var outline = CityNamedAreaPolygonUtility.ResolveOutlineXZ(marker);
            var groundY = areaTransform.position.y;
            var placements = CityNamedAreaBuildingLayout.BuildPlacements(
                new CityNamedAreaBuildingLayoutRequest(
                    new CityNamedAreaBuildingLayoutArea(
                        marker.BoundsXZ,
                        outline,
                        marker.RoundedCorners,
                        groundY,
                        marker.DistrictType),
                    new CityNamedAreaBuildingLayoutCatalog(
                        catalog,
                        settings,
                        useProxyPrefabs,
                        rng)));

            if (placements.Count == 0)
                return 0;

            var container = new GameObject(PlacedBuildingsContainerName);
            container.transform.SetParent(areaTransform, false);

            var placedCount = 0;
            for (var p = 0; p < placements.Count; p++)
            {
                var placement = placements[p];
                var prefab = placement.UseProxy && placement.Entry.proxyPrefab != null
                    ? placement.Entry.proxyPrefab
                    : placement.Entry.prefab;
                if (prefab == null)
                    continue;

                var instance = Object.Instantiate(prefab, container.transform);
                var centerOffset = placement.Entry.ResolvePlacementCenterOffset(placement.UseProxy);
                var rootPosition = CityBuildingPrefabFootprintUtility.ResolveRootPosition(
                    placement.WorldPosition,
                    placement.Rotation,
                    centerOffset);
                instance.transform.SetPositionAndRotation(rootPosition, placement.Rotation);

                if (!CityBuildingPrefabPlacementValidator.FitsInsideOutline(
                        instance,
                        outline,
                        settings.polygonSafetyMarginMeters,
                        settings.gridCellMeters))
                {
                    Object.Destroy(instance);
                    continue;
                }

                structureFixer?.ProcessPlacedStructure(instance);
                placedCount++;
            }

            return placedCount;
        }

        public static CityNamedAreaBuildingLayoutSettings PrepareSettings(
            CityNamedAreaBuildingLayoutSettings layoutSettings,
            float minimumStreetSetbackMeters)
        {
            var settings = layoutSettings;
            settings.Clamp();
            settings.streetSetbackMeters = Mathf.Max(settings.streetSetbackMeters, minimumStreetSetbackMeters);
            return settings;
        }

        public static void ClearPlacedBuildingsUnder(Transform areaTransform)
        {
            if (areaTransform == null)
                return;

            var container = areaTransform.Find(PlacedBuildingsContainerName);
            if (container == null)
                return;

            Object.Destroy(container.gameObject);
        }
    }
}