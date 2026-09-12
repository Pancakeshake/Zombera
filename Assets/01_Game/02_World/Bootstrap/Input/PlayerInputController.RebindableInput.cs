#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Debugging.DebugLogging;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {
        private const string DefaultGameplayActionMapName = "Gameplay";
        private const string PlayerInputBindingOverridesPrefKey = "Zombera.Input.PlayerInputController.BindingOverrides";

        public const string InputActionNamePointerPosition = "PointerPosition";
        public const string InputActionNameLeftClick = "LeftClick";
        public const string InputActionNameRightClick = "RightClick";
        public const string InputActionNameInteract = "Interact";
        public const string InputActionNameReload = "Reload";
        public const string InputActionNameSprintToggle = "SprintToggle";
        public const string InputActionNameCrouchToggle = "CrouchToggle";
        public const string InputActionNameCrawlToggle = "CrawlToggle";
        public const string InputActionNameWeightTraining = "WeightTraining";
        public const string InputActionNameSelectAll = "SelectAll";
        public const string InputActionNameAdditiveSelectionModifier = "AdditiveSelectionModifier";
        public const string InputActionNameSquadMoveCommand = "SquadMoveCommand";
        public const string InputActionNameSquadAttackCommand = "SquadAttackCommand";
        public const string InputActionNameSquadHoldCommand = "SquadHoldCommand";
        public const string InputActionNameSquadFollowCommand = "SquadFollowCommand";
        public const string InputActionNameSquadDefendCommand = "SquadDefendCommand";

        [Header("Input Rebinding")] [SerializeField]
        private InputActionAsset gameplayInputActionAsset;

        [SerializeField] private string gameplayInputActionMapName = DefaultGameplayActionMapName;
        [SerializeField] private bool autoCreateDefaultGameplayInputActions = true;
        [SerializeField] private bool loadSavedInputBindingOverridesOnEnable = true;
        [SerializeField] private bool saveInputBindingOverridesOnDisable = true;
        [SerializeField] private bool logInputBindingDiagnostics;

        private readonly Dictionary<string, InputAction> _cachedGameplayActions = new(StringComparer.Ordinal);
        private InputActionMap _runtimeGameplayActionMap;
        private InputActionRebindingExtensions.RebindingOperation _activeRebindOperation;
        private bool _loadedSavedInputBindingOverrides;

        public event Action<string> InputBindingRebound;
        public event Action<string> InputBindingRebindCanceled;
        public event Action InputBindingsReset;

        public InputActionAsset GameplayInputActionAsset => gameplayInputActionAsset;

        public void SetGameplayInputActionAsset(InputActionAsset inputActionAsset)
        {
            if (ReferenceEquals(gameplayInputActionAsset, inputActionAsset)) return;

            CancelActiveInputRebind();

            if (_runtimeGameplayActionMap != null && _runtimeGameplayActionMap.enabled)
                _runtimeGameplayActionMap.Disable();

            gameplayInputActionAsset = inputActionAsset;
            _runtimeGameplayActionMap = null;
            _cachedGameplayActions.Clear();
            _loadedSavedInputBindingOverrides = false;

            if (isActiveAndEnabled) EnableRebindableInputActions();
        }

        public bool StartInteractiveRebind(string actionName, int bindingIndex = 0)
        {
            if (!TryGetGameplayAction(actionName, out var action)) return false;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return false;

            CancelActiveInputRebind();

            if (_runtimeGameplayActionMap != null && _runtimeGameplayActionMap.enabled)
                _runtimeGameplayActionMap.Disable();

            _activeRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnCancel(operation =>
                {
                    operation.Dispose();
                    _activeRebindOperation = null;
                    if (_runtimeGameplayActionMap != null) _runtimeGameplayActionMap.Enable();
                    InputBindingRebindCanceled?.Invoke(actionName);
                    LogInputTrace("Rebind canceled for action '" + actionName + "'.");
                })
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    _activeRebindOperation = null;
                    if (_runtimeGameplayActionMap != null) _runtimeGameplayActionMap.Enable();
                    SaveInputBindingOverrides();
                    InputBindingRebound?.Invoke(actionName);
                    LogInputTrace("Rebind completed for action '" + actionName + "'.");
                });

            _activeRebindOperation.WithControlsExcluding("<Pointer>/position");
            _activeRebindOperation.Start();
            return true;
        }

        public void CancelActiveInputRebind()
        {
            if (_activeRebindOperation == null) return;

            _activeRebindOperation.Cancel();
            _activeRebindOperation.Dispose();
            _activeRebindOperation = null;

            if (_runtimeGameplayActionMap != null && !_runtimeGameplayActionMap.enabled)
                _runtimeGameplayActionMap.Enable();
        }

        public string GetBindingDisplayString(string actionName, int bindingIndex = 0)
        {
            if (!TryGetGameplayAction(actionName, out var action)) return string.Empty;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return string.Empty;

            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        public bool SaveInputBindingOverrides()
        {
            if (!TryEnsureGameplayInputActionMap()) return false;

            var overridesJson = SaveBindingOverridesAsJson();
            if (overridesJson == null) return false;

            PlayerPrefs.SetString(BuildBindingOverridesPrefKey(), overridesJson);
            PlayerPrefs.Save();
            return true;
        }

        public bool LoadInputBindingOverrides()
        {
            if (!TryEnsureGameplayInputActionMap()) return false;

            var prefKey = BuildBindingOverridesPrefKey();
            if (!PlayerPrefs.HasKey(prefKey))
            {
                _loadedSavedInputBindingOverrides = true;
                return false;
            }

            var overridesJson = PlayerPrefs.GetString(prefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(overridesJson))
            {
                _loadedSavedInputBindingOverrides = true;
                return false;
            }

            LoadBindingOverridesFromJson(overridesJson);
            _loadedSavedInputBindingOverrides = true;
            LogInputTrace("Loaded input binding overrides from PlayerPrefs.");
            return true;
        }

        public void ResetInputBindingOverrides()
        {
            if (TryEnsureGameplayInputActionMap())
                RemoveAllBindingOverrides();

            PlayerPrefs.DeleteKey(BuildBindingOverridesPrefKey());
            InputBindingsReset?.Invoke();
            LogInputTrace("Reset input binding overrides.");
        }

        private void EnableRebindableInputActions()
        {
            if (!TryEnsureGameplayInputActionMap()) return;

            AutoWireOptionalActionReferencesFromGameplayMap();

            if (loadSavedInputBindingOverridesOnEnable && !_loadedSavedInputBindingOverrides)
                LoadInputBindingOverrides();

            if (!_runtimeGameplayActionMap.enabled)
                _runtimeGameplayActionMap.Enable();
        }

        private void DisableRebindableInputActions()
        {
            if (saveInputBindingOverridesOnDisable)
                SaveInputBindingOverrides();

            CancelActiveInputRebind();

            if (_runtimeGameplayActionMap != null && _runtimeGameplayActionMap.enabled)
                _runtimeGameplayActionMap.Disable();
        }

        private bool TryEnsureGameplayInputActionMap()
        {
            if (_runtimeGameplayActionMap != null)
                return true;

            var mapName = ResolveGameplayInputActionMapName();

            if (gameplayInputActionAsset != null)
                _runtimeGameplayActionMap = gameplayInputActionAsset.FindActionMap(mapName, false);

            if (_runtimeGameplayActionMap == null && autoCreateDefaultGameplayInputActions)
                _runtimeGameplayActionMap = CreateDefaultGameplayActionMap(gameplayInputActionAsset, mapName);

            if (_runtimeGameplayActionMap == null) return false;

            RebuildGameplayActionCache();
            return true;
        }

        private void RebuildGameplayActionCache()
        {
            _cachedGameplayActions.Clear();

            if (_runtimeGameplayActionMap == null) return;

            var actions = _runtimeGameplayActionMap.actions;
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.name)) continue;
                _cachedGameplayActions[action.name] = action;
            }
        }

        private bool TryGetGameplayAction(string actionName, out InputAction action)
        {
            action = null;
            if (string.IsNullOrWhiteSpace(actionName)) return false;
            if (!TryEnsureGameplayInputActionMap()) return false;

            if (_cachedGameplayActions.TryGetValue(actionName, out action) && action != null)
                return true;

            action = _runtimeGameplayActionMap.FindAction(actionName, false);
            if (action == null) return false;

            _cachedGameplayActions[actionName] = action;
            return true;
        }

        private bool WasActionPressedThisFrame(string actionName)
        {
            return TryGetGameplayAction(actionName, out var action) && action.WasPressedThisFrame();
        }

        private bool IsActionPressed(string actionName)
        {
            return TryGetGameplayAction(actionName, out var action) && action.IsPressed();
        }

        private bool TryReadActionButtonState(string actionName, out bool pressed, out bool released, out bool held)
        {
            pressed = false;
            released = false;
            held = false;

            if (!TryGetGameplayAction(actionName, out var action)) return false;

            pressed = action.WasPressedThisFrame();
            released = action.WasReleasedThisFrame();
            held = action.IsPressed();
            return true;
        }

        private bool TryReadSecondaryMouseButtonState(out bool pressed, out bool released, out bool held)
        {
            if (TryReadActionButtonState(InputActionNameRightClick, out pressed, out released, out held))
                return true;

            pressed = false;
            released = false;
            held = false;

            if (Mouse.current == null) return false;

            pressed = Mouse.current.rightButton.wasPressedThisFrame;
            released = Mouse.current.rightButton.wasReleasedThisFrame;
            held = Mouse.current.rightButton.isPressed;
            return true;
        }

        private bool TryReadPointerScreenPosition(out Vector2 screenPosition)
        {
            if (TryGetGameplayAction(InputActionNamePointerPosition, out var pointerAction))
                return CursorService.TryGetGameplayPointerScreenPosition(pointerAction, out screenPosition);

            return CursorService.TryGetGameplayPointerScreenPosition(out screenPosition);
        }

        private string ResolveGameplayInputActionMapName()
        {
            return string.IsNullOrWhiteSpace(gameplayInputActionMapName)
                ? DefaultGameplayActionMapName
                : gameplayInputActionMapName.Trim();
        }

        private void AutoWireOptionalActionReferencesFromGameplayMap()
        {
            if (_runtimeGameplayActionMap == null) return;

            AutoWireActionReference(ref crouchToggleAction, InputActionNameCrouchToggle);
            AutoWireActionReference(ref crawlToggleAction, InputActionNameCrawlToggle);
            AutoWireActionReference(ref interactAction, InputActionNameInteract);
            AutoWireActionReference(ref squadMoveCommandAction, InputActionNameSquadMoveCommand);
            AutoWireActionReference(ref squadAttackCommandAction, InputActionNameSquadAttackCommand);
            AutoWireActionReference(ref squadHoldCommandAction, InputActionNameSquadHoldCommand);
            AutoWireActionReference(ref squadFollowCommandAction, InputActionNameSquadFollowCommand);
            AutoWireActionReference(ref squadDefendCommandAction, InputActionNameSquadDefendCommand);
        }

        private void AutoWireActionReference(ref InputActionReference actionReference, string actionName)
        {
            if (!TryGetGameplayAction(actionName, out var action)) return;

            // Cannot create an InputActionReference for an action that is not part of an asset.
            if (action.actionMap?.asset == null) return;

            if (actionReference != null && actionReference.action == action) return;

            actionReference = InputActionReference.Create(action);
        }

        private string BuildBindingOverridesPrefKey()
        {
            var mapName = ResolveGameplayInputActionMapName();
            return PlayerInputBindingOverridesPrefKey + "." + mapName;
        }

        private void LogInputTrace(string message)
        {
            if (!logInputBindingDiagnostics) return;
            DebugLogger.LogTrace(LogCategory.Input, "[PlayerInputController] " + message, this);
        }
    }
}
