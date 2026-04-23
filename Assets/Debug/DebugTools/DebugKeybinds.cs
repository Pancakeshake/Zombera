using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zombera.UI.SquadManagement;

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    /// Global debug keybinds.
    /// Required mappings:
    /// - F1: toggle debug menu
    /// - F2: toggle slow motion
    /// - F3: toggle AI debug visuals
    /// - F4: spawn zombie
    /// - F5: spawn survivor
    /// - F6: toggle god mode
    /// - F7: spawn zombie horde
    /// - F8: advance world simulation
    /// </summary>
    public sealed class DebugKeybinds : MonoBehaviour, IDebugTool
    {
        [Header("Keybinds")]
        [SerializeField] private KeyCode toggleMenuKey = KeyCode.F1;
        [SerializeField] private KeyCode toggleSlowMotionKey = KeyCode.F2;
        [SerializeField] private KeyCode toggleAIDebugKey = KeyCode.F3;
        [SerializeField] private KeyCode spawnZombieKey = KeyCode.F4;
        [SerializeField] private KeyCode spawnSurvivorKey = KeyCode.F5;
        [SerializeField] private KeyCode toggleGodModeKey = KeyCode.F6;
        [SerializeField] private KeyCode spawnHordeKey = KeyCode.F7;
        [SerializeField] private KeyCode advanceWorldSimulationKey = KeyCode.F8;

        [Header("Tool References")]
        [SerializeField] private SpawnDebugTools spawnDebugTools;
        [SerializeField] private WorldDebugTools worldDebugTools;
        [SerializeField] private AISimulationTools aiSimulationTools;

        [Header("Input Arbitration")]
        [SerializeField] private bool suppressMenuHotkeyConflicts = true;
        [SerializeField] private ZomberaSquadManagementUI squadManagementUI;

        public string ToolName => nameof(DebugKeybinds);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        private void Update()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            if (ShouldSuppressDebugFunctionHotkeysThisFrame())
            {
                return;
            }

            if (WasKeyPressedThisFrame(toggleMenuKey))
            {
                DebugManager.Instance?.ToggleDebugMenu();
            }

            if (WasKeyPressedThisFrame(toggleSlowMotionKey))
            {
                DebugManager.Instance?.ToggleSlowMotion();
            }

            if (WasKeyPressedThisFrame(toggleAIDebugKey))
            {
                DebugManager.Instance?.ToggleAIDebugVisuals();
            }

            if (WasKeyPressedThisFrame(spawnZombieKey))
            {
                spawnDebugTools?.SpawnZombie();
            }

            if (WasKeyPressedThisFrame(spawnSurvivorKey))
            {
                spawnDebugTools?.SpawnSurvivor();
            }

            if (WasKeyPressedThisFrame(toggleGodModeKey))
            {
                DebugManager.Instance?.ToggleGodMode();
            }

            if (WasKeyPressedThisFrame(spawnHordeKey))
            {
                spawnDebugTools?.SpawnZombieHorde();
            }

            if (WasKeyPressedThisFrame(advanceWorldSimulationKey))
            {
                worldDebugTools?.AdvanceWorldSimulation();
            }
        }

        private bool ShouldSuppressDebugFunctionHotkeysThisFrame()
        {
            if (!suppressMenuHotkeyConflicts)
            {
                return false;
            }

            if (squadManagementUI == null)
            {
                squadManagementUI = FindFirstObjectByType<ZomberaSquadManagementUI>();
            }

            if (squadManagementUI == null)
            {
                return false;
            }

            return WasKeyPressedThisFrame(toggleMenuKey)
                || WasKeyPressedThisFrame(toggleSlowMotionKey)
                || WasKeyPressedThisFrame(toggleAIDebugKey)
                || WasKeyPressedThisFrame(spawnZombieKey)
                || WasKeyPressedThisFrame(spawnSurvivorKey);
        }

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && TryMapKeyCodeToInputSystemKey(keyCode, out Key mappedKey))
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
            switch (keyCode)
            {
                case KeyCode.Alpha0: key = Key.Digit0; return true;
                case KeyCode.Alpha1: key = Key.Digit1; return true;
                case KeyCode.Alpha2: key = Key.Digit2; return true;
                case KeyCode.Alpha3: key = Key.Digit3; return true;
                case KeyCode.Alpha4: key = Key.Digit4; return true;
                case KeyCode.Alpha5: key = Key.Digit5; return true;
                case KeyCode.Alpha6: key = Key.Digit6; return true;
                case KeyCode.Alpha7: key = Key.Digit7; return true;
                case KeyCode.Alpha8: key = Key.Digit8; return true;
                case KeyCode.Alpha9: key = Key.Digit9; return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    key = Key.Enter;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;
                case KeyCode.LeftCommand:
                    key = Key.LeftMeta;
                    return true;
                case KeyCode.RightCommand:
                    key = Key.RightMeta;
                    return true;
                case KeyCode.BackQuote:
                    key = Key.Backquote;
                    return true;
                default:
                    if (Enum.TryParse(keyCode.ToString(), true, out key))
                    {
                        return true;
                    }

                    key = Key.None;
                    return false;
            }
        }
#endif

        // Runtime rebind and modifier support are intentionally deferred to a future
        // keybind settings screen.  The groundwork is laid here:
        // - rebindOverrides maps Action → Key for player-configured overrides.
        // - modifierKey holds an optional secondary key (Shift/Ctrl) that must be held.
        private readonly System.Collections.Generic.Dictionary<DebugAction, Key> rebindOverrides
            = new System.Collections.Generic.Dictionary<DebugAction, Key>();

#pragma warning disable CS0414
        [SerializeField] private Key modifierKey = Key.LeftShift;
#pragma warning restore CS0414
        [SerializeField] private bool requireModifier;

        public void SetRebind(DebugAction action, Key key)
        {
            rebindOverrides[action] = key;
        }

        public void ClearRebind(DebugAction action)
        {
            rebindOverrides.Remove(action);
        }
    }

    /// <summary>Enumeration of rebindable debug actions.</summary>
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
}