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

        private void SyncDeadBool()
        {
            if (_animator == null || !_hasIsDead) return;
            _animator.SetBool(_isDeadHash, unitHealth != null && unitHealth.IsDead);
        }


        private void SyncCombatAnimatorState()
        {
            if (_animator == null || !_hasIsInCombat) return;

            if (unit == null) unit = GetComponent<Unit>();

            if (unitHealth != null && unitHealth.IsDead)
            {
                SetCombatAnimatorFlag(false);
                return;
            }

            if (_encounterManager == null)
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            var inCombatEncounter = unit != null
                                    && _encounterManager != null
                                    && _encounterManager.IsUnitInEncounter(unit);

            var inCombatHoldWindow = Time.time <= _combatStateHoldUntilTime;
            SetCombatAnimatorFlag(inCombatEncounter || inCombatHoldWindow);
        }


        private void RefreshCombatStateHold()
        {
            _combatStateHoldUntilTime = Mathf.Max(
                _combatStateHoldUntilTime,
                Time.time + Mathf.Max(0f, combatStateHoldSeconds));
        }


        private void ApplyCombatEyeFocus()
        {
            if (_animator == null || !enableCombatEyeFocus) return;

            if (unitHealth != null && unitHealth.IsDead)
            {
                ResetCombatEyeFocus(false);
                return;
            }

            var focusTarget = ResolveCombatLookTarget();
            var desiredWeight = focusTarget != null ? 1f : 0f;
            var weightStep = Mathf.Max(0.01f, combatLookAtWeightLerpSpeed) * Time.deltaTime;
            _smoothedCombatLookAtWeight = Mathf.MoveTowards(_smoothedCombatLookAtWeight, desiredWeight, weightStep);

            if (focusTarget != null)
            {
                var desiredLookPosition = focusTarget.transform.position +
                                          Vector3.up * Mathf.Max(0f, combatLookAtHeightOffset);

                if (!_hasSmoothedCombatLookAtPosition)
                {
                    _smoothedCombatLookAtPosition = desiredLookPosition;
                    _hasSmoothedCombatLookAtPosition = true;
                }
                else
                {
                    var lerpT = 1f - Mathf.Exp(-Mathf.Max(0.01f, combatLookAtPositionLerpSpeed) * Time.deltaTime);
                    _smoothedCombatLookAtPosition =
                        Vector3.Lerp(_smoothedCombatLookAtPosition, desiredLookPosition, lerpT);
                }
            }

            if (_smoothedCombatLookAtWeight <= 0.0001f || !_hasSmoothedCombatLookAtPosition)
            {
                _animator.SetLookAtWeight(0f);
                return;
            }

            var overallWeight = Mathf.Clamp01(combatLookAtOverallWeight) * _smoothedCombatLookAtWeight;
            _animator.SetLookAtWeight(
                overallWeight,
                Mathf.Clamp01(combatLookAtBodyWeight),
                Mathf.Clamp01(combatLookAtHeadWeight),
                Mathf.Clamp01(combatLookAtEyesWeight),
                Mathf.Clamp01(combatLookAtClampWeight));
            _animator.SetLookAtPosition(_smoothedCombatLookAtPosition);
        }


        private Unit ResolveCombatLookTarget()
        {
            if (!enableCombatEyeFocus)
            {
                return ClearCombatLookTarget();
            }

            if (unit == null) unit = GetComponent<Unit>();

            if (unit == null)
            {
                return ClearCombatLookTarget();
            }

            if (_encounterManager == null)
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            if (_encounterManager == null || !_encounterManager.IsUnitInEncounter(unit))
            {
                return ClearCombatLookTarget();
            }

            if (!_encounterManager.TryGetEncounterOpponent(unit, out var encounterOpponent))
            {
                return ClearCombatLookTarget();
            }

            if (_currentCombatLookTarget == null || !_currentCombatLookTarget.IsAlive)
            {
                _currentCombatLookTarget = encounterOpponent;
                _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
                return _currentCombatLookTarget;
            }

            if (_currentCombatLookTarget == encounterOpponent)
            {
                _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
                return _currentCombatLookTarget;
            }

            if (Time.time < _nextCombatLookTargetSwitchAt) return _currentCombatLookTarget;

            _currentCombatLookTarget = encounterOpponent;
            _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
            return _currentCombatLookTarget;

            Unit ClearCombatLookTarget()
            {
                _currentCombatLookTarget = null;
                return null;
            }
        }


        private static AnimationClip FindBestMatchingClip(AnimationClip[] clips, string desiredName, bool skipPreviewClips)
        {
            return ClipMatchHelper.FindBestMatchingClip(clips, desiredName, skipPreviewClips);
        }


        private void ResetCombatEyeFocus(bool immediate)
        {
            _currentCombatLookTarget = null;
            _nextCombatLookTargetSwitchAt = 0f;

            if (immediate)
            {
                _smoothedCombatLookAtWeight = 0f;
                _hasSmoothedCombatLookAtPosition = false;
            }

            if (_animator != null && _isApplyingAnimatorIk) _animator.SetLookAtWeight(0f);
        }
    }
}
