#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Resolves combat targets using hybrid logic:
    ///     prefer player-marked target, otherwise auto-select best enemy.
    ///     Scoring weighs proximity, line-of-sight, and threat level (low-HP preference).
    /// </summary>
    public sealed class TargetingSystem : MonoBehaviour
    {
        [Header("Scoring Weights")] [SerializeField]
        private float losBonus = 3f;

        [SerializeField] private float losPenalty = 2f;
        [SerializeField] private float lowHpBonusMax = 4f;

        [Tooltip("Layer mask for line-of-sight obstruction checks.")] [SerializeField]
        private LayerMask losObstructionMask = ~0;

        public IDamageable ResolveHybridTarget(IDamageable markedTarget, IReadOnlyList<IDamageable> candidates,
            Vector3 sourcePosition)
        {
            return markedTarget is { IsDead: false }
                ? markedTarget
                : SelectBestTarget(candidates, sourcePosition);
        }

        public IDamageable SelectBestTarget(IReadOnlyList<IDamageable> candidates, Vector3 sourcePosition)
        {
            if (candidates == null || candidates.Count == 0) return null;

            IDamageable bestTarget = null;
            var bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.IsDead || candidate is not Component targetComponent) continue;

                var score = ScoreTarget(candidate, targetComponent.transform.position, sourcePosition);

                if (score <= bestScore) continue;

                bestScore = score;
                bestTarget = candidate;
            }

            return bestTarget;
        }

        /// <summary>
        ///     Composite score = proximity + line-of-sight + low-HP threat bonus.
        ///     Higher is better.
        /// </summary>
        private float ScoreTarget(IDamageable candidate, Vector3 targetPos, Vector3 sourcePos)
        {
            var distance = Vector3.Distance(sourcePos, targetPos);
            var score = -distance;

            // Line-of-sight bonus/penalty.
            var eyeSource = sourcePos + Vector3.up * 1.2f;
            var eyeTarget = targetPos + Vector3.up * 1.2f;
            var hasLoS = !Physics.Linecast(eyeSource, eyeTarget, losObstructionMask);
            score += hasLoS ? losBonus : -losPenalty;

            // Low-HP preference: reward nearly-dead targets (easier kills).
            if (candidate is not UnitHealth { MaxHealth: > 0f } uh) return score;

            var healthRatio = Mathf.Clamp01(uh.CurrentHealth / uh.MaxHealth);
            return score + (1f - healthRatio) * lowHpBonusMax;
        }
    }
}