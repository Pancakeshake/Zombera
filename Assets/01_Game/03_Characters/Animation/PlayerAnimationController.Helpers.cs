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
        private static class ClipMatchHelper
        {
            public static bool ContainsAny(string value, params string[] tokens)
            {
                if (string.IsNullOrEmpty(value) || tokens == null) return false;

                var normalizedValue = value.ToLowerInvariant();

                foreach (var token in tokens)
                    if (!string.IsNullOrEmpty(token) && normalizedValue.Contains(token.ToLowerInvariant()))
                        return true;

                return false;
            }

            public static AnimationClip FindBestMatchingClip(AnimationClip[] clips, string desiredName,
                bool skipPreviewClips)
            {
                if (clips == null || clips.Length == 0) return null;

                var hasDesiredName = !string.IsNullOrWhiteSpace(desiredName);
                AnimationClip fallback = null;

                foreach (var clip in clips)
                {
                    if (clip == null) continue;

                    if (skipPreviewClips && clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        continue;

                    fallback ??= clip;

                    if (hasDesiredName && string.Equals(clip.name, desiredName, StringComparison.OrdinalIgnoreCase))
                        return clip;
                }

                if (hasDesiredName)
                    foreach (var clip in clips)
                    {
                        if (clip == null) continue;

                        if (skipPreviewClips &&
                            clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (clip.name.IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0) return clip;
                    }

                return fallback;
            }
        }

        private static class WeightedAttackSelectionHelper
        {
            public static bool TrySelectWeightedAttackOption(IReadOnlyList<CachedWeightedAttackOption> options,
                float random01,
                out CachedWeightedAttackOption selectedAttack)
            {
                selectedAttack = default;

                if (options == null || options.Count == 0) return false;

                var totalWeight = 0f;
                for (var i = 0; i < options.Count; i++)
                    totalWeight += Mathf.Max(0f, options[i].Weight);

                if (totalWeight <= 0f) return false;

                var roll = Mathf.Clamp01(random01) * totalWeight;
                var runningWeight = 0f;

                foreach (var option in options)
                {
                    runningWeight += Mathf.Max(0f, option.Weight);
                    if (roll <= runningWeight)
                    {
                        selectedAttack = option;
                        return true;
                    }
                }

                selectedAttack = options[options.Count - 1];
                return true;
            }

            public static CombatReactionArea ResolveReactionAreaForHitTiming(CachedWeightedAttackOption selectedAttack,
                float projectedHitTimeSeconds,
                AttackReactionTimeline[] attackReactionTimelines)
            {
                var resolvedReactionArea = selectedAttack.PreferredReactionArea == CombatReactionArea.Default
                    ? CombatReactionArea.Chest
                    : selectedAttack.PreferredReactionArea;

                if (attackReactionTimelines == null || attackReactionTimelines.Length == 0) return resolvedReactionArea;

                AttackReactionTimeline matchedTimeline = null;
                foreach (var timeline in attackReactionTimelines)
                {
                    if (timeline == null || timeline.attackStyle != selectedAttack.AttackStyle) continue;

                    matchedTimeline = timeline;
                    break;
                }

                if (matchedTimeline == null) return resolvedReactionArea;

                if (matchedTimeline.fallbackReactionArea != CombatReactionArea.Default)
                    resolvedReactionArea = matchedTimeline.fallbackReactionArea;

                var timedReactions = matchedTimeline.timedReactions;
                if (timedReactions == null || timedReactions.Length == 0) return resolvedReactionArea;

                var sampleTime = Mathf.Max(0f, projectedHitTimeSeconds);
                var bestReactionTime = float.NegativeInfinity;

                foreach (var reactionCue in timedReactions)
                {
                    if (reactionCue == null) continue;

                    var cueTime = Mathf.Max(0f, reactionCue.hitTimeSeconds);
                    if (cueTime > sampleTime + 0.0001f || cueTime < bestReactionTime) continue;

                    bestReactionTime = cueTime;
                    if (reactionCue.reactionArea != CombatReactionArea.Default)
                        resolvedReactionArea = reactionCue.reactionArea;
                }

                return resolvedReactionArea;
            }
        }
    }
}