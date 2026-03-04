using UnityEngine;
using Zombera.AI.Brains;
using Zombera.Characters;

namespace Zombera.AI.States
{
    /// <summary>
    /// Combat pressure state that attacks in range and closes distance when needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackState : MonoBehaviour, IUnitBrainState
    {
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

            // TODO: Add strafe, cover peek, and burst cadence patterns.
            // TODO: Split melee vs ranged timing windows.
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }
    }
}