#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Debugging;
using Zombera.Systems;

#endregion

namespace Zombera.World
{
    public partial class WorldManager
    {
        public void ForceRefreshChunks()
        {
            if (!TryResolvePlayerTransform()) return;

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator, tileStreamBridge);
            chunkCache?.Clear();
        }

        public void TeleportPlayer(Transform target, Vector3 destination)
        {
            if (target == null) return;

            target.position = destination;
            playerTransform = target;
            ForceRefreshChunks();
        }

        private void RunChunkStreamingTick()
        {
            ResolveRuntimeReferencesIfNeeded();
            EnforceWorldSpawnedBuildingMode();

            if (!TryResolvePlayerTransform()) return;

            using (PerfTrace.Measure("World: ChunkLoader.UpdateStreaming", chunkLoader))
            {
                chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator, tileStreamBridge);
            }

            mapStateService?.TickPlayer(playerTransform.position);

            tileStreamBridge?.RefreshTileMetrics();
            worldBuildingMaterializer?.UpdateStreaming(playerTransform.position);
            worldSimulationManager?.RefreshSimulationLayers(playerTransform.position);

            lootSpawner?.TickNearPlayer(playerTransform.position);
        }

        private void RunWorldSimulationTick()
        {
            if (!IsCharacterSimulationReady()) return;

            if (!TryResolvePlayerTransform()) return;

            EnsureZombieManagerReady();

            if (worldSimulationManager != null)
                worldSimulationManager.TickSimulation(worldSimulationInterval, playerTransform.position);
            else
                worldEventSystem?.TickDynamicEvents(playerTransform.position);

            CoreEventBus.PublishGlobal(new WorldSimulationTickEvent
            {
                DeltaTime = worldSimulationInterval,
                PlayerPosition = playerTransform.position
            });

            if (CoreEventBus.Instance == null) zombieManager?.TickAmbientSpawn(playerTransform.position);

            worldEventSystem?.TickDynamicEvents(playerTransform.position);
        }

        private bool IsCharacterSimulationReady()
        {
            if (!enforceOrderedCharacterSpawn) return true;
            if (!IsCharacterSpawnDependencyOrderReady(out _)) return false;

            if (_cachedPlayerSpawner == null)
                _cachedPlayerSpawner = FindFirstObjectByType<PlayerSpawner>(FindObjectsInactive.Include);

            return _cachedPlayerSpawner == null || _cachedPlayerSpawner.HasFinalizedWorldPlayerSpawn;
        }

        private bool TryResolvePlayerTransform()
        {
            if (playerTransform != null) return true;

            if (UnitManager.Instance == null) return false;

            var players = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, _playerUnitBuffer);

            if (players.Count <= 0 || players[0] == null) return false;

            playerTransform = players[0].transform;
            return true;
        }

        private void EnsureZombieManagerReady()
        {
            if (zombieManager == null) zombieManager = FindFirstObjectByType<ZombieManager>();

            if (zombieManager == null)
            {
                var runtimeRoot = GameObject.Find("RuntimeWorldSystems");
                if (runtimeRoot == null) runtimeRoot = new GameObject("RuntimeWorldSystems");

                zombieManager = runtimeRoot.AddComponent<ZombieManager>();
            }

            if (zombieManager != null && !zombieManager.IsInitialized) zombieManager.Initialize();
        }

        private void TrySpawnValidationZombieNearPlayer()
        {
            if (_validationZombieAttemptFinished) return;
            if (!IsCharacterSimulationReady()) return;

            var now = Time.unscaledTime;
            if (now < _nextValidationZombieAttemptAt) return;

            _nextValidationZombieAttemptAt = now + Mathf.Max(0.25f, validationZombieAttemptIntervalSeconds);

            if (!TryResolvePlayerTransform()) return;

            EnsureZombieManagerReady();
            if (zombieManager == null) return;

            if (!zombieManager.ValidationSpawnEnabled || zombieManager.HasSpawnedValidationZombieNearPlayer)
            {
                _validationZombieAttemptFinished = true;
                return;
            }

            if (zombieManager.TrySpawnValidationZombieNearPlayer(playerTransform.position))
                _validationZombieAttemptFinished = true;
        }
    }
}
