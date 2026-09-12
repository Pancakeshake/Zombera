using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.World
{
    public sealed partial class MapMarkerManager
    {
        private const string DynamicPrefix = "dyn:";

        private void SyncDynamicMarkers()
        {
            if (_mapStateService == null)
                _mapStateService = FindFirstObjectByType<MapStateService>();

            // Markers update in place by id; only markers absent this sync get removed.
            _dynamicKeysWrittenThisSync.Clear();

            SyncPlayerMarker();
            SyncSquadMarkers();
            SyncPoiMarkers();
            SyncWaypointMarker();
            SyncMissionMarker();

            RemoveStaleDynamicMarkers();
        }

        private void RemoveStaleDynamicMarkers()
        {
            _dynamicRemovalBuffer.Clear();
            foreach (var key in _markers.Keys)
            {
                if (key.StartsWith(DynamicPrefix, System.StringComparison.Ordinal)
                    && !_dynamicKeysWrittenThisSync.Contains(key))
                    _dynamicRemovalBuffer.Add(key);
            }

            for (var i = 0; i < _dynamicRemovalBuffer.Count; i++)
                _markers.Remove(_dynamicRemovalBuffer[i]);
        }

        private void SyncPlayerMarker()
        {
            if (_mapStateService == null) return;

            var position = _mapStateService.CurrentPlayerWorldPosition;
            var heading = ResolvePlayerHeadingDegrees();
            RegisterMarker(new MapMarkerRuntimeData(
                DynamicPrefix + "player",
                MapMarkerType.Player,
                position,
                "Player",
                headingDegrees: heading));
        }

        private void SyncSquadMarkers()
        {
            if (!SquadManager.HasInstance) return;

            var members = SquadManager.Instance.SquadMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null) continue;

                var unit = member.Unit != null ? member.Unit : member.GetComponent<Unit>();
                if (unit == null || unit.Role == UnitRole.Player) continue;

                var memberId = string.IsNullOrWhiteSpace(member.MemberId) ? member.name : member.MemberId;
                RegisterMarker(new MapMarkerRuntimeData(
                    DynamicPrefix + "squad:" + memberId,
                    MapMarkerType.Squad,
                    member.transform.position,
                    unit.name));
            }
        }

        private void SyncPoiMarkers()
        {
            if (_mapStateService == null) return;

            foreach (var poi in _mapStateService.Pois)
            {
                if (!poi.IsValid) continue;

                RegisterMarker(new MapMarkerRuntimeData(
                    DynamicPrefix + "poi:" + poi.poiId,
                    MapMarkerType.POI,
                    poi.worldPosition,
                    poi.label));
            }
        }

        private void SyncWaypointMarker()
        {
            if (_mapStateService == null || !_mapStateService.HasWaypoint) return;

            RegisterMarker(new MapMarkerRuntimeData(
                DynamicPrefix + "waypoint",
                MapMarkerType.Waypoint,
                _mapStateService.WaypointWorldPosition,
                "Waypoint"));
        }

        private void SyncMissionMarker()
        {
            if (!_hasMissionObjective) return;

            RegisterMarker(new MapMarkerRuntimeData(
                DynamicPrefix + "mission",
                MapMarkerType.Mission,
                _missionObjectivePosition,
                string.IsNullOrWhiteSpace(_missionObjectiveLabel) ? "Objective" : _missionObjectiveLabel));
        }

        private float ResolvePlayerHeadingDegrees()
        {
            if (_cachedPlayerUnit == null || !_cachedPlayerUnit.IsAlive)
            {
                var unitManager = UnitManager.Instance;
                _cachedPlayerUnit = unitManager != null ? unitManager.FindFirstUnitByRole(UnitRole.Player) : null;
            }

            if (_cachedPlayerUnit == null) return 0f;

            return _cachedPlayerUnit.transform.eulerAngles.y;
        }
    }
}
