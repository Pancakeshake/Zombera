#region

using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        private float _nextFootGroundAt;

        private void LateUpdate()
        {
            var profile = MovementGroundingSettings.Active;
            if (profile.EnableRuntimeFootGrounding)
            {
                TryApplyRuntimeFootGrounding();
                return;
            }

            TrySyncTransformToNavMeshAgent();
        }

        private void TrySyncTransformToNavMeshAgent()
        {
            if (!ShouldApplyRuntimeFootGrounding()) return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            if (!_agent.updatePosition)
                transform.position = _agent.nextPosition;
        }

        private void TryApplyRuntimeFootGrounding()
        {
            var profile = MovementGroundingSettings.Active;
            if (!profile.EnableRuntimeFootGrounding) return;
            if (!ShouldApplyRuntimeFootGrounding()) return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            if (Time.unscaledTime < _nextFootGroundAt) return;

            _nextFootGroundAt = Time.unscaledTime + profile.FootGroundInterval;

            var pos = transform.position;
            if (!profile.TryGetFootSnapTarget(pos, out var targetY)) return;
            if (!profile.ShouldSnapDown(pos.y, targetY)) return;

            var newY = profile.ComputeSnapDownY(pos.y, targetY);
            ApplyGroundedPosition(new Vector3(pos.x, newY, pos.z), profile);
        }

        private void ApplyGroundedPosition(Vector3 corrected, MovementGroundingProfile profile)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.Warp(corrected);
                _agent.nextPosition = corrected;
            }
            else if (useRigidbodyMovement && movementBody != null)
            {
                movementBody.MovePosition(corrected);
            }
            else
            {
                transform.position = corrected;
            }

            if (ShouldLogGrounding(profile))
                LogGroundingState("FootGroundContact");
        }

        private bool ShouldApplyRuntimeFootGrounding()
        {
            return role == UnitRole.Player
                   || role == UnitRole.SquadMember
                   || role == UnitRole.Survivor;
        }

        private bool ShouldLogGrounding(MovementGroundingProfile profile)
        {
            return logGroundingDiagnostics || profile.LogUnitGroundingState;
        }
    }
}