#region

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zombera.Combat;
using Zombera.Core;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    public sealed partial class PlayerAnimationController
    {

        public void SetBowAimTarget(Unit targetUnit)
        {
            _bowAimTarget = targetUnit != null && targetUnit.IsAlive ? targetUnit : null;

            if (_bowAimTarget != null) RefreshCombatStateHold();
        }


        public void ClearBowAimTarget()
        {
            _bowAimTarget = null;
            ResetBowVisualPlayback(false);

            if (CanWriteAnimatorParameters() && _paramsCached && _hasBowRapidFire)
                _animator.SetBool(_bowRapidFireHash, false);
        }


        public void TriggerBowDraw(bool rapidFire)
        {
            RefreshCombatStateHold();

            if (_animator == null || !_paramsCached || !IsBowEquippedRuntime()) return;

            if (_bowAimTarget != null && !_bowAimTarget.IsAlive) _bowAimTarget = null;

            if (_bowAimTarget == null) _bowAimTarget = ResolveBowAimTargetFallback();

            if (_bowAimTarget == null || !_bowAimTarget.IsAlive) return;

            if (_hasBowEquipped) _animator.SetBool(_bowEquippedHash, true);

            _ = rapidFire;

            if (_hasBowRapidFire) _animator.SetBool(_bowRapidFireHash, false);

            if (_hasBowNotch) _animator.SetTrigger(_bowNotchHash);

            StartBowVisualDraw();
        }


        public void TriggerBowRelease(bool rapidFire)
        {
            RefreshCombatStateHold();

            if (_animator == null || !_paramsCached || !IsBowEquippedRuntime()) return;

            _ = rapidFire;

            if (_hasBowRapidFire) _animator.SetBool(_bowRapidFireHash, false);

            if (_hasBowShoot) _animator.SetTrigger(_bowShootHash);

            if (IsAnimatorInStateOrTransitionTo(bowNotchStateName))
            {
                var previousCrossFadeSeconds = fallbackCrossFadeSeconds;
                fallbackCrossFadeSeconds = Mathf.Max(0f, bowShootFallbackCrossFadeSeconds);
                _ = TryCrossFadeState(bowShootStateName);
                fallbackCrossFadeSeconds = previousCrossFadeSeconds;
            }

            StartBowVisualRelease();
        }


        private void DriveBowParameters()
        {
            if (_animator == null || !_paramsCached) return;

            var bowEquippedRuntime = IsBowEquippedRuntime();

            if (_bowAimTarget != null && !_bowAimTarget.IsAlive) _bowAimTarget = null;

            if (_bowAimTarget == null && bowEquippedRuntime) _bowAimTarget = ResolveBowAimTargetFallback();

            var hasBowAimTarget = _bowAimTarget != null && _bowAimTarget.IsAlive;

            if (_hasBowEquipped) _animator.SetBool(_bowEquippedHash, bowEquippedRuntime && hasBowAimTarget);

            if (!bowEquippedRuntime || !hasBowAimTarget)
            {
                SetBowAnimatorDefaults();
                return;
            }

            var bowAnimationAllowed = IsBowAnimationAllowed();
            if (!bowAnimationAllowed)
            {
                if (_hasBowAimY) _animator.SetFloat(_bowAimYHash, 0f);

                if (_hasBowRapidFire) _animator.SetBool(_bowRapidFireHash, false);

                return;
            }

            var normalizedAimY = 0f;

            if (_bowAimTarget != null)
            {
                RefreshCombatStateHold();

                var aimOrigin = transform.position + Vector3.up * bowAimHeightOffset;
                var toTarget = _bowAimTarget.transform.position - aimOrigin;
                var horizontal = toTarget;
                horizontal.y = 0f;
                var horizontalDistance = horizontal.magnitude;
                const float minHorizontal = 0.05f;
                if (horizontalDistance < minHorizontal)
                {
                    normalizedAimY = Mathf.Abs(toTarget.y) < 0.001f ? 0f : Mathf.Sign(toTarget.y);
                }
                else
                {
                    var pitch = Mathf.Atan2(toTarget.y, horizontalDistance);
                    const float maxPitchRad = Mathf.PI / 3f;
                    normalizedAimY = Mathf.Clamp(pitch / maxPitchRad, -1f, 1f);
                }
            }

            if (_hasBowAimY) _animator.SetFloat(_bowAimYHash, normalizedAimY);

            if (_hasBowRapidFire && _bowAimTarget == null) _animator.SetBool(_bowRapidFireHash, false);
        }


        private bool IsAnimatorInStateOrTransitionTo(string stateName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName)) return false;

            var currentState = _animator.GetCurrentAnimatorStateInfo(0);
            if (StateMatchesName(currentState, stateName)) return true;

            if (_animator.IsInTransition(0))
            {
                var nextState = _animator.GetNextAnimatorStateInfo(0);
                return StateMatchesName(nextState, stateName);
            }

            return false;
        }


        private bool IsBowAnimationAllowed()
        {
            if (!IsBowEquippedRuntime()) return false;

            if (_bowAimTarget == null || !_bowAimTarget.IsAlive) return false;

            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_unitController == null) return true;

            if (_unitController.IsSprinting) return false;

            if (_unitController.HasMoveTarget || _unitController.IsMoving) return false;

            var speed = LocomotionWorldVelocityAfterDeadZone().magnitude;
            if (speed > Mathf.Max(0f, bowAimActivationMaxSpeedMetersPerSecond)) return false;

            var maxSpeed = Mathf.Max(0.01f, _unitController.MoveSpeed);
            var normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);
            if (normalizedSpeed > Mathf.Clamp01(bowAnimationMaxNormalizedMoveSpeed)) return false;

            return IsFacingBowAimTarget(_bowAimTarget);
        }


        private bool IsFacingBowAimTarget(Unit targetUnit)
        {
            if (targetUnit == null) return false;

            var toTarget = targetUnit.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f) return true;

            var forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var angle = Vector3.Angle(forward, toTarget.normalized);
            return angle <= Mathf.Clamp(bowAimFacingToleranceDegrees, 0f, 180f);
        }


        private Unit ResolveBowAimTargetFallback()
        {
            if (unit == null) unit = GetComponent<Unit>();

            if (unit == null) return null;

            if (_encounterManager == null)
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            if (_encounterManager != null
                && _encounterManager.TryGetEncounterOpponent(unit, out var encounterOpponent)
                && encounterOpponent != null
                && encounterOpponent.IsAlive)
                return encounterOpponent;

            return null;
        }


        private bool IsBowEquippedRuntime()
        {
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();

            return weaponSystem != null && weaponSystem.IsBowEquipped;
        }


        private void SetBowAnimatorDefaults()
        {
            if (!CanWriteAnimatorParameters() || !_paramsCached) return;

            if (_hasBowEquipped) _animator.SetBool(_bowEquippedHash, false);

            if (_hasBowAimY) _animator.SetFloat(_bowAimYHash, 0f);

            if (_hasBowRapidFire) _animator.SetBool(_bowRapidFireHash, false);
        }
    }
}
