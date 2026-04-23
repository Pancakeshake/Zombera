using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// High-level combat coordinator that routes attack requests to unit combat components.
    /// </summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        private readonly List<IDamageable> damageableBuffer = new List<IDamageable>();

        public bool TryExecuteAttack(UnitCombat attacker, IReadOnlyList<UnitHealth> visibleTargets)
        {
            if (attacker == null)
            {
                return false;
            }

            damageableBuffer.Clear();

            if (visibleTargets != null)
            {
                for (int i = 0; i < visibleTargets.Count; i++)
                {
                    UnitHealth health = visibleTargets[i];

                    if (health != null)
                    {
                        damageableBuffer.Add(health);
                    }
                }
            }

            return attacker.Attack(damageableBuffer);
        }

        public void Reload(UnitCombat attacker)
        {
            attacker?.Reload();
        }

        /// <summary>
        /// Records one attack exchange for deterministic replay.
        /// Extend with a ring-buffer or write-to-disk strategy when needed.
        /// </summary>
        public void RecordReplayFrame(int tick, UnitCombat attacker, UnitHealth target, float damage)
        {
            // Replay frame storage is intentionally left to external replay systems.
            // This entry point ensures CombatManager can route without future breaking changes.
            _ = tick;
            _ = attacker;
            _ = target;
            _ = damage;
        }

        /// <summary>
        /// Cancels any pending attack sequences whose target matches the dead unit object.
        /// </summary>
        public void ClearAttacksTargeting(GameObject deadUnit)
        {
            // Active attack sequences referencing this object are invalidated
            // automatically when UnitHealth.IsDead → true; no explicit queue to flush yet.
            _ = deadUnit;
        }
    }
}