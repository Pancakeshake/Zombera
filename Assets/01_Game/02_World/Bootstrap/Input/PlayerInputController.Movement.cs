#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;

#endregion

namespace Zombera.Systems
{
    public enum LeftClickMovementPhase
    {
        Idle,
        WorldPressActive,
        SuppressNextRelease
    }

    public enum MovementTriggerKind
    {
        None,
        Hold,
        Discrete
    }

    public struct MovementTriggerPolicyInput
    {
        public LeftClickMovementPhase CurrentPhase;
        public bool PointerOverUi;
        public bool Pressed;
        public bool Released;
        public bool Held;
        public bool CursorHasTarget;
        public bool SuppressLeftClickMovementUntilRelease;
        public bool AllowHoldMove;
        public bool CanEvaluateMoveTrigger;
    }

    public readonly struct MovementTriggerPolicyResult
    {
        public readonly LeftClickMovementPhase NextPhase;
        public readonly MovementTriggerKind TriggerKind;

        public MovementTriggerPolicyResult(LeftClickMovementPhase nextPhase, MovementTriggerKind triggerKind)
        {
            NextPhase = nextPhase;
            TriggerKind = triggerKind;
        }
    }

    public static class MovementTriggerPolicy
    {
        public static MovementTriggerPolicyResult Evaluate(in MovementTriggerPolicyInput input)
        {
            var phase = ResolveNextPhase(in input);

            if (!CanTriggerMovement(in input, phase))
                return new MovementTriggerPolicyResult(phase, MovementTriggerKind.None);

            return ResolveMovementTrigger(in input, phase);
        }

        private static MovementTriggerPolicyResult ResolveMovementTrigger(
            in MovementTriggerPolicyInput input,
            LeftClickMovementPhase phase)
        {
            if (!ShouldMoveThisFrame(in input, phase))
                return new MovementTriggerPolicyResult(phase, MovementTriggerKind.None);

            if (!input.Released)
                return new MovementTriggerPolicyResult(phase, MovementTriggerKind.Hold);

            return new MovementTriggerPolicyResult(LeftClickMovementPhase.Idle, MovementTriggerKind.Discrete);
        }

        private static bool ShouldMoveThisFrame(in MovementTriggerPolicyInput input, LeftClickMovementPhase phase)
        {
            if (phase != LeftClickMovementPhase.WorldPressActive)
                return false;

            return input.Released || (input.AllowHoldMove && input.Held);
        }

        private static LeftClickMovementPhase ResolveNextPhase(in MovementTriggerPolicyInput input)
        {
            if (!input.Pressed)
                return input.CurrentPhase;

            var phase = input.PointerOverUi ? LeftClickMovementPhase.Idle : LeftClickMovementPhase.WorldPressActive;

            if (phase == LeftClickMovementPhase.WorldPressActive && input.CursorHasTarget)
                return LeftClickMovementPhase.SuppressNextRelease;

            return phase;
        }

        private static bool CanTriggerMovement(in MovementTriggerPolicyInput input, LeftClickMovementPhase phase)
        {
            if (input.PointerOverUi)
                return false;

            if (input.Released && phase == LeftClickMovementPhase.SuppressNextRelease)
                return false;

            if (!input.CanEvaluateMoveTrigger)
                return false;

            if (input.SuppressLeftClickMovementUntilRelease)
                return false;

            return true;
        }
    }

    public sealed partial class PlayerInputController
    {
        private enum MovementExecutionIntent
        {
            LocalMove,
            SelectionMove
        }

        private readonly struct MovementClickSample
        {
            public readonly bool Pressed;
            public readonly bool Released;
            public readonly bool Held;

            public MovementClickSample(bool pressed, bool released, bool held)
            {
                Pressed = pressed;
                Released = released;
                Held = held;
            }
        }

        private void HandleMovementInput(bool pointerOverUi)
        {
            if (!TryReadPrimaryMouseState(out var pressed, out var released, out var held)) return;

            var clickSample = new MovementClickSample(pressed, released, held);
            var isMovementBlocked = IsMovementInputBlocked();
            var triggerKind = ResolveMovementTrigger(pointerOverUi, in clickSample, !isMovementBlocked);

            if (isMovementBlocked) return;
            if (triggerKind == MovementTriggerKind.None) return;

            var isDiscreteClickMove = triggerKind == MovementTriggerKind.Discrete;
            if (!CanIssueMoveWhileLocked(isDiscreteClickMove)) return;

            var movementIntent = ResolveMovementExecutionIntent(isDiscreteClickMove);
            TryExecuteMoveOrder(movementIntent, isDiscreteClickMove);
        }

        private bool IsLeftClickWorldPressActive => _leftClickMovementPhase == LeftClickMovementPhase.WorldPressActive;

        private void SetLeftClickMovementPhase(LeftClickMovementPhase phase)
        {
            _leftClickMovementPhase = phase;
        }

        private void SuppressNextLeftClickMoveRelease()
        {
            SetLeftClickMovementPhase(LeftClickMovementPhase.SuppressNextRelease);
        }


