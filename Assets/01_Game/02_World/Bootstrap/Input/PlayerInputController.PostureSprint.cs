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

        private void HandleSprintInput()
        {
            if (unitController == null) return;

            if (ShouldForceSprintOff())
            {
                DisableSprint();
                return;
            }

            ToggleSprintOnShiftPress();

            if (IsSprintBlockedByPosture())
            {
                unitController.SetSprintActive(false);
                return;
            }

            ApplySprintToSelf();
            SyncSprintToSquad();
        }

        private void ApplySprintToSelf()
        {
            var hasStamina = playerUnit?.Stats == null || playerUnit.Stats.Stamina > 0f;
            if (_sprintToggleActive && !hasStamina) _sprintToggleActive = false;

            var hasMoveIntent = unitController.IsMoving || unitController.HasMoveTarget;
            var wantsToSprint = _sprintToggleActive && hasMoveIntent && hasStamina;
            unitController.SetSprintActive(wantsToSprint);
        }

        private void SyncSprintToSquad()
        {
            if (squadManager == null || !squadManager.HasSelection) return;

            var selected = squadManager.SelectedMembers;
            for (var i = 0; i < selected.Count; i++)
            {
                var member = selected[i];
                if (member == null || member.UnitController == null || member.UnitController == unitController)
                    continue;

                var mStamina = member.UnitStats == null || member.UnitStats.Stamina > 0f;
                var mIntent = member.UnitController.IsMoving || member.UnitController.HasMoveTarget;
                member.UnitController.SetSprintActive(_sprintToggleActive && mIntent && mStamina);
            }
        }


        private bool ShouldForceSprintOff()
        {
            if (!enableSprinting) return true;

            var inCombatEncounter = IsPlayerInEncounter() || _pendingEncounterTarget != null;
            return inCombatEncounter;
        }


        private void DisableSprint()
        {
            _sprintToggleActive = false;
            unitController.SetSprintActive(false);

            if (squadManager == null || !squadManager.HasSelection) return;

            var selected = squadManager.SelectedMembers;
            for (var i = 0; i < selected.Count; i++)
            {
                if (selected[i]?.UnitController != null)
                    selected[i].UnitController.SetSprintActive(false);
            }
        }


        private void ToggleSprintOnShiftPress()
        {
            if (_suppressSprintToggleThisFrame)
            {
                _suppressSprintToggleThisFrame = false;
                return;
            }

            var sprintTogglePressed = WasActionPressedThisFrame(InputActionNameSprintToggle)
                                      || (Keyboard.current != null &&
                                          (Keyboard.current.leftShiftKey.wasPressedThisFrame
                                           || Keyboard.current.rightShiftKey.wasPressedThisFrame));

            if (!sprintTogglePressed) return;

            _sprintToggleActive = !_sprintToggleActive;
        }


        private bool IsSprintBlockedByPosture()
        {
            var inPosture = playerUnit?.Stats != null && playerUnit.Stats.CurrentPosture != PostureState.Upright;
            return inPosture || _standUpTimer > 0f;
        }


        private void HandlePostureInput()
        {
            if (playerUnit == null) return;

            if (WasActionPressedThisFrame(crouchToggleAction, crouchKey))
            {
                var nowCrouching = playerUnit.Stats?.CurrentPosture != PostureState.Crouching;
                ApplyPosture(nowCrouching ? PostureState.Crouching : PostureState.Upright);
            }
            else if (WasActionPressedThisFrame(crawlToggleAction, crawlKey))
            {
                var nowCrawling = playerUnit.Stats?.CurrentPosture != PostureState.Crawling;
                ApplyPosture(nowCrawling ? PostureState.Crawling : PostureState.Upright);
            }
        }


        private void ApplyPosture(PostureState state)
        {
            if (playerUnit?.Stats == null || unitController == null) return;

            var oldPlayerPosture = playerUnit.Stats.CurrentPosture;
            if (oldPlayerPosture == state) return;

            ApplyPostureToUnit(playerUnit, unitController, state, oldPlayerPosture, true);

            if (squadManager == null || !squadManager.HasSelection) return;

            var selected = squadManager.SelectedMembers;
            for (var i = 0; i < selected.Count; i++)
            {
                var member = selected[i];
                if (member == null || member.Unit == null || member.Unit == playerUnit) continue;

                var oldPosture = member.UnitStats != null ? member.UnitStats.CurrentPosture : PostureState.Upright;
                ApplyPostureToUnit(member.Unit, member.UnitController, state, oldPosture, false);
            }
        }

        private void ApplyPostureToUnit(Unit unit, UnitController controller, PostureState state,
            PostureState oldState, bool isLocalPlayer)
        {
            if (unit == null || controller == null || unit.Stats == null) return;

            if (state != PostureState.Upright)
                ApplyLoweredPosture(unit, controller, state, isLocalPlayer);
            else
                ApplyUprightPosture(unit, controller, oldState, isLocalPlayer);

            var anim = unit.GetComponentInChildren<PlayerAnimationController>();
            if (anim != null) anim.ApplyPostureState(state);
        }

        private void ApplyLoweredPosture(Unit unit, UnitController controller, PostureState state, bool isLocalPlayer)
        {
            controller.SetSprintActive(false);
            var speedMult = unit.Stats.SetPostureState(state);
            controller.SetPostureSpeedMultiplier(speedMult);

            if (!isLocalPlayer) return;

            _standUpTimer = 0f;
            _pendingPostStandMoveRamp = false;
            var lockSecs = state == PostureState.Crawling
                ? crawlTransitionLockSeconds
                : crouchTransitionLockSeconds;
            BeginPostureTransitionMovementLock(lockSecs);
        }

        private void ApplyUprightPosture(Unit unit, UnitController controller, PostureState oldState, bool isLocalPlayer)
        {
            unit.Stats.SetPostureState(PostureState.Upright);

            if (isLocalPlayer)
            {
                var lockSecs = oldState == PostureState.Crawling
                    ? standUpFromCrawlLockSeconds
                    : standUpFromCrouchLockSeconds;
                _standUpTimer = Mathf.Max(standUpDurationSeconds, lockSecs);
                _pendingPostStandMoveRamp = true;
            }
            else
            {
                controller.SetPostureSpeedMultiplier(1f);
            }
        }


        private void TickPostureTransition()
        {
            if (_postureTransitionTimer > 0f)
                _postureTransitionTimer = Mathf.Max(0f, _postureTransitionTimer - Time.deltaTime);

            if (_standUpTimer <= 0f) return;

            _standUpTimer -= Time.deltaTime;
            if (_standUpTimer > 0f) return;

            _standUpTimer = 0f;
            unitController?.SetPostureSpeedMultiplier(1f);
        }


        private void BeginPostureTransitionMovementLock(float durationSeconds)
        {
            if (!lockMovementDuringPostureTransitions)
            {
                _postureTransitionTimer = 0f;
                return;
            }

            var clampedDuration = Mathf.Max(0f, durationSeconds);
            _postureTransitionTimer = Mathf.Max(_postureTransitionTimer, clampedDuration);
            if (_postureTransitionTimer > 0f) unitController?.Stop();
        }


        internal bool IsPostureTransitionMovementLocked()
        {
            return lockMovementDuringPostureTransitions
                   && (_postureTransitionTimer > 0f || _standUpTimer > 0f);
        }


        internal void TryBeginPostStandMoveRamp()
        {
            if (!_pendingPostStandMoveRamp || unitController == null) return;

            if (IsPostureTransitionMovementLocked()) return;

            unitController.BeginMoveSpeedRamp(postStandJogBuildUpSeconds, postStandJogStartSpeedMultiplier);
            _pendingPostStandMoveRamp = false;
        }
    }
}
