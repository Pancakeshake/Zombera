#region

using UnityEngine;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        private bool TryHandleDeadMovementState()
        {
            if (unitHealth == null || !unitHealth.IsDead) return false;

            if (HasMoveTarget || IsMoving) Stop();

            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            UpdateAnimator(0f);
            return true;
        }

        private bool TryHandlePlayerInputDisabledState()
        {
            if (InputEnabled || role != UnitRole.Player) return false;

            IsMoving = false;

            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            UpdateAnimator(0f);
            return true;
        }

        private bool TryUpdateUsingNavMeshAgent()
        {
            // NavMeshAgent handles its own movement; just track state and rotation.
            if (_agent == null || !_agent.isOnNavMesh) return false;

            if (HasArrivedAtMoveTarget())
            {
                Stop();
                _arrivalStallTimer = 0f;
                UpdateAnimator(0f);
                return true;
            }

            var hasRoute = _agent.pathPending || _agent.hasPath;
            var speed = _agent.velocity.magnitude;
            var velocityMoving = speed > 0.05f;

            if (TryHandleAgentStall(hasRoute, velocityMoving)) return true;

            var agentMoving = !_agent.isStopped && hasRoute && velocityMoving;
            IsMoving = agentMoving;

            if (agentMoving && _agent.velocity.sqrMagnitude > 0.01f)
            {
                Rotate(_agent.velocity);
                var distThisFrame = speed * Time.deltaTime;
                TryRecordHeavyCarryWalkDistance(distThisFrame);
                TickStamina(distThisFrame, IsSprinting);
            }
            else
            {
                TickStamina(0f, false);
            }

            // Movement-state contract: once routed motion ends, clear target intent.
            if (!agentMoving && HasMoveTarget) HasMoveTarget = false;

            UpdateAnimator(agentMoving ? speed : 0f);
            return true;
        }

        private bool HasArrivedAtMoveTarget()
        {
            var arrivalTolerance = ResolveActiveArrivalTolerance();
            var closeEnoughByPath = !_agent.pathPending && _agent.remainingDistance <= arrivalTolerance;

            if (!HasMoveTarget) return closeEnoughByPath;

            var toTarget = MoveTarget - transform.position;
            toTarget.y = 0f;
            var closeEnoughByTarget = toTarget.sqrMagnitude <= arrivalTolerance * arrivalTolerance;
            return closeEnoughByPath || closeEnoughByTarget;
        }

        private bool TryHandleAgentStall(bool hasRoute, bool velocityMoving)
        {
            // If we have a path but velocity stays near zero, treat as stalled and stop.
            if (!_agent.pathPending && hasRoute && !velocityMoving)
            {
                _arrivalStallTimer += Time.deltaTime;
                var stallThreshold = _activeMoveArrivalProfile == MoveArrivalProfile.GroupCommand
                    ? Mathf.Max(0.35f, groupMoveSettleStallSeconds)
                    : 0.35f;
                if (_arrivalStallTimer < stallThreshold) return false;

                Stop();
                _arrivalStallTimer = 0f;
                UpdateAnimator(0f);
                return true;
            }

            _arrivalStallTimer = 0f;

            return false;
        }
    }
}
