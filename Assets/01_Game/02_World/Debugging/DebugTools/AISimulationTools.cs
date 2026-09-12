#region

using UnityEngine;
using Zombera.AI;
using Zombera.Debugging.DebugLogging;
using Zombera.Systems;

#endregion

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    ///     Debug controls for AI simulation state.
    ///     Responsibilities:
    ///     - Toggle AI updates
    ///     - Provide future step/single-tick hooks
    /// </summary>
    public sealed class AISimulationTools : MonoBehaviour, IDebugTool
    {
        [Header("Runtime State")] [SerializeField]
        private bool aiEnabled = true;

        [SerializeField] [Min(0f)] private float aiThrottleIntervalSeconds = 0.1f;
        private bool _singleStepPending;
        public bool AIEnabled => aiEnabled;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(AISimulationTools);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        public void ToggleAISimulation()
        {
            SetAISimulationEnabled(!aiEnabled);
        }

        public void SetAISimulationEnabled(bool isEnabled)
        {
            aiEnabled = isEnabled;

            var zombieAIs = FindObjectsByType<ZombieController>(FindObjectsSortMode.None);
            foreach (var zombieAi in zombieAIs)
            {
                if (zombieAi == null) continue;
                zombieAi.SetActive(aiEnabled);
            }

            var squadAIs = FindObjectsByType<SquadController>(FindObjectsSortMode.None);
            foreach (var squadAi in squadAIs)
            {
                if (squadAi == null) continue;
                squadAi.enabled = aiEnabled;
            }

            DebugLogger.Log(LogCategory.AI, $"AI simulation {(aiEnabled ? "enabled" : "disabled")}", this);

            _ = aiThrottleIntervalSeconds; // reserved until throttle UI is wired
        }

        /// <summary>
        ///     In single-step mode, triggers exactly one AI tick then pauses.
        ///     Call repeatedly to step the simulation forward one tick at a time.
        /// </summary>
        [ContextMenu("AI/Request Single Step")]
        // ReSharper disable once UnusedMember.Global
        public void RequestSingleStep()
        {
            _singleStepPending = true;
            SetAISimulationEnabled(true);
            // Caller must call PauseSingleStep() after observing results.
        }

        [ContextMenu("AI/Pause Single Step")]
        // ReSharper disable once UnusedMember.Global
        public void PauseSingleStep()
        {
            if (!_singleStepPending) return;

            SetAISimulationEnabled(false);
            _singleStepPending = false;
        }

        /// <summary>Sets the minimum tick interval for all ZombieController components.</summary>
        public void SetZombieThrottle(float intervalSeconds)
        {
            aiThrottleIntervalSeconds = Mathf.Max(0f, intervalSeconds);
            var zombies = FindObjectsByType<ZombieController>(FindObjectsSortMode.None);

            foreach (var zombie in zombies)
            {
                if (zombie == null) continue;
                zombie.SetAITickInterval(aiThrottleIntervalSeconds);
            }
        }

        [ContextMenu("AI/Set Zombie Throttle (0.1s)")]
        // ReSharper disable once UnusedMember.Global
        private void SetZombieThrottle_0_1s()
        {
            SetZombieThrottle(0.1f);
        }
    }
}