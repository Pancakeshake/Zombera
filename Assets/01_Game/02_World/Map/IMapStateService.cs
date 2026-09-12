using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    public interface IMapStateService
    {
        Vector2Int CurrentPlayerChunk { get; }
        Vector3 CurrentPlayerWorldPosition { get; }
        RegionDefinition CurrentRegion { get; }

        bool HasWaypoint { get; }
        Vector3 WaypointWorldPosition { get; }

        float MapZoom { get; }
        Vector2 MapPan { get; }
        RenderTexture FogTexture { get; }

        IReadOnlyCollection<Vector2Int> DiscoveredChunks { get; }
        IReadOnlyCollection<MapPoiRuntimeData> Pois { get; }

        event Action<MapPoiRuntimeData> PoiAdded;
        event Action<string> PoiRemoved;

        event Action<Vector2Int> DiscoveredChunkAdded;

        bool IsChunkDiscovered(Vector2Int chunkCoordinates);
        bool DiscoverChunk(Vector2Int chunkCoordinates);
        void ClearDiscoveredChunks();

        bool TryGetPoi(string poiId, out MapPoiRuntimeData poi);
        bool RegisterPoi(MapPoiRuntimeData poi);
        bool UnregisterPoi(string poiId);
        void ClearPois();

        void SetWaypoint(Vector3 worldPosition);
        void ClearWaypoint();
        void SetView(float zoom, Vector2 pan);
    }
}

