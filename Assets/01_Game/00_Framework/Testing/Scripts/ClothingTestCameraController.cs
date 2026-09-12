using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Systems;

namespace Zombera.Testing
{
    /// <summary>
    /// Camera controller for testing scenes.
    /// WASD: Move
    /// QE: Rotate
    /// Scroll: Zoom
    /// Mouse Right-Click: Orbit
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ClothingTestCameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 10f;
        public float fastMoveMultiplier = 3f;

        [Header("Rotation")]
        public float rotationSpeed = 150f;
        public float mouseSensitivity = 0.5f;

        [Header("Zoom")]
        public float zoomSpeed = 30f;

        private float _lastLogTime;
        private CursorStateHandle _orbitCursorStateHandle;

        private void OnDisable()
        {
            _orbitCursorStateHandle.Dispose();
        }

        private void LateUpdate()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                if (Time.time - _lastLogTime > 5f)
                {
                    Debug.LogWarning($"[ClothingTestCameraController] Input devices missing: Keyboard={keyboard != null}, Mouse={mouse != null}");
                    _lastLogTime = Time.time;
                }
                return;
            }

            HandleMovement(keyboard);
            HandleRotation(keyboard, mouse);
            HandleZoom(mouse);
        }

        private void HandleMovement(Keyboard keyboard)
        {
            Vector3 move = Vector3.zero;

            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.dKey.isPressed) move += transform.right;

            if (move.sqrMagnitude > 0.001f)
            {
                float currentSpeed = moveSpeed;
                if (keyboard.leftShiftKey.isPressed) currentSpeed *= fastMoveMultiplier;
                transform.position += move.normalized * (currentSpeed * Time.unscaledDeltaTime);
            }
            }

            private void HandleRotation(Keyboard keyboard, Mouse mouse)
            {
            // QE Keys
            float rotation = 0f;
            if (keyboard.qKey.isPressed) rotation -= 1f;
            if (keyboard.eKey.isPressed) rotation += 1f;

            if (Mathf.Abs(rotation) > 0.01f)
            {
                transform.Rotate(Vector3.up, rotation * rotationSpeed * Time.unscaledDeltaTime, Space.World);
            }

            // Mouse Right-Click Drag
            if (mouse.rightButton.isPressed)
            {
                if (!_orbitCursorStateHandle.IsValid)
                    _orbitCursorStateHandle = CursorService.RequestCapturedPointer();

                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.0001f)
                {
                    // Horizontal rotation
                    transform.Rotate(Vector3.up, delta.x * mouseSensitivity, Space.World);
                    
                    // Vertical rotation
                    float angleX = transform.localEulerAngles.x;
                    if (angleX > 180) angleX -= 360;
                    
                    float verticalRot = -delta.y * mouseSensitivity;
                    if (angleX + verticalRot > -85 && angleX + verticalRot < 85)
                    {
                        transform.Rotate(Vector3.right, verticalRot, Space.Self);
                    }
                }
            }
            else
            {
                _orbitCursorStateHandle.Dispose();
            }
        }

        private void HandleZoom(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                transform.Translate(Vector3.forward * (scroll * 0.01f * zoomSpeed), Space.Self);
            }
        }
    }
}