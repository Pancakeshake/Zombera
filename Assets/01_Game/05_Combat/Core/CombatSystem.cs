#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     High-level combat coordinator that routes attack requests to unit combat components.
    /// </summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        private readonly List<IDamageable> _damageableBuffer = new();

        public bool TryExecuteAttack(UnitCombat attacker, IReadOnlyList<UnitHealth> visibleTargets)
        {
            if (attacker == null) return false;

            _damageableBuffer.Clear();

            if (visibleTargets == null) return attacker.Attack(_damageableBuffer);

            foreach (var health in visibleTargets)
                if (health != null)
                    _damageableBuffer.Add(health);

            return attacker.Attack(_damageableBuffer);
        }

        public void Reload(UnitCombat attacker)
        {
            attacker?.Reload();
        }

        /// <summary>
        ///     Cancels any pending attack sequences whose target matches the dead unit object.
        /// </summary>
        public void ClearAttacksTargeting(GameObject deadUnit)
        {
            // Active attack sequences referencing this object are invalidated
            // automatically when UnitHealth.IsDead → true; no explicit queue to flush yet.
            _ = deadUnit;
        }
    }
}