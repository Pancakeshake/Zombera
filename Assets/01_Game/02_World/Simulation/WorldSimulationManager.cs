#region

using UnityEngine;
using Zombera.World.Regions;

#endregion

namespace Zombera.World.Simulation
{
    /// <summary>
    ///     Central distant-world simulation coordinator.
    ///     Responsibilities:
    ///     - run periodic world simulation ticks
    ///     - update region simulation layers by distance
    ///     - drive abstract horde/survivor simulation
    ///     - trigger dynamic world events through world systems
    /// </summary>
    public sealed class WorldSimulationManager : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private Transform playerTransform;

        [SerializeField] private RegionManager regionManager;
        [SerializeField] private HordeManager hordeManager;
        [SerializeField] private SurvivorManager survivorManager;
        [SerializeField] private WorldEventSystem worldEventSystem;
        [SerializeField] private WorldStateSimulationBridge worldStateSimulationBridge;

        [Header("Simulation Layers")] [SerializeField]
        private float fullSimulationRange = 80f;

        [SerializeField] private float reducedSimulationRange = 250f;

        [Header("Simulation Tick")] [SerializeField]
        private float simulationTickInterval = 10f;

        [SerializeField] private bool simulationActive = true;

        [Header("Events")] [SerializeField] [Range(0f, 1f)]
        private float worldEventChancePerTick = 0.25f;

        [Header("Debug")] [SerializeField] private bool logSimulationEvents;

        [SerializeField] private bool visualizeLayerRanges = true;

        [Header("Determinism & Budget")] [SerializeField]
        private int simulationSeed = 54321;

        [SerializeField] [Min(1)] private int maxRegionsTickedPerFrame = 4;

        private float _simulationTimer;

        // ReSharper disable once UnusedMember.Global
        public bool SimulationActive => simulationActive;
        // ReSharper disable once UnusedMember.Global
        public float FullSimulationRange => fullSimulationRange;
        // ReSharper disable once UnusedMember.Global
        public float ReducedSimulationRange => reducedSimulationRange;
        // ReSharper disable once UnusedMember.Global
        public float SimulationTickInterval => simulationTickInterval;

        // ReSharper disable once UnusedMember.Global
        public int SimulationSeed => simulationSeed;
        // ReSharper disable once UnusedMember.Global
        public int MaxRegionsTickedPerFrame => maxRegionsTickedPerFrame;

        private void OnDrawGizmosSelected()
        {
            if (!visualizeLayerRanges || playerTransform == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(playerTransform.position, fullSimulationRange);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(playerTransform.position, reducedSimulationRange);
        }

        public void InjectWorldEventSystem(WorldEventSystem eventSystem)
        {
            if (eventSystem != null) worldEventSystem = eventSystem;
        }

        public void InitializeSimulation(Transform trackedPlayer = null)
        {
            if (trackedPlayer != null) playerTransform = trackedPlayer;

            _simulationTimer = 0f;
            regionManager?.InitializeRegions();
            SynchronizeWorldStateSimulation();
            LogEvent("World simulation initialized.");
        }

        public void SetSimulationActive(bool active)
        {
            simulationActive = active;
        }

        public void RefreshSimulationLayers(Vector3 playerPosition)
        {
            var cap = Mathf.Max(1, maxRegionsTickedPerFrame);
            regionManager?.RefreshRegionLayers(playerPosition, fullSimulationRange, reducedSimulationRange, cap);
        }

        public void TickSimulation(float deltaTime, Vector3 playerPosition)
        {
            if (!simulationActive) return;

            _simulationTimer += Mathf.Max(0f, deltaTime);

            if (_simulationTimer < simulationTickInterval) return;

            _simulationTimer = 0f;

            SynchronizeWorldStateSimulation();
            RefreshSimulationLayers(playerPosition);
            hordeManager?.SimulateHordes(simulationTickInterval, regionManager != null ? regionManager.Regions : null,
                survivorManager, logSimulationEvents);
            survivorManager?.SimulateSurvivorGroups(simulationTickInterval,
                regionManager != null ? regionManager.Regions : null, logSimulationEvents);
            regionManager?.UpdateRegionStatsFromSimulation(hordeManager, survivorManager);

            if (worldEventSystem != null && Random.value <= worldEventChancePerTick)
                worldEventSystem.TriggerRandomEvent(playerPosition);

            LogEvent($"Simulation tick complete at player position {playerPosition}.");
        }

        public void SetSimulationSeed(int seed)
        {
            simulationSeed = seed;
            Random.InitState(seed);
        }

        // ReSharper disable once UnusedMember.Global
        public void ForceSimulationTick(Vector3 playerPosition, float overrideDelta = -1f)
        {
            var delta = overrideDelta > 0f ? overrideDelta : simulationTickInterval;
            _simulationTimer = simulationTickInterval;
            TickSimulation(delta, playerPosition);
        }

        private void LogEvent(string message)
        {
            if (!logSimulationEvents) return;

            Debug.Log($"[WorldSimulationManager] {message}", this);
        }

        private void SynchronizeWorldStateSimulation()
        {
            EnsureWorldStateSimulationBridge();
            _ = worldStateSimulationBridge != null && worldStateSimulationBridge.SynchronizeToDayNight();
        }

        private void EnsureWorldStateSimulationBridge()
        {
            if (worldStateSimulationBridge != null)
                return;

            if (!TryGetComponent(out worldStateSimulationBridge))
                worldStateSimulationBridge = gameObject.AddComponent<WorldStateSimulationBridge>();
        }
    }
}