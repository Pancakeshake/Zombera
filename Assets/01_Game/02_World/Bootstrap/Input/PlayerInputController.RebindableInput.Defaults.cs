#region

using System;
using UnityEngine;
using UnityEngine.InputSystem;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {
        // ── Default Action Map Construction ───────────────────────────

        private static bool HasBinding(InputAction action, string bindingPath)
        {
            if (action == null || string.IsNullOrWhiteSpace(bindingPath)) return false;

            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (string.Equals(bindings[i].path, bindingPath, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static InputAction AddButtonAction(InputActionMap map, string actionName, params string[] bindings)
        {
            var action = map.FindAction(actionName, false) ?? map.AddAction(actionName, InputActionType.Button);

            if (bindings == null) return action;

            for (var i = 0; i < bindings.Length; i++)
            {
                var bindingPath = bindings[i];
                if (string.IsNullOrWhiteSpace(bindingPath)) continue;
                if (HasBinding(action, bindingPath)) continue;
                action.AddBinding(bindingPath);
            }

            return action;
        }

        private static InputAction AddValueAction(InputActionMap map, string actionName, string expectedControlType,
            params string[] bindings)
        {
            var action = map.FindAction(actionName, false) ?? map.AddAction(actionName, InputActionType.Value,
                expectedControlType);

            if (bindings == null) return action;

            for (var i = 0; i < bindings.Length; i++)
            {
                var bindingPath = bindings[i];
                if (string.IsNullOrWhiteSpace(bindingPath)) continue;
                if (HasBinding(action, bindingPath)) continue;
                action.AddBinding(bindingPath);
            }

            return action;
        }

        private InputActionMap CreateDefaultGameplayActionMap(InputActionAsset asset, string mapName)
        {
            InputActionMap map = null;

            if (asset != null)
            {
                map = asset.FindActionMap(mapName, false);
                if (map == null)
                {
                    map = new InputActionMap(mapName);
                    asset.AddActionMap(map);
                }
            }
            else
            {
                map = new InputActionMap(mapName);
            }

            AddDefaultGameplayActions(map);
            return map;
        }

        private void AddDefaultGameplayActions(InputActionMap map)
        {
            if (map == null) return;

            AddValueAction(map, InputActionNamePointerPosition, "Vector2", "<Mouse>/position");
            AddButtonAction(map, InputActionNameLeftClick, "<Mouse>/leftButton");
            AddButtonAction(map, InputActionNameRightClick, "<Mouse>/rightButton");
            AddButtonAction(map, InputActionNameInteract, ResolveKeyboardPathFromKey(interactKey, "<Keyboard>/e"));
            AddButtonAction(map, InputActionNameReload, "<Keyboard>/r");
            AddButtonAction(map, InputActionNameSprintToggle, "<Keyboard>/leftShift", "<Keyboard>/rightShift");
            AddButtonAction(map, InputActionNameCrouchToggle, ResolveKeyboardPathFromKey(crouchKey, "<Keyboard>/c"));
            AddButtonAction(map, InputActionNameCrawlToggle, ResolveKeyboardPathFromKey(crawlKey, "<Keyboard>/z"));
            AddButtonAction(map, InputActionNameWeightTraining, "<Keyboard>/t");
            AddButtonAction(map, InputActionNameAdditiveSelectionModifier,
                "<Keyboard>/leftShift",
                "<Keyboard>/rightShift",
                "<Keyboard>/leftCtrl",
                "<Keyboard>/rightCtrl");

            AddButtonAction(map, InputActionNameSquadMoveCommand, "<Keyboard>/1");
            AddButtonAction(map, InputActionNameSquadAttackCommand, "<Keyboard>/2");
            AddButtonAction(map, InputActionNameSquadHoldCommand, "<Keyboard>/3");
            AddButtonAction(map, InputActionNameSquadFollowCommand, "<Keyboard>/4");
            AddButtonAction(map, InputActionNameSquadDefendCommand, "<Keyboard>/5");

            var selectAllAction = map.FindAction(InputActionNameSelectAll, false) ??
                                  map.AddAction(InputActionNameSelectAll, InputActionType.Button);

            if (selectAllAction.bindings.Count == 0)
            {
                selectAllAction.AddCompositeBinding("ButtonWithOneModifier")
                    .With("modifier", "<Keyboard>/leftCtrl")
                    .With("button", "<Keyboard>/a");
                selectAllAction.AddCompositeBinding("ButtonWithOneModifier")
                    .With("modifier", "<Keyboard>/rightCtrl")
                    .With("button", "<Keyboard>/a");
            }
        }

        private static string ResolveKeyboardPathFromKey(Key key, string fallbackPath)
        {
            if (key == Key.None) return fallbackPath;

            if (key >= Key.A && key <= Key.Z)
            {
                var letter = key - Key.A;
                return "<Keyboard>/" + (char)('a' + letter);
            }

            if (key >= Key.Digit0 && key <= Key.Digit9)
            {
                var number = key - Key.Digit0;
                return "<Keyboard>/" + number;
            }

            switch (key)
            {
                case Key.LeftShift:
                    return "<Keyboard>/leftShift";
                case Key.RightShift:
                    return "<Keyboard>/rightShift";
                case Key.LeftCtrl:
                    return "<Keyboard>/leftCtrl";
                case Key.RightCtrl:
                    return "<Keyboard>/rightCtrl";
                case Key.Space:
                    return "<Keyboard>/space";
                case Key.Tab:
                    return "<Keyboard>/tab";
                case Key.Enter:
                    return "<Keyboard>/enter";
                case Key.Escape:
                    return "<Keyboard>/escape";
                default:
                    return fallbackPath;
            }
        }

        // ── Binding Override Serialization ────────────────────────────

        private string SaveBindingOverridesAsJson()
        {
            if (gameplayInputActionAsset != null) return gameplayInputActionAsset.SaveBindingOverridesAsJson();
            return _runtimeGameplayActionMap != null ? _runtimeGameplayActionMap.SaveBindingOverridesAsJson() : null;
        }

        private void LoadBindingOverridesFromJson(string overridesJson)
        {
            if (string.IsNullOrWhiteSpace(overridesJson)) return;

            if (gameplayInputActionAsset != null)
            {
                gameplayInputActionAsset.LoadBindingOverridesFromJson(overridesJson);
                return;
            }

            _runtimeGameplayActionMap?.LoadBindingOverridesFromJson(overridesJson);
        }

        private void RemoveAllBindingOverrides()
        {
            if (gameplayInputActionAsset != null)
            {
                gameplayInputActionAsset.RemoveAllBindingOverrides();
                return;
            }

            _runtimeGameplayActionMap?.RemoveAllBindingOverrides();
        }
    }
}
