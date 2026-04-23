using UnityEngine;
using UnityEngine.AI;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Sits on the same GameObject as DoorScript.Door (the rotating door leaf).
    /// Watches the door open/closed state and toggles a NavMeshObstacle (carve mode)
    /// on the sibling "DoorBlockerNode".
    ///
    /// When the door is CLOSED the obstacle carves a hole in the NavMesh so agents
    /// cannot path through. When OPEN the obstacle is disabled and the NavMesh fills
    /// back in — agents walk through naturally with no warp.
    ///
    /// The obstacle must be on a NON-rotating sibling, never on the door leaf itself.
    /// </summary>
    [RequireComponent(typeof(DoorScript.Door))]
    public class DoorBlocker : MonoBehaviour
    {
        [Tooltip("NavMeshObstacle on the DoorBlockerNode sibling. Assign via DoorPrefabSetupTool or Inspector.")]
        [SerializeField] private NavMeshObstacle _obstacle;

        private DoorScript.Door _door;

        private void Awake()
        {
            _door = GetComponent<DoorScript.Door>();
        }

        private void Update()
        {
            if (_obstacle == null)
                return;

            bool shouldBlock = !_door.open;
            if (_obstacle.enabled != shouldBlock)
                _obstacle.enabled = shouldBlock;
        }
    }
}
