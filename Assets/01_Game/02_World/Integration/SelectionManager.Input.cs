using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Characters;

namespace Zombera.Systems
{
    public sealed partial class SelectionManager
    {
        private void EnableOptionalActions()
        {
            EnableAction(leftClickAction, ref _enabledLeftClickAction);
            EnableAction(pointerPositionAction, ref _enabledPointerPositionAction);
            EnableAction(additiveSelectionAction, ref _enabledAdditiveAction);
            EnableAction(rtsModifierAction, ref _enabledRtsModifierAction);
        }

        private void DisableOptionalActions()
        {
            DisableAction(leftClickAction, _enabledLeftClickAction);
            DisableAction(pointerPositionAction, _enabledPointerPositionAction);
            DisableAction(additiveSelectionAction, _enabledAdditiveAction);
            DisableAction(rtsModifierAction, _enabledRtsModifierAction);

            _enabledLeftClickAction = false;
            _enabledPointerPositionAction = false;
            _enabledAdditiveAction = false;
            _enabledRtsModifierAction = false;
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

        private void ApplyLegacyMouseOverride(bool enabled, bool force = false)
        {
            if (!overrideLegacyPlayerMouseInput) return;
            if (!force && _legacyMouseOverrideApplied == enabled) return;

            _legacyMouseOverrideApplied = enabled;

            var activeControllers = PlayerInputController.ActiveInstances;
            for (var i = 0; i < activeControllers.Count; i++)
            {
                var controller = activeControllers[i];
                if (controller == null) continue;

                controller.SetExternalMouseInputOverride(enabled);
            }
        }

        private bool ShouldCaptureRtsMouseInput(Vector2 pointerScreenPosition)
        {
            if (!overrideLegacyPlayerMouseInput) return true;

            var selectedCount = squadManager != null ? squadManager.SelectedMembers.Count : 0;
            var context = new PlayerControlModeContext(
                false,
                IsPointerOverUi(pointerScreenPosition),
                hybridSingleCharacterAndRtsMode,
                autoSwitchToRtsWhenMultipleSelected,
                selectedCount,
                rtsMouseCaptureSelectionThreshold,
                requireRtsModifierForMouseCapture,
                IsRtsModifierActive());

            var mode = PlayerControlModePolicy.Evaluate(context);
            if (controlModeCoordinator != null)
                controlModeCoordinator.RequestMode(mode, PlayerControlModeSource.SelectionManager);

            if (!hybridSingleCharacterAndRtsMode) return true;

            if (_dragCandidateActive || _isDragging) return true;

            return mode == PlayerControlMode.SquadRts;
        }

        private bool IsRtsModifierActive()
        {
            if (rtsModifierAction?.action != null) return rtsModifierAction.action.IsPressed();

            if (!useAltAsRtsModifierFallback || Keyboard.current == null) return false;

            return Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;
        }

        private bool TryReadPointerPosition(out Vector2 screenPosition)
        {
            if (pointerPositionAction?.action != null)
                return CursorService.TryGetGameplayPointerScreenPosition(
                    pointerPositionAction.action,
                    out screenPosition);

            return CursorService.TryGetGameplayPointerScreenPosition(out screenPosition);
        }

        private bool TryReadLeftClickState(out bool wasPressed, out bool wasReleased, out bool isHeld)
        {
            wasPressed = false;
            wasReleased = false;
            isHeld = false;

            if (leftClickAction?.action != null)
            {
                var action = leftClickAction.action;
                wasPressed = action.WasPressedThisFrame();
                wasReleased = action.WasReleasedThisFrame();
                isHeld = action.IsPressed();
                return true;
            }

            if (Mouse.current == null) return false;

            wasPressed = Mouse.current.leftButton.wasPressedThisFrame;
            wasReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            isHeld = Mouse.current.leftButton.isPressed;
            return true;
        }
    }
}
