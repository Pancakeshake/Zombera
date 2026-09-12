#region

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeNullComparison

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Sits on the same GameObject as a door leaf.
    ///     Watches the door open/closed state and toggles a NavMeshObstacle (carve mode)
    ///     on the sibling "DoorBlockerNode".
    ///     When the door is CLOSED the obstacle carves a hole in the NavMesh so agents
    ///     cannot path through. When OPEN the obstacle is disabled and the NavMesh fills
    ///     back in — agents walk through naturally with no warp.
    ///     The obstacle must be on a NON-rotating sibling, never on the door leaf itself.
    /// </summary>
    public class DoorBlocker : MonoBehaviour
    {
        [Tooltip("NavMeshObstacle on the DoorBlockerNode sibling (assign in Inspector).")]
        [FormerlySerializedAs("_obstacle")]
        [SerializeField]
        private NavMeshObstacle obstacle;

        [Tooltip("Optional custom door leaf controller on this object.")]
        [SerializeField]
        private DoorController customDoorLeaf;

        [Tooltip("Optional third-party door component (DoorScript.Door) on this object.")]
        [SerializeField]
        private Component thirdPartyDoorLeaf;

        private void Awake()
        {
            if (customDoorLeaf == null) customDoorLeaf = GetComponent<DoorController>();
            if (thirdPartyDoorLeaf == null)
                ThirdPartyDoorBridge.TryGetOnTransform(transform, out thirdPartyDoorLeaf);
        }

        private void Update()
        {
            if (!obstacle) return;

            var hasOpenState = false;
            var isOpen = false;

            if (customDoorLeaf != null)
            {
                isOpen = customDoorLeaf.IsOpen;
                hasOpenState = true;
            }

            if (thirdPartyDoorLeaf != null &&
                ThirdPartyDoorBridge.TryGetIsOpen(thirdPartyDoorLeaf, out var thirdPartyOpen))
            {
                isOpen = thirdPartyOpen;
                hasOpenState = true;
            }

            if (!hasOpenState) return;

            var shouldBlock = !isOpen;
            if (obstacle.enabled != shouldBlock)
                obstacle.enabled = shouldBlock;
        }
    }
}