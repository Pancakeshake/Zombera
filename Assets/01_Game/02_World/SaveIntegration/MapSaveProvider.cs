using UnityEngine;
using Zombera.Core;
using Zombera.World;

namespace Zombera.Systems
{
    public sealed class MapSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private MapStateService mapStateService;
        [SerializeField] private MapMarkerManager mapMarkerManager;

        public int Priority => 90; // After world + player snapshot, before late simulation systems

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData == null) return;

            saveData.map ??= new MapSaveData();
            saveData.map.discoveredChunks.Clear();
            saveData.map.pois.Clear();
            saveData.map.customMarkers.Clear();

            if (mapStateService == null)
            {
                saveData.map.hasData = false;
                return;
            }

            saveData.map.hasData = true;
            saveData.map.worldSeed = ProceduralWorldSession.IsActive ? ProceduralWorldSession.WorldSeed : 0;

            foreach (var chunk in mapStateService.DiscoveredChunks)
                saveData.map.discoveredChunks.Add(chunk);

            foreach (var poi in mapStateService.Pois)
            {
                if (!poi.IsValid) continue;

                saveData.map.pois.Add(new MapPoiSaveData
                {
                    poiId = poi.poiId,
                    label = poi.label,
                    worldPosition = poi.worldPosition
                });
            }

            saveData.map.hasWaypoint = mapStateService.HasWaypoint;
            saveData.map.waypointWorldPosition = mapStateService.WaypointWorldPosition;
            saveData.map.mapZoom = mapStateService.MapZoom;
            saveData.map.mapPan = mapStateService.MapPan;

            if (mapMarkerManager == null) return;

            var markers = mapMarkerManager.GetVisibleMarkers();
            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (marker.type is MapMarkerType.Player or MapMarkerType.Squad or MapMarkerType.Waypoint or MapMarkerType.POI)
                    continue;
                if (marker.markerId.StartsWith("dyn:", System.StringComparison.Ordinal))
                    continue;

                saveData.map.customMarkers.Add(new MapMarkerSaveData
                {
                    markerId = marker.markerId,
                    markerType = (int)marker.type,
                    worldPosition = marker.worldPosition,
                    label = marker.label
                });
            }

            LogConsistency("save", saveData.map.discoveredChunks.Count, mapStateService.DiscoveredChunks.Count);
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData == null || mapStateService == null) return;
            if (saveData.map is not { hasData: true }) return;
            if (!CanApplyMapSave(saveData.map)) return;

            mapStateService.ClearDiscoveredChunks();
            mapStateService.ClearPois();
            mapMarkerManager?.ClearMarkers();
            mapMarkerManager?.ClearMissionObjective();

            var discovered = saveData.map.discoveredChunks;
            if (discovered != null)
            {
                for (var i = 0; i < discovered.Count; i++)
                    mapStateService.DiscoverChunk(discovered[i]);
            }

            if (saveData.map.hasWaypoint)
                mapStateService.SetWaypoint(saveData.map.waypointWorldPosition);
            else
                mapStateService.ClearWaypoint();

            mapStateService.SetView(saveData.map.mapZoom, saveData.map.mapPan);

            RestorePois(saveData.map);
            RestoreCustomMarkers(saveData.map);

            LogConsistency("load", saveData.map.discoveredChunks?.Count ?? 0, mapStateService.DiscoveredChunks.Count);
        }

        private void RestorePois(MapSaveData mapSave)
        {
            if (mapStateService == null || mapSave.pois == null) return;

            for (var i = 0; i < mapSave.pois.Count; i++)
            {
                var saved = mapSave.pois[i];
                if (string.IsNullOrWhiteSpace(saved.poiId)) continue;

                mapStateService.RegisterPoi(new MapPoiRuntimeData(saved.poiId, saved.label, saved.worldPosition));
            }
        }

        private void RestoreCustomMarkers(MapSaveData mapSave)
        {
            if (mapMarkerManager == null || mapSave.customMarkers == null) return;

            for (var i = 0; i < mapSave.customMarkers.Count; i++)
            {
                var saved = mapSave.customMarkers[i];
                if (string.IsNullOrWhiteSpace(saved.markerId)) continue;
                if (saved.markerId.StartsWith("dyn:", System.StringComparison.Ordinal)) continue;

                var type = (MapMarkerType)saved.markerType;
                if (type == MapMarkerType.Mission)
                {
                    mapMarkerManager.SetMissionObjective(saved.worldPosition, saved.label);
                    continue;
                }

                if (type == MapMarkerType.POI && mapStateService != null)
                {
                    mapStateService.RegisterPoi(new MapPoiRuntimeData(saved.markerId, saved.label, saved.worldPosition));
                    continue;
                }

                mapMarkerManager.RegisterMarker(new MapMarkerRuntimeData(
                    saved.markerId,
                    type,
                    saved.worldPosition,
                    saved.label));
            }
        }

        private static bool CanApplyMapSave(MapSaveData mapSave)
        {
            if (!ProceduralWorldSession.IsActive) return true;
            if (mapSave.worldSeed == 0) return true;

            if (mapSave.worldSeed == ProceduralWorldSession.WorldSeed) return true;

            Debug.LogWarning(
                $"[MapSaveProvider] Skipping map restore: save seed {mapSave.worldSeed} != session seed {ProceduralWorldSession.WorldSeed}.");
            return false;
        }

        private static void LogConsistency(string phase, int saveCount, int runtimeCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (saveCount == runtimeCount) return;

            Debug.LogWarning(
                $"[MapSaveProvider] Map chunk count mismatch on {phase}: save={saveCount}, runtime={runtimeCount}.");
#endif
        }

        private void EnsureReferences()
        {
            if (mapStateService == null) mapStateService = Object.FindFirstObjectByType<MapStateService>();
            if (mapMarkerManager == null) mapMarkerManager = MapMarkerManager.Instance
                                                              ?? Object.FindFirstObjectByType<MapMarkerManager>();
        }
    }
}
