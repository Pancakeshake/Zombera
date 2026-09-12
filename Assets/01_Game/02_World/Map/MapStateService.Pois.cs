using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    public sealed partial class MapStateService
    {
        private readonly Dictionary<string, MapPoiRuntimeData> _pois = new();

        public IReadOnlyCollection<MapPoiRuntimeData> Pois => _pois.Values;

        public event Action<MapPoiRuntimeData> PoiAdded;
        public event Action<string> PoiRemoved;

        public bool TryGetPoi(string poiId, out MapPoiRuntimeData poi)
        {
            if (string.IsNullOrWhiteSpace(poiId))
            {
                poi = default;
                return false;
            }

            return _pois.TryGetValue(poiId, out poi);
        }

        public bool RegisterPoi(MapPoiRuntimeData poi)
        {
            if (!poi.IsValid) return false;

            var isNew = !_pois.ContainsKey(poi.poiId);
            _pois[poi.poiId] = poi;

            if (isNew) PoiAdded?.Invoke(poi);
            return true;
        }

        public bool UnregisterPoi(string poiId)
        {
            if (string.IsNullOrWhiteSpace(poiId)) return false;
            if (!_pois.Remove(poiId)) return false;

            PoiRemoved?.Invoke(poiId);
            return true;
        }

        public void ClearPois()
        {
            if (_pois.Count == 0) return;

            _pois.Clear();
        }
    }
}
