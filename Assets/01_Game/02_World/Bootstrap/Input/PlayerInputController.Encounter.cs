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
using Zombera.Factions;
using Zombera.Inventory;
using Zombera.Systems.Digging;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {

        private void TryAutoStartEncounterFromNearbyThreat()
        {
            if (playerUnit == null || !playerUnit.IsAlive) return;

            if (IsBowRangedModeActive()) return;

            if (IsMoveOrderFacingSuppressed()) return;

            if (IsPostureTransitionMovementLocked()) return;

            if (Time.time < _suppressAutoEngageUntilAt) return;

            var autoEngageDistance = Mathf.Max(0f, autoEngageEnemyDistanceMeters);
            if (autoEngageDistance <= 0f) return;

            if (ResolveEncounterManager() == null || encounterManager.IsUnitInEncounter(playerUnit) ||
                _pendingEncounterTarget != null) return;

            // Throttled: nearest-enemy scans don't need per-frame freshness for auto-engage.
            if (Time.time < _nextAutoEngageScanAt) return;
            _nextAutoEngageScanAt = Time.time + 1f / Mathf.Max(1f, nearbyEnemyScansPerSecond);

            var nearbyTarget = FindNearestEncounterTarget(autoEngageDistance);
            if (!IsValidEncounterTarget(nearbyTarget)) return;

            if (TryStartPlayerEncounter(nearbyTarget, false)) BeginPostAttackMovementLock();
        }


        internal bool TryStartPlayerEncounter(Unit preferredTarget = null, bool allowApproachWhenOutOfRange = true)
        {
            if (!TryResolveEncounterManagerForPlayer()) return false;

            if (encounterManager.IsUnitInEncounter(playerUnit))
            {
                _pendingEncounterTarget = null;
                return true;
            }

            var target = ResolveEncounterStartTarget(preferredTarget);
            if (!IsValidEncounterTarget(target))
            {
                _pendingEncounterTarget = null;
                return false;
            }

            QueueCombatFacingAssist(target, true);
            var withinPreferredDistance = IsWithinPreferredEncounterStartDistance(target);
            var isFacingWithinTolerance = IsFacingTargetWithin(target, encounterStartFacingToleranceDegrees);

            if (withinPreferredDistance) playerAnimationController?.TryTriggerCombatEntry();

            if (!TryPrepareEncounterStart(target, allowApproachWhenOutOfRange, withinPreferredDistance,
                    isFacingWithinTolerance)) return false;

            if (!encounterManager.TryStartEncounter(playerUnit, target))
                return HandleEncounterStartFailure(target, allowApproachWhenOutOfRange);

            _pendingEncounterTarget = null;
            return true;
        }


        /// <summary>Returns the cached encounter manager, lazily resolving once if scene wiring was missing.</summary>
        private CombatEncounterManager ResolveEncounterManager()
        {
            if (encounterManager == null)
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            return encounterManager;
        }


        private bool TryResolveEncounterManagerForPlayer()
        {
            if (playerUnit == null || ResolveEncounterManager() == null)
            {
                _pendingEncounterTarget = null;
                return false;
            }

            return true;
        }


        private Unit ResolveEncounterStartTarget(Unit preferredTarget)
        {
            return IsValidEncounterTarget(preferredTarget) ? preferredTarget : FindNearestEncounterTarget();
        }


        private bool TryPrepareEncounterStart(
            Unit target,
            bool allowApproachWhenOutOfRange,
            bool withinPreferredDistance,
            bool isFacingWithinTolerance)
        {
            if (withinPreferredDistance && isFacingWithinTolerance) return true;

            _pendingEncounterTarget = target;

            if (!allowApproachWhenOutOfRange)
            {
                unitController?.Stop();
                return false;
            }

            if (withinPreferredDistance) return false;

            TryBeginPostStandMoveRamp();
            unitController?.MoveTo(ResolveEncounterApproachPosition(target));

            return false;
        }


        private bool HandleEncounterStartFailure(Unit target, bool allowApproachWhenOutOfRange)
        {
            if (!allowApproachWhenOutOfRange)
            {
                _pendingEncounterTarget = null;
                unitController?.Stop();
                return false;
            }

            _pendingEncounterTarget = target;
            TryBeginPostStandMoveRamp();
            unitController?.MoveTo(ResolveEncounterApproachPosition(target));
            return false;
        }


        private void BeginPostAttackMovementLock()
        {
            var lockDuration = Mathf.Max(postAttackMovementLockSeconds, minimumAttackMovementLockSeconds);
            if (lockDuration <= 0f) return;

            _movementLockExpiresAt = Mathf.Max(_movementLockExpiresAt, Time.time + lockDuration);
        }


        private bool IsMovementTemporarilyLocked()
        {
            return _movementLockExpiresAt > Time.time;
        }


        private bool HasEncounterSystemAvailable()
        {
            return ResolveEncounterManager() != null;
        }


        private void TickPendingEncounterJoin()
        {
            if (IsMoveOrderFacingSuppressed())
            {
                _pendingEncounterTarget = null;
                return;
            }

            if (IsPostureTransitionMovementLocked())
            {
                unitController?.Stop();
                return;
            }

            if (_pendingEncounterTarget == null) return;

            if (!IsValidEncounterTarget(_pendingEncounterTarget) || playerUnit == null || !playerUnit.IsAlive)
            {
                _pendingEncounterTarget = null;
                return;
            }

            if (ResolveEncounterManager() == null || encounterManager.IsUnitInEncounter(playerUnit))
            {
                _pendingEncounterTarget = null;
                return;
            }

            QueueCombatFacingAssist(_pendingEncounterTarget, false);
            var withinPreferredDistance = IsWithinPreferredEncounterStartDistance(_pendingEncounterTarget);

            switch (withinPreferredDistance)
            {
                case true:
                    playerAnimationController?.TryTriggerCombatEntry();
                    break;
                case false:
                    TryBeginPostStandMoveRamp();
                    unitController?.MoveTo(ResolveEncounterApproachPosition(_pendingEncounterTarget));
                    break;
            }

            if (!IsFacingTargetWithin(_pendingEncounterTarget, encounterStartFacingToleranceDegrees) ||
                !withinPreferredDistance) return;

            if (encounterManager.TryStartEncounter(playerUnit, _pendingEncounterTarget)) _pendingEncounterTarget = null;
        }


        private Vector3 ResolveEncounterApproachPosition(Unit target)
        {
            if (target == null) return transform.position;

            var desiredDistance = GetPreferredEncounterStartDistance();

            var targetPosition = target.transform.position;
            var fromTargetToPlayer = transform.position - targetPosition;
            fromTargetToPlayer.y = 0f;

            if (fromTargetToPlayer.sqrMagnitude <= 0.0001f)
            {
                fromTargetToPlayer = -target.transform.forward;
                fromTargetToPlayer.y = 0f;

                if (fromTargetToPlayer.sqrMagnitude <= 0.0001f) fromTargetToPlayer = Vector3.back;
            }

            fromTargetToPlayer.Normalize();

            var approachPosition = targetPosition + fromTargetToPlayer * desiredDistance;
            approachPosition.y = transform.position.y;
            return approachPosition;
        }


        private bool IsWithinPreferredEncounterStartDistance(Unit target)
        {
            if (target == null) return false;

            var preferredDistance = GetPreferredEncounterStartDistance();
            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude <= preferredDistance * preferredDistance;
        }


        private float GetPreferredEncounterStartDistance()
        {
            var engageRange = encounterManager != null
                ? encounterManager.EngageRange
                : Zombera.Combat.CombatTuningConfig.EngageRangeOr(1.9f);
            var distanceFactor = Mathf.Clamp(preferredEncounterStartDistanceFactor, 0.1f, 1f);
            return Mathf.Max(0.2f, engageRange * distanceFactor);
        }


        internal void QueueCombatFacingAssist(Unit target, bool requestTurnAnimation)
        {
            if (target == null) return;

            if (IsMoveOrderFacingSuppressed()) return;

            _combatFacingAssistTarget = target;
            var duration = Mathf.Max(0f, combatFaceAssistDurationSeconds);
            _combatFacingAssistExpiresAt = Mathf.Max(_combatFacingAssistExpiresAt, Time.time + duration);

            FaceTargetForCombat(target);

            if (requestTurnAnimation) playerAnimationController?.TryPlayRandomFacingTurn(target);
        }


        private void TickCombatFacingAssist()
        {
            if (IsMoveOrderFacingSuppressed())
            {
                _combatFacingAssistTarget = null;
                _combatFacingAssistExpiresAt = 0f;
                return;
            }

            if (_combatFacingAssistTarget == null) return;

            if (!IsValidEncounterTarget(_combatFacingAssistTarget) || Time.time > _combatFacingAssistExpiresAt)
            {
                _combatFacingAssistTarget = null;
                _combatFacingAssistExpiresAt = 0f;
                return;
            }

            FaceTargetForCombat(_combatFacingAssistTarget);

            if (!IsFacingTargetWithin(_combatFacingAssistTarget, combatFaceAssistCompleteAngleDegrees)) return;

            _combatFacingAssistTarget = null;
            _combatFacingAssistExpiresAt = 0f;
        }


        private void FaceTargetForCombat(Unit target)
        {
            if (target == null || unitController == null) return;

            var turnSpeed = Zombera.Combat.CombatTuningConfig.FacingTurnSpeedOr(
                Mathf.Max(0f, combatFaceTurnSpeedDegreesPerSecond));
            if (turnSpeed > 0f)
            {
                unitController.RotateTowardsPosition(target.transform.position, turnSpeed);
                return;
            }

            unitController.FacePositionInstant(target.transform.position);
        }


        private void BeginMoveOrderFacingSuppression()
        {
            _suppressCombatFacingUntilMoveOrderCompletes = true;
            _pendingEncounterTarget = null;
            _combatFacingAssistTarget = null;
            _combatFacingAssistExpiresAt = 0f;
        }


        private void TickMoveOrderFacingSuppression()
        {
            if (!IsMoveOrderFacingSuppressed()) return;

            _pendingEncounterTarget = null;
            _combatFacingAssistTarget = null;
            _combatFacingAssistExpiresAt = 0f;
        }


        internal bool IsMoveOrderFacingSuppressed()
        {
            if (!_suppressCombatFacingUntilMoveOrderCompletes) return false;

            if (unitController != null
                && (unitController.HasMoveTarget || unitController.IsMoving)) return true;

            _suppressCombatFacingUntilMoveOrderCompletes = false;
            return false;
        }


        internal bool IsFacingTargetWithin(Unit target, float toleranceDegrees)
        {
            if (target == null) return false;

            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f) return true;

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var angle = Vector3.Angle(forward, toTarget.normalized);
            return angle <= Mathf.Clamp(toleranceDegrees, 0f, 180f);
        }


        private Unit FindNearestEncounterTarget(float maxDistanceMeters = -1f)
        {
            if (playerUnit == null || UnitManager.Instance == null) return null;

            var maxDistanceSqr = float.PositiveInfinity;
            var searchRadius = EffectiveAttackScanRadius;
            if (maxDistanceMeters > 0f)
            {
                maxDistanceSqr = maxDistanceMeters * maxDistanceMeters;
                searchRadius = Mathf.Min(searchRadius, maxDistanceMeters);
            }

            if (searchRadius <= 0f) return null;

            UnitManager.Instance.FindNearbyEnemies(playerUnit, searchRadius, _nearbyEnemyBuffer);
            Unit nearest = null;
            var nearestDistSqr = float.MaxValue;

            for (var i = 0; i < _nearbyEnemyBuffer.Count; i++)
            {
                var candidate = _nearbyEnemyBuffer[i];
                if (!IsValidEncounterTarget(candidate)) continue;

                var dSqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (dSqr > maxDistanceSqr) continue;

                if (dSqr >= nearestDistSqr) continue;

                nearestDistSqr = dSqr;
                nearest = candidate;
            }

            return nearest;
        }


        internal bool IsValidEncounterTarget(Unit target)
        {
            if (target == null || !target.IsAlive || playerUnit == null) return false;

            return FactionManager.AreUnitsHostile(playerUnit, target);
        }
    }
}
