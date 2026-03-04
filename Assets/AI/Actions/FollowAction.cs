using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable follow action for squad cohesion and command-driven movement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowAction : MonoBehaviour
    {
        [SerializeField] private UnitController unitController;
        [SerializeField] private FollowController followController;
        [SerializeField] private float followOffsetDistance = 2.5f;

        public void Initialize(UnitController controller, FollowController follow)
        {
            if (controller != null)
            {
                unitController = controller;
            }

            if (follow != null)
            {
                followController = follow;
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (followController == null)
            {
                followController = GetComponent<FollowController>();
            }
        }

        public bool ExecuteFollow(Transform leader)
        {
            if (leader == null || unitController == null)
            {
                return false;
            }

            followController?.TickFollow(leader.position, leader.forward);

            Vector3 fallbackSlot = leader.position - leader.forward * followOffsetDistance;
            unitController.MoveTo(fallbackSlot);
            return true;
        }

        public bool ExecuteMoveTo(Vector3 worldPosition)
        {
            if (unitController == null)
            {
                return false;
            }

            unitController.MoveTo(worldPosition);
            return true;
        }

        public void StopFollowing()
        {
            unitController?.Stop();
        }

        // TODO: Integrate formation slot service so follow offsets are deterministic.
        // TODO: Add leader-loss recovery behavior and regroup staging logic.
    }
}