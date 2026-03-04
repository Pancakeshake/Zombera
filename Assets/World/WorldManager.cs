using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.World.Simulation;

namespace Zombera.World
{
    /// <summary>
    /// Coordinates chunk streaming and world simulation ticks.
    /// </summary>
    public sealed class WorldManager : MonoBehaviour
    {
        [Header("Streaming")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ChunkLoader chunkLoader;
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private ChunkCache chunkCache;
        [SerializeField] private RegionSystem regionSystem;

        [Header("Spawners")]
        [SerializeField] private MapSpawner mapSpawner;
        [SerializeField] private LootSpawner lootSpawner;

        [Header("Dynamic Events")]
        [SerializeField] private WorldEventSystem worldEventSystem;
        [SerializeField] private WorldSimulationManager worldSimulationManager;

        [Header("Simulation")]
        [SerializeField] private float chunkStreamingTickInterval = 0.25f;
        [SerializeField] private float worldSimulationInterval = 10f;
        [SerializeField] private bool initializeOnStart = true;

        public bool IsSimulationActive { get; private set; }

        private float chunkStreamingTickTimer;
        private float worldSimulationTimer;
        private readonly List<Unit> playerUnitBuffer = new List<Unit>();

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeWorld();
            }
        }

        public void InitializeWorld()
        {
            if (IsSimulationActive)
            {
                return;
            }

            TryResolvePlayerTransform();

            IsSimulationActive = true;
            chunkStreamingTickTimer = 0f;
            worldSimulationTimer = 0f;

            chunkCache?.Clear();
            mapSpawner?.SpawnPrototypeMap();
            lootSpawner?.PrimePrototypeLoot();
            worldSimulationManager?.InjectWorldEventSystem(worldEventSystem);
            worldSimulationManager?.InitializeSimulation(playerTransform);

            // TODO: Build world seed/session metadata and prime initial chunks.
            // TODO: Initialize world event cadence and runtime entity pools.
        }

        public void SetSimulationActive(bool active)
        {
            IsSimulationActive = active;
            worldSimulationManager?.SetSimulationActive(active);

            // TODO: Pause/resume world subsystems at a granular level.
        }

        public void ForceRefreshChunks()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator);

            // TODO: Handle hard refresh for teleports/scene transitions.
        }

        private void Update()
        {
            if (!IsSimulationActive)
            {
                return;
            }

            chunkStreamingTickTimer += Time.deltaTime;
            worldSimulationTimer += Time.deltaTime;

            if (chunkStreamingTickTimer >= chunkStreamingTickInterval)
            {
                chunkStreamingTickTimer = 0f;
                RunChunkStreamingTick();
            }

            if (worldSimulationTimer >= worldSimulationInterval)
            {
                worldSimulationTimer = 0f;
                RunWorldSimulationTick();
            }
        }

        private void RunChunkStreamingTick()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator);
            worldSimulationManager?.RefreshSimulationLayers(playerTransform.position);

            // TODO: Tick near-player systems that require frequent updates.
        }

        private void RunWorldSimulationTick()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            if (worldSimulationManager != null)
            {
                worldSimulationManager.TickSimulation(worldSimulationInterval, playerTransform.position);
            }
            else
            {
                worldEventSystem?.TickDynamicEvents(playerTransform.position);
            }

            EventSystem.PublishGlobal(new WorldSimulationTickEvent
            {
                DeltaTime = worldSimulationInterval,
                PlayerPosition = playerTransform.position
            });

            // TODO: Tick lightweight world simulation systems (AI director, ambient systems).
        }

        private bool TryResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return true;
            }

            if (UnitManager.Instance == null)
            {
                return false;
            }

            List<Unit> players = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, playerUnitBuffer);

            if (players.Count <= 0 || players[0] == null)
            {
                return false;
            }

            playerTransform = players[0].transform;
            return true;
        }
    }
}