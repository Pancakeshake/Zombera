#region

using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

// ReSharper disable MemberCanBePrivate.Global

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Attach to the door panel (the swinging mesh child) of a door wall prefab.
    ///     Call Toggle() to open or close. Swings the door pivot around the Y axis.
    ///     Setup in prefab:
    ///     SM_DoorWall_A  (root — has StructureHealth, MeshCollider)
    ///     └─ DoorPanel   (child mesh — add this component HERE, no other colliders needed)
    ///     If your door panel is not a separate child yet:
    ///     1. Select the door wall prefab → Open Prefab
    ///     2. Create an empty child named "DoorPanel"
    ///     3. Move the door mesh into "DoorPanel"
    ///     4. Add DoorController to "DoorPanel"
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        [Header("Swing")]
        [Tooltip("Degrees to rotate open around local Y axis. Use negative to swing the other way.")]
        [SerializeField]
        private float openAngle = 90f;

        [SerializeField] [Min(0.05f)] private float swingDuration = 0.25f;

        [Header("State")] [FormerlySerializedAs("StartsOpen")] [SerializeField]
        private bool startsOpen;

        private Quaternion _closedRot;
        private Quaternion _openRot;
        private Coroutine _swingCoroutine;

        // ── Runtime state ──────────────────────────────────────────────────
        // ReSharper disable once UnusedMember.Global
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _closedRot = transform.localRotation;
            _openRot = _closedRot * Quaternion.Euler(0f, openAngle, 0f);

            if (!startsOpen) return;

            transform.localRotation = _openRot;
            IsOpen = true;
        }

        /// <summary>Opens or closes the door. Safe to call from any context.</summary>
        public void Toggle()
        {
            if (_swingCoroutine != null)
                StopCoroutine(_swingCoroutine);

            IsOpen = !IsOpen;
            _swingCoroutine = StartCoroutine(Swing(IsOpen ? _openRot : _closedRot));
        }

        // ReSharper disable once UnusedMember.Global
        public void Open()
        {
            if (!IsOpen) Toggle();
        }

        // ReSharper disable once UnusedMember.Global
        public void Close()
        {
            if (IsOpen) Toggle();
        }

        private IEnumerator Swing(Quaternion target)
        {
            var start = transform.localRotation;
            var elapsed = 0f;

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(start, target, elapsed / swingDuration);
                yield return null;
            }

            transform.localRotation = target;
            _swingCoroutine = null;
        }
    }
}