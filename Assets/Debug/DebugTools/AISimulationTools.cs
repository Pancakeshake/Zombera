using UnityEngine;
using Zombera.AI;
using Zombera.AI.Brains;
using Zombera.Debugging.DebugLogging;
using Zombera.Systems;

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    /// Debug controls for AI simulation state.
    /// Responsibilities:
    /// - Toggle AI updates
    /// - Provide future step/single-tick hooks
    /// </summary>
    public sealed class AISimulationTools : MonoBehaviour, IDebugTool
    {
        [Header("Runtime State")]
        [SerializeField] private bool aiEnabled = true;

        public string ToolName => nameof(AISimulationTools);
        public bool IsToolEnabled { get; private set; } = true;
        public bool AIEnabled => aiEnabled;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        public void ToggleAISimulation()
        {
            SetAISimulationEnabled(!aiEnabled);
        }

        public void SetAISimulationEnabled(bool enabled)
        {
            aiEnabled = enabled;

            UnitBrain[] brains = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);

            for (int i = 0; i < brains.Length; i++)
            {
                brains[i].SetBrainActive(aiEnabled);
                brains[i].enabled = aiEnabled;
            }

            ZombieAI[] zombieAIs = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            for (int i = 0; i < zombieAIs.Length; i++)
            {
                zombieAIs[i].SetActive(aiEnabled);
            }

            SquadAI[] squadAIs = FindObjectsByType<SquadAI>(FindObjectsSortMode.None);
            for (int i = 0; i < squadAIs.Length; i++)
            {
                squadAIs[i].enabled = aiEnabled;
            }

            DebugLogger.Log(LogCategory.AI, $"AI simulation {(aiEnabled ? "enabled" : "disabled")}", this);

            _ = aiThrottleIntervalSeconds; // reserved until throttle UI is wired
        }

        [SerializeField, Min(0f)] private float aiThrottleIntervalSeconds = 0.1f;
        private bool singleStepPending;

        /// <summary>
        /// In single-step mode, triggers exactly one AI tick then pauses.
        /// Call repeatedly to step the simulation forward one tick at a time.
        /// </summary>
        public void RequestSingleStep()
        {
            singleStepPending = true;
            SetAISimulationEnabled(true);
            // Caller must call PauseSingleStep() after observing results.
        }

        public void PauseSingleStep()
        {
            if (singleStepPending)
            {
                SetAISimulationEnabled(false);
                singleStepPending = false;
            }
        }

        /// <summary>Sets the minimum tick interval for all ZombieAI components.</summary>
        public void SetZombieThrottle(float intervalSeconds)
        {
            aiThrottleIntervalSeconds = Mathf.Max(0f, intervalSeconds);
            Zombera.AI.ZombieAI[] zombies = FindObjectsByType<Zombera.AI.ZombieAI>(FindObjectsSortMode.None);

            for (int i = 0; i < zombies.Length; i++)
            {
                zombies[i].SetAITickInterval(aiThrottleIntervalSeconds);
            }
        }
    }
}