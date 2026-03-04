using UnityEngine;
using Zombera.AI.Brains;
using Zombera.Characters;

namespace Zombera.AI.States
{
    /// <summary>
    /// Pursuit state for moving toward a target enemy or investigation point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChaseState : MonoBehaviour, IUnitBrainState
    {
        public UnitBrainStateType StateType => UnitBrainStateType.Chase;

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

            if (target != null)
            {
                brain.MoveAction?.ExecuteMove(target.transform.position);
                return;
            }

            if (decision.TargetPosition != Vector3.zero)
            {
                brain.MoveAction?.ExecuteMove(decision.TargetPosition);
                return;
            }

            if (sensorFrame.HasHeardNoise)
            {
                brain.MoveAction?.ExecuteMove(sensorFrame.LastNoisePosition);
                return;
            }

            brain.MoveAction?.StopMovement();

            // TODO: Add nav fallback when direct chase path is blocked.
            // TODO: Add chase timeout and target-loss recovery.
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }
    }
}