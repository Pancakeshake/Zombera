using UnityEngine;
using UnityEngine.InputSystem;

namespace Zombera.Systems
{
    public sealed partial class CommandManager
    {
        private void EnableOptionalActions()
        {
            EnableAction(rightClickAction, ref _enabledRightClickAction);
            EnableAction(pointerPositionAction, ref _enabledPointerPositionAction);
            EnableAction(rtsModifierAction, ref _enabledRtsModifierAction);
        }

        private void DisableOptionalActions()
        {
            DisableAction(rightClickAction, _enabledRightClickAction);
            DisableAction(pointerPositionAction, _enabledPointerPositionAction);
            DisableAction(rtsModifierAction, _enabledRtsModifierAction);

            _enabledRightClickAction = false;
            _enabledPointerPositionAction = false;
            _enabledRtsModifierAction = false;
        }

        private bool ShouldProcessRtsCommandInput()
        {
            if (!hybridSingleCharacterAndRtsMode) return true;

            // Evaluate policy directly so right-click commands are not blocked by stale
            // coordinator state when this component runs before SelectionManager.
            var selectedCount = squadManager != null ? squadManager.SelectedMembers.Count : 0;
            var context = new PlayerControlModeContext(
                false,
                false,
                hybridSingleCharacterAndRtsMode,
                autoSwitchToRtsWhenMultipleSelected,
                selectedCount,
                rtsCommandSelectionThreshold,
                requireRtsModifierForCommandMouseInput,
                IsRtsModifierActive());

            return PlayerControlModePolicy.ShouldCaptureRtsMouseInput(context);
        }

        private bool IsRtsModifierActive()
        {
            if (rtsModifierAction?.action != null) return rtsModifierAction.action.IsPressed();

            if (!useAltAsRtsModifierFallback || Keyboard.current == null) return false;

            return Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;
        }

        private static void EnableAction(InputActionReference actionReference, ref bool enabledByThisComponent)
        {
            enabledByThisComponent = false;

            if (actionReference?.action == null || actionReference.action.enabled) return;

            actionReference.action.Enable();
            enabledByThisComponent = true;
        }

        private static void DisableAction(InputActionReference actionReference, bool enabledByThisComponent)
        {
            if (!enabledByThisComponent || actionReference?.action == null || !actionReference.action.enabled) return;

            actionReference.action.Disable();
        }

        private bool TryReadPointerPosition(out Vector2 screenPosition)
        {
            if (pointerPositionAction?.action != null)
                return CursorService.TryGetGameplayPointerScreenPosition(
                    pointerPositionAction.action,
                    out screenPosition);

            return CursorService.TryGetGameplayPointerScreenPosition(out screenPosition);
        }

        private bool TryReadRightClickPressed(out bool wasPressed)
        {
            wasPressed = false;

            if (rightClickAction?.action != null)
            {
                wasPressed = rightClickAction.action.WasPressedThisFrame();
                return true;
            }

            if (Mouse.current == null) return false;

            wasPressed = Mouse.current.rightButton.wasPressedThisFrame;
            return true;
        }

        private bool TryReadRightClickState(out bool pressedThisFrame, out bool releasedThisFrame, out bool held)
        {
            pressedThisFrame = false;
            releasedThisFrame = false;
            held = false;

            if (rightClickAction?.action != null)
            {
                pressedThisFrame = rightClickAction.action.WasPressedThisFrame();
                releasedThisFrame = rightClickAction.action.WasReleasedThisFrame();
                held = rightClickAction.action.IsPressed();
                return true;
            }

            if (Mouse.current == null) return false;

            pressedThisFrame = Mouse.current.rightButton.wasPressedThisFrame;
            releasedThisFrame = Mouse.current.rightButton.wasReleasedThisFrame;
            held = Mouse.current.rightButton.isPressed;
            return true;
        }
    }
}
