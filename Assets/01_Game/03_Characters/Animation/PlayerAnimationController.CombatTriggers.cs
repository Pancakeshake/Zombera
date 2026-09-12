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

        private CachedWeightedAttackOption TriggerAttack()
        {
            RefreshCombatStateHold();
            var selectedAttack = ResolveSelectedAttackOption();

            if (_animator == null) return selectedAttack;

            if (!TryApplyFilteredAttackVariant(selectedAttack.ClipNameKeywords))
                TryApplyRandomVariant(baseAttackClip, _attackVariants);

            var triggered = false;

            if (selectedAttack.HasTrigger)
            {
                var triggerHash = selectedAttack.TriggerHash;
                ResetCombatTriggersExcept(triggerHash);
                _animator.SetTrigger(triggerHash);
                triggered = true;
            }
            else if (_hasAttack)
            {
                ResetCombatTriggersExcept(_attackHash);
                _animator.SetTrigger(_attackHash);
                triggered = true;
            }

            var fallbackStateName = !string.IsNullOrWhiteSpace(selectedAttack.FallbackStateName)
                ? selectedAttack.FallbackStateName
                : attackStateName;

            TryPlayFallbackState(fallbackStateName, triggered);

            if (!string.Equals(fallbackStateName, attackStateName, StringComparison.Ordinal))
                TryPlayFallbackState(attackStateName, triggered);

            if (suppressDefensiveReactionsWhileAttacking)
                _defensiveReactionSuppressUntilTime = Mathf.Max(
                    _defensiveReactionSuppressUntilTime,
                    Time.time + Mathf.Max(0f, defensiveReactionSuppressAfterAttackTriggerSeconds));

            return selectedAttack;
        }


        private void TriggerDodge()
        {
            if (ShouldSuppressDefensiveReaction()) return;

            if (_animator == null) return;

            var dodgeBaseClip = baseDodgeClip != null ? baseDodgeClip : baseLocomotionClip;
            var dodgeVariants = _dodgeVariants.Count > 0 ? _dodgeVariants : _locomotionVariants;
            TryApplyRandomVariant(dodgeBaseClip, dodgeVariants);

            // Randomly pick left or right dodge, falling back to whichever exists.
            var useLeft = _hasDodgeLeft && HasAnimatorState(dodgeLeftStateName);
            var useRight = _hasDodgeRight && HasAnimatorState(dodgeRightStateName);

            if (useLeft || useRight)
            {
                var pickLeft = useLeft && (!useRight || Random.value < 0.5f);
                var hash = pickLeft ? _dodgeHash : _dodgeRightHash;
                var state = pickLeft ? dodgeLeftStateName : dodgeRightStateName;
                var dodgeOpponent = ResolveEncounterOpponent();
                var dodgeDir = ResolveBackstepDirection(dodgeOpponent);
                var dodgeDistance = Mathf.Clamp(dodgeStepDistance, 0f, 0.5f);
                var dodgeDuration = Mathf.Max(0.05f, dodgeStepDurationSeconds);
                StartDodgeFaceLock(dodgeOpponent, dodgeDuration);
                _unitController?.BeginDodgeStep(dodgeDir, dodgeDistance, dodgeDuration);
                ResetCombatTriggersExcept(hash);
                _animator.SetTrigger(hash);
                TryPlayFallbackState(state, true);
                return;
            }

            // Fallback to hit so dodge still has visual feedback on legacy controllers.
            if (_hasHit)
            {
                _animator.SetTrigger(_hitHash);
                TryPlayFallbackState(hitStateName, true);
                return;
            }

            TryPlayFallbackState(dodgeLeftStateName, false);
        }


        private Unit ResolveEncounterOpponent()
        {
            if (unit == null) unit = GetComponent<Unit>();

            if (unit == null) return null;

            if (_encounterManager == null)
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            if (_encounterManager == null || !_encounterManager.IsUnitInEncounter(unit)) return null;

            if (!_encounterManager.TryGetEncounterOpponent(unit, out var encounterOpponent)) return null;

            if (encounterOpponent == null || !encounterOpponent.IsAlive) return null;

            return encounterOpponent;
        }


        private Vector3 ResolveBackstepDirection(Unit dodgeOpponent)
        {
            if (dodgeOpponent != null)
            {
                var awayFromOpponent = transform.position - dodgeOpponent.transform.position;
                awayFromOpponent.y = 0f;

                if (awayFromOpponent.sqrMagnitude > 0.0001f) return awayFromOpponent.normalized;
            }

            var fallback = -transform.forward;
            fallback.y = 0f;

            if (fallback.sqrMagnitude <= 0.0001f) return Vector3.back;

            return fallback.normalized;
        }


        private void StartDodgeFaceLock(Unit dodgeOpponent, float dodgeDuration)
        {
            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_unitController == null || dodgeOpponent == null)
            {
                _dodgeFaceTarget = null;
                _dodgeFaceLockExpiresAt = 0f;
                return;
            }

            _dodgeFaceTarget = dodgeOpponent;
            _dodgeFaceLockExpiresAt = Time.time + Mathf.Max(0f, dodgeDuration + dodgeFaceLockExtraSeconds);
            _unitController.FacePositionInstant(dodgeOpponent.transform.position);
        }


        private void TickDodgeFaceLock()
        {
            if (_dodgeFaceTarget == null) return;

            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_unitController == null
                || !_dodgeFaceTarget.IsAlive
                || Time.time > _dodgeFaceLockExpiresAt)
            {
                _dodgeFaceTarget = null;
                _dodgeFaceLockExpiresAt = 0f;
                return;
            }

            var turnSpeed = Mathf.Max(0f, dodgeFaceLockTurnSpeedDegreesPerSecond);
            if (turnSpeed <= 0f)
            {
                _unitController.FacePositionInstant(_dodgeFaceTarget.transform.position);
                return;
            }

            _unitController.RotateTowardsPosition(_dodgeFaceTarget.transform.position, turnSpeed);
        }


        private void TriggerHit()
        {
            if (ShouldSuppressDefensiveReaction()) return;

            // Guard against multiple hits resolving in the same frame.
            if (Time.frameCount == _lastHitFrame) return;
            _lastHitFrame = Time.frameCount;

            if (_animator == null) return;

            TryApplyRandomVariant(baseHitClip, _hitVariants);

            var triggered = false;
            if (_hasHit)
            {
                ResetCombatTriggersExcept(_hitHash);
                _animator.SetTrigger(_hitHash);
                triggered = true;
            }

            TryPlayFallbackState(hitStateName, triggered);
        }


        private CachedWeightedAttackOption ResolveSelectedAttackOption()
        {
            if (TrySelectWeightedAttackOption(out var selectedAttack)) return selectedAttack;

            return new CachedWeightedAttackOption(
                CombatAttackStyle.Unknown,
                CombatReactionArea.Chest,
                1f,
                _attackHash,
                _hasAttack,
                attackStateName,
                null);
        }


        private bool TrySelectWeightedAttackOption(out CachedWeightedAttackOption selectedAttack)
        {
            return WeightedAttackSelectionHelper.TrySelectWeightedAttackOption(
                _cachedWeightedAttackOptions,
                Random.value,
                out selectedAttack);
        }


        private CombatReactionArea ResolveReactionAreaForHitTiming(CachedWeightedAttackOption selectedAttack,
            float projectedHitTimeSeconds)
        {
            return WeightedAttackSelectionHelper.ResolveReactionAreaForHitTiming(
                selectedAttack,
                projectedHitTimeSeconds,
                attackReactionTimelines);
        }


        private void CacheWeightedAttackOptions()
        {
            _cachedWeightedAttackOptions.Clear();
            _weightedAttackTriggerHashes.Clear();

            if (weightedAttackOptions == null || weightedAttackOptions.Length == 0) return;

            foreach (var option in weightedAttackOptions)
            {
                if (option == null) continue;

                var weight = Mathf.Max(0f, option.weight);
                if (weight <= 0f) continue;

                var triggerParameter = string.IsNullOrWhiteSpace(option.triggerParameter)
                    ? attackTriggerParameter
                    : option.triggerParameter;

                var triggerHash = Animator.StringToHash(triggerParameter);
                var hasTrigger = HasParam(triggerParameter, AnimatorControllerParameterType.Trigger);

                _cachedWeightedAttackOptions.Add(new CachedWeightedAttackOption(
                    option.attackStyle,
                    option.preferredReactionArea == CombatReactionArea.Default
                        ? CombatReactionArea.Chest
                        : option.preferredReactionArea,
                    weight,
                    triggerHash,
                    hasTrigger,
                    option.fallbackStateName,
                    option.clipNameKeywords));

                if (hasTrigger && triggerHash != _attackHash && !_weightedAttackTriggerHashes.Contains(triggerHash))
                    _weightedAttackTriggerHashes.Add(triggerHash);
            }
        }


        private bool TryApplyFilteredAttackVariant(string[] clipKeywords)
        {
            if (!enableFolderClipVariants
                || baseAttackClip == null
                || _attackVariants.Count == 0
                || clipKeywords == null
                || clipKeywords.Length == 0)
                return false;

            _attackVariantFilterBuffer.Clear();

            foreach (var candidate in _attackVariants)
            {
                if (candidate == null) continue;

                var candidateName = candidate.name.ToLowerInvariant();
                if (ContainsAny(candidateName, clipKeywords)) _attackVariantFilterBuffer.Add(candidate);
            }

            if (_attackVariantFilterBuffer.Count == 0) return false;

            var selectedVariant = _attackVariantFilterBuffer[Random.Range(0, _attackVariantFilterBuffer.Count)];
            TryApplySpecificVariant(baseAttackClip, selectedVariant);
            return true;
        }


        private bool IsWithinAttackAnimationDistance(Unit defender)
        {
            var maxDistance = Mathf.Max(0f, maxAttackWindupAnimationDistance);
            if (maxDistance <= 0f || defender == null) return true;

            var delta = defender.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= maxDistance * maxDistance;
        }


        private void ResetCombatTriggersExcept(int keepHash)
        {
            if (_animator == null) return;

            if (_hasAttack && _attackHash != keepHash) _animator.ResetTrigger(_attackHash);

            foreach (var weightedHash in _weightedAttackTriggerHashes)
            {
                if (weightedHash == keepHash) continue;

                _animator.ResetTrigger(weightedHash);
            }

            if (_hasDodgeLeft && _dodgeHash != keepHash) _animator.ResetTrigger(_dodgeHash);

            if (_hasDodgeRight && _dodgeRightHash != keepHash) _animator.ResetTrigger(_dodgeRightHash);

            if (_hasHit && _hitHash != keepHash) _animator.ResetTrigger(_hitHash);

            if (_hasCombatEntry && _combatEntryHash != keepHash) _animator.ResetTrigger(_combatEntryHash);
        }


        private bool HasAnimatorState(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return false;
            var hash = Animator.StringToHash(stateName);
            // Check all layers for a state with this name hash.
            for (var layer = 0; layer < _animator.layerCount; layer++)
                if (_animator.HasState(layer, hash))
                    return true;
            return false;
        }


        private void ResetAllCombatTriggers()
        {
            if (_animator == null) return;

            if (_hasAttack) _animator.ResetTrigger(_attackHash);

            foreach (var weightedAttackTriggerHash in _weightedAttackTriggerHashes)
                _animator.ResetTrigger(weightedAttackTriggerHash);

            if (_hasDodgeLeft) _animator.ResetTrigger(_dodgeHash);

            if (_hasDodgeRight) _animator.ResetTrigger(_dodgeRightHash);

            if (_hasHit) _animator.ResetTrigger(_hitHash);

            if (_hasCombatEntry) _animator.ResetTrigger(_combatEntryHash);
        }


        private void TryPlayFallbackState(string stateName, bool triggerWasSet)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName)) return;

            if (!forceStateFallbackWhenTriggerSet && triggerWasSet) return;

            _ = TryCrossFadeState(stateName);
        }
    }
}
