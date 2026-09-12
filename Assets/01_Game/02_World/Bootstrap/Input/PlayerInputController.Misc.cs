#region

using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {
        private void OnGUI()
        {
            if (!enableDragBoxSquadSelection || !_isDragSelecting) return;

            var screenRect = GetNormalizedScreenRect(_dragSelectStartScreen, _dragSelectCurrentScreen);
            if (screenRect.width < 1f || screenRect.height < 1f) return;

            var guiRect = ConvertScreenRectToGuiRect(screenRect);
            DrawGuiRect(guiRect, dragSelectionFillColor);

            var borderThickness = Mathf.Max(1f, dragSelectionBorderThickness);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, borderThickness), dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMax - borderThickness, guiRect.width, borderThickness),
                dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMin, borderThickness, guiRect.height),
                dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMax - borderThickness, guiRect.yMin, borderThickness, guiRect.height),
                dragSelectionBorderColor);
        }

        public void SetExternalMouseInputOverride(bool enabled)
        {
            if (externalMouseInputOverride == enabled) return;

            externalMouseInputOverride = enabled;
            if (!externalMouseInputOverride) return;

            _leftClickMovementPhase = LeftClickMovementPhase.Idle;
            _isDragSelectCandidateActive = false;
            _isDragSelecting = false;
            _suppressLeftClickMovementUntilRelease = false;
        }

        private void UpdateAttackHoverCursor(bool pointerOverUi)
        {
            if (cursorManager == null) return;

            var showAttackCursor = !pointerOverUi && TryGetUnitHealthUnderCursor(out _);
            cursorManager.SetAttackHover(showAttackCursor);
        }

        private void HandleWeightTrainingInput(bool pointerOverUi)
        {
            if (!enableWeightTrainingHotkey || pointerOverUi || playerUnit == null || playerUnit.Stats == null) return;

            var weightTrainingPressed = WasActionPressedThisFrame(InputActionNameWeightTraining)
                                        || (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame);
            if (!weightTrainingPressed) return;

            if (Time.time < _nextWeightTrainingRepAt) return;

            if (requireHeavyCarryForWeightTraining)
            {
                var inventory = playerUnit.Inventory;
                if (inventory == null || !playerUnit.Stats.IsHeavyCarry(inventory.CarryRatio)) return;
            }

            playerUnit.Stats.RecordWeightTrainingRep();
            _nextWeightTrainingRepAt = Time.time + Mathf.Max(0f, weightTrainingRepCooldownSeconds);
        }

        private bool IsPlayerInEncounter()
        {
            if (playerUnit == null) return false;

            return ResolveEncounterManager() != null && encounterManager.IsUnitInEncounter(playerUnit);
        }

        private bool IsBowRangedModeActive()
        {
            if (!enableBowRangedCombatState) return false;

            var resolvedWeaponSystem = ResolveWeaponSystem();
            return resolvedWeaponSystem != null && resolvedWeaponSystem.IsBowEquipped;
        }

        private void HandleSquadCommandInput()
        {
            if (squadManager == null) return;

            if (WasActionPressedThisFrame(squadMoveCommandAction, Key.Digit1) && TryGetGroundPoint(out var movePoint))
                squadManager.IssueOrder(SquadCommandType.Move, movePoint);

            if (WasActionPressedThisFrame(squadAttackCommandAction, Key.Digit2) &&
                TryGetGroundPoint(out var attackPoint))
                squadManager.IssueOrder(SquadCommandType.Attack, attackPoint);

            if (WasActionPressedThisFrame(squadHoldCommandAction, Key.Digit3))
                squadManager.IssueOrder(SquadCommandType.HoldPosition);

            if (WasActionPressedThisFrame(squadFollowCommandAction, Key.Digit4))
                squadManager.IssueOrder(SquadCommandType.Follow);

            if (WasActionPressedThisFrame(squadDefendCommandAction, Key.Digit5) &&
                TryGetGroundPoint(out var defendPoint))
                squadManager.IssueOrder(SquadCommandType.Defend, defendPoint);
        }

        private bool TryGetGroundPoint(out Vector3 worldPoint)
        {
            worldPoint = default;
            if (worldCamera == null) return false;

            var mousePos = TryReadPointerScreenPosition(out var pointerPosition) ? pointerPosition : Vector2.zero;

            return MovementDestinationResolver.TryResolveFromPointer(
                new MovementDestinationResolveRequest
                {
                    Camera = worldCamera,
                    PointerScreenPosition = mousePos,
                    GroundMask = groundMask,
                    RayDistance = targetingRayDistance,
                    LogDiagnostics = logMovementGroundingDiagnostics
                },
                out worldPoint);
        }
    }
}
