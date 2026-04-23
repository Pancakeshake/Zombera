using System.Collections;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Attach to the door panel (the swinging mesh child) of a door wall prefab.
    /// Call Toggle() to open or close. Swings the door pivot around the Y axis.
    ///
    /// Setup in prefab:
    ///   SM_DoorWall_A  (root — has StructureHealth, MeshCollider)
    ///   └─ DoorPanel   (child mesh — add this component HERE, no other colliders needed)
    ///
    /// If your door panel is not a separate child yet:
    ///   1. Select the door wall prefab → Open Prefab
    ///   2. Create an empty child named "DoorPanel"
    ///   3. Move the door mesh into "DoorPanel"
    ///   4. Add DoorController to "DoorPanel"
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        [Header("Swing")]
        [Tooltip("Degrees to rotate open around local Y axis. Use negative to swing the other way.")]
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(0.05f)] private float swingDuration = 0.25f;

        [Header("State")]
        public bool StartsOpen = false;

        // ── Runtime state ──────────────────────────────────────────────────
        public bool IsOpen { get; private set; }

        private Quaternion closedRot;
        private Quaternion openRot;
        private Coroutine swingCoroutine;

        private void Awake()
        {
            closedRot = transform.localRotation;
            openRot   = closedRot * Quaternion.Euler(0f, openAngle, 0f);

            if (StartsOpen)
            {
                transform.localRotation = openRot;
                IsOpen = true;
            }
        }

        /// <summary>Opens or closes the door. Safe to call from any context.</summary>
        public void Toggle()
        {
            if (swingCoroutine != null)
                StopCoroutine(swingCoroutine);

            IsOpen = !IsOpen;
            swingCoroutine = StartCoroutine(Swing(IsOpen ? openRot : closedRot));
        }

        public void Open()  { if (!IsOpen) Toggle(); }
        public void Close() { if (IsOpen)  Toggle(); }

        private IEnumerator Swing(Quaternion target)
        {
            Quaternion start = transform.localRotation;
            float elapsed = 0f;

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(start, target, elapsed / swingDuration);
                yield return null;
            }

            transform.localRotation = target;
            swingCoroutine = null;
        }
    }
}
