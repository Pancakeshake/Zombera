using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Handles exploration follow behavior for squad members.
    /// Calculates a slot offset behind the leader and moves the unit
    /// there, with sprint catch-up when the gap is large.
    /// </summary>
    public sealed class FollowController : MonoBehaviour
    {
        [SerializeField] private FollowStyle followStyle = FollowStyle.Loose;
        [SerializeField] private float followDistance = 2.5f;
        [SerializeField, Min(0f)] private float sprintCatchUpDistance = 6f;
        [SerializeField, Min(0f)] private float arrivalRadius = 0.8f;

        private UnitController _unitController;

        public FollowStyle CurrentFollowStyle => followStyle;
        public float FollowDistance => followDistance;

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

            Vector3 fwd = leaderForward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            // Choose lateral offset based on style.
            float lateralOffset = followStyle switch
            {
                FollowStyle.OrderedMarch => 0f,          // directly behind
                FollowStyle.Loose        => 0.6f,         // slight diagonal
                _                        => 1.2f          // Free: wide side spread
            };

            // Alternate left/right using this unit's stable hash.
            float side = (gameObject.GetHashCode() % 2 == 0) ? 1f : -1f;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            Vector3 desiredSlot = leaderPosition
                - fwd * followDistance
                + right * (side * lateralOffset);

            // Obstacle avoidance: sample nearest NavMesh point to desired slot.
            if (NavMesh.SamplePosition(desiredSlot, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                desiredSlot = hit.position;
            }

            float distToSlot = Vector3.Distance(transform.position, desiredSlot);

            if (distToSlot <= arrivalRadius)
            {
                _unitController.Stop();
                return;
            }

            // Sprint if far behind.
            bool shouldSprint = distToSlot >= sprintCatchUpDistance;
            _unitController.SetSprintActive(shouldSprint);
            _unitController.MoveTo(desiredSlot);
        }
    }

    public enum FollowStyle
    {
        Free,
        Loose,
        OrderedMarch
    }
}