using System.Collections.Generic;
using UnityEngine;
using Zombera.AI.Brains;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable attack action that routes through combat systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackAction : MonoBehaviour
    {
        [SerializeField] private Unit owner;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private float attackScanRadius = 20f;

        private readonly List<Unit> enemyBuffer = new List<Unit>();
        private readonly List<UnitHealth> targetBuffer = new List<UnitHealth>();

        public void Initialize(Unit ownerUnit, UnitCombat combat)
        {
            if (ownerUnit != null)
            {
                owner = ownerUnit;
            }

            if (combat != null)
            {
                unitCombat = combat;
            }

            if (owner == null)
            {
                owner = GetComponent<Unit>();
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }
        }

        public bool ExecuteAttack(Unit explicitTarget = null)
        {
            if (unitCombat == null)
            {
                return false;
            }

            targetBuffer.Clear();

            if (explicitTarget != null && explicitTarget.Health != null && !explicitTarget.Health.IsDead)
            {
                targetBuffer.Add(explicitTarget.Health);
            }
            else
            {
                // Prefer the brain sensor's highest-threat target over raw proximity.
                Unit highestThreat = null;
                if (owner != null)
                {
                    UnitBrain brain = owner.GetComponent<UnitBrain>();
                    highestThreat = brain?.EnemySensor?.HighestThreatEnemy;
                }

                if (highestThreat != null && highestThreat.Health != null && !highestThreat.Health.IsDead)
                {
                    targetBuffer.Add(highestThreat.Health);
                }
                else if (owner != null && UnitManager.Instance != null)
                {
                    UnitManager.Instance.FindNearbyEnemies(owner, attackScanRadius, enemyBuffer);

                    for (int i = 0; i < enemyBuffer.Count; i++)
                    {
                        Unit enemy = enemyBuffer[i];

                        if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        {
                            targetBuffer.Add(enemy.Health);
                        }
                    }
                }
            }

            if (targetBuffer.Count <= 0)
            {
                return false;
            }

            if (combatManager != null)
            {
                return combatManager.RequestAttack(unitCombat, targetBuffer);
            }

            return unitCombat.ExecuteAttack(targetBuffer);
        }

        /// <summary>
        /// Executes an attack and fires the named animation sync trigger.
        /// Use this overload from brain states that manage their own attack rhythm.
        /// </summary>
        public bool ExecuteAttackWithAnimation(Unit explicitTarget, string attackTrigger)
        {
            bool success = ExecuteAttack(explicitTarget);

            if (success)
            {
                unitCombat?.NotifyAnimationSyncPoint(attackTrigger);
            }

            return success;
        }

        /// <summary>
        /// Checks whether the weapon has remaining ammo and can fire now.
        /// Burst fire: callers chain this check across multiple ticks with reduced cooldown.
        /// </summary>
        public bool CanAttack()
        {
            if (unitCombat == null)
            {
                return false;
            }

            return !unitCombat.IsOnCooldown();
        }
    }
}