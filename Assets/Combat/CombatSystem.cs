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
    }
}