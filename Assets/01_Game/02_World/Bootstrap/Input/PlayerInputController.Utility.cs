#region

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems.Digging;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {

        private bool TryReadPrimaryMouseState(out bool pressed, out bool released, out bool held)
        {
            pressed = false;
            released = false;
            held = false;

            if (TryReadActionButtonState(InputActionNameLeftClick, out pressed, out released, out held))
                return true;

            if (Mouse.current == null) return false;

            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            released = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            return true;
        }


        private static Rect GetNormalizedScreenRect(Vector2 start, Vector2 end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxX = Mathf.Max(start.x, end.x);
            var maxY = Mathf.Max(start.y, end.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }


        private static Rect ConvertScreenRectToGuiRect(Rect screenRect)
        {
            var guiY = Screen.height - screenRect.yMax;
            return new Rect(screenRect.xMin, guiY, screenRect.width, screenRect.height);
        }


        private static void DrawGuiRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }


        private static bool IsPointerOverUi()
        {
            if (_lastUiCheckFrame == Time.frameCount) return _cachedIsPointerOverUi;

            _lastUiCheckFrame = Time.frameCount;
            _cachedIsPointerOverUi = false;

            var uiEventSystem = ResolveUiEventSystem();

            if (uiEventSystem == null) return false;

            if (Mouse.current != null)
            {
                if (TryIsPointerOverGameObject(uiEventSystem))
                {
                    _cachedIsPointerOverUi = true;
                    return true;
                }

                if (CursorService.TryGetGameplayPointerScreenPosition(out var gameplayPointerPosition)
                    && IsScreenPositionOverUi(uiEventSystem, gameplayPointerPosition))
                {
                    _cachedIsPointerOverUi = true;
                    return true;
                }
            }

            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (var i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (!touch.press.isPressed) continue;
                    if (IsScreenPositionOverUi(uiEventSystem, touch.position.ReadValue()))
                    {
                        _cachedIsPointerOverUi = true;
                        return true;
                    }
                }
            }

            return false;
        }


        private static EventSystem ResolveUiEventSystem()
        {
            EventSystem current;

            try
            {
                current = EventSystem.current;
            }
            catch (Exception)
            {
                InvalidateUiEventSystemCache();
                return null;
            }

            if (IsUsableUiEventSystem(current))
            {
                _cachedResolvedUiEventSystem = current;
                _nextUiEventSystemResolveAt = Time.unscaledTime + UiEventSystemHealthyRecheckSeconds;
                return current;
            }

            if (IsUsableUiEventSystem(_cachedResolvedUiEventSystem))
            {
                if (Time.unscaledTime < _nextUiEventSystemResolveAt)
                    return _cachedResolvedUiEventSystem;
            }
            else
            {
                _cachedResolvedUiEventSystem = null;
            }

            if (Time.unscaledTime < _nextUiEventSystemResolveAt)
                return _cachedResolvedUiEventSystem;

            _nextUiEventSystemResolveAt = Time.unscaledTime + UiEventSystemRecoveryRetrySeconds;

            try
            {
                RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
                current = EventSystem.current;
            }
            catch (Exception)
            {
                InvalidateUiEventSystemCache();
                return null;
            }

            if (IsUsableUiEventSystem(current))
            {
                _cachedResolvedUiEventSystem = current;
                _nextUiEventSystemResolveAt = Time.unscaledTime + UiEventSystemHealthyRecheckSeconds;
                return current;
            }

            InvalidateUiEventSystemCache();
            return null;
        }


        private static bool IsUsableUiEventSystem(EventSystem uiEventSystem)
        {
            if (uiEventSystem == null) return false;

            try
            {
                if (!uiEventSystem.isActiveAndEnabled) return false;

                var inputModule = uiEventSystem.currentInputModule;
                return inputModule != null && inputModule.isActiveAndEnabled;
            }
            catch (Exception exception)
                when (exception is MissingReferenceException or NullReferenceException or InvalidOperationException)
            {
                return false;
            }
        }


        private static bool IsScreenPositionOverUi(EventSystem uiEventSystem, Vector2 screenPosition)
        {
            if (uiEventSystem == null) return false;

            try
            {
                if (_cachedUiPointerEventData == null || _cachedUiPointerEventDataOwner != uiEventSystem)
                {
                    _cachedUiPointerEventDataOwner = uiEventSystem;
                    _cachedUiPointerEventData = new PointerEventData(uiEventSystem);
                }

                _cachedUiPointerEventData.Reset();
                _cachedUiPointerEventData.position = screenPosition;

                UiRaycastResults.Clear();
                uiEventSystem.RaycastAll(_cachedUiPointerEventData, UiRaycastResults);
                return UiRaycastResults.Count > 0;
            }
            catch (Exception exception)
                when (exception is MissingReferenceException or NullReferenceException or InvalidOperationException)
            {
                InvalidateUiEventSystemCache();
                UiRaycastResults.Clear();
                return false;
            }
        }


        private static bool TryIsPointerOverGameObject(EventSystem uiEventSystem)
        {
            if (uiEventSystem == null) return false;

            try
            {
                return uiEventSystem.IsPointerOverGameObject();
            }
            catch (Exception exception)
                when (exception is MissingReferenceException or NullReferenceException or InvalidOperationException)
            {
                InvalidateUiEventSystemCache();
                return false;
            }
        }


        private static void InvalidateUiEventSystemCache()
        {
            _cachedResolvedUiEventSystem = null;
            _cachedUiPointerEventDataOwner = null;
            _cachedUiPointerEventData = null;
            _nextUiEventSystemResolveAt = Time.unscaledTime + UiEventSystemRecoveryRetrySeconds;
        }


        private void EnableOptionalInputActions()
        {
            EnableInputAction(crouchToggleAction, ref _enabledCrouchToggleAction);
            EnableInputAction(crawlToggleAction, ref _enabledCrawlToggleAction);
            EnableInputAction(interactAction, ref _enabledInteractAction);
            EnableInputAction(squadMoveCommandAction, ref _enabledSquadMoveCommandAction);
            EnableInputAction(squadAttackCommandAction, ref _enabledSquadAttackCommandAction);
            EnableInputAction(squadHoldCommandAction, ref _enabledSquadHoldCommandAction);
            EnableInputAction(squadFollowCommandAction, ref _enabledSquadFollowCommandAction);
            EnableInputAction(squadDefendCommandAction, ref _enabledSquadDefendCommandAction);
        }


        private void DisableOptionalInputActions()
        {
            DisableInputAction(crouchToggleAction, _enabledCrouchToggleAction);
            DisableInputAction(crawlToggleAction, _enabledCrawlToggleAction);
            DisableInputAction(interactAction, _enabledInteractAction);
            DisableInputAction(squadMoveCommandAction, _enabledSquadMoveCommandAction);
            DisableInputAction(squadAttackCommandAction, _enabledSquadAttackCommandAction);
            DisableInputAction(squadHoldCommandAction, _enabledSquadHoldCommandAction);
            DisableInputAction(squadFollowCommandAction, _enabledSquadFollowCommandAction);
            DisableInputAction(squadDefendCommandAction, _enabledSquadDefendCommandAction);

            _enabledCrouchToggleAction = false;
            _enabledCrawlToggleAction = false;
            _enabledInteractAction = false;
            _enabledSquadMoveCommandAction = false;
            _enabledSquadAttackCommandAction = false;
            _enabledSquadHoldCommandAction = false;
            _enabledSquadFollowCommandAction = false;
            _enabledSquadDefendCommandAction = false;
        }


        private static void EnableInputAction(InputActionReference actionReference, ref bool enabledByThisComponent)
        {
            enabledByThisComponent = false;

            if (actionReference?.action == null || actionReference.action.enabled) return;

            actionReference.action.Enable();
            enabledByThisComponent = true;
        }


        private static void DisableInputAction(InputActionReference actionReference, bool enabledByThisComponent)
        {
            if (!enabledByThisComponent || actionReference?.action == null || !actionReference.action.enabled) return;

            actionReference.action.Disable();
        }


        private static bool WasActionPressedThisFrame(InputActionReference actionReference, Key fallbackKey = Key.None)
        {
            if (actionReference?.action != null) return actionReference.action.WasPressedThisFrame();

            if (fallbackKey == Key.None || Keyboard.current == null) return false;

            return Keyboard.current[fallbackKey].wasPressedThisFrame;
        }
    }
}
