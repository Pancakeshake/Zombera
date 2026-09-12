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

        private void HandleCombatInputIfReady(bool consumeRightClickCombat, bool pointerOverUi)
        {
            if (unitCombat == null || playerUnit == null) return;

            HandleCombatInput(consumeRightClickCombat, pointerOverUi);
        }


        private void HandleCombatInput(bool consumeRightClickCombat, bool pointerOverUi)
        {
            var rightClickThisFrame = TryReadSecondaryMouseButtonState(out var rightPressed, out _, out _)
                                      && rightPressed
                                      && !consumeRightClickCombat
                                      && !pointerOverUi;
            var leftClickThisFrame = TryReadPrimaryMouseState(out var leftPressed, out _, out _)
                                     && leftPressed
                                     && !pointerOverUi;
            var rKeyThisFrame = WasActionPressedThisFrame(InputActionNameReload)
                                || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);

            if (TryResolveClickedHostileTarget(rightClickThisFrame, leftClickThisFrame, out var preferredTarget)
                && TryHandleHostileTargetEngagement(preferredTarget))
                return;

            if (rKeyThisFrame) HandleReloadInput();
        }


        private bool TryResolveClickedHostileTarget(bool rightClickThisFrame, bool leftClickThisFrame,
            out Unit preferredTarget)
        {
            preferredTarget = null;

            if (!(rightClickThisFrame || leftClickThisFrame)
                || !TryGetUnitHealthUnderCursor(out var markedTarget))
                return false;

            unitCombat.SetMarkedTarget(markedTarget);
            preferredTarget = markedTarget != null ? markedTarget.GetComponentInParent<Unit>() : null;
            if (preferredTarget == null) return false;

            var requestTurnAnimation = playFacingTurnAnimationOnHostileClick
                                       && playerAnimationController != null
                                       && !unitController.IsMoving
                                       && !IsPlayerInEncounter();
            QueueCombatFacingAssist(preferredTarget, requestTurnAnimation);

            if (!leftClickThisFrame) return true;

            // A hostile left-click is a combat intent; do not let movement consume this press.
            _suppressLeftClickMovementUntilRelease = true;
            _pendingEncounterTarget = null;
            unitController?.Stop();

            return true;
        }


        private bool TryHandleHostileTargetEngagement(Unit preferredTarget)
        {
            _suppressCombatFacingUntilMoveOrderCompletes = false;

            if (TryIssueSelectedSquadAttack(preferredTarget))
            {
                BeginPostAttackMovementLock();
                return true;
            }

            if (IsBowRangedModeActive())
            {
                StartBowRangedCombat(preferredTarget);
                return true;
            }

            ClearBowRangedCombatState(true);

            if (TryStartPlayerEncounter(preferredTarget))
            {
                BeginPostAttackMovementLock();
                return true;
            }

            // Keep hit timing animation-synced by avoiding immediate legacy attacks
            // whenever the encounter system is available.
            if (HasEncounterSystemAvailable()) return true;

            ExecuteLegacyFallbackAttack();
            return false;
        }

        private bool TryIssueSelectedSquadAttack(Unit preferredTarget)
        {
            if (preferredTarget == null || squadManager == null) return false;

            var selectedMembers = squadManager.SelectedMembers;
            if (selectedMembers == null || selectedMembers.Count == 0) return false;
            if (IsOnlyActivePlayerSelected(selectedMembers)) return false;

            var targetHealth = preferredTarget.Health;
            if (targetHealth == null || targetHealth.IsDead) return false;

            return squadManager.TryIssueAttackOrderToSelection(targetHealth, preferredTarget.transform.position);
        }

        private bool IsOnlyActivePlayerSelected(IReadOnlyList<SquadMember> selectedMembers)
        {
            if (selectedMembers == null || selectedMembers.Count != 1 || playerUnit == null) return false;

            var activePlayerMember = playerUnit.GetComponent<SquadMember>();
            if (activePlayerMember == null) return false;

            return selectedMembers[0] == activePlayerMember;
        }


        private void ExecuteLegacyFallbackAttack()
        {
            PopulateVisibleFallbackTargets();

            var attacked = TryExecuteLegacyFallbackAttack();
            if (!attacked) return;

            BeginPostAttackMovementLock();
            _pendingEncounterTarget = null;
            unitController?.Stop();
        }


        private void PopulateVisibleFallbackTargets()
        {
            _visibleTargets.Clear();
            if (UnitManager.Instance == null) return;

            UnitManager.Instance.FindNearbyEnemies(playerUnit, EffectiveAttackScanRadius, _nearbyEnemyBuffer);

            for (var i = 0; i < _nearbyEnemyBuffer.Count; i++)
            {
                var enemy = _nearbyEnemyBuffer[i];
                if (enemy == null) continue;

                var health = enemy.Health;
                if (health == null || health.IsDead) continue;

                _visibleTargets.Add(health);
            }
        }


        private bool TryExecuteLegacyFallbackAttack()
        {
            return combatSystem?.TryExecuteAttack(unitCombat, _visibleTargets)
                ?? combatManager?.RequestAttack(unitCombat, _visibleTargets)
                ?? unitCombat.ExecuteAttack(_visibleTargets);
        }


        private void HandleReloadInput()
        {
            if (combatSystem != null)
                combatSystem.Reload(unitCombat);
            else if (combatManager != null)
                combatManager.RequestReload(unitCombat);
            else
                unitCombat.Reload();
        }


        private bool HasNearbyLiveEnemies()
        {
            if (playerUnit == null || UnitManager.Instance == null) return false;

            // Throttled: spatial-grid scans are cheap but not free; per-frame freshness isn't needed here.
            if (Time.time < _nextNearbyEnemyScanAt) return _cachedHasNearbyLiveEnemies;

            _nextNearbyEnemyScanAt = Time.time + 1f / Mathf.Max(1f, nearbyEnemyScansPerSecond);

            UnitManager.Instance.FindNearbyEnemies(playerUnit, EffectiveAttackScanRadius, _nearbyEnemyBuffer);

            _cachedHasNearbyLiveEnemies = false;
            for (var i = 0; i < _nearbyEnemyBuffer.Count; i++)
            {
                var unit = _nearbyEnemyBuffer[i];
                if (unit != null && unit.IsAlive)
                {
                    _cachedHasNearbyLiveEnemies = true;
                    break;
                }
            }

            return _cachedHasNearbyLiveEnemies;
        }


        private bool TryGetUnitHealthUnderCursor(out UnitHealth unitHealth)
        {
            unitHealth = null;

            if (worldCamera == null) return false;
            if (!TryReadPointerScreenPosition(out var mousePos2)) return false;

            _cursorTargetFilter ??= IsValidCursorTarget;

            return RtsPointerQueryUtility.TryGetNearestUnitHealthUnderPointer(
                worldCamera,
                mousePos2,
                targetableMask,
                targetingQueryTriggerInteraction,
                targetingRayDistance,
                _targetRaycastHits,
                out unitHealth,
                Mathf.Max(0f, zombieHoverAssistRadius),
                _targetSpherecastHits,
                _cursorTargetFilter);
        }


        private System.Func<UnitHealth, bool> _cursorTargetFilter;


        private bool IsValidCursorTarget(UnitHealth candidateHealth)
        {
            if (candidateHealth == null || candidateHealth.IsDead) return false;

            if (!requireHostileTargetsForCursorAndClick) return true;

            var candidateUnit = candidateHealth.GetComponentInParent<Unit>();
            return IsValidEncounterTarget(candidateUnit);
        }
    }
}
