using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// Resolves combat targets using hybrid logic:
    /// prefer player-marked target, otherwise auto-select best enemy.
    /// Scoring weighs proximity, line-of-sight, and threat level (low-HP preference).
    /// </summary>
    public sealed class TargetingSystem : MonoBehaviour
    {
        [Header("Scoring Weights")]
        [SerializeField] private float losBonus = 3f;
        [SerializeField] private float losPenalty = 2f;
        [SerializeField] private float lowHpBonusMax = 4f;
        [Tooltip("Layer mask for line-of-sight obstruction checks.")]
        [SerializeField] private LayerMask losObstructionMask = ~0;

        public IDamageable ResolveHybridTarget(IDamageable markedTarget, IReadOnlyList<IDamageable> candidates, Vector3 sourcePosition)
        {
            if (markedTarget != null && !markedTarget.IsDead)
            {
                return markedTarget;
            }

            return SelectBestTarget(candidates, sourcePosition);
        }

        public IDamageable SelectBestTarget(IReadOnlyList<IDamageable> candidates, Vector3 sourcePosition)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            IDamageable bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                IDamageable candidate = candidates[i];

                if (candidate == null || candidate.IsDead || !(candidate is Component targetComponent))
                {
                    continue;
                }

                float score = ScoreTarget(candidate, targetComponent.transform.position, sourcePosition);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        public UnitHealth ResolveHybridTarget(UnitHealth markedTarget, IReadOnlyList<UnitHealth> candidates, Vector3 sourcePosition)
        {
            if (markedTarget != null && !markedTarget.IsDead)
            {
                return markedTarget;
            }

            return SelectBestTarget(candidates, sourcePosition);
        }

        public UnitHealth SelectBestTarget(IReadOnlyList<UnitHealth> candidates, Vector3 sourcePosition)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            UnitHealth bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                UnitHealth candidate = candidates[i];

                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                float score = ScoreTarget(candidate, candidate.transform.position, sourcePosition);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Composite score = proximity + line-of-sight + low-HP threat bonus.
        /// Higher is better.
        /// </summary>
        private float ScoreTarget(IDamageable candidate, Vector3 targetPos, Vector3 sourcePos)
        {
            float distance = Vector3.Distance(sourcePos, targetPos);
            float score = -distance;

            // Line-of-sight bonus/penalty.
            Vector3 eyeSource = sourcePos + Vector3.up * 1.2f;
            Vector3 eyeTarget = targetPos + Vector3.up * 1.2f;
            bool hasLoS = !Physics.Linecast(eyeSource, eyeTarget, losObstructionMask);
            score += hasLoS ? losBonus : -losPenalty;

            // Low-HP preference: reward nearly-dead targets (easier kills).
            if (candidate is UnitHealth uh && uh.MaxHealth > 0f)
            {
                float healthRatio = Mathf.Clamp01(uh.CurrentHealth / uh.MaxHealth);
                score += (1f - healthRatio) * lowHpBonusMax;
            }

            return score;
        }
    }
}