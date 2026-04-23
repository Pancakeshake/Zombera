using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;

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
            EventSystem.Instance?.Subscribe<CombatEncounterStartedEvent>(OnCombatEncounterStarted);
            EventSystem.Instance?.Subscribe<UnitDeathEvent>(OnUnitDeath);
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            EventSystem.Instance?.Unsubscribe<CombatEncounterStartedEvent>(OnCombatEncounterStarted);
            EventSystem.Instance?.Unsubscribe<UnitDeathEvent>(OnUnitDeath);
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

        /// <summary>
        /// Applies armor resistance to raw damage and returns the reduced amount.
        /// Resistance is derived from the Toughness stat (0–100 mapped to 0–50% reduction).
        /// </summary>
        public float ApplyArmorReduction(float rawDamage, UnitStats stats)
        {
            if (stats == null)
            {
                return rawDamage;
            }

            // Map toughness 0-100 → 0-0.5 damage resistance.
            float resistance = Mathf.Clamp01(stats.Toughness / 200f);
            return rawDamage * (1f - resistance);
        }

        /// <summary>
        /// Records an attack exchange for later deterministic replay.
        /// The replay log is keyed by session tick and stored in CombatSystem.
        /// </summary>
        public void RecordReplayEvent(int tick, UnitCombat attacker, UnitHealth target, float damage)
        {
            combatSystem?.RecordReplayFrame(tick, attacker, target, damage);
        }

        private void OnCombatEncounterStarted(CombatEncounterStartedEvent gameEvent)
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