#region

using System;
using UnityEngine;
using UnityEngine.UI;
using EventSystem = UnityEngine.EventSystems.EventSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zombera.Systems;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class MainMenuController
    {
        private void TryHandlePointerFallback()
        {
            if (ShouldSkipPointerFallback()) return;
            if (!TryResolveButtonsForFallback()) return;

            if (!TryReadPointerDownPosition(out var screenPosition)) return;
            TryInvokeFallbackActionsInPriorityOrder(screenPosition);
        }

        private bool ShouldSkipPointerFallback()
        {
            if (!Application.isPlaying) return true;
            if (!enablePointerFallbackWhenUiEventsFail) return true;

            // Fallback is only for main menu button interaction. If main menu root is hidden
            // (e.g. transition/start flow), do not process fallback clicks.
            if (menuRoot != null && !menuRoot.activeInHierarchy) return true;

            // Never run fallback while subpanels are open; their own buttons should own input.
            if (characterCreatorPanel != null && characterCreatorPanel.IsVisible) return true;
            if (settingsPanel != null && settingsPanel.IsVisible) return true;

            // If EventSystem is currently over UI, let normal UI processing handle the click.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;

            return false;
        }

        private bool TryResolveButtonsForFallback()
        {
            if (startGameButton == null || loadGameButton == null || settingsButton == null || quitButton == null)
                EnsureButtonsBound();

            return startGameButton != null
                   || loadGameButton != null
                   || settingsButton != null
                   || quitButton != null;
        }

        private void TryInvokeFallbackActionsInPriorityOrder(Vector2 screenPosition)
        {
            if (TryInvokeIfPointerOverButton(startGameButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Start Game.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(startGameButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Start Game.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(loadGameButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Load Game.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(settingsButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Settings.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(quitButton, screenPosition))
                Debug.Log("[MainMenuController] Pointer fallback invoked Quit.", this);
        }

        private static bool TryReadPointerDownPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return CursorService.TryGetRawPointerScreenPosition(out screenPosition);

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                return CursorService.TryGetRawPointerScreenPosition(out screenPosition);
#endif

            try
            {
                if (Input.GetMouseButtonDown(0))
                    return CursorService.TryGetRawPointerScreenPosition(out screenPosition);
            }
            catch (InvalidOperationException)
            {
                // Ignore legacy input API when project input backend does not support it.
            }

            screenPosition = Vector2.zero;
            return false;
        }

        private static bool TryInvokeIfPointerOverButton(Button button, Vector2 screenPosition)
        {
            if (button == null || !button.isActiveAndEnabled || !button.interactable) return false;

            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null) return false;

            var eventCamera = ResolveEventCamera(button);
            var containsPointer =
                RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
            if (!containsPointer && eventCamera != null)
                containsPointer =
                    RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, null);

            if (!containsPointer) return false;

            button.onClick.Invoke();
            return true;
        }

        private static Camera ResolveEventCamera(Button button)
        {
            if (button == null) return null;

            var parentCanvas = button.GetComponentInParent<Canvas>();
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            return parentCanvas.worldCamera;
        }
    }
}
