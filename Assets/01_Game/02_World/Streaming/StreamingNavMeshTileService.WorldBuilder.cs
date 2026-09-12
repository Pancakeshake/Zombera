using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    /// <summary>
    ///     World-builder tile stream subscriptions and ContentReady / RoadsApplied enqueue.
    /// </summary>
    public sealed partial class StreamingNavMeshTileService
    {
        public void ConfigureWorldTileStream(
            WorldTileStreamSource stream,
            IWorldGenerationBackend backend)
        {
            UnsubscribeWorldTileStream();
            worldTileStream = stream;
            _worldGenerationBackend = backend;
            SubscribeWorldTileStream();
        }

        private void SubscribeWorldTileStream()
        {
            if (worldTileStream == null || _subscribedWorldTileStream) return;
            worldTileStream.TileStateChanged += HandleWorldTileStateChanged;
            worldTileStream.BeforeTileReset += HandleWorldTileBeforeReset;
            _subscribedWorldTileStream = true;
        }

        private void UnsubscribeWorldTileStream()
        {
            if (!_subscribedWorldTileStream || worldTileStream == null)
            {
                _subscribedWorldTileStream = false;
                return;
            }

            worldTileStream.TileStateChanged -= HandleWorldTileStateChanged;
            worldTileStream.BeforeTileReset -= HandleWorldTileBeforeReset;
            _subscribedWorldTileStream = false;
        }

        private void HandleWorldTileStateChanged(WorldTileTransition transition)
        {
            if (!IsDrivingRuntimeNavMesh) return;
            if (!IsWorldSessionStateForNavMeshWork()) return;
            // When a road system is present, wait for RoadsApplied so navmesh includes road colliders.
            if (roadSystem != null) return;
            if (transition.Current != WorldTileState.ContentReady) return;
            if (transition.Tile.Terrain == null || transition.Tile.Terrain.terrainData == null) return;

            QueueTileBake(transition.Tile);
        }

        private void HandleWorldTileBeforeReset(WorldTileInfo tile)
        {
            if (!IsDrivingRuntimeNavMesh) return;
            if (!IsWorldSessionStateForNavMeshWork()) return;
            RemoveTile(tile.Coord);
        }

        private void HandleRoadsApplied(WorldTileInfo tile)
        {
            if (!IsDrivingRuntimeNavMesh) return;
            if (!IsWorldSessionStateForNavMeshWork()) return;
            if (tile.Terrain == null || tile.Terrain.terrainData == null) return;

            if (tile.Terrain.gameObject != null)
            {
                var tileScene = tile.Terrain.gameObject.scene;
                if (!_ownerScene.IsValid()) _ownerScene = tileScene;
                if (_ownerScene.IsValid() && tileScene != _ownerScene)
                {
                    LogSceneMismatch("roads-applied enqueue", tileScene);
                    return;
                }
            }

            QueueTileBake(tile);
        }

        private void RebindNearbyUnitsAfterWorldTileBaked(WorldTileInfo tile)
        {
            if (!UnitManager.HasInstance) return;

            var rect = tile.WorldRectXZ;
            var center = new Vector3(rect.center.x, 0f, rect.center.y);
            var searchRadius = Mathf.Max(rect.width, rect.height) * 0.5f + 10f;
            var unitBuffer = new List<Unit>(16);
            var nearbyUnits = UnitManager.Instance.FindNearbyUnits(center, searchRadius, unitBuffer);

            for (var i = 0; i < nearbyUnits.Count; i++)
            {
                var unit = nearbyUnits[i];
                if (unit == null) continue;

                var pos = unit.transform.position;
                if (!rect.Contains(new Vector2(pos.x, pos.z))) continue;
                if (!unit.TryGetComponent<UnitController>(out var controller)) continue;
                if (controller.AgentIsOnNavMesh) continue;
                if (!UnitNavUtils.IsNavMeshReadyAt(pos)) continue;
                controller.TryEnableAgentOnNavMesh();
            }
        }
    }
}
