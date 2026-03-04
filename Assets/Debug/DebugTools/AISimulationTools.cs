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

            UnitBrain[] brains = FindObjectsOfType<UnitBrain>();

            for (int i = 0; i < brains.Length; i++)
            {
                brains[i].SetBrainActive(aiEnabled);
                brains[i].enabled = aiEnabled;
            }

            ZombieAI[] zombieAIs = FindObjectsOfType<ZombieAI>();
            for (int i = 0; i < zombieAIs.Length; i++)
            {
                zombieAIs[i].SetActive(aiEnabled);
            }

            SquadAI[] squadAIs = FindObjectsOfType<SquadAI>();
            for (int i = 0; i < squadAIs.Length; i++)
            {
                squadAIs[i].enabled = aiEnabled;
            }

            DebugLogger.Log(LogCategory.AI, $"AI simulation {(aiEnabled ? "enabled" : "disabled")}", this);

            // TODO: Add AI single-step simulation mode.
            // TODO: Add AI throttle controls per faction/type.
        }
    }
}