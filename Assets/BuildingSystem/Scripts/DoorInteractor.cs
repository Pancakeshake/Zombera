using UnityEngine;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Scans for nearby doors each frame and calls OpenDoor() on the nearest one.
    /// Supports DoorScript.Door (Free Wood Door Pack) and the custom DoorController.
    /// Attach to the player unit alongside ContainerInteractor.
    /// </summary>
    public sealed class DoorInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactRadius = 2.5f;
        [SerializeField] private LayerMask doorLayerMask = ~0;

        // Nearest door component this frame (either type)
        private DoorScript.Door     s_nearestThirdParty;
        private DoorController      s_nearestCustom;

        public bool HasNearbyDoor => s_nearestThirdParty != null || s_nearestCustom != null;

        private readonly Collider[] overlapBuffer = new Collider[16];

        private void Update()
        {
            s_nearestThirdParty = null;
            s_nearestCustom     = null;
            FindNearestDoor();
        }

        /// <summary>Toggles the nearest door. Returns true if a door was found.</summary>
        public bool Interact()
        {
            if (s_nearestThirdParty != null)
            {
                // Set swing direction before toggling: door swings away from the player.
                if (!s_nearestThirdParty.open)
                {
                    Vector3 toPlayer = transform.position - s_nearestThirdParty.transform.position;
                    float dot = Vector3.Dot(s_nearestThirdParty.transform.right, toPlayer.normalized);
                    s_nearestThirdParty.DoorOpenAngle = dot >= 0f ? -90f : 90f;
                }
                s_nearestThirdParty.OpenDoor();
                return true;
            }
            if (s_nearestCustom != null) { s_nearestCustom.Toggle(); return true; }
            return false;
        }

        private void FindNearestDoor()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, overlapBuffer, doorLayerMask,
                QueryTriggerInteraction.Collide);

            float nearestSqDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Transform root = overlapBuffer[i].transform.root;
                float sqDist = (root.position - transform.position).sqrMagnitude;
                if (sqDist >= nearestSqDist) continue;

                // Prefer the third-party Door script; fall back to custom DoorController
                var tp = root.GetComponentInChildren<DoorScript.Door>();
                var cu = root.GetComponentInChildren<DoorController>();
                if (tp == null && cu == null) continue;

                nearestSqDist       = sqDist;
                s_nearestThirdParty = tp;
                s_nearestCustom     = cu;
            }
        }
    }
}
