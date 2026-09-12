using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Core
{
    [Serializable]
    public sealed class MapMarkerSaveData
    {
        public string markerId = string.Empty;
        public int markerType;
        public Vector3 worldPosition;
        public string label = string.Empty;
    }

    [Serializable]
    public sealed class MapPoiSaveData
    {
        public string poiId = string.Empty;
        public string label = string.Empty;
        public Vector3 worldPosition;
    }

    [Serializable]
    public sealed class MapSaveData
    {
        public bool hasData;
        public int worldSeed;
        public List<Vector2Int> discoveredChunks = new();
        public List<MapPoiSaveData> pois = new();
        public List<MapMarkerSaveData> customMarkers = new();
        public bool hasWaypoint;
        public Vector3 waypointWorldPosition;
        public float mapZoom = 1f;
        public Vector2 mapPan;
    }
}
