#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Coordinates combat services without containing gameplay logic.
    ///     Routes requests through CombatSystem and related combat modules.
    /// </summary>
    public sealed class CombatManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private TargetingSystem targetingSystem;
        [SerializeField] private WeaponSystem fallbackWeaponSystem;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            IsInitialized = true;
            CoreEventBus.Instance?.Subscribe<CombatEncounterStartedEvent>(OnCombatEncounterStarted);
            CoreEventBus.Instance?.Subscribe<UnitDeathEvent>(OnUnitDeath);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            IsInitialized = false;
            CoreEventBus.Instance?.Unsubscribe<CombatEncounterStartedEvent>(OnCombatEncounterStarted);
            CoreEventBus.Instance?.Unsubscribe<UnitDeathEvent>(OnUnitDeath);
        }

        public bool RequestAttack(UnitCombat attacker, IReadOnlyList<UnitHealth> visibleTargets)
        {
            return attacker != null
                   && (combatSystem != null
                       ? combatSystem.TryExecuteAttack(attacker, visibleTargets)
                       : attacker.ExecuteAttack(visibleTargets));
        }

        public void RequestReload(UnitCombat attacker)
        {
            if (attacker == null) return;

            if (combatSystem != null)
            {
                combatSystem.Reload(attacker);
                return;
            }

            attacker.Reload();
        }

        private static void OnCombatEncounterStarted(CombatEncounterStartedEvent gameEvent)
        {
            // Telemetry entry point — subscriber count shown via EventSystem diagnostics.
            _ = gameEvent;
        }

        private void OnUnitDeath(UnitDeathEvent gameEvent)
        {
            // Flush any lingering attack sequences targeting the dead unit.
            combatSystem?.ClearAttacksTargeting(gameEvent.UnitObject);
        }
    }
}