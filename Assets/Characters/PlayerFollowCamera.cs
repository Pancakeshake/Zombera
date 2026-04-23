using UnityEngine;
using UnityEngine.InputSystem;

namespace Zombera.Characters
{
    /// <summary>
    /// Smooth third-person follow camera for the player unit in the world scene.
    /// Operates in top-down / isometric offset style suitable for squad gameplay.
    /// </summary>
    public sealed class PlayerFollowCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Offset")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;

        [Header("Scroll Tilt")]
        [SerializeField] private bool enableShiftScrollTilt = true;
        [SerializeField, Min(0f)] private float shiftScrollTiltSpeed = 0.08f;

        [Header("Orbit")]
        [SerializeField, Min(0f)] private float middleMouseOrbitSensitivity = 0.2f;
        [SerializeField] private bool invertMiddleMouseOrbit;
        [SerializeField] private bool allowVerticalOrbit = true;
        [SerializeField] private bool invertMiddleMouseVerticalOrbit;
        [SerializeField] private Vector2 verticalOrbitPitchRange = new Vector2(-80f, 85f);

        private float currentZoomMultiplier = 1f;
        private float orbitYawDegrees;
        private float orbitPitchDegrees;
        private float baseOffsetDistance = 1f;
        private bool orbitInitialized;

        private void Awake()
        {
            // Ensure existing scenes that had this disabled still get full 3D orbit behavior.
            allowVerticalOrbit = true;
            SyncOrbitFromOffset();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target != null)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!orbitInitialized)
            {
                SyncOrbitFromOffset();
            }

            HandleScrollInput();
            HandleMiddleMouseOrbit();

            Vector3 focusPoint = target.position;
            float orbitDistance = Mathf.Max(0.01f, baseOffsetDistance) * currentZoomMultiplier;
            Vector3 orbitDirection = GetOrbitDirection();
            Vector3 desiredPosition = focusPoint + orbitDirection * orbitDistance;
            float frameDelta = Mathf.Max(0f, Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * frameDelta);
            transform.LookAt(focusPoint + Vector3.up * 1f);
        }

        private void SnapToTarget()
        {
            if (!orbitInitialized)
            {
                SyncOrbitFromOffset();
            }

            Vector3 focusPoint = target.position;
            float orbitDistance = Mathf.Max(0.01f, baseOffsetDistance) * currentZoomMultiplier;
            Vector3 orbitDirection = GetOrbitDirection();
            Vector3 desiredPosition = focusPoint + orbitDirection * orbitDistance;
            transform.position = desiredPosition;
            transform.LookAt(focusPoint + Vector3.up * 1f);
        }

        private void HandleScrollInput()
        {
            if (Mouse.current == null)
            {
                return;
            }

            float scroll = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (enableShiftScrollTilt && IsShiftPressed())
                {
                    orbitPitchDegrees = ClampOrbitPitch(orbitPitchDegrees + scroll * shiftScrollTiltSpeed);
                    return;
                }

                float baseDistance = Mathf.Max(0.01f, baseOffsetDistance);
                currentZoomMultiplier = Mathf.Clamp(currentZoomMultiplier - scroll * zoomSpeed * 0.01f, minZoom / baseDistance, maxZoom / baseDistance);
            }
        }

        private void HandleMiddleMouseOrbit()
        {
            if (Mouse.current == null || !Mouse.current.middleButton.isPressed)
            {
                return;
            }

            Vector2 delta = Mouse.current.delta.ReadValue();
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float yawDirection = invertMiddleMouseOrbit ? -1f : 1f;
            orbitYawDegrees += delta.x * middleMouseOrbitSensitivity * yawDirection;
            orbitYawDegrees = Mathf.Repeat(orbitYawDegrees, 360f);

            if (!allowVerticalOrbit)
            {
                return;
            }

            float pitchDirection = invertMiddleMouseVerticalOrbit ? 1f : -1f;
            orbitPitchDegrees += delta.y * middleMouseOrbitSensitivity * pitchDirection;
            orbitPitchDegrees = ClampOrbitPitch(orbitPitchDegrees);
        }

        private void SyncOrbitFromOffset()
        {
            baseOffsetDistance = Mathf.Max(0.01f, offset.magnitude);
            Vector3 direction = offset / baseOffsetDistance;

            orbitYawDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            orbitPitchDegrees = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            orbitPitchDegrees = ClampOrbitPitch(orbitPitchDegrees);
            orbitInitialized = true;
        }

        private Vector3 GetOrbitDirection()
        {
            float yawRadians = orbitYawDegrees * Mathf.Deg2Rad;
            float pitchRadians = orbitPitchDegrees * Mathf.Deg2Rad;
            float horizontalMagnitude = Mathf.Cos(pitchRadians);

            Vector3 direction = new Vector3(
                Mathf.Sin(yawRadians) * horizontalMagnitude,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * horizontalMagnitude);

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.back;
            }

            return direction.normalized;
        }

        private float ClampOrbitPitch(float pitch)
        {
            float minPitch = Mathf.Clamp(Mathf.Min(verticalOrbitPitchRange.x, verticalOrbitPitchRange.y), -89f, 89f);
            float maxPitch = Mathf.Clamp(Mathf.Max(verticalOrbitPitchRange.x, verticalOrbitPitchRange.y), -89f, 89f);
            return Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private static bool IsShiftPressed()
        {
            if (Keyboard.current == null)
            {
                return false;
            }

            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
    }
}
