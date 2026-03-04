using UnityEngine;
using Zombera.AI.Brains;

namespace Zombera.AI.States
{
    /// <summary>
    /// Default low-intensity state for standby behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IdleState : MonoBehaviour, IUnitBrainState
    {
        public UnitBrainStateType StateType => UnitBrainStateType.Idle;

        public void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            _ = sensorFrame;
            _ = decision;
            brain?.MoveAction?.StopMovement();
        }

        public void Tick(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            if (brain == null)
            {
                return;
            }

            switch (decision.DecisionType)
            {
                case UnitDecisionType.Wander:
                    if (decision.TargetPosition != Vector3.zero)
                    {
                        brain.MoveAction?.ExecuteMove(decision.TargetPosition);
                    }
                    break;

                case UnitDecisionType.Idle:
                default:
                    brain.MoveAction?.StopMovement();
                    break;
            }

            // TODO: Add idle look-around and subtle patrol behaviors.
            // TODO: Trigger context-specific idle animations/stances.
            _ = sensorFrame;
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }
    }
}