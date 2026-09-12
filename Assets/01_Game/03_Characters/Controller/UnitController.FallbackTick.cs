#region

using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        private void UpdateUsingFallbackMovement()
        {
            // Fallback: direct movement for units without a NavMeshAgent.
            _desiredMoveDirection = ResolveDesiredDirection();

            if (_desiredMoveDirection.sqrMagnitude <= 0.0001f)
            {
                IsMoving = false;
                TryClampFallbackGrounding();
                return;
            }

            IsMoving = true;
            var movementDelta = _desiredMoveDirection.normalized * (moveSpeed * Time.deltaTime);
            ApplyMovement(movementDelta);
            Rotate(_desiredMoveDirection);
            var fallbackDist = movementDelta.magnitude;
            TryRecordHeavyCarryWalkDistance(fallbackDist);
            TickStamina(fallbackDist, IsSprinting);
            UpdateAnimator(moveSpeed);
            TryClampFallbackGrounding();
        }

        private void TryClampFallbackGrounding()
        {
            var profile = MovementGroundingSettings.Active;
            if (!profile.EnableFallbackTerrainClamp) return;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) return;
            if (Time.unscaledTime < _nextFallbackGroundClampAt) return;

            _nextFallbackGroundClampAt = Time.unscaledTime + profile.FallbackGroundClampInterval;

            if (!TryResolveFallbackTargetY(profile, out var targetY)) return;

            if (!profile.ShouldFallbackSnapDown(transform.position.y, targetY)) return;

            var clamped = transform.position;
            var verticalGap = clamped.y - targetY;
            clamped.y = verticalGap > 3f
                ? targetY
                : profile.ComputeFallbackSnapDownY(clamped.y, targetY);

            if (useRigidbodyMovement && movementBody != null)
                movementBody.MovePosition(clamped);
            else
                transform.position = clamped;

            if (ShouldLogGrounding(profile))
                LogGroundingState("FallbackTerrainClamp");
        }

        private bool TryResolveFallbackTargetY(MovementGroundingProfile profile, out float targetY)
        {
            targetY = transform.position.y;
            var sampleOrigin = transform.position + Vector3.up * profile.NavSampleUpOffset;

            if (!profile.PreferTerrainHeightOverNavMeshAt(transform.position)
                && UnitNavUtils.TrySampleTiered(
                    sampleOrigin,
                    out var navPoint,
                    profile.NavSampleRadii,
                    UnitNavUtils.WalkableAreaMask))
            {
                targetY = navPoint.y;
                return true;
            }

            if (profile.TryGetFootSnapTarget(transform.position, out targetY)) return true;

            return UnitNavUtils.TryResolveGroundReferenceY(transform.position, out targetY);
        }

        private Vector3 ResolveDesiredDirection()
        {
            if (role == UnitRole.Player && MoveInput.sqrMagnitude > 0.0001f)
                return new Vector3(MoveInput.x, 0f, MoveInput.y);

            if (!HasMoveTarget) return Vector3.zero;

            var toTarget = MoveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > stoppingDistance * stoppingDistance) return toTarget;

            Stop();
            return Vector3.zero;
        }

        private void ApplyMovement(Vector3 movementDelta)
        {
            if (useRigidbodyMovement && movementBody != null)
            {
                movementBody.MovePosition(movementBody.position + movementDelta);
                return;
            }

            transform.position += movementDelta;
        }
    }
}
