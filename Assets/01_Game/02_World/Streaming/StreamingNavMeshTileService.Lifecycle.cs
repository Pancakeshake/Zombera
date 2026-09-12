using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World.Roads;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private void Awake()
        {
            _runtimeInstance = this;
            if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (playerSpawner != null) _ownerScene = playerSpawner.gameObject.scene;
        }

        private void OnEnable()
        {
            _runtimeInstance = this;
            TunnelNavMeshBakeHooks.RequestEnqueueOverlappingFloors = EnqueueOverlappingFloorsIfPresent;
            if (roadSystem == null) roadSystem = FindFirstObjectByType<ProceduralRoadSystem>();

            if (roadSystem != null)
                roadSystem.RoadsApplied += HandleRoadsApplied;

            SubscribeWorldTileStream();
        }

        private void OnDisable()
        {
            if (TunnelNavMeshBakeHooks.RequestEnqueueOverlappingFloors == EnqueueOverlappingFloorsIfPresent)
                TunnelNavMeshBakeHooks.RequestEnqueueOverlappingFloors = null;

            if (_runtimeInstance == this)
                _runtimeInstance = null;

            if (roadSystem != null)
                roadSystem.RoadsApplied -= HandleRoadsApplied;

            UnsubscribeWorldTileStream();
            _pendingTileBakeQueue.Clear();
            _pendingTileBakeSet.Clear();
            _pendingTileByCoord.Clear();
            CancelAllPendingAsyncTileBakes();
            RemoveAllTiles();
        }

        private void Update()
        {
            var bakedAnyFromAsync = DrainCompletedAsyncTileBakes();
            if (bakedAnyFromAsync)
            {
                LastBootstrapHadTriangles = true;
                MaybePruneAfterSuccessfulBake();
            }

            ProcessQueuedTileBakes();
        }
    }
}
