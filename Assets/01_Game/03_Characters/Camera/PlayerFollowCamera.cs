#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Smooth third-person follow camera for the player unit in the world scene.
    ///     Operates in top-down / isometric offset style suitable for squad gameplay.
    /// </summary>
    public sealed class PlayerFollowCamera : MonoBehaviour
    {
        [Header("Target")] [SerializeField] private Transform target;

        [Tooltip(
            "When no target is assigned, try to follow the first alive player Unit (e.g. scene Main Camera before PlayerSpawnWiringService runs).")]
        [SerializeField]
        private bool autoResolvePlayerTarget = true;

        [Header("Offset")] [SerializeField] private Vector3 offset = new(0f, 12f, -8f);

        [SerializeField] private float smoothSpeed = 8f;

        [Header("Stability")]
        [SerializeField] [Min(0.01f)] private float focusSmoothTime = 0.08f;
        [SerializeField] [Min(0f)] private float focusVerticalDeadZone = 0.03f;
        [SerializeField] [Min(0f)] private float lookSmoothing = 14f;

        [Header("Zoom")] [SerializeField] private float zoomSpeed = 2f;

        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;
        [SerializeField] [Min(0.01f)] private float zoomSmoothTime = 0.08f;
        [SerializeField] [Min(1f)] private float maxScrollStepPerFrame = 120f;

        [Header("Scroll Tilt")] [SerializeField]
        private bool enableShiftScrollTilt = true;

        [SerializeField] [Min(0f)] private float shiftScrollTiltSpeed = 0.08f;

        [Header("Orbit")] [SerializeField] [Min(0f)]
        private float middleMouseOrbitSensitivity = 0.2f;

        [SerializeField] private bool invertMiddleMouseOrbit;
        [SerializeField] private bool allowVerticalOrbit = true;
        [SerializeField] private bool invertMiddleMouseVerticalOrbit;
        [SerializeField] private Vector2 verticalOrbitPitchRange = new(-80f, 85f);
        [SerializeField] [Min(0.01f)] private float orbitSmoothTime = 0.05f;
        [SerializeField] [Min(1f)] private float maxOrbitMouseDeltaPerFrame = 120f;
        private float _baseOffsetDistance = 1f;

        private float _currentZoomMultiplier = 1f;
        private float _targetZoomMultiplier = 1f;
        private Vector3 _focusPointVelocity;
        private bool _hasSmoothedFocusPoint;
        private Vector3 _smoothedFocusPoint;
        private bool _orbitInitialized;
        private float _orbitPitchDegrees;
        private float _orbitPitchVelocity;
        private float _targetOrbitPitchDegrees;
        private float _orbitYawDegrees;
        private float _orbitYawVelocity;
        private float _targetOrbitYawDegrees;
        private float _zoomVelocity;
        private bool _loggedMissingTarget;
        private float _nextAutoResolvePlayerAt;
        private static readonly List<Unit> SPlayerBuffer = new(4);

        private void Awake()
        {
            // Ensure existing scenes that had this disabled still get full 3D orbit behavior.
            allowVerticalOrbit = true;
            SyncOrbitFromOffset();
        }

        private void LateUpdate()
        {
            TryResolvePlayerTargetIfNeeded();

            if (target == null)
            {
                LogMissingTargetWarningOnce();
                return;
            }

            _loggedMissingTarget = false;

            if (!_orbitInitialized) SyncOrbitFromOffset();

            HandleScrollInput();
            HandleMiddleMouseOrbit();

            var frameDelta = Mathf.Max(0.0001f, Time.deltaTime);
            SmoothOrbitAndZoom(frameDelta);
            var focusPoint = ResolveSmoothedFocusPoint(frameDelta);
            var orbitDistance = Mathf.Max(0.01f, _baseOffsetDistance) * _currentZoomMultiplier;
            var orbitDirection = GetOrbitDirection();
            var desiredPosition = focusPoint + orbitDirection * orbitDistance;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * frameDelta);

            var targetLookRotation = Quaternion.LookRotation(focusPoint + Vector3.up - transform.position,
                Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetLookRotation, lookSmoothing * frameDelta);
        }

        private void TryResolvePlayerTargetIfNeeded()
        {
            if (target != null || !autoResolvePlayerTarget || Time.unscaledTime < _nextAutoResolvePlayerAt) return;

            _nextAutoResolvePlayerAt = Time.unscaledTime + 0.25f;
            if (!TryResolvePlayerFollowTarget(SPlayerBuffer, out var resolved)) return;

            SetTarget(resolved);
        }

        private void LogMissingTargetWarningOnce()
        {
            if (_loggedMissingTarget) return;

            _loggedMissingTarget = true;
            if (!Application.isEditor && !Debug.isDebugBuild) return;

            Debug.LogWarning(
                $"[PlayerFollowCamera] No target assigned on '{name}'. Camera follow is idle.",
                this);
        }

        private static bool TryResolvePlayerFollowTarget(List<Unit> buffer, out Transform resolved)
        {
            resolved = null;
            if (UnitManager.Instance == null) return false;

            var players = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, buffer);
            foreach (var u in players)
            {
                if (u == null) continue;

                resolved = u.transform;
                return true;
            }

            return false;
        }

        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget)
            {
                if (target != null) SnapToTarget();
                return;
            }

            target = newTarget;

            if (Application.isEditor || Debug.isDebugBuild)
                Debug.Log($"[PlayerFollowCamera] Target set to '{(target != null ? target.name : "null")}' on camera '{name}'.", this);

            if (target == null) return;
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (!_orbitInitialized) SyncOrbitFromOffset();

            var focusPoint = target.position;
            _smoothedFocusPoint = focusPoint;
            _focusPointVelocity = Vector3.zero;
            _hasSmoothedFocusPoint = true;
            var orbitDistance = Mathf.Max(0.01f, _baseOffsetDistance) * _currentZoomMultiplier;
            var orbitDirection = GetOrbitDirection();
            var desiredPosition = focusPoint + orbitDirection * orbitDistance;
            transform.position = desiredPosition;
            transform.LookAt(focusPoint + Vector3.up * 1f);
        }

        private Vector3 ResolveSmoothedFocusPoint(float deltaTime)
        {
            var targetPoint = target.position;
            if (!_hasSmoothedFocusPoint)
            {
                _smoothedFocusPoint = targetPoint;
                _hasSmoothedFocusPoint = true;
                return _smoothedFocusPoint;
            }

            if (Mathf.Abs(targetPoint.y - _smoothedFocusPoint.y) < focusVerticalDeadZone)
                targetPoint.y = _smoothedFocusPoint.y;

            _smoothedFocusPoint = Vector3.SmoothDamp(
                _smoothedFocusPoint,
                targetPoint,
                ref _focusPointVelocity,
                Mathf.Max(0.01f, focusSmoothTime),
                Mathf.Infinity,
                deltaTime);

            return _smoothedFocusPoint;
        }

        private void HandleScrollInput()
        {
            if (Mouse.current == null) return;

            var scroll = Mathf.Clamp(Mouse.current.scroll.ReadValue().y,
                -Mathf.Max(1f, maxScrollStepPerFrame),
                Mathf.Max(1f, maxScrollStepPerFrame));

            if (Mathf.Abs(scroll) <= 0.01f) return;

            if (enableShiftScrollTilt && IsShiftPressed())
            {
                _targetOrbitPitchDegrees = ClampOrbitPitch(_targetOrbitPitchDegrees + scroll * shiftScrollTiltSpeed);
                return;
            }

            var baseDistance = Mathf.Max(0.01f, _baseOffsetDistance);
            _targetZoomMultiplier = Mathf.Clamp(_targetZoomMultiplier - scroll * zoomSpeed * 0.01f,
                minZoom / baseDistance, maxZoom / baseDistance);
        }

        private void HandleMiddleMouseOrbit()
        {
            if (Mouse.current == null || !Mouse.current.middleButton.isPressed) return;

            var maxDelta = Mathf.Max(1f, maxOrbitMouseDeltaPerFrame);
            var delta = Mouse.current.delta.ReadValue();
            delta.x = Mathf.Clamp(delta.x, -maxDelta, maxDelta);
            delta.y = Mathf.Clamp(delta.y, -maxDelta, maxDelta);
            if (delta.sqrMagnitude <= 0.0001f) return;

            var yawDirection = invertMiddleMouseOrbit ? -1f : 1f;
            _targetOrbitYawDegrees += delta.x * middleMouseOrbitSensitivity * yawDirection;
            _targetOrbitYawDegrees = Mathf.Repeat(_targetOrbitYawDegrees, 360f);

            if (!allowVerticalOrbit) return;

            var pitchDirection = invertMiddleMouseVerticalOrbit ? 1f : -1f;
            _targetOrbitPitchDegrees += delta.y * middleMouseOrbitSensitivity * pitchDirection;
            _targetOrbitPitchDegrees = ClampOrbitPitch(_targetOrbitPitchDegrees);
        }

        private void SmoothOrbitAndZoom(float deltaTime)
        {
            _currentZoomMultiplier = Mathf.SmoothDamp(
                _currentZoomMultiplier,
                _targetZoomMultiplier,
                ref _zoomVelocity,
                Mathf.Max(0.01f, zoomSmoothTime),
                Mathf.Infinity,
                deltaTime);

            _orbitYawDegrees = Mathf.SmoothDampAngle(
                _orbitYawDegrees,
                _targetOrbitYawDegrees,
                ref _orbitYawVelocity,
                Mathf.Max(0.01f, orbitSmoothTime),
                Mathf.Infinity,
                deltaTime);

            _orbitPitchDegrees = Mathf.SmoothDampAngle(
                _orbitPitchDegrees,
                _targetOrbitPitchDegrees,
                ref _orbitPitchVelocity,
                Mathf.Max(0.01f, orbitSmoothTime),
                Mathf.Infinity,
                deltaTime);

            _orbitPitchDegrees = ClampOrbitPitch(_orbitPitchDegrees);
        }

        private void SyncOrbitFromOffset()
        {
            _baseOffsetDistance = Mathf.Max(0.01f, offset.magnitude);
            var direction = offset / _baseOffsetDistance;

            _orbitYawDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _orbitPitchDegrees = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            _orbitPitchDegrees = ClampOrbitPitch(_orbitPitchDegrees);
            _targetOrbitYawDegrees = _orbitYawDegrees;
            _targetOrbitPitchDegrees = _orbitPitchDegrees;
            _targetZoomMultiplier = _currentZoomMultiplier;
            _zoomVelocity = 0f;
            _orbitYawVelocity = 0f;
            _orbitPitchVelocity = 0f;
            _orbitInitialized = true;
        }

        private Vector3 GetOrbitDirection()
        {
            var yawRadians = _orbitYawDegrees * Mathf.Deg2Rad;
            var pitchRadians = _orbitPitchDegrees * Mathf.Deg2Rad;
            var horizontalMagnitude = Mathf.Cos(pitchRadians);

            var direction = new Vector3(
                Mathf.Sin(yawRadians) * horizontalMagnitude,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * horizontalMagnitude);

            return direction.sqrMagnitude <= 0.0001f ? Vector3.back : direction.normalized;
        }

        private float ClampOrbitPitch(float pitch)
        {
            var minPitch = Mathf.Clamp(Mathf.Min(verticalOrbitPitchRange.x, verticalOrbitPitchRange.y), -89f, 89f);
            var maxPitch = Mathf.Clamp(Mathf.Max(verticalOrbitPitchRange.x, verticalOrbitPitchRange.y), -89f, 89f);
            return Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private static bool IsShiftPressed()
        {
            if (Keyboard.current == null) return false;

            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
    }
}