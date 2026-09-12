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

        private void SubscribeHealthEvents()
        {
            if (_healthSubscribed || unitHealth == null) return;

            unitHealth.Damaged += HandleDamaged;
            unitHealth.Died += HandleDied;
            _healthSubscribed = true;
        }


        private void UnsubscribeHealthEvents()
        {
            if (!_healthSubscribed || unitHealth == null)
            {
                _healthSubscribed = false;
                return;
            }

            unitHealth.Damaged -= HandleDamaged;
            unitHealth.Died -= HandleDied;
            _healthSubscribed = false;
        }


        private void TrySubscribeCombatTickEvents()
        {
            if (_combatTickSubscribed || CoreEventBus.Instance == null) return;

            CoreEventBus.Instance.Subscribe<CombatAttackWindupEvent>(HandleCombatAttackWindup);
            CoreEventBus.Instance.Subscribe<CombatTickResolvedEvent>(HandleCombatTickResolved);
            _combatTickSubscribed = true;
        }


        private void UnsubscribeCombatTickEvents()
        {
            if (!_combatTickSubscribed) return;

            if (CoreEventBus.Instance != null)
            {
                CoreEventBus.Instance.Unsubscribe<CombatAttackWindupEvent>(HandleCombatAttackWindup);
                CoreEventBus.Instance.Unsubscribe<CombatTickResolvedEvent>(HandleCombatTickResolved);
            }

            _combatTickSubscribed = false;
        }


        private void HandleCombatAttackWindup(CombatAttackWindupEvent gameEvent)
        {
            if (unit == null) return;

            if (triggerAttackFromCombatTicks && gameEvent.Attacker == unit &&
                IsWithinAttackAnimationDistance(gameEvent.Defender)) TriggerAttack();
        }


        private void HandleCombatTickResolved(CombatTickResolvedEvent gameEvent)
        {
            if (unit == null) return;

            if (gameEvent.Defender == unit && gameEvent.DidHit) CacheIncomingHitContext(gameEvent);

            TryApplyPlayerHitStun(gameEvent);

            if (triggerDodgeFromCombatTicks
                && gameEvent.Defender == unit
                && gameEvent.DidDefenderDodge
                && IsWithinAttackAnimationDistance(gameEvent.Attacker))
            {
                TriggerDodge();
                return;
            }

            // Fallback only: if health callbacks are missing, still react to resolved hits.
            if (triggerHitFromCombatTicks && unitHealth == null && gameEvent.Defender == unit && gameEvent.DidHit)
                TriggerHit(gameEvent.PreferredReactionArea, gameEvent.Damage, gameEvent.AttackerStunChance01);
        }


        private void TryApplyPlayerHitStun(CombatTickResolvedEvent gameEvent)
        {
            if (!stunOnPlayerMeleeHits
                || !gameEvent.DidHit
                || gameEvent.Defender != unit
                || gameEvent.Attacker == null
                || gameEvent.Attacker.Role != UnitRole.Player)
                return;

            var stunSeconds = Mathf.Max(0f, playerHitStunSeconds);
            if (gameEvent.IsCritical) stunSeconds += Mathf.Max(0f, criticalPlayerHitStunBonusSeconds);

            if (stunSeconds <= 0f) return;

            if (_zombieStateMachine == null || _zombieStateMachine.gameObject.scene != gameObject.scene)
                _zombieStateMachine = GetComponent<ZombieStateMachine>();

            _zombieStateMachine?.ApplyCombatStun(stunSeconds);
        }


        private void HandleDamaged(float amount)
        {
            if (amount <= 0f) return;

            var preferredReactionArea = CombatReactionArea.Default;
            var damage = Mathf.Max(0f, amount);
            var stunChance01 = 0f;

            if (TryGetCachedIncomingHitContext(out var cachedDamage, out var cachedStunChance01,
                    out var cachedReactionArea))
            {
                if (cachedDamage > 0f) damage = cachedDamage;

                stunChance01 = cachedStunChance01;
                if (cachedReactionArea != CombatReactionArea.Default) preferredReactionArea = cachedReactionArea;
            }

            if (unit != null
                && CombatAttackPresentationRegistry.TryConsumeIncomingReactionHint(unit, out var hintArea)
                && hintArea != CombatReactionArea.Default)
                preferredReactionArea = hintArea;

            TriggerHit(preferredReactionArea, damage, stunChance01);
        }


        private void TriggerAttack()
        {
            if (animator == null) return;

            // 80% chance: fire AltAttackTrigger (Scratch state)
            // 20% chance: fire AttackTrigger (Bite state)
            var useAlt = _hasAltAttackTrigger && Random.value < altAttackChance;
            if (useAlt)
                animator.SetTrigger(_altAttackTriggerHash);
            else if (_hasAttackTrigger) animator.SetTrigger(_attackTriggerHash);
        }


        private void TriggerDodge()
        {
            if (Time.frameCount == _lastDodgeTriggerFrame) return;

            _lastDodgeTriggerFrame = Time.frameCount;

            if (animator == null) return;

            var dodgeBaseClip = baseDodgeClip != null ? baseDodgeClip : baseLocomotionClip;
            var preferredDodgeVariants = _dodgeVariants.Count > 0 ? _dodgeVariants : _locomotionVariants;
            TryApplyRandomVariant(dodgeBaseClip, preferredDodgeVariants);

            if (_hasDodgeTrigger)
            {
                animator.SetTrigger(_dodgeTriggerHash);
                return;
            }

            // Fallback to hit so dodge still has visual feedback on legacy controllers.
            if (_hasHitTrigger) animator.SetTrigger(_hitTriggerHash);
        }


        private void TriggerHit(
            CombatReactionArea preferredReactionArea = CombatReactionArea.Default,
            float damage = 0f,
            float stunChance01 = 0f)
        {
            if (Time.frameCount == _lastHitTriggerFrame) return;

            _lastHitTriggerFrame = Time.frameCount;

            if (animator == null || !_hasHitTrigger) return;

            if (!TryApplyHitReactionVariant(preferredReactionArea, damage, stunChance01))
                TryApplyRandomVariant(baseHitClip, _hitVariants, true);

            animator.SetTrigger(_hitTriggerHash);
        }


        private bool TryApplyHitReactionVariant(CombatReactionArea preferredReactionArea, float damage,
            float stunChance01)
        {
            if (baseHitClip == null
                || _hitVariants.Count == 0)
                return false;

            var normalizedDamage = Mathf.Clamp01(damage / Mathf.Max(0.1f, damageForMaxHitWeight));
            var normalizedStunChance01 = Mathf.Clamp01(stunChance01);
            var areaKeywords = ReactionAreaKeywords(preferredReactionArea);

            _filteredHitVariants.Clear();
            _filteredHitVariantWeights.Clear();

            CollectWeightedHitReactionVariants(areaKeywords, normalizedDamage, normalizedStunChance01, true);

            if (_filteredHitVariants.Count == 0 && HasAreaKeywords(areaKeywords))
                CollectWeightedHitReactionVariants(areaKeywords, normalizedDamage, normalizedStunChance01, false);

            if (_filteredHitVariants.Count == 0) return false;

            var selected = SelectWeightedHitReactionVariant(_filteredHitVariants, _filteredHitVariantWeights);
            if (selected == null) return false;

            TryApplySpecificVariant(baseHitClip, selected, true);
            return true;
        }


        private void CollectWeightedHitReactionVariants(
            string[] areaKeywords,
            float normalizedDamage,
            float normalizedStunChance01,
            bool requireAreaMatch)
        {
            var requiresAreaKeywordMatch = requireAreaMatch && HasAreaKeywords(areaKeywords);

            for (var i = 0; i < _hitVariants.Count; i++)
            {
                var variant = _hitVariants[i];
                if (variant == null) continue;

                var variantName = variant.name.ToLowerInvariant();
                if (requiresAreaKeywordMatch && !ContainsAny(variantName, areaKeywords)) continue;

                var weight = EvaluateHitReactionWeight(variantName, normalizedDamage, normalizedStunChance01);
                if (weight <= 0f) continue;

                _filteredHitVariants.Add(variant);
                _filteredHitVariantWeights.Add(weight);
            }
        }


        private static bool HasAreaKeywords(string[] areaKeywords)
        {
            return areaKeywords != null && areaKeywords.Length > 0;
        }


        private void CacheIncomingHitContext(CombatTickResolvedEvent gameEvent)
        {
            _cachedIncomingHitDamage = Mathf.Max(0f, gameEvent.Damage);
            _cachedIncomingHitStunChance01 = Mathf.Clamp01(gameEvent.AttackerStunChance01);
            _cachedIncomingHitReactionArea = gameEvent.PreferredReactionArea;
            _cachedIncomingHitContextExpiresAt = Time.time + Mathf.Max(0f, cachedCombatHitContextTtlSeconds);
        }


        private bool TryGetCachedIncomingHitContext(out float damage, out float stunChance01,
            out CombatReactionArea reactionArea)
        {
            damage = 0f;
            stunChance01 = 0f;
            reactionArea = CombatReactionArea.Default;

            if (_cachedIncomingHitContextExpiresAt <= 0f || Time.time > _cachedIncomingHitContextExpiresAt)
                return false;

            damage = Mathf.Max(0f, _cachedIncomingHitDamage);
            stunChance01 = Mathf.Clamp01(_cachedIncomingHitStunChance01);
            reactionArea = _cachedIncomingHitReactionArea;
            return true;
        }


        private float EvaluateHitReactionWeight(string variantNameLower, float damage01, float stunChance01)
        {
            return EvaluateHitReactionWeightCore(
                variantNameLower,
                damage01,
                stunChance01,
                useWeightedHitReactions,
                weightedHitReactionRules,
                unmatchedHitReactionBaseWeight);
        }


        private static float EvaluateHitReactionWeightCore(
            string variantNameLower,
            float damage01,
            float stunChance01,
            bool useWeightedRules,
            WeightedHitReactionRule[] rules,
            float unmatchedBaseWeight)
        {
            var weight = Mathf.Max(0f, unmatchedBaseWeight);

            if (!useWeightedRules || rules == null || rules.Length == 0)
                return Mathf.Max(0.0001f, weight);

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || !RuleMatchesVariantCore(rule, variantNameLower, damage01, stunChance01)) continue;

                var weightedContribution = Mathf.Max(0f, rule.baseWeight);
                weightedContribution *= Mathf.Max(0f, 1f + damage01 * rule.damageInfluence);
                weightedContribution *= Mathf.Max(0f, 1f + stunChance01 * rule.stunChanceInfluence);
                weight += weightedContribution;
            }

            return Mathf.Max(0.0001f, weight);
        }


        private static bool RuleMatchesVariant(WeightedHitReactionRule rule, string variantNameLower, float damage01,
            float stunChance01)
        {
            return RuleMatchesVariantCore(rule, variantNameLower, damage01, stunChance01);
        }


        private static bool RuleMatchesVariantCore(WeightedHitReactionRule rule, string variantNameLower, float damage01,
            float stunChance01)
        {
            if (rule == null) return false;

            if (damage01 < Mathf.Clamp01(rule.minDamage01) ||
                stunChance01 < Mathf.Clamp01(rule.minStunChance01)) return false;

            if (rule.clipNameKeywords == null || rule.clipNameKeywords.Length == 0) return true;

            return ClipNameTokenUtility.Matches(variantNameLower, rule.clipNameKeywords);
        }


        private static AnimationClip SelectWeightedHitReactionVariant(List<AnimationClip> variants, List<float> weights)
        {
            return SelectWeightedHitReactionVariantCore(variants, weights);
        }


        private static AnimationClip SelectWeightedHitReactionVariantCore(List<AnimationClip> variants, List<float> weights)
        {
            if (variants == null || variants.Count == 0) return null;

            if (weights == null || weights.Count != variants.Count) return variants[Random.Range(0, variants.Count)];

            var totalWeight = 0f;
            for (var i = 0; i < weights.Count; i++) totalWeight += Mathf.Max(0f, weights[i]);

            if (totalWeight <= 0.0001f) return variants[Random.Range(0, variants.Count)];

            var roll = Random.value * totalWeight;
            var cumulative = 0f;

            for (var i = 0; i < variants.Count; i++)
            {
                cumulative += Mathf.Max(0f, weights[i]);
                if (roll <= cumulative) return variants[i];
            }

            return variants[variants.Count - 1];
        }


        private static string[] ReactionAreaKeywords(CombatReactionArea preferredReactionArea)
        {
            return ReactionAreaKeywordsCore(preferredReactionArea);
        }


        private static string[] ReactionAreaKeywordsCore(CombatReactionArea preferredReactionArea)
        {
            switch (preferredReactionArea)
            {
                case CombatReactionArea.Default:
                    return null;
                case CombatReactionArea.Head:
                    return new[] { "head" };
                case CombatReactionArea.ShoulderLeft:
                    return new[] { "shoulder_l", "shoulderl", "left_shoulder", "shoulderleft" };
                case CombatReactionArea.ShoulderRight:
                    return new[] { "shoulder_r", "shoulderr", "right_shoulder", "shoulderright" };
                case CombatReactionArea.Stomach:
                    return new[] { "stomach", "gut", "abdomen", "torso" };
                case CombatReactionArea.Legs:
                    return new[] { "leg", "knee", "shin" };
                case CombatReactionArea.Chest:
                    return new[] { "chest", "torso" };
                default:
                    return null;
            }
        }


        private bool IsWithinAttackAnimationDistance(Unit defender)
        {
            var maxDistance = Mathf.Max(0f, maxAttackWindupAnimationDistance);
            if (maxDistance <= 0f || defender == null) return true;

            var delta = defender.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= maxDistance * maxDistance;
        }
    }
}
