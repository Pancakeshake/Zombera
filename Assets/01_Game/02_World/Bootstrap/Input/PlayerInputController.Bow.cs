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

        private Unit _activeBowRangedTarget;
        private bool _bowQueuedRapidFire;

        private float _bowQueuedDurationSeconds;
        private float _bowQueuedReleaseAt;
        private float _nextBowRepositionAt;
        private float _nextBowShotAt;

        public float BowShotChargeProgress01
        {
            get
            {
                if (!IsBowShotCharging) return 0f;

                var duration = Mathf.Max(0.001f, _bowQueuedDurationSeconds);
                var startAt = _bowQueuedReleaseAt - duration;
                var elapsed = Mathf.Max(0f, Time.time - startAt);
                return Mathf.Clamp01(elapsed / duration);
            }
        }

        private void TickBowRangedCombatState()
        {
            if (!TryValidateBowRangedState()) return;
            if (TryProcessQueuedBowShot()) return;
            if (IsMoveOrderFacingSuppressed()) return;

            UpdateBowCombatTargetingState();
            if (IsPostureTransitionMovementLocked()) return;

            var distanceToTarget = Vector3.Distance(transform.position, _activeBowRangedTarget.transform.position);
            if (TryFallbackFromBowToMelee(distanceToTarget)) return;

            MaintainBowStandoffDistance(_activeBowRangedTarget, distanceToTarget);
            if (!CanQueueBowShot(distanceToTarget)) return;

            QueueBowShot();
        }


        private bool TryValidateBowRangedState()
        {
            if (IsBowRangedModeActive()
                && _activeBowRangedTarget != null
                && IsValidEncounterTarget(_activeBowRangedTarget))
                return true;

            if (_activeBowRangedTarget != null || IsBowShotCharging) ClearBowRangedCombatState(true);
            return false;
        }


        private bool TryProcessQueuedBowShot()
        {
            if (!IsBowShotCharging) return false;

            // Never let suppression/posture gates trap an already-queued release.
            if (unitController != null && (unitController.HasMoveTarget || unitController.IsMoving))
                unitController.Stop();

            TryReleaseQueuedBowShot();
            return true;
        }


        private void UpdateBowCombatTargetingState()
        {
            if (unitCombat != null && _activeBowRangedTarget.Health != null)
                unitCombat.SetMarkedTarget(_activeBowRangedTarget.Health);

            // Keep bow stance ownership stable while ranged combat remains active.
            playerAnimationController?.SetBowAimTarget(_activeBowRangedTarget);
        }


        private bool TryFallbackFromBowToMelee(float distanceToTarget)
        {
            var fallbackDistance = Mathf.Max(0f, bowMeleeFallbackDistanceMeters);
            if (fallbackDistance <= 0f || distanceToTarget > fallbackDistance) return false;

            var fallbackTarget = _activeBowRangedTarget;
            ClearBowRangedCombatState(true);
            _ = TryStartPlayerEncounter(fallbackTarget);
            return true;
        }


        private bool CanQueueBowShot(float distanceToTarget)
        {
            if (distanceToTarget > ResolveCurrentBowRange()) return false;

            if (!IsReadyToInitiateBowAim(_activeBowRangedTarget)) return false;

            return Time.time >= _nextBowShotAt;
        }


        private void StartBowRangedCombat(Unit target)
        {
            if (!enableBowRangedCombatState || !IsBowRangedModeActive() || !IsValidEncounterTarget(target)) return;

            if (ResolveEncounterManager() != null && playerUnit != null && encounterManager.IsUnitInEncounter(playerUnit))
                encounterManager.TryDisengageUnit(playerUnit, "player-bow-ranged");

            _movementLockExpiresAt = 0f;
            _pendingEncounterTarget = null;
            _activeBowRangedTarget = target;
            _nextBowRepositionAt = 0f;

            if (unitCombat != null && target.Health != null) unitCombat.SetMarkedTarget(target.Health);

            unitController?.Stop();
            playerAnimationController?.ClearBowAimTarget();
            QueueCombatFacingAssist(target, true);

            if (!IsBowShotCharging && Time.time >= _nextBowShotAt) QueueBowShot();
        }


        private void QueueBowShot()
        {
            if (_activeBowRangedTarget == null || !IsValidEncounterTarget(_activeBowRangedTarget)) return;

            if (!IsReadyToInitiateBowAim(_activeBowRangedTarget)) return;

            if (unitController != null && (unitController.HasMoveTarget || unitController.IsMoving))
                unitController.Stop();

            playerAnimationController?.SetBowAimTarget(_activeBowRangedTarget);

            IsBowShotCharging = true;
            _bowQueuedRapidFire = ResolveBowRapidFireMode();
            _bowQueuedDurationSeconds = ResolveBowChargeDurationSeconds();
            _bowQueuedReleaseAt = Time.time + _bowQueuedDurationSeconds;
            playerAnimationController?.TriggerBowDraw(_bowQueuedRapidFire);
        }


        private bool IsReadyToInitiateBowAim(Unit target)
        {
            if (target == null || !IsValidEncounterTarget(target)) return false;

            if (unitController != null)
            {
                var maxSpeed = Mathf.Max(0f, bowAimInitiationMaxSpeedMetersPerSecond);
                var isMoving = unitController.HasMoveTarget
                               || unitController.IsMoving
                               || unitController.WorldVelocity.magnitude > maxSpeed;

                if (isMoving) return false;
            }

            if (IsFacingTargetWithin(target, bowStartFacingToleranceDegrees)) return true;

            QueueCombatFacingAssist(target, false);
            return false;
        }


        private void TryReleaseQueuedBowShot()
        {
            if (!IsBowShotCharging || !IsBowChargeComplete()) return;

            if (_activeBowRangedTarget == null || !IsValidEncounterTarget(_activeBowRangedTarget))
            {
                IsBowShotCharging = false;
                _bowQueuedRapidFire = false;
                _bowQueuedReleaseAt = 0f;
                _bowQueuedDurationSeconds = 0f;
                return;
            }

            var rapidFire = _bowQueuedRapidFire;
            IsBowShotCharging = false;
            _bowQueuedRapidFire = false;
            _bowQueuedReleaseAt = 0f;
            _bowQueuedDurationSeconds = 0f;

            playerAnimationController?.TriggerBowRelease(rapidFire);

            var attacked = TryExecuteBowAttack(_activeBowRangedTarget);
            if (attacked)
            {
                _nextBowShotAt = Time.time + ResolveBowShotCadenceSeconds();
                return;
            }

            _nextBowShotAt = Time.time + 0.15f;
            TryReloadCurrentWeapon();
        }


        private bool TryExecuteBowAttack(Unit explicitTarget)
        {
            if (unitCombat == null || explicitTarget == null || explicitTarget.Health == null ||
                explicitTarget.Health.IsDead) return false;

            var resolvedWeaponSystem = ResolveWeaponSystem();
            if (resolvedWeaponSystem != null && resolvedWeaponSystem.IsBowEquipped)
                return resolvedWeaponSystem.TryAttackTarget(explicitTarget.Health);

            _visibleTargets.Clear();
            _visibleTargets.Add(explicitTarget.Health);

            return combatSystem?.TryExecuteAttack(unitCombat, _visibleTargets)
                ?? combatManager?.RequestAttack(unitCombat, _visibleTargets)
                ?? unitCombat.ExecuteAttack(_visibleTargets);
        }


        private void TryReloadCurrentWeapon()
        {
            if (unitCombat == null) return;

            if (combatSystem != null)
                combatSystem.Reload(unitCombat);
            else if (combatManager != null)
                combatManager.RequestReload(unitCombat);
            else
                unitCombat.Reload();
        }


        private void MaintainBowStandoffDistance(Unit target, float distanceToTarget)
        {
            if (target == null || unitController == null) return;

            if (Time.time < _nextBowRepositionAt) return;

            _nextBowRepositionAt = Time.time + Mathf.Max(0.05f, bowRepositionIntervalSeconds);

            var effectiveRange = ResolveCurrentBowRange();

            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f) return;

            var directionToTarget = toTarget.normalized;
            var stepDistance = Mathf.Max(0.1f, bowRepositionStepMeters);

            if (distanceToTarget > effectiveRange)
            {
                var desiredDistance = Mathf.Max(0.5f, effectiveRange - 0.5f);
                var approachPosition = target.transform.position -
                                       directionToTarget * Mathf.Max(desiredDistance, stepDistance);
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(approachPosition);
                return;
            }

            if (!maintainBowPreferredStandoffDistance)
            {
                if (!unitController.HasMoveTarget && !unitController.IsMoving) return;

                unitController.Stop();
                return;
            }

            var preferredMin = Mathf.Clamp(bowPreferredMinDistanceMeters, 0f, Mathf.Max(0.1f, effectiveRange - 0.2f));
            var preferredMax = Mathf.Clamp(
                bowPreferredMaxDistanceMeters,
                Mathf.Max(preferredMin + 0.1f, 0.2f),
                Mathf.Max(preferredMin + 0.1f, effectiveRange));

            if (distanceToTarget < preferredMin)
            {
                var retreatPosition = transform.position - directionToTarget * stepDistance;
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(retreatPosition);
                return;
            }

            if (distanceToTarget > preferredMax)
            {
                var approachPosition = target.transform.position -
                                       directionToTarget * Mathf.Max(preferredMax, stepDistance);
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(approachPosition);
                return;
            }

            if (unitController.HasMoveTarget) unitController.Stop();
        }


        private float ResolveCurrentBowRange()
        {
            return Mathf.Max(1f, bowMaximumAttackRangeMeters);
        }


        private bool IsBowChargeComplete()
        {
            return IsBowShotCharging && (Time.time >= _bowQueuedReleaseAt || BowShotChargeProgress01 >= 0.999f);
        }


        private bool ResolveBowRapidFireMode()
        {
            return ResolveWeaponSystem() is { EquippedWeapon: { fireRate: >= 1.75f } };
        }


        private float ResolveBowChargeDurationSeconds()
        {
            var fallbackDuration = Mathf.Max(0.1f, bowDrawDurationSeconds);

            var stats = playerUnit != null ? playerUnit.Stats : null;
            if (stats == null) return fallbackDuration;

            var shootingLevel = Mathf.Clamp(
                stats.GetSkillValue(UnitSkillType.Shooting),
                UnitStats.MinSkillLevel,
                UnitStats.MaxSkillLevel);

            var levelT = (shootingLevel - UnitStats.MinSkillLevel)
                         / (float)(UnitStats.MaxSkillLevel - UnitStats.MinSkillLevel);

            var levelOneDuration = Mathf.Max(0.1f, bowChargeDurationAtLevel1Seconds);
            var levelHundredDuration = Mathf.Max(0.1f, bowChargeDurationAtLevel100Seconds);
            return Mathf.Lerp(levelOneDuration, levelHundredDuration, Mathf.Clamp01(levelT));
        }


        private float ResolveBowShotCadenceSeconds()
        {
            var byAttackCooldown = unitCombat != null ? unitCombat.EffectiveAttackCooldown : 0f;
            var byWeaponFireRate = 0.75f;

            var resolvedWeaponSystem = ResolveWeaponSystem();
            if (resolvedWeaponSystem?.EquippedWeapon != null && resolvedWeaponSystem.EquippedWeapon.fireRate > 0.01f)
                byWeaponFireRate = 1f / resolvedWeaponSystem.EquippedWeapon.fireRate;

            return Mathf.Max(byAttackCooldown, byWeaponFireRate) + Mathf.Max(0f, bowShotCadencePaddingSeconds);
        }


        private WeaponSystem ResolveWeaponSystem()
        {
            if (weaponSystem != null) return weaponSystem;

            if (unitCombat != null) weaponSystem = unitCombat.GetComponent<WeaponSystem>();

            if (weaponSystem == null && playerUnit != null) weaponSystem = playerUnit.GetComponent<WeaponSystem>();

            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();

            return weaponSystem;
        }


        public bool IsRangedCombatActive => _activeBowRangedTarget != null && IsBowRangedModeActive();
        public bool IsBowShotCharging { get; private set; }


        private void ClearBowRangedCombatState(bool clearAimTarget)
        {
            _activeBowRangedTarget = null;
            IsBowShotCharging = false;
            _bowQueuedReleaseAt = 0f;
            _bowQueuedDurationSeconds = 0f;
            _bowQueuedRapidFire = false;
            _nextBowRepositionAt = 0f;

            if (clearAimTarget) playerAnimationController?.ClearBowAimTarget();
        }
    }
}
