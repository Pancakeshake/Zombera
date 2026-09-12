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

        private void TickBowVisualPlayback()
        {
            if (!driveBowVisualRig || _bowVisualPlaybackPhase == BowVisualPlaybackPhase.None) return;

            if (!EnsureBowVisualPlayable()) return;

            var clipLength = Mathf.Max(0.01f, _bowVisualClip.length);
            var holdTime = clipLength * Mathf.Clamp01(bowVisualHoldNormalizedTime);

            switch (_bowVisualPlaybackPhase)
            {
                case BowVisualPlaybackPhase.Draw:
                    _bowVisualSampleTime += Time.deltaTime * Mathf.Max(0.01f, bowVisualDrawPlaybackSpeed);
                    if (_bowVisualSampleTime >= holdTime)
                    {
                        _bowVisualSampleTime = holdTime;
                        _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Hold;
                    }

                    break;

                case BowVisualPlaybackPhase.Hold:
                    _bowVisualSampleTime = holdTime;
                    break;

                case BowVisualPlaybackPhase.Release:
                    _bowVisualSampleTime += Time.deltaTime * Mathf.Max(0.01f, bowVisualReleasePlaybackSpeed);
                    if (_bowVisualSampleTime >= clipLength)
                    {
                        _bowVisualSampleTime = clipLength;
                        _bowVisualPlaybackPhase = BowVisualPlaybackPhase.None;
                    }

                    break;
                case BowVisualPlaybackPhase.None:
                    break;
                default:
                    _bowVisualPlaybackPhase = BowVisualPlaybackPhase.None;
                    break;
            }

            _bowVisualPlayable.SetTime(_bowVisualSampleTime);
            _bowVisualPlayable.SetSpeed(0f);
            _bowVisualGraph.Evaluate(0f);
        }


        private void StartBowVisualDraw()
        {
            if (!driveBowVisualRig || !EnsureBowVisualPlayable()) return;

            _bowVisualSampleTime = 0f;
            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Draw;
            _bowVisualPlayable.SetTime(_bowVisualSampleTime);
            _bowVisualPlayable.SetSpeed(0f);
            _bowVisualGraph.Evaluate(0f);
        }


        private void StartBowVisualRelease()
        {
            if (!driveBowVisualRig || !EnsureBowVisualPlayable()) return;

            if (_bowVisualPlaybackPhase == BowVisualPlaybackPhase.None)
            {
                var clipLength = Mathf.Max(0.01f, _bowVisualClip.length);
                _bowVisualSampleTime = clipLength * Mathf.Clamp01(bowVisualHoldNormalizedTime);
            }

            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Release;
        }


        private bool EnsureBowVisualPlayable()
        {
            if (!driveBowVisualRig) return false;

            if (_bowVisualAnimator == null) _bowVisualAnimator = ResolveBowVisualAnimator();

            if (_bowVisualAnimator == null) return false;

            if (_bowVisualClip == null) _bowVisualClip = ResolveBowVisualClip();

            if (_bowVisualClip == null) return false;

            if (_bowVisualGraph.IsValid()) return true;

            _bowVisualGraph = PlayableGraph.Create($"{name}_BowVisualRig");
            _bowVisualPlayable = AnimationClipPlayable.Create(_bowVisualGraph, _bowVisualClip);
            _bowVisualPlayable.SetApplyFootIK(false);
            _bowVisualPlayable.SetApplyPlayableIK(false);
            _bowVisualPlayable.SetTime(0d);
            _bowVisualPlayable.SetSpeed(0d);

            var output = AnimationPlayableOutput.Create(_bowVisualGraph, "BowVisual", _bowVisualAnimator);
            output.SetSourcePlayable(_bowVisualPlayable);

            _bowVisualGraph.Play();
            _bowVisualGraph.Evaluate(0f);
            return true;
        }


        private Animator ResolveBowVisualAnimator()
        {
            var animators = GetComponentsInChildren<Animator>(true);
            if (animators == null || animators.Length == 0) return null;

            var nameToken = string.IsNullOrWhiteSpace(bowVisualNameContains)
                ? string.Empty
                : bowVisualNameContains.ToLowerInvariant();

            foreach (var candidate in animators)
            {
                if (candidate == null || candidate == _animator) continue;

                if (string.IsNullOrEmpty(nameToken) || candidate.name.ToLowerInvariant().Contains(nameToken))
                    return candidate;
            }

            foreach (var candidate in animators)
                if (candidate != null && candidate != _animator)
                    return candidate;

            return null;
        }


        private AnimationClip ResolveBowVisualClip()
        {
            if (string.IsNullOrWhiteSpace(bowVisualClipName))
            {
                var animatorFallback = ResolveBowVisualClipFromAnimatorController();
                if (animatorFallback != null) return animatorFallback;

#if UNITY_EDITOR
                return ResolveBowVisualClipFromAssetPath();
#else
                return null;
#endif
            }

            var allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            if (allClips == null || allClips.Length == 0)
            {
                var animatorFallback = ResolveBowVisualClipFromAnimatorController();
                if (animatorFallback != null) return animatorFallback;

#if UNITY_EDITOR
                return ResolveBowVisualClipFromAssetPath();
#else
                return null;
#endif
            }

            foreach (var clip in allClips)
            {
                if (clip == null) continue;

                if (string.Equals(clip.name, bowVisualClipName, StringComparison.OrdinalIgnoreCase)) return clip;
            }

            foreach (var clip in allClips)
            {
                if (clip == null) continue;

                if (clip.name.IndexOf(bowVisualClipName, StringComparison.OrdinalIgnoreCase) >= 0) return clip;
            }

            var fallbackClip = ResolveBowVisualClipFromAnimatorController();
            if (fallbackClip != null) return fallbackClip;

#if UNITY_EDITOR
            return ResolveBowVisualClipFromAssetPath();
#else
            return null;
#endif
        }


        private AnimationClip ResolveBowVisualClipFromAnimatorController()
        {
            if (_bowVisualAnimator == null || _bowVisualAnimator.runtimeAnimatorController == null) return null;

            var controllerClips = _bowVisualAnimator.runtimeAnimatorController.animationClips;
            return FindBestMatchingClip(controllerClips, bowVisualClipName, false);
        }

        private AnimationClip ResolveBowVisualClipFromAssetPath()
        {
            if (string.IsNullOrWhiteSpace(bowVisualClipAssetPath)) return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(bowVisualClipAssetPath);
            if (assets == null || assets.Length == 0) return null;

            var clips = new List<AnimationClip>(assets.Length);
            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip == null) continue;
                clips.Add(clip);
            }

            return FindBestMatchingClip(clips.ToArray(), bowVisualClipName, true);
        }


        private void ResetBowVisualPlayback(bool clearRigState)
        {
            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.None;
            _bowVisualSampleTime = 0f;

            if (!_bowVisualGraph.IsValid())
            {
                if (clearRigState)
                {
                    _bowVisualAnimator = null;
                    _bowVisualClip = null;
                }

                return;
            }

            _bowVisualPlayable.SetTime(0d);
            _bowVisualPlayable.SetSpeed(0d);
            _bowVisualGraph.Evaluate(0f);

            if (!clearRigState) return;

            _bowVisualGraph.Destroy();
            _bowVisualAnimator = null;
            _bowVisualClip = null;
        }
    }
}
