using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates combat services without containing gameplay logic.
    /// Routes requests through CombatSystem and related combat modules.
    /// </summary>
    public sealed class CombatManager : MonoBehaviour, Zombera.Core.IGameSystem
    {
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private TargetingSystem targetingSystem;
        [SerializeField] private WeaponSystem fallbackWeaponSystem;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;

            // TODO: Register combat telemetry and event listeners.
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;

            // TODO: Flush pending combat events.
        }

        public bool RequestAttack(UnitCombat attacker, IReadOnlyList<UnitHealth> visibleTargets)
        {
            if (attacker == null)
            {
                return false;
            }

            if (combatSystem != null)
            {
                return combatSystem.TryExecuteAttack(attacker, visibleTargets);
            }

            return attacker.ExecuteAttack(visibleTargets);
        }

        public void RequestReload(UnitCombat attacker)
        {
            if (attacker == null)
            {
                return;
            }

            if (combatSystem != null)
            {
                combatSystem.Reload(attacker);
                return;
            }

            attacker.Reload();
        }

        public UnitHealth ResolveTarget(UnitCombat attacker, IReadOnlyList<UnitHealth> candidates)
        {
            if (attacker == null)
            {
                return null;
            }

            if (targetingSystem != null)
            {
                return targetingSystem.ResolveHybridTarget(attacker.MarkedTarget as UnitHealth, candidates, attacker.transform.position);
            }

            return attacker.SelectTarget(candidates);
        }

        // TODO: Add damage-type and armor service integration point.
        // TODO: Add deterministic combat replay hooks.
    }
}