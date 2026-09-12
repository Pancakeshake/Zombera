using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeInputFrameHelper
    {
        internal static bool TryHandleForceCloseHotkey(Keyboard keyboard, Action forceCloseBuildUi)
        {
            if (keyboard == null || forceCloseBuildUi == null)
                return false;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                forceCloseBuildUi.Invoke();
                return true;
            }

            // Legacy alternate close (kept for existing bindings); Esc is the primary cancel key.
            if (keyboard.eKey.wasPressedThisFrame)
            {
                forceCloseBuildUi.Invoke();
                return true;
            }

            return false;
        }

        internal static bool TryHandleRightClickToggle(
            Mouse mouse,
            bool rightClickTogglesRadialMenu,
            bool ignoreRightClickWhenPointerOverUi,
            Func<bool> canProcessRightClickToggle,
            Func<bool> isPointerOverUi,
            Func<bool> tryToggleBuildingRadialMenu)
        {
            if (!rightClickTogglesRadialMenu || mouse == null)
                return false;

            if (canProcessRightClickToggle != null && !canProcessRightClickToggle())
                return false;

            if (!mouse.rightButton.wasPressedThisFrame)
                return false;

            if (ignoreRightClickWhenPointerOverUi && isPointerOverUi != null && isPointerOverUi())
                return false;

            return tryToggleBuildingRadialMenu != null && tryToggleBuildingRadialMenu();
        }

        internal static bool TryHandleKeyboardBuildToggle(
            Keyboard keyboard,
            bool toggleRadialMenuWithKeyboard,
            Key toggleRadialMenuKey,
            bool blockBuildToggleThisFrame,
            bool manualBuildModeActive,
            Action ensureRadialMenuClosed,
            Action enterManualBuildMode,
            Action exitManualBuildMode,
            out bool nextManualBuildModeActive)
        {
            nextManualBuildModeActive = manualBuildModeActive;

            if (!toggleRadialMenuWithKeyboard || keyboard == null)
                return false;

            if (blockBuildToggleThisFrame)
                return false;

            if (!keyboard[toggleRadialMenuKey].wasPressedThisFrame)
                return false;

            nextManualBuildModeActive = !manualBuildModeActive;
            ensureRadialMenuClosed?.Invoke();

            if (nextManualBuildModeActive)
                enterManualBuildMode?.Invoke();
            else
                exitManualBuildMode?.Invoke();

            return true;
        }

        internal static bool IsPointerOverUi()
        {
            var uiEventSystem = EventSystem.current;
            if (uiEventSystem == null) return false;

            return uiEventSystem.IsPointerOverGameObject();
        }
    }
}
