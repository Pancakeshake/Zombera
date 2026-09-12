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

        private void OnCombatAttackWindup(CombatAttackWindupEvent e)
        {
            if (unit == null) return;

            if (e.Attacker == unit || e.Defender == unit) RefreshCombatStateHold();

            if (e.Attacker == unit && IsWithinAttackAnimationDistance(e.Defender))
            {
                var selectedAttack = TriggerAttack();
                var timedReactionArea = ResolveReactionAreaForHitTiming(selectedAttack, e.WindupSeconds);
                CombatAttackPresentationRegistry.RegisterSelection(
                    e.EncounterId,
                    e.Attacker,
                    e.Defender,
                    selectedAttack.AttackStyle,
                    timedReactionArea,
                    e.WindupSeconds + 0.5f);
            }
        }


        private void OnCombatTickResolved(CombatTickResolvedEvent e)
        {
            if (unit == null) return;

            if (e.Attacker == unit || e.Defender == unit) RefreshCombatStateHold();

            if (triggerDodgeFromCombatTicks
                && e.Defender == unit
                && e.DidDefenderDodge
                && IsWithinAttackAnimationDistance(e.Attacker))
            {
                TriggerDodge();
                return;
            }

            // Fallback only: when health events are unavailable, keep hit feedback.
            if (unitHealth == null && e.Defender == unit && e.DidHit)
                TriggerHit();
        }


        public void TryPlayRandomFacingTurn(Unit targetUnit)
        {
            if (!playRandomTurnOnFacing || targetUnit == null) return;

            if (Time.time < _nextTurnAnimationTime) return;

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached) CacheParameters();
            }

            if (_animator == null) return;

            if (IsAttackStateActive()) return;

            var toTarget = targetUnit.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f) return;

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var turnAngle = Vector3.Angle(forward, toTarget.normalized);
            if (turnAngle < Mathf.Max(0f, minimumFacingTurnAngleDegrees)) return;

            var maxTurnAngle = Mathf.Clamp(maximumFacingTurnAnimationAngleDegrees, 0f, 180f);
            if (maxTurnAngle > 0f && turnAngle > maxTurnAngle) return;

            var preferredState = ResolvePreferredTurnState(forward, toTarget.normalized);
            var alternateState = preferredState == turnLeftStateName ? turnRightStateName : turnLeftStateName;
            var chooseAlternate = Random.value < Mathf.Clamp01(oppositeTurnDirectionChance);
            var primaryState = chooseAlternate ? alternateState : preferredState;
            var secondaryState = chooseAlternate ? preferredState : alternateState;

            if (TryCrossFadeTurnState(primaryState) || TryCrossFadeTurnState(secondaryState))
                _nextTurnAnimationTime = Time.time + Mathf.Max(0f, turnAnimationCooldownSeconds);
        }


        public void CancelAttackForMovement(Vector3 destinationWorldPosition)
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached) CacheParameters();
            }

            if (_animator == null) return;

            var wasAttacking = IsAttackStateActive();
            ResetAllCombatTriggers();

            if (!wasAttacking) return;

            var desiredDirection = destinationWorldPosition - transform.position;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                TryExitAttackToLocomotion();
                return;
            }

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var desiredDirectionNormalized = desiredDirection.normalized;
            var turnAngle = Vector3.Angle(forward, desiredDirectionNormalized);
            var shouldPlayTurn = turnAngle >= Mathf.Clamp(turnAroundAngleDegrees, 0f, 180f);
            var maxTurnAngle = Mathf.Clamp(maximumFacingTurnAnimationAngleDegrees, 0f, 180f);
            var withinTurnAnimationCap = maxTurnAngle <= 0f || turnAngle <= maxTurnAngle;

            if (shouldPlayTurn && withinTurnAnimationCap &&
                TryPlayTurnTowardDirection(forward, desiredDirectionNormalized)) return;

            TryExitAttackToLocomotion();
        }


        public void CancelCombatFacingForMovement()
        {
            _dodgeFaceTarget = null;
            _dodgeFaceLockExpiresAt = 0f;
        }


        public void TryTriggerCombatEntry()
        {
            if (Time.time < _nextCombatEntryTriggerTime) return;

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached) CacheParameters();
            }

            if (_animator == null) return;

            if (unitHealth != null && unitHealth.IsDead) return;

            var triggered = false;
            if (_hasCombatEntry)
            {
                ResetCombatTriggersExcept(_combatEntryHash);
                _animator.SetTrigger(_combatEntryHash);
                triggered = true;
            }

            TryPlayFallbackState(combatEntryStateName, triggered);
            _nextCombatEntryTriggerTime = Time.time + Mathf.Max(0f, combatEntryTriggerCooldownSeconds);
        }


        private string ResolvePreferredTurnState(Vector3 forward, Vector3 toTargetNormalized)
        {
            if (!biasTurnDirectionTowardTargetSide) return Random.value < 0.5f ? turnLeftStateName : turnRightStateName;

            var side = Vector3.Cross(forward, toTargetNormalized).y;
            if (Mathf.Abs(side) <= 0.0001f) return Random.value < 0.5f ? turnLeftStateName : turnRightStateName;

            // Positive means target is to the right of current forward.
            return side > 0f ? turnRightStateName : turnLeftStateName;
        }


        private bool TryPlayTurnTowardDirection(Vector3 forward, Vector3 directionNormalized)
        {
            var preferredState = ResolvePreferredTurnState(forward, directionNormalized);
            var alternateState = preferredState == turnLeftStateName ? turnRightStateName : turnLeftStateName;

            if (TryCrossFadeTurnState(preferredState) || TryCrossFadeTurnState(alternateState))
            {
                _nextTurnAnimationTime = Time.time + Mathf.Max(0f, turnAnimationCooldownSeconds);
                return true;
            }

            return false;
        }


        private void TryExitAttackToLocomotion()
        {
            TryApplyRandomVariant(baseLocomotionClip, _locomotionVariants);
            if (TryCrossFadeState(locomotionStateName)) return;

            TryApplyRandomVariant(baseIdleClip, _idleVariants);
            _ = TryCrossFadeState(idleStateName);
        }


        private bool TryCrossFadeTurnState(string stateName)
        {
            if (string.Equals(stateName, turnLeftStateName, StringComparison.Ordinal))
                TryApplyRandomVariant(baseTurnLeftClip, _turnLeftVariants);
            else if (string.Equals(stateName, turnRightStateName, StringComparison.Ordinal))
                TryApplyRandomVariant(baseTurnRightClip, _turnRightVariants);

            return TryCrossFadeState(stateName);
        }


        private bool IsAttackStateActive()
        {
            if (_animator == null || string.IsNullOrWhiteSpace(attackStateName)) return false;

            var currentState = _animator.GetCurrentAnimatorStateInfo(0);
            if (StateMatchesName(currentState, attackStateName)) return true;

            if (_animator.IsInTransition(0))
            {
                var nextState = _animator.GetNextAnimatorStateInfo(0);
                if (StateMatchesName(nextState, attackStateName)) return true;
            }

            return false;
        }


        private bool ShouldSuppressDefensiveReaction()
        {
            if (!suppressDefensiveReactionsWhileAttacking) return false;

            if (Time.time <= _defensiveReactionSuppressUntilTime) return true;

            return IsAttackStateActive();
        }


        private static bool StateMatchesName(AnimatorStateInfo stateInfo, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return false;

            var shortHash = Animator.StringToHash(stateName);
            if (stateInfo.shortNameHash == shortHash) return true;

            var fullPathHash = Animator.StringToHash("Base Layer." + stateName);
            return stateInfo.fullPathHash == fullPathHash;
        }


        private void OnDamaged(float amount)
        {
            if (amount > 0f) TriggerHit();
        }


        private void OnDied()
        {
            _combatStateHoldUntilTime = 0f;
            SyncDeadBool();
            SetCombatAnimatorFlag(false);
            ResetCombatEyeFocus(true);
            TryApplyRandomVariant(baseDeadClip, _deadVariants);

            var triggered = false;
            if (_animator != null && _hasDie)
            {
                _animator.SetTrigger(_dieHash);
                triggered = true;
            }

            TryPlayFallbackState(deadStateName, triggered);
        }


        private bool TryCrossFadeState(string stateName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName)) return false;

            var fadeSeconds = Mathf.Max(0f, fallbackCrossFadeSeconds);
            const int baseLayer = 0;

            var stateHash = Animator.StringToHash(stateName);
            if (_animator.HasState(baseLayer, stateHash))
            {
                _animator.CrossFadeInFixedTime(stateHash, fadeSeconds, baseLayer);
                return true;
            }

            var qualifiedStateName = "Base Layer." + stateName;
            var qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (_animator.HasState(baseLayer, qualifiedHash))
            {
                _animator.CrossFadeInFixedTime(qualifiedHash, fadeSeconds, baseLayer);
                return true;
            }

            return false;
        }
    }
}
