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
        [Flags]
        private enum VariantBuckets
        {
            None = 0,
            Idle = 1 << 0,
            CombatIdle = 1 << 1,
            Locomotion = 1 << 2,
            Attack = 1 << 3,
            Hit = 1 << 4,
            Death = 1 << 5,
            Dodge = 1 << 6
        }


        private void BuildFolderVariantCategories()
        {
            if (_folderVariantCategoriesBuilt) return;

            _folderVariantCategoriesBuilt = true;

            ClearVariantCategories();
            AddExplicitVariantOverrides();

            if (zombieFolderClips == null || zombieFolderClips.Length == 0)
            {
                AddBaseReactionAndDeathVariants();
                return;
            }

            for (var i = 0; i < zombieFolderClips.Length; i++) ClassifyFolderVariantClip(zombieFolderClips[i]);

            EnsureDodgeVariantsFromLocomotion();
            AddBaseReactionAndDeathVariants();
        }


        private void ClearVariantCategories()
        {
            _idleVariants.Clear();
            _locomotionVariants.Clear();
            _attackVariants.Clear();
            _dodgeVariants.Clear();
            _hitVariants.Clear();
            _deathVariants.Clear();
            _combatIdleVariants.Clear();
        }


        private void AddExplicitVariantOverrides()
        {
            AddRangeUnique(_combatIdleVariants, combatIdleOverrideClips);
            AddRangeUnique(_hitVariants, reactionOverrideClips);
            AddRangeUnique(_deathVariants, deathOverrideClips);
        }


        private void ClassifyFolderVariantClip(AnimationClip clip)
        {
            if (clip == null) return;

            var buckets = ClassifyVariantBuckets(clip.name);

            if ((buckets & VariantBuckets.CombatIdle) != 0)
                AddUnique(_combatIdleVariants, clip);
            else if ((buckets & VariantBuckets.Idle) != 0)
                AddUnique(_idleVariants, clip);

            if ((buckets & VariantBuckets.Locomotion) != 0) AddUnique(_locomotionVariants, clip);

            if ((buckets & VariantBuckets.Attack) != 0) AddUnique(_attackVariants, clip);

            if ((buckets & VariantBuckets.Hit) != 0) AddUnique(_hitVariants, clip);

            if ((buckets & VariantBuckets.Death) != 0) AddUnique(_deathVariants, clip);

            if ((buckets & VariantBuckets.Dodge) != 0) AddUnique(_dodgeVariants, clip);
        }


        private static VariantBuckets ClassifyVariantBuckets(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName)) return VariantBuckets.None;

            var normalizedName = clipName.ToLowerInvariant();
            var buckets = VariantBuckets.None;

            if (ClipNameTokenUtility.Matches(normalizedName, "idle", "scream"))
            {
                if (ClipNameTokenUtility.Matches(normalizedName, "combat"))
                    buckets |= VariantBuckets.CombatIdle;
                else
                    buckets |= VariantBuckets.Idle;
            }

            if (ClipNameTokenUtility.Matches(normalizedName, "walk", "run", "crawl"))
                buckets |= VariantBuckets.Locomotion;

            if (ClipNameTokenUtility.Matches(normalizedName, "attack", "bite", "neck")
                || ClipNameTokenUtility.Matches(normalizedName, "scratch", "swipe", "claw"))
                buckets |= VariantBuckets.Attack;

            if (ClipNameTokenUtility.Matches(normalizedName, "reaction", "hit"))
                buckets |= VariantBuckets.Hit;

            if (ClipNameTokenUtility.Matches(normalizedName, "death", "dying", "die"))
                buckets |= VariantBuckets.Death;

            if (ClipNameTokenUtility.Matches(normalizedName, "dodge", "crawl"))
                buckets |= VariantBuckets.Dodge;

            return buckets;
        }


        private void EnsureDodgeVariantsFromLocomotion()
        {
            if (_dodgeVariants.Count > 0) return;

            for (var i = 0; i < _locomotionVariants.Count; i++) AddUnique(_dodgeVariants, _locomotionVariants[i]);
        }


        private void AddBaseReactionAndDeathVariants()
        {
            AddUnique(_hitVariants, baseHitClip);
            AddUnique(_deathVariants, baseDeathClip);
            AddUnique(_deathVariants, baseDeathClipSecondary);
        }


        private void EnsureRuntimeOverrideController(bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || animator == null) return;

            var currentController = animator.runtimeAnimatorController;
            if (currentController == null) return;

            var baseController = currentController;
            if (currentController is AnimatorOverrideController existingOverride &&
                existingOverride.runtimeAnimatorController != null)
                baseController = existingOverride.runtimeAnimatorController;

            var requiresOverride = _runtimeOverrideController == null
                                   || _runtimeOverrideController.runtimeAnimatorController != baseController
                                   || animator.runtimeAnimatorController != _runtimeOverrideController;

            if (!requiresOverride) return;

            _runtimeOverrideController = new AnimatorOverrideController(baseController);
            animator.runtimeAnimatorController = _runtimeOverrideController;
            _runtimeOverrides.Clear();
            _runtimeOverrideController.GetOverrides(_runtimeOverrides);
        }


        private void ApplyIdleAndLocomotionVariants()
        {
            TryApplyRandomVariant(baseIdleClip, _idleVariants);

            var combatIdleBaseClip = baseCombatIdleClip;
            if (combatIdleBaseClip == null && combatIdleOverrideClips != null && combatIdleOverrideClips.Length > 0)
                combatIdleBaseClip = combatIdleOverrideClips[0];

            if (combatIdleBaseClip != null)
            {
                var forceCombatIdleOverride = _combatIdleVariants.Count > 0;
                TryApplyRandomVariant(combatIdleBaseClip, _combatIdleVariants, forceCombatIdleOverride);
            }

            // Keep locomotion clips fixed: randomizing the forward base clip can break
            // directional blend-tree mapping (e.g. forward movement using backward clips).
        }


        private void TryApplySpecificVariant(AnimationClip baseClip, AnimationClip candidate,
            bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || baseClip == null || candidate == null)
                return;

            EnsureRuntimeOverrideController(ignoreVariantToggle);
            if (_runtimeOverrideController == null)
                return;

            ApplyVariantOverride(baseClip, candidate);
        }


        private void ApplyVariantOverride(AnimationClip baseClip, AnimationClip candidate)
        {
            var found = false;
            var changed = false;

            for (var i = 0; i < _runtimeOverrides.Count; i++)
            {
                var overridePair = _runtimeOverrides[i];
                if (!IsEquivalentBaseClip(overridePair.Key, baseClip)) continue;
                found = true;
                if (overridePair.Value == candidate) continue;
                _runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
            {
                _runtimeOverrideController.ApplyOverrides(_runtimeOverrides);
            }
            else if (!found)
            {
                var fallbackBaseClip = FindCompatibleOverrideBaseClip(baseClip);
                if (fallbackBaseClip != null)
                    _runtimeOverrideController[fallbackBaseClip.name] = candidate;
                else
                    _runtimeOverrideController[baseClip.name] = candidate;
            }
        }


        private void TryApplyRandomVariant(AnimationClip baseClip, List<AnimationClip> variants,
            bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || baseClip == null || variants == null ||
                variants.Count == 0) return;

            EnsureRuntimeOverrideController(ignoreVariantToggle);
            if (_runtimeOverrideController == null) return;

            var candidate = SelectRandomClip(variants);
            if (candidate == null || candidate == baseClip) return;

            ApplyVariantOverride(baseClip, candidate);
        }


        private static AnimationClip SelectRandomClip(List<AnimationClip> variants)
        {
            if (variants == null || variants.Count == 0) return null;

            return variants[Random.Range(0, variants.Count)];
        }


        private AnimationClip FindCompatibleOverrideBaseClip(AnimationClip baseClip)
        {
            if (baseClip == null) return null;

            for (var i = 0; i < _runtimeOverrides.Count; i++)
            {
                var key = _runtimeOverrides[i].Key;
                if (IsEquivalentBaseClip(key, baseClip)) return key;
            }

            return null;
        }


        private static bool IsEquivalentBaseClip(AnimationClip controllerBaseClip, AnimationClip requestedBaseClip)
        {
            if (controllerBaseClip == null || requestedBaseClip == null) return false;

            if (controllerBaseClip == requestedBaseClip) return true;

            var controllerName = NormalizeClipLookupName(controllerBaseClip.name);
            var requestedName = NormalizeClipLookupName(requestedBaseClip.name);

            if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(requestedName)) return false;

            if (controllerName == requestedName) return true;

            return controllerName == requestedName + "loop"
                   || requestedName == controllerName + "loop";
        }
    }
}
