using UnityEngine;
using Zombera.AI;
using Zombera.AI.Brains;
using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates AI systems and tick-based simulation toggles.
    /// Manager owns orchestration only and avoids behavior implementation.
    /// </summary>
    public sealed class AIManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private bool aiEnabled = true;
        [SerializeField] private ZombieManager zombieManager;

        public bool IsInitialized { get; private set; }
        public bool AIEnabled => aiEnabled;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            ApplyAIEnabledState();

            // TODO: Register per-faction AI schedulers.
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;

            // TODO: Persist AI diagnostic snapshots.
        }

        public void SetAIEnabled(bool enabled)
        {
            aiEnabled = enabled;
            ApplyAIEnabledState();
        }

        public void ToggleAI()
        {
            SetAIEnabled(!aiEnabled);
        }

        private void ApplyAIEnabledState()
        {
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

            SurvivorAI[] survivorAIs = FindObjectsOfType<SurvivorAI>();

            for (int i = 0; i < survivorAIs.Length; i++)
            {
                survivorAIs[i].enabled = aiEnabled;
            }

            _ = zombieManager;
        }
    }
}