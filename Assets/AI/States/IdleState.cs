using UnityEngine;
using Zombera.AI.Brains;

namespace Zombera.AI.States
{
    /// <summary>
    /// Default low-intensity state for standby behavior.
    /// Periodically issues small wander steps so the unit faces different directions while idle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IdleState : MonoBehaviour, IUnitBrainState
    {
        [SerializeField, Min(0.5f)] private float lookAroundInterval = 3.5f;
        [SerializeField, Min(0.1f)] private float lookAroundRadius = 2f;

        public UnitBrainStateType StateType => UnitBrainStateType.Idle;

        private float _nextLookAroundAt;

        public void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            _ = sensorFrame;
            _ = decision;
            brain?.MoveAction?.StopMovement();
            _nextLookAroundAt = Time.time + lookAroundInterval;
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

                    // Periodic look-around: issue a small wander step so the unit
                    // gradually faces different directions without walking away.
                    if (Time.time >= _nextLookAroundAt)
                    {
                        _nextLookAroundAt = Time.time + lookAroundInterval;
                        brain.MoveAction?.ExecuteWander(brain.transform.position, lookAroundRadius);
                    }
                    break;
            }

            _ = sensorFrame;
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }
    }
}