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

        // TODO: Add weighted context channels (hunger, group pressure, sound memory).
        // TODO: Replace static formulas with data-driven curves per AI archetype.
    }
}