        private MovementTriggerKind ResolveMovementTrigger(
            bool pointerOverUi,
            in MovementClickSample clickSample,
            bool canEvaluateMoveTrigger)
        {
            var policyInput = BuildMovementTriggerPolicyInput(pointerOverUi, in clickSample, canEvaluateMoveTrigger);
            var policyResult = EvaluateMovementTriggerPolicy(policyInput);
            SetLeftClickMovementPhase(policyResult.NextPhase);
            return policyResult.TriggerKind;
        }

        private MovementTriggerPolicyInput BuildMovementTriggerPolicyInput(
            bool pointerOverUi,
            in MovementClickSample clickSample,
            bool canEvaluateMoveTrigger)
        {
            return new MovementTriggerPolicyInput
            {
                CurrentPhase = _leftClickMovementPhase,
                PointerOverUi = pointerOverUi,
                Pressed = clickSample.Pressed,
                Released = clickSample.Released,
                Held = clickSample.Held,
                CursorHasTarget = !pointerOverUi && clickSample.Pressed && TryGetUnitHealthUnderCursor(out _),
                SuppressLeftClickMovementUntilRelease = _suppressLeftClickMovementUntilRelease,
                AllowHoldMove = allowMoveWhileHoldingLeftMouse && !enableDragBoxSquadSelection,
                CanEvaluateMoveTrigger = canEvaluateMoveTrigger
            };
        }

        private static MovementTriggerPolicyResult EvaluateMovementTriggerPolicy(in MovementTriggerPolicyInput input)
        {
            return MovementTriggerPolicy.Evaluate(input);
        }


        private bool IsMovementInputBlocked()
        {
            return (WasActionPressedThisFrame(crouchToggleAction, crouchKey)
                    || WasActionPressedThisFrame(crawlToggleAction, crawlKey))
                   || IsPostureTransitionMovementLocked();
        }


        private MovementExecutionIntent ResolveMovementExecutionIntent(bool discreteClickMove)
        {
            var shouldIssueSelectionMove = discreteClickMove
                                           && issueSquadMoveOnLeftClick
                                           && squadManager != null
                                           && squadManager.SelectedMembers.Count > 0;

            return shouldIssueSelectionMove ? MovementExecutionIntent.SelectionMove : MovementExecutionIntent.LocalMove;
        }


        private bool CanIssueMoveWhileLocked(bool discreteClickMove)
        {
            if (!IsMovementTemporarilyLocked()) return true;

            var canBreakEncounterWithMove = discreteClickMove && IsPlayerInEncounter();
            if (!discreteClickMove || (!allowMoveCommandToCancelAttack && !canBreakEncounterWithMove)) return false;

            _movementLockExpiresAt = 0f;
            return true;
        }


        private void TryExecuteMoveOrder(MovementExecutionIntent movementIntent, bool discreteClickMove)
        {
            if (!TryGetGroundPoint(out var groundPoint)) return;

            diggingSystem?.CancelDig();
            _pendingEncounterTarget = null;
            _combatFacingAssistTarget = null;

            var issuedSelectionMove = movementIntent == MovementExecutionIntent.SelectionMove
                                      && squadManager.TryIssueOrderToSelection(SquadCommandType.Move, groundPoint);

            if (issuedSelectionMove)
            {
                if (IsActivePlayerMemberSelected()) NotifyPlayerMoveIssued(groundPoint, true);

                return;
            }

            NotifyPlayerMoveIssued(groundPoint, discreteClickMove);
            unitController.MoveTo(groundPoint);
        }

        private void NotifyPlayerMoveIssued(Vector3 groundPoint, bool discreteClickMove)
        {
            if (discreteClickMove) HandleDiscreteClickMoveStart(groundPoint);

            TryBeginPostStandMoveRamp();
            combatFootwork?.NotifyPlayerIssuedMove();
        }

        private bool IsActivePlayerMemberSelected()
        {
            if (squadManager == null || playerUnit == null) return false;

            var activePlayerMember = playerUnit.GetComponent<SquadMember>();
            if (activePlayerMember == null) return false;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
                if (selectedMembers[i] == activePlayerMember)
                    return true;

            return false;
        }


        private void HandleDiscreteClickMoveStart(Vector3 groundPoint)
        {
            BeginMoveOrderFacingSuppression();
            ClearBowRangedCombatState(true);

            _suppressAutoEngageUntilAt = Mathf.Max(
                _suppressAutoEngageUntilAt,
                Time.time + Mathf.Max(0f, autoEngageSuppressAfterPlayerMoveSeconds));

            TryDisengagePlayerEncounterForMovement();
            playerAnimationController?.CancelAttackForMovement(groundPoint);
            playerAnimationController?.CancelCombatFacingForMovement();
        }


        private void TryDisengagePlayerEncounterForMovement()
        {
            if (playerUnit == null) return;

            if (ResolveEncounterManager() == null) return;

            if (!encounterManager.TryDisengageUnit(playerUnit, "player-move-disengage")) return;

            ClearBowRangedCombatState(true);
            _movementLockExpiresAt = 0f;
            _pendingEncounterTarget = null;
            _combatFacingAssistTarget = null;
            _combatFacingAssistExpiresAt = 0f;
            _suppressAutoEngageUntilAt = Mathf.Max(
                _suppressAutoEngageUntilAt,
                Time.time + Mathf.Max(0f, autoEngageSuppressAfterPlayerMoveSeconds));
        }
    }
}
