#region

using System;
using UnityEngine;
using Zombera.UI.SquadManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#endregion

namespace Zombera.Debugging.DebugTools
{
    // ReSharper disable MergeIntoPattern
    // ReSharper disable InvertIf
    /// <summary>
    ///     Global debug keybinds.
    ///     Required mappings:
    ///     - F8: toggle debug menu
    ///     - F2: toggle slow motion
    ///     - F3: toggle AI debug visuals
    ///     - F4: spawn zombie
    ///     - F5: spawn survivor
    ///     - F6: toggle god mode
    ///     - F7: spawn zombie horde
    ///     - F9: advance world simulation
    /// </summary>
    public sealed class DebugKeybinds : MonoBehaviour, IDebugTool
    {
        [Header("Keybinds")] [SerializeField] private KeyCode toggleMenuKey = KeyCode.F8;

        [SerializeField] private KeyCode toggleSlowMotionKey = KeyCode.F2;
        [SerializeField] private KeyCode toggleAIDebugKey = KeyCode.F3;
        [SerializeField] private KeyCode spawnZombieKey = KeyCode.F4;
        [SerializeField] private KeyCode spawnSurvivorKey = KeyCode.F5;
        [SerializeField] private KeyCode toggleGodModeKey = KeyCode.F6;
        [SerializeField] private KeyCode spawnHordeKey = KeyCode.F7;
        [SerializeField] private KeyCode advanceWorldSimulationKey = KeyCode.F9;

        [Header("Tool References")] [SerializeField]
        private SpawnDebugTools spawnDebugTools;

        [SerializeField] private WorldDebugTools worldDebugTools;
        [SerializeField] private AISimulationTools aiSimulationTools;

        [Header("Input Arbitration")] [SerializeField]
        private bool suppressMenuHotkeyConflicts = true;

        [SerializeField] private ZomberaSquadManagementUI squadManagementUI;

#pragma warning disable CS0414
        [SerializeField] private Key modifierKey = Key.LeftShift;
#pragma warning restore CS0414
        [SerializeField] private bool requireModifier;

        private void Update()
        {
            if (!IsToolEnabled) return;

            if (WasKeyPressedThisFrame(toggleMenuKey))
            {
                DebugManager.Instance?.ToggleDebugMenu();
                return;
            }

            if (ShouldSuppressDebugFunctionHotkeysThisFrame()) return;

            if (WasKeyPressedThisFrame(toggleSlowMotionKey)) DebugManager.Instance?.ToggleSlowMotion();

            if (WasKeyPressedThisFrame(toggleAIDebugKey)) DebugManager.Instance?.ToggleAIDebugVisuals();

            if (WasKeyPressedThisFrame(spawnZombieKey)) spawnDebugTools?.SpawnZombie();

            if (WasKeyPressedThisFrame(spawnSurvivorKey)) spawnDebugTools?.SpawnSurvivor();

            if (WasKeyPressedThisFrame(toggleGodModeKey)) DebugManager.Instance?.ToggleGodMode();

            if (WasKeyPressedThisFrame(spawnHordeKey)) spawnDebugTools?.SpawnZombieHorde();

            if (WasKeyPressedThisFrame(advanceWorldSimulationKey)) worldDebugTools?.AdvanceWorldSimulation();
        }

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(DebugKeybinds);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        private bool ShouldSuppressDebugFunctionHotkeysThisFrame()
        {
            if (!suppressMenuHotkeyConflicts) return false;

            if (squadManagementUI == null) squadManagementUI = FindFirstObjectByType<ZomberaSquadManagementUI>();

            if (squadManagementUI == null) return false;

            if (!squadManagementUI.IsVisible) return false;

            return WasKeyPressedThisFrame(toggleSlowMotionKey)
                   || WasKeyPressedThisFrame(toggleAIDebugKey)
                   || WasKeyPressedThisFrame(spawnZombieKey)
                   || WasKeyPressedThisFrame(spawnSurvivorKey);
        }

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && TryMapKeyCodeToInputSystemKey(keyCode, out var mappedKey))
            {
                var keyControl = keyboard[mappedKey];
                return keyControl != null && keyControl.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKeyCodeToInputSystemKey(KeyCode keyCode, out Key key)
        {
            if (Enum.TryParse(keyCode.ToString(), true, out key)) return true;

            key = keyCode switch
            {
                KeyCode.Alpha0 => Key.Digit0,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.Alpha5 => Key.Digit5,
                KeyCode.Alpha6 => Key.Digit6,
                KeyCode.Alpha7 => Key.Digit7,
                KeyCode.Alpha8 => Key.Digit8,
                KeyCode.Alpha9 => Key.Digit9,
                KeyCode.Return or KeyCode.KeypadEnter => Key.Enter,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.RightControl => Key.RightCtrl,
                KeyCode.LeftCommand => Key.LeftMeta,
                KeyCode.RightCommand => Key.RightMeta,
                KeyCode.BackQuote => Key.Backquote,
                KeyCode.Keypad0 => Key.Numpad0,
                KeyCode.Keypad1 => Key.Numpad1,
                KeyCode.Keypad2 => Key.Numpad2,
                KeyCode.Keypad3 => Key.Numpad3,
                KeyCode.Keypad4 => Key.Numpad4,
                KeyCode.Keypad5 => Key.Numpad5,
                KeyCode.Keypad6 => Key.Numpad6,
                KeyCode.Keypad7 => Key.Numpad7,
                KeyCode.Keypad8 => Key.Numpad8,
                KeyCode.Keypad9 => Key.Numpad9,
                KeyCode.KeypadPeriod => Key.NumpadPeriod,
                KeyCode.KeypadDivide => Key.NumpadDivide,
                KeyCode.KeypadMultiply => Key.NumpadMultiply,
                KeyCode.KeypadMinus => Key.NumpadMinus,
                KeyCode.KeypadPlus => Key.NumpadPlus,
                KeyCode.KeypadEquals => Key.NumpadEquals,
                _ => Key.None
            };

            return key != Key.None;
        }
#endif
    }

    /// <summary>Enumeration of rebindable debug actions.</summary>
    // ReSharper disable UnusedType.Global
    // ReSharper disable UnusedMember.Global
    public enum DebugAction
    {
        ToggleMenu,
        ToggleSlowMotion,
        ToggleAIDebug,
        SpawnZombie,
        SpawnSurvivor,
        ToggleGodMode,
        SpawnHorde,
        AdvanceWorldSimulation
    }
    // ReSharper restore UnusedMember.Global
    // ReSharper restore UnusedType.Global
}