using UnityEngine;
using Zombera.World.Regions;

namespace Zombera.World.Simulation
{
    /// <summary>
    /// Central distant-world simulation coordinator.
    /// Responsibilities:
    /// - run periodic world simulation ticks
    /// - update region simulation layers by distance
    /// - drive abstract horde/survivor simulation
    /// - trigger dynamic world events through world systems
    /// </summary>
    public sealed class WorldSimulationManager : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private HordeManager hordeManager;
        [SerializeField] private SurvivorManager survivorManager;
        [SerializeField] private WorldEventSystem worldEventSystem;

        [Header("Simulation Layers")]
        [SerializeField] private float fullSimulationRange = 80f;
        [SerializeField] private float reducedSimulationRange = 250f;

        [Header("Simulation Tick")]
        [SerializeField] private float simulationTickInterval = 10f;
        [SerializeField] private bool simulationActive = true;

        [Header("Events")]
        [SerializeField, Range(0f, 1f)] private float worldEventChancePerTick = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool logSimulationEvents = true;
        [SerializeField] private bool visualizeLayerRanges = true;

        private float simulationTimer;

        public bool SimulationActive => simulationActive;
        public float FullSimulationRange => fullSimulationRange;
        public float ReducedSimulationRange => reducedSimulationRange;
        public float SimulationTickInterval => simulationTickInterval;

        public void InjectWorldEventSystem(WorldEventSystem eventSystem)
        {
            if (eventSystem != null)
            {
                worldEventSystem = eventSystem;
            }
        }

        public void InitializeSimulation(Transform trackedPlayer = null)
        {
            if (trackedPlayer != null)
            {
                playerTransform = trackedPlayer;
            }

            simulationTimer = 0f;
            regionManager?.InitializeRegions();
            LogEvent("World simulation initialized.");
        }

        public void SetSimulationActive(bool active)
        {
            simulationActive = active;
        }

        public void RefreshSimulationLayers(Vector3 playerPosition)
        {
            int cap = Mathf.Max(1, maxRegionsTickedPerFrame);
            regionManager?.RefreshRegionLayers(playerPosition, fullSimulationRange, reducedSimulationRange, cap);
        }

        public void TickSimulation(float deltaTime, Vector3 playerPosition)
        {
            if (!simulationActive)
            {
                return;
            }

            simulationTimer += Mathf.Max(0f, deltaTime);

            if (simulationTimer < simulationTickInterval)
            {
                return;
            }

            simulationTimer = 0f;

            RefreshSimulationLayers(playerPosition);
            hordeManager?.SimulateHordes(simulationTickInterval, regionManager != null ? regionManager.Regions : null, survivorManager, logSimulationEvents);
            survivorManager?.SimulateSurvivorGroups(simulationTickInterval, regionManager != null ? regionManager.Regions : null, logSimulationEvents);
            regionManager?.UpdateRegionStatsFromSimulation(hordeManager, survivorManager);

            if (worldEventSystem != null && Random.value <= worldEventChancePerTick)
            {
                worldEventSystem.TriggerRandomEvent(playerPosition);
            }

            LogEvent($"Simulation tick complete at player position {playerPosition}.");
        }

        [Header("Determinism & Budget")]
        [SerializeField] private int simulationSeed = 54321;
        [SerializeField, Min(1)] private int maxRegionsTickedPerFrame = 4;

        public int SimulationSeed => simulationSeed;
        public int MaxRegionsTickedPerFrame => maxRegionsTickedPerFrame;

        public void SetSimulationSeed(int seed)
        {
            simulationSeed = seed;
            Random.InitState(seed);
        }

        public void ForceSimulationTick(Vector3 playerPosition, float overrideDelta = -1f)
        {
            float delta = overrideDelta > 0f ? overrideDelta : simulationTickInterval;
            simulationTimer = simulationTickInterval;
            TickSimulation(delta, playerPosition);
        }

        private void OnDrawGizmosSelected()
        {
            if (!visualizeLayerRanges || playerTransform == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(playerTransform.position, fullSimulationRange);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(playerTransform.position, reducedSimulationRange);
        }

        private void LogEvent(string message)
        {
            if (!logSimulationEvents)
            {
                return;
            }

            Debug.Log($"[WorldSimulationManager] {message}", this);
        }
    }
}