#region

using UnityEngine;
using Zombera.AI;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Coordinates AI systems and tick-based simulation toggles.
    ///     Manager owns orchestration only and avoids behavior implementation.
    /// </summary>
    public sealed class AIManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private bool aiEnabled = true;
        public bool AIEnabled => aiEnabled;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            IsInitialized = true;
            var snapshot = CollectBrainSnapshot();
            ApplyAIEnabledState(snapshot);
            RegisterFactionSchedulers(snapshot);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            IsInitialized = false;
            EmitDiagnosticSnapshot();
        }

        public void SetAIEnabled(bool isEnabled)
        {
            aiEnabled = isEnabled;
            ApplyAIEnabledState(CollectBrainSnapshot());
        }

        private static BrainSnapshot CollectBrainSnapshot()
        {
            return new BrainSnapshot(
                RuntimeAiRegistry.Zombies.ToArray(),
                RuntimeGameplayAiRegistry.Squads.ToArray(),
                RuntimeGameplayAiRegistry.Survivors.ToArray());
        }

        private void RegisterFactionSchedulers(in BrainSnapshot snapshot)
        {
            // Each faction's AI scheduler tag is: ZombieController, SquadController, SurvivorController.
            // They are registered here so future per-faction throttle controls have a
            // consistent registration point.  Active state is already applied by
            // ApplyAIEnabledState() above.
            if (aiEnabled)
                Debug.Log(
                    $"[AIManager] Registered {snapshot.ZombieBrains.Length} zombie, {snapshot.SquadBrains.Length} squad AI schedulers.");
        }

        private static void EmitDiagnosticSnapshot()
        {
            var snapshot = CollectBrainSnapshot();
            Debug.Log(
                $"[AIManager] Shutdown snapshot — zombies:{snapshot.ZombieBrains.Length} squads:{snapshot.SquadBrains.Length}");
        }

        private void ApplyAIEnabledState(in BrainSnapshot snapshot)
        {
            foreach (var zombieBrain in snapshot.ZombieBrains) zombieBrain.SetActive(aiEnabled);

            foreach (var squadBrain in snapshot.SquadBrains) squadBrain.enabled = aiEnabled;

            foreach (var survivorBrain in snapshot.SurvivorBrains) survivorBrain.enabled = aiEnabled;
        }

        private readonly struct BrainSnapshot
        {
            public readonly ZombieController[] ZombieBrains;
            public readonly SquadController[] SquadBrains;
            public readonly SurvivorController[] SurvivorBrains;

            public BrainSnapshot(
                ZombieController[] zombieBrains,
                SquadController[] squadBrains,
                SurvivorController[] survivorBrains)
            {
                ZombieBrains = zombieBrains;
                SquadBrains = squadBrains;
                SurvivorBrains = survivorBrains;
            }
        }
    }
}
