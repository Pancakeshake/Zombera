#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.AI
{
    public sealed partial class ZombieAnimationController
    {

        private void HandleDied()
        {
            SyncDeadState();

            // Randomise which death state plays (Dead1/Dead2 in the controller).
            if (animator != null && _hasDeathRoll)
                animator.SetFloat(_deathRollHash, Random.value);

            TryApplyDeathVariants();

            var triggered = false;

            if (animator != null && _hasDieTrigger)
            {
                animator.SetTrigger(_dieTriggerHash);
                triggered = true;
            }

            TryPlayFallbackState(deadStateName, triggered);
            ApplyDeathPresentation();
        }


        private void ApplyDeathPresentation()
        {
            if (_hasAppliedDeathPresentation) return;

            _hasAppliedDeathPresentation = true;

            _unitController?.Stop();

            if (disableAiOnDeath && _zombieAi != null) _zombieAi.SetActive(false);

            if (disableNavMeshAgentOnDeath && _navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
                _navMeshAgent.enabled = false;
            }

            if (!tryEnableRagdollOnDeath || !_hasRagdollRig) return;

            var delay = Mathf.Max(0f, ragdollActivationDelaySeconds);

            if (delay <= 0f)
            {
                EnableRagdollNow();
                return;
            }

            CancelPendingRagdoll();
            _pendingRagdollCoroutine = StartCoroutine(EnableRagdollAfterDelay(delay));
        }


        private IEnumerator EnableRagdollAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            _pendingRagdollCoroutine = null;

            if (!isActiveAndEnabled || unitHealth == null || !unitHealth.IsDead) yield break;

            EnableRagdollNow();
        }


        private void EnableRagdollNow()
        {
            if (!_hasRagdollRig) return;

            if (disableAnimatorWhenRagdollEnabled && animator != null) animator.enabled = false;

            if (disableMainColliderWhenRagdolled && _mainCollider != null) _mainCollider.enabled = false;

            SetRagdollEnabled(true);
        }


        private void TryApplyDeathVariants()
        {
            if (_deathVariants.Count == 0) return;

            var primaryVariant = SelectRandomClip(_deathVariants);
            if (primaryVariant != null) TryApplySpecificVariant(baseDeathClip, primaryVariant, true);

            var secondaryBase = baseDeathClipSecondary;
            if (secondaryBase == null) return;

            var secondaryVariant = SelectSecondaryDeathVariant(primaryVariant);

            if (secondaryVariant != null) TryApplySpecificVariant(secondaryBase, secondaryVariant, true);
        }


        private AnimationClip SelectSecondaryDeathVariant(AnimationClip primaryVariant)
        {
            if (_deathVariants.Count <= 1) return primaryVariant;

            var sampled = TrySampleDifferentDeathVariant(primaryVariant, 6);
            if (sampled != null) return sampled;

            return FindFirstDifferentDeathVariant(primaryVariant) ?? primaryVariant;
        }


        private AnimationClip TrySampleDifferentDeathVariant(AnimationClip primaryVariant, int attempts)
        {
            for (var i = 0; i < attempts; i++)
            {
                var candidate = SelectRandomClip(_deathVariants);
                if (candidate != null && candidate != primaryVariant) return candidate;
            }

            return null;
        }


        private AnimationClip FindFirstDifferentDeathVariant(AnimationClip primaryVariant)
        {
            for (var i = 0; i < _deathVariants.Count; i++)
            {
                var candidate = _deathVariants[i];
                if (candidate != null && candidate != primaryVariant) return candidate;
            }

            return null;
        }
    }
}
