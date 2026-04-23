using UnityEngine;

namespace Zombera.AI
{
    /// <summary>
    /// Utility scoring helper for AI state selection.
    /// </summary>
    public sealed class UtilitySystem : MonoBehaviour
    {
        public float ScoreIdle(float threatLevel)
        {
            return Mathf.Clamp01(1f - threatLevel);
        }

        public float ScoreInvestigate(float noiseLevel, float distanceToNoise)
        {
            float distanceFactor = 1f - Mathf.Clamp01(distanceToNoise / 40f);
            return Mathf.Clamp01(noiseLevel * distanceFactor);
        }

        public float ScoreChase(float targetVisibility, float targetDistance)
        {
            float distanceFactor = 1f - Mathf.Clamp01(targetDistance / 30f);
            return Mathf.Clamp01(targetVisibility * distanceFactor);
        }

        public float ScoreAttack(float inRangeFactor, float aggression)
        {
            return Mathf.Clamp01(inRangeFactor * aggression);
        }

        /// <summary>
        /// Computes an attack score with additional context pressure factors.
        /// groupPressure: 0–1 representing how many nearby allies are also engaging.
        /// soundMemory: 0–1 built up from recent high-weight noise events.
        /// </summary>
        public float ScoreAttackWithContext(float inRangeFactor, float aggression, float groupPressure, float soundMemory)
        {
            float baseScore = Mathf.Clamp01(inRangeFactor * aggression);
            float pressureBonus = groupPressure * 0.25f;
            float noiseBonus = soundMemory * 0.15f;
            return Mathf.Clamp01(baseScore + pressureBonus + noiseBonus);
        }

        /// <summary>Evaluates an idle score boosted by the inverse of group pressure and noise.</summary>
        public float ScoreIdleWithContext(float threatLevel, float groupPressure, float soundMemory)
        {
            float base01 = Mathf.Clamp01(1f - threatLevel);
            float suppression = (groupPressure * 0.2f) + (soundMemory * 0.15f);
            return Mathf.Clamp01(base01 - suppression);
        }
    }
}