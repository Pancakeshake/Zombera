using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

namespace Zombera.World
{
    /// <summary>
    ///     Central registry for map/minimap markers with per-type visibility filters.
    /// </summary>
    public sealed partial class MapMarkerManager : MonoBehaviour
    {
        private static MapMarkerManager _instance;

        private readonly Dictionary<string, MapMarkerRuntimeData> _markers = new();
        private readonly HashSet<MapMarkerType> _enabledTypes = new();
        private readonly List<MapMarkerRuntimeData> _visibleBuffer = new();
        private readonly List<string> _dynamicRemovalBuffer = new();
        private readonly HashSet<string> _dynamicKeysWrittenThisSync = new();

        [Tooltip("How often per second dynamic markers (player, squad, POI, waypoint) are re-synced. Markers update in place; stale ones are removed.")]
        [SerializeField] [Range(1f, 30f)] private float dynamicMarkerSyncsPerSecond = 10f;
        private float _nextDynamicSyncAt;

        private MapStateService _mapStateService;
        private Zombera.Characters.Unit _cachedPlayerUnit;
        private bool _isSyncingMarkers;
        private bool _hasMissionObjective;
        private Vector3 _missionObjectivePosition;
        private string _missionObjectiveLabel = string.Empty;

        public static MapMarkerManager Instance => _instance;

        public event Action MarkersChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            EnableAllMarkerTypes();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (!IsWorldSessionActive()) return;

            // Throttled incremental sync instead of a full wipe-and-rebuild every frame.
            if (Time.unscaledTime < _nextDynamicSyncAt) return;
            _nextDynamicSyncAt = Time.unscaledTime + 1f / Mathf.Max(1f, dynamicMarkerSyncsPerSecond);

            _isSyncingMarkers = true;
            SyncDynamicMarkers();
            _isSyncingMarkers = false;
        }

        public void Configure(MapStateService mapStateService)
        {
            _mapStateService = mapStateService;
        }

        public void EnableAllMarkerTypes()
        {
            _enabledTypes.Clear();
            foreach (MapMarkerType type in Enum.GetValues(typeof(MapMarkerType)))
                _enabledTypes.Add(type);
        }

        public void SetMarkerTypeVisible(MapMarkerType type, bool visible)
        {
            if (visible) _enabledTypes.Add(type);
            else _enabledTypes.Remove(type);

            MarkersChanged?.Invoke();
        }

        public bool IsMarkerTypeVisible(MapMarkerType type)
        {
            return _enabledTypes.Contains(type);
        }

        public bool RegisterMarker(MapMarkerRuntimeData marker)
        {
            if (!marker.IsValid) return false;

            _markers[marker.markerId] = marker;

            if (_isSyncingMarkers)
            {
                if (marker.markerId.StartsWith(DynamicPrefix, StringComparison.Ordinal))
                    _dynamicKeysWrittenThisSync.Add(marker.markerId);
                return true;
            }

            MarkersChanged?.Invoke();
            return true;
        }

        public bool UnregisterMarker(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId)) return false;
            if (!_markers.Remove(markerId)) return false;

            MarkersChanged?.Invoke();
            return true;
        }

        public void ClearMarkers()
        {
            if (_markers.Count == 0) return;

            _markers.Clear();
            MarkersChanged?.Invoke();
        }

        public IReadOnlyList<MapMarkerRuntimeData> GetVisibleMarkers(List<MapMarkerRuntimeData> buffer = null)
        {
            buffer ??= _visibleBuffer;
            buffer.Clear();

            foreach (var marker in _markers.Values)
            {
                if (!_enabledTypes.Contains(marker.type)) continue;
                buffer.Add(marker);
            }

            return buffer;
        }

        public int VisibleMarkerCount => GetVisibleMarkers().Count;

        public void SetMissionObjective(Vector3 worldPosition, string label)
        {
            _hasMissionObjective = true;
            _missionObjectivePosition = worldPosition;
            _missionObjectiveLabel = label ?? string.Empty;
        }

        public void ClearMissionObjective()
        {
            _hasMissionObjective = false;
            _missionObjectiveLabel = string.Empty;
        }

        private static bool IsWorldSessionActive()
        {
            return WorldSessionGate.IsWorldSessionActive;
        }
    }
}
