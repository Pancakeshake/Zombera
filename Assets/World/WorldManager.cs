using UnityEngine;
using Zombera.Core;

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
        [SerializeField] private RegionSystem regionSystem;

        [Header("Dynamic Events")]
        [SerializeField] private WorldEventSystem worldEventSystem;

        [Header("Simulation")]
        [SerializeField] private float chunkStreamingTickInterval = 0.25f;
        [SerializeField] private float worldSimulationInterval = 10f;

        public bool IsSimulationActive { get; private set; }

        private float chunkStreamingTickTimer;
        private float worldSimulationTimer;

        public void InitializeWorld()
        {
            IsSimulationActive = true;
            chunkStreamingTickTimer = 0f;
            worldSimulationTimer = 0f;

            // TODO: Build world seed/session metadata and prime initial chunks.
            // TODO: Initialize world event cadence and runtime entity pools.
        }

        public void SetSimulationActive(bool active)
        {
            IsSimulationActive = active;

            // TODO: Pause/resume world subsystems at a granular level.
        }

        public void ForceRefreshChunks()
        {
            if (playerTransform == null)
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
            if (playerTransform == null)
            {
                return;
            }

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator);

            // TODO: Tick near-player systems that require frequent updates.
        }

        private void RunWorldSimulationTick()
        {
            if (playerTransform == null)
            {
                return;
            }

            worldEventSystem?.TickDynamicEvents(playerTransform.position);

            EventSystem.PublishGlobal(new WorldSimulationTickEvent
            {
                DeltaTime = worldSimulationInterval,
                PlayerPosition = playerTransform.position
            });

            // TODO: Tick lightweight world simulation systems (AI director, ambient systems).
        }
    }
}