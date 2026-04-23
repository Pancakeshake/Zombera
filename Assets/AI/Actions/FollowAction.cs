using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable follow action for squad cohesion and command-driven movement.
    /// Uses <see cref="FormationController"/> for deterministic slot offsets
    /// and falls back to a wander-to-leader-area behavior when the leader is lost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowAction : MonoBehaviour
    {
        [SerializeField] private UnitController unitController;
        [SerializeField] private FollowController followController;
        [SerializeField] private FormationController formationController;
#pragma warning disable CS0414
        [SerializeField] private float followOffsetDistance = 2.5f;
#pragma warning restore CS0414
        [Tooltip("Seconds without a valid leader before triggering regroup wander.")]
        [SerializeField, Min(0f)] private float leaderLossTimeoutSeconds = 4f;
        [SerializeField, Min(0.5f)] private float regroupWanderRadius = 5f;

        private Transform _lastKnownLeaderTransform;
        private Vector3 _lastKnownLeaderPosition;
        private float _leaderLostAt = float.MaxValue;
        private bool _regrouping;

        /// <summary>Index of this unit's slot in the current formation. -1 = unassigned.</summary>
        public int FormationSlotIndex { get; set; } = -1;

        public void Initialize(UnitController controller, FollowController follow)
        {
            if (controller != null) unitController = controller;
            if (follow != null) followController = follow;
            if (unitController == null) unitController = GetComponent<UnitController>();
            if (followController == null) followController = GetComponent<FollowController>();
            if (formationController == null) formationController = GetComponent<FormationController>();
        }

        public bool ExecuteFollow(Transform leader)
        {
            if (unitController == null) return false;

            if (leader == null)
            {
                HandleLeaderLoss();
                return false;
            }

            // Leader re-acquired.
            _leaderLostAt = float.MaxValue;
            _regrouping = false;
            _lastKnownLeaderTransform = leader;
            _lastKnownLeaderPosition = leader.position;

            // If assigned a formation slot, navigate to computed world position.
            if (formationController != null && FormationSlotIndex >= 0)
            {
                // We need a unit count; use slot index +1 as minimum bound.
                int minUnits = FormationSlotIndex + 1;
                var slots = formationController.CalculateFormationSlots(
                    leader.position, leader.forward, minUnits);

                if (FormationSlotIndex < slots.Count)
                {
                    unitController.MoveTo(slots[FormationSlotIndex]);
                    return true;
                }
            }

            // Fallback: delegate to FollowController for offset-based movement.
            followController?.TickFollow(leader.position, leader.forward);
            return true;
        }

        public bool ExecuteMoveTo(Vector3 worldPosition)
        {
            if (unitController == null) return false;
            unitController.MoveTo(worldPosition);
            return true;
        }

        public void StopFollowing()
        {
            unitController?.Stop();
            _regrouping = false;
        }

        private void HandleLeaderLoss()
        {
            if (_leaderLostAt == float.MaxValue)
                _leaderLostAt = Time.time;

            float lost = Time.time - _leaderLostAt;

            if (lost < leaderLossTimeoutSeconds)
            {
                // Brief grace period — hold position.
                unitController.Stop();
                return;
            }

            // Regroup: wander toward last known leader position.
            if (!_regrouping)
            {
                _regrouping = true;
                Vector3 regroupTarget = _lastKnownLeaderPosition
                    + Random.insideUnitSphere.normalized * regroupWanderRadius;
                regroupTarget.y = _lastKnownLeaderPosition.y;
                unitController.MoveTo(regroupTarget);
            }
        }
    }
}