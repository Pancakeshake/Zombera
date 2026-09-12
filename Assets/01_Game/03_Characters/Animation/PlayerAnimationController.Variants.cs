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

        private void BuildFolderVariantCategories()
        {
            if (_folderVariantCategoriesBuilt) return;

            _folderVariantCategoriesBuilt = true;

            _idleVariants.Clear();
            _locomotionVariants.Clear();
            _attackVariants.Clear();
            _dodgeVariants.Clear();
            _hitVariants.Clear();
            _deadVariants.Clear();
            _turnLeftVariants.Clear();
            _turnRightVariants.Clear();

            if (playerFolderClips == null || playerFolderClips.Length == 0) return;

            foreach (var clip in playerFolderClips)
            {
                if (clip == null) continue;

                var clipName = clip.name.ToLowerInvariant();

                if (ContainsAny(clipName, "idle")) AddUnique(_idleVariants, clip);

                if (ContainsAny(clipName, "walk", "run", "strafe")) AddUnique(_locomotionVariants, clip);

                if (ContainsAny(clipName, "attack", "punch", "kick")) AddUnique(_attackVariants, clip);

                if (ContainsAny(clipName, "dodge", "strafe", "jump")) AddUnique(_dodgeVariants, clip);

                if (ContainsAny(clipName, "reaction", "hit", "react")) AddUnique(_hitVariants, clip);

                if (ContainsAny(clipName, "death", "dying", "die")) AddUnique(_deadVariants, clip);

                var hasTurn = ContainsAny(clipName, "turn");
                if (hasTurn && ContainsAny(clipName, "left")) AddUnique(_turnLeftVariants, clip);

                if (hasTurn && ContainsAny(clipName, "right")) AddUnique(_turnRightVariants, clip);
            }

            if (_dodgeVariants.Count == 0)
                foreach (var locomotionVariant in _locomotionVariants)
                    AddUnique(_dodgeVariants, locomotionVariant);

            if (_turnLeftVariants.Count == 0)
                foreach (var clip in _locomotionVariants)
                    if (clip != null && clip.name.ToLowerInvariant().Contains("left"))
                        AddUnique(_turnLeftVariants, clip);

            if (_turnRightVariants.Count == 0)
                foreach (var clip in _locomotionVariants)
                    if (clip != null && clip.name.ToLowerInvariant().Contains("right"))
                        AddUnique(_turnRightVariants, clip);
        }


        private void EnsureRuntimeOverrideController()
        {
            if (!enableFolderClipVariants || _animator == null) return;

            var currentController = _animator.runtimeAnimatorController;
            if (currentController == null) return;

            var baseController = currentController;
            if (currentController is AnimatorOverrideController existingOverride &&
                existingOverride.runtimeAnimatorController != null)
                baseController = existingOverride.runtimeAnimatorController;

            var requiresOverride = _runtimeOverrideController == null
                                   || _runtimeOverrideController.runtimeAnimatorController != baseController
                                   || _animator.runtimeAnimatorController != _runtimeOverrideController;

            if (!requiresOverride) return;

            _runtimeOverrideController = new AnimatorOverrideController(baseController);
            _animator.runtimeAnimatorController = _runtimeOverrideController;
            _runtimeOverrides.Clear();
            _runtimeOverrideController.GetOverrides(_runtimeOverrides);
        }


        private void ApplyIdleAndLocomotionVariants()
        {
            TryApplyRandomVariant(baseIdleClip, _idleVariants);
            TryApplyRandomVariant(baseLocomotionClip, _locomotionVariants);
        }


        private void TryApplySpecificVariant(AnimationClip baseClip, AnimationClip candidate)
        {
            if (!enableFolderClipVariants || baseClip == null || candidate == null) return;

            EnsureRuntimeOverrideController();
            if (_runtimeOverrideController == null) return;

            var found = false;
            var changed = false;

            for (var i = 0; i < _runtimeOverrides.Count; i++)
            {
                var overridePair = _runtimeOverrides[i];
                if (overridePair.Key != baseClip) continue;

                found = true;
                if (overridePair.Value == candidate) continue;

                _runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
                _runtimeOverrideController.ApplyOverrides(_runtimeOverrides);
            else if (!found) _runtimeOverrideController[baseClip.name] = candidate;
        }


        private void TryApplyRandomVariant(AnimationClip baseClip, List<AnimationClip> variants)
        {
            if (!enableFolderClipVariants || baseClip == null || variants == null || variants.Count == 0) return;

            EnsureRuntimeOverrideController();
            if (_runtimeOverrideController == null) return;

            var candidate = variants[Random.Range(0, variants.Count)];
            if (candidate == null || candidate == baseClip) return;

            var found = false;
            var changed = false;

            for (var i = 0; i < _runtimeOverrides.Count; i++)
            {
                var overridePair = _runtimeOverrides[i];
                if (overridePair.Key != baseClip) continue;

                found = true;
                if (overridePair.Value == candidate) continue;

                _runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
                _runtimeOverrideController.ApplyOverrides(_runtimeOverrides);
            else if (!found) _runtimeOverrideController[baseClip.name] = candidate;
        }


        private static void AddUnique(List<AnimationClip> destination, AnimationClip clip)
        {
            if (destination == null || clip == null || destination.Contains(clip)) return;

            destination.Add(clip);
        }


        private static bool ContainsAny(string value, params string[] tokens)
        {
            return ClipMatchHelper.ContainsAny(value, tokens);
        }
    }
}
