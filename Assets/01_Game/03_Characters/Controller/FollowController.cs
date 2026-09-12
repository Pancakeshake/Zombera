#region

using UnityEngine;
using UnityEngine.AI;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Handles exploration follow behavior for squad members.
    ///     Calculates a slot offset behind the leader and moves the unit
    ///     there, with sprint catch-up when the gap is large.
    /// </summary>
    public sealed class FollowController : MonoBehaviour
    {
        [SerializeField] private FollowStyle followStyle = FollowStyle.Loose;
        [SerializeField] private float followDistance = 2.5f;
        [SerializeField] [Min(0f)] private float sprintCatchUpDistance = 6f;
        [SerializeField] [Min(0f)] private float arrivalRadius = 0.8f;

        [Header("Anti-Bunching")]
        [SerializeField] [Min(0.5f)] private float neighborRepulsionRadius = 2.5f;

        [SerializeField] [Min(0f)] private float neighborRepulsionStrength = 0.85f;
        [SerializeField] [Min(0f)] private float overlapLateralWidenPerNeighbor = 0.22f;
        [SerializeField] [Min(0f)] private float followSlotSmoothing = 0.4f;
        [SerializeField] [Min(0.25f)] private float followSlotMemoryRadius = 3.5f;

        private static readonly Collider[] NeighborOverlapBuffer = new Collider[24];

        private UnitController _unitController;
        private Vector3 _smoothedFollowSlot;
        private bool _hasSmoothedFollowSlot;

        private void Awake()
        {
            _unitController = GetComponent<UnitController>();
        }

        public void SetFollowStyle(FollowStyle style)
        {
            followStyle = style;
        }

        public void TickFollow(Vector3 leaderPosition, Vector3 leaderForward)
        {
            if (_unitController == null) return;

            var fwd = leaderForward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            var lateralOffset = followStyle switch
            {
                FollowStyle.OrderedMarch => 0f,
                FollowStyle.Loose => 0.6f,
                _ => 1.2f
            };

            var side = gameObject.GetInstanceID() % 2 == 0 ? 1f : -1f;
            var right = new Vector3(fwd.z, 0f, -fwd.x);
            var neighborCount = CountNearbyFollowers(neighborRepulsionRadius);
            lateralOffset *= 1f + neighborCount * overlapLateralWidenPerNeighbor;

            var desiredSlot = leaderPosition
                              - fwd * followDistance
                              + right * (side * lateralOffset);

            desiredSlot += ComputeNeighborRepulsion(desiredSlot, neighborRepulsionRadius);

            var profile = MovementGroundingSettings.Active;
            if (profile.TryResolveGroundedPosition(desiredSlot, out var grounded))
                desiredSlot = grounded;

            desiredSlot = ApplyFollowSlotMemory(desiredSlot);

            var distToSlot = Vector3.Distance(transform.position, desiredSlot);

            if (distToSlot <= arrivalRadius)
            {
                _unitController.Stop();
                return;
            }

            var shouldSprint = distToSlot >= sprintCatchUpDistance;
            _unitController.SetSprintActive(shouldSprint);
            _unitController.MoveTo(desiredSlot, MoveArrivalProfile.GroupCommand);
        }

        private Vector3 ApplyFollowSlotMemory(Vector3 desiredSlot)
        {
            if (!_hasSmoothedFollowSlot)
            {
                _smoothedFollowSlot = desiredSlot;
                _hasSmoothedFollowSlot = true;
                return desiredSlot;
            }

            var delta = desiredSlot - _smoothedFollowSlot;
            delta.y = 0f;
            if (delta.sqrMagnitude <= followSlotMemoryRadius * followSlotMemoryRadius)
            {
                var blend = Mathf.Clamp01(followSlotSmoothing);
                desiredSlot = Vector3.Lerp(_smoothedFollowSlot, desiredSlot, blend);
            }

            _smoothedFollowSlot = desiredSlot;
            return desiredSlot;
        }

        private int CountNearbyFollowers(float radius)
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                NeighborOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            var followers = 0;
            for (var i = 0; i < count; i++)
            {
                var collider = NeighborOverlapBuffer[i];
                if (collider == null) continue;

                var otherController = collider.GetComponentInParent<UnitController>();
                if (otherController == null || otherController == _unitController) continue;
                if (otherController.GetComponent<FollowController>() == null) continue;

                followers++;
            }

            return followers;
        }

        private Vector3 ComputeNeighborRepulsion(Vector3 desiredSlot, float radius)
        {
            if (neighborRepulsionStrength <= 0f) return Vector3.zero;

            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                NeighborOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            var repulsion = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                var collider = NeighborOverlapBuffer[i];
                if (collider == null) continue;

                var otherController = collider.GetComponentInParent<UnitController>();
                if (otherController == null || otherController == _unitController) continue;

                var away = transform.position - otherController.transform.position;
                away.y = 0f;
                var dist = away.magnitude;
                if (dist < 0.05f || dist > radius) continue;

                var weight = 1f - dist / radius;
                repulsion += away.normalized * (weight * neighborRepulsionStrength);
            }

            return repulsion;
        }
    }

    public enum FollowStyle
    {
        Free,
        Loose,
        OrderedMarch
    }
}
