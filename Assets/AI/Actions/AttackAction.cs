using System.Collections.Generic;
using UnityEngine;
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

        // TODO: Add target priority by threat, distance, and command context.
        // TODO: Support burst fire, melee chain windows, and animation events.
    }
}