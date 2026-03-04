using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// Resolves combat targets using hybrid logic:
    /// prefer player-marked target, otherwise auto-select best enemy.
    /// </summary>
    public sealed class TargetingSystem : MonoBehaviour
    {
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

                float distance = Vector3.Distance(sourcePosition, targetComponent.transform.position);
                float score = -distance;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            // TODO: Improve scoring with threat level, line-of-sight, and priority tags.
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

                float distance = Vector3.Distance(sourcePosition, candidate.transform.position);
                float score = -distance;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }
    }
}