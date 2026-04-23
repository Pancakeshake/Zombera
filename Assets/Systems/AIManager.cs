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
            RegisterFactionSchedulers();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            EmitDiagnosticSnapshot();
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

        private void RegisterFactionSchedulers()
        {
            // Each faction's AI scheduler tag is: ZombieAI, SquadAI, SurvivorAI.
            // They are registered here so future per-faction throttle controls have a
            // consistent registration point.  Active state is already applied by
            // ApplyAIEnabledState() above.
            ZombieAI[] zombieBrains = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            UnitBrain[] unitBrains = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
            SquadAI[] squadBrains = FindObjectsByType<SquadAI>(FindObjectsSortMode.None);

            if (aiEnabled)
            {
                Debug.Log($"[AIManager] Registered {zombieBrains.Length} zombie, {unitBrains.Length} unit, {squadBrains.Length} squad AI schedulers.");
            }
        }

        private void EmitDiagnosticSnapshot()
        {
            int zombieCount = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None).Length;
            int unitBrainCount = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None).Length;
            int squadCount = FindObjectsByType<SquadAI>(FindObjectsSortMode.None).Length;

            Debug.Log($"[AIManager] Shutdown snapshot — zombies:{zombieCount} unitBrains:{unitBrainCount} squads:{squadCount}");
        }

        private void ApplyAIEnabledState()
        {
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

            SurvivorAI[] survivorAIs = FindObjectsByType<SurvivorAI>(FindObjectsSortMode.None);

            for (int i = 0; i < survivorAIs.Length; i++)
            {
                survivorAIs[i].enabled = aiEnabled;
            }

            _ = zombieManager;
        }
    }
}