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
        [SerializeField, Min(1f)] private float chaseTimeoutSeconds = 10f;

        public UnitBrainStateType StateType => UnitBrainStateType.Chase;

        private float _lastHadTargetTime;

        public void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            _ = brain;
            _ = sensorFrame;
            _ = decision;
            _lastHadTargetTime = Time.time;
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
                _lastHadTargetTime = Time.time;
                brain.MoveAction?.ExecuteMoveWithNavFallback(target.transform.position);
                return;
            }

            if (decision.TargetPosition != Vector3.zero)
            {
                _lastHadTargetTime = Time.time;
                brain.MoveAction?.ExecuteMoveWithNavFallback(decision.TargetPosition);
                return;
            }

            if (sensorFrame.HasHeardNoise)
            {
                _lastHadTargetTime = Time.time;
                brain.MoveAction?.ExecuteMoveWithNavFallback(sensorFrame.LastNoisePosition);
                return;
            }

            // Target lost — stop movement. If consistently lost past the timeout the
            // utility evaluator will naturally score a lower-cost action higher.
            if (Time.time - _lastHadTargetTime > chaseTimeoutSeconds)
            {
                brain.MoveAction?.StopMovement();
            }
        }

        public void Exit(UnitBrain brain)
        {
            _ = brain;
        }
    }
}