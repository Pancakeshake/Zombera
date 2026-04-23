using UnityEngine;
using Zombera.AI.Brains;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.AI.States
{
    /// <summary>
    /// Combat pressure state that attacks in range and closes or maintains distance based on weapon type.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackState : MonoBehaviour, IUnitBrainState
    {
        [SerializeField, Min(0f)] private float rangedStandoffMinDistance = 5f;
        [SerializeField, Min(0f)] private float rangedStandoffMaxDistance = 12f;

        public UnitBrainStateType StateType => UnitBrainStateType.Attack;

        public void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            _ = brain;
            _ = sensorFrame;
            _ = decision;
        }

        public void Tick(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            if (brain == null)
            {
                return;
            }

            Unit target = decision.TargetUnit != null ? decision.TargetUnit : sensorFrame.NearestEnemy;

            if (target == null)
            {
                brain.MoveAction?.StopMovement();
                return;
            }

            float distance = Vector3.Distance(brain.transform.position, target.transform.position);
            bool isRanged = IsRangedWeaponEquipped(brain);

            if (isRanged)
            {
                // Ranged: attack when within max range; back off when too close;
                // close in when too far.
                if (distance <= brain.AttackRange)
                {
                    bool attacked = brain.AttackAction != null && brain.AttackAction.ExecuteAttack(target);

                    if (!attacked)
                    {
                        brain.ReloadAction?.ExecuteReload();
                    }

                    // Step back if inside minimum standoff.
                    if (distance < rangedStandoffMinDistance)
                    {
                        Vector3 awayDir = (brain.transform.position - target.transform.position).normalized;
                        Vector3 backoff = brain.transform.position + awayDir * rangedStandoffMinDistance;
                        brain.MoveAction?.ExecuteMove(backoff);
                    }
                }
                else if (distance > rangedStandoffMaxDistance)
                {
                    brain.MoveAction?.ExecuteMove(target.transform.position);
                }
            }
            else
            {
                // Melee: close in and attack.
                if (distance <= brain.AttackRange)
                {
                    bool attacked = brain.AttackAction != null && brain.AttackAction.ExecuteAttack(target);

                    if (!attacked)
                    {
                        brain.ReloadAction?.ExecuteReload();
                    }
                }
                else
                {
                    brain.MoveAction?.ExecuteMove(target.transform.position);
                }
            }
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }

        private static bool IsRangedWeaponEquipped(UnitBrain brain)
        {
            WeaponSystem ws = brain.UnitCombat != null
                ? brain.UnitCombat.GetComponent<WeaponSystem>()
                : null;

            if (ws?.EquippedWeapon == null) return false;

            WeaponCategory cat = ws.EquippedWeapon.weaponCategory;
            return WeaponSystem.IsRangedCategory(cat);
        }
    }
